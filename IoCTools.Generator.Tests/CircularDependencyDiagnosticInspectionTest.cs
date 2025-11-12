namespace IoCTools.Generator.Tests;

using System.Diagnostics;

using Microsoft.CodeAnalysis;

/// <summary>
///     Test to inspect the actual IOC003 diagnostic messages and verify their content.
///     This helps validate that the error messages are helpful and accurate.
/// </summary>
public class CircularDependencyDiagnosticInspectionTest
{
    [Fact]
    public void IOC003_DiagnosticMessage_ContainsHelpfulInformation()
    {
        // Arrange - Simple circular dependency
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;
public interface IA { }
public interface IB { }
public partial class ServiceA : IA
{
    [Inject] private readonly IB _serviceB;
}
public partial class ServiceB : IB
{
    [Inject] private readonly IA _serviceA;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        ioc003Diagnostics.Should().NotBeEmpty();

        // Inspect the actual diagnostic messages
        var diagnosticMessages = ioc003Diagnostics.Select(d => d.GetMessage()).ToList();

        // Log the actual messages for debugging
        foreach (var message in diagnosticMessages)
            // In a real test environment, you'd use output helpers
            Debug.WriteLine($"IOC003 Message: {message}");

        // Validate diagnostic properties
        foreach (var diagnostic in ioc003Diagnostics)
        {
            diagnostic.Id.Should().Be("IOC003");
            diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
            diagnostic.GetMessage().Should().Contain("Circular dependency detected");

            var message = diagnostic.GetMessage();

            // Should mention the services involved
            (message.Contains("ServiceA") || message.Contains("ServiceB")).Should()
                .BeTrue($"Expected message to contain service names. Got: {message}");

            // Should be a descriptive warning message
            (message.Length > 20).Should().BeTrue("Diagnostic message should be descriptive");
        }
    }

    [Fact]
    public void IOC003_SelfReference_DiagnosticMessage()
    {
        // Arrange - Self-referencing service
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;
public interface ISelfService { }
public partial class SelfService : ISelfService
{
    [Inject] private readonly ISelfService _self;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        ioc003Diagnostics.Should().NotBeEmpty();

        // Validate self-reference diagnostic
        var diagnostic = ioc003Diagnostics.First();
        var message = diagnostic.GetMessage();

        Debug.WriteLine($"Self-Reference IOC003 Message: {message}");

        diagnostic.Id.Should().Be("IOC003");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        message.Should().Contain("Circular dependency detected");
        message.Should().Contain("SelfService");
    }

    [Fact]
    public void IOC003_ThreeServiceCycle_DiagnosticMessage()
    {
        // Arrange - Three service cycle
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;
public interface IA { } public interface IB { } public interface IC { }
public partial class ServiceA : IA { [Inject] private readonly IB _b; }
public partial class ServiceB : IB { [Inject] private readonly IC _c; }
public partial class ServiceC : IC { [Inject] private readonly IA _a; }";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        ioc003Diagnostics.Should().NotBeEmpty();

        // Validate three-service cycle diagnostic
        var allMessages = string.Join(" | ", ioc003Diagnostics.Select(d => d.GetMessage()));
        Debug.WriteLine($"Three-Service Cycle IOC003 Messages: {allMessages}");

        // Should reference services involved in the cycle
        (allMessages.Contains("ServiceA") || allMessages.Contains("ServiceB") || allMessages.Contains("ServiceC"))
            .Should().BeTrue($"Expected cycle message to reference involved services. Got: {allMessages}");
    }
}
