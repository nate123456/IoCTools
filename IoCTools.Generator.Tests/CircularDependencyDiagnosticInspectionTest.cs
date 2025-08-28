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
        Assert.NotEmpty(ioc003Diagnostics);

        // Inspect the actual diagnostic messages
        var diagnosticMessages = ioc003Diagnostics.Select(d => d.GetMessage()).ToList();

        // Log the actual messages for debugging
        foreach (var message in diagnosticMessages)
            // In a real test environment, you'd use output helpers
            Debug.WriteLine($"IOC003 Message: {message}");

        // Validate diagnostic properties
        foreach (var diagnostic in ioc003Diagnostics)
        {
            Assert.Equal("IOC003", diagnostic.Id);
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("Circular dependency detected", diagnostic.GetMessage());

            var message = diagnostic.GetMessage();

            // Should mention the services involved
            Assert.True(message.Contains("ServiceA") || message.Contains("ServiceB"),
                $"Expected message to contain service names. Got: {message}");

            // Should be a descriptive warning message
            Assert.True(message.Length > 20, "Diagnostic message should be descriptive");
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
        Assert.NotEmpty(ioc003Diagnostics);

        // Validate self-reference diagnostic
        var diagnostic = ioc003Diagnostics.First();
        var message = diagnostic.GetMessage();

        Debug.WriteLine($"Self-Reference IOC003 Message: {message}");

        Assert.Equal("IOC003", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.Contains("Circular dependency detected", message);
        Assert.Contains("SelfService", message);
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
        Assert.NotEmpty(ioc003Diagnostics);

        // Validate three-service cycle diagnostic
        var allMessages = string.Join(" | ", ioc003Diagnostics.Select(d => d.GetMessage()));
        Debug.WriteLine($"Three-Service Cycle IOC003 Messages: {allMessages}");

        // Should reference services involved in the cycle
        Assert.True(
            allMessages.Contains("ServiceA") || allMessages.Contains("ServiceB") || allMessages.Contains("ServiceC"),
            $"Expected cycle message to reference involved services. Got: {allMessages}");
    }
}
