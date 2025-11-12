namespace IoCTools.Generator.Tests;

using Microsoft.CodeAnalysis;

/// <summary>
///     Tests for IOC035 which guides developers toward DependsOn instead of Inject when default naming is used.
/// </summary>
public class InjectUsageDiagnosticTests
{
    [Fact]
    public void InjectFieldWithDefaultName_GeneratesIOC035Warning()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IAuditLogger { }
public interface IReportService { }

[Scoped]
public partial class ReportService : IReportService
{
    [Inject] private readonly IAuditLogger _auditLogger;

    public void Execute()
        => _auditLogger.ToString();
}
";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var diagnostics = result.GetDiagnosticsByCode("IOC035");
        diagnostics.Should().ContainSingle();

        var diagnostic = diagnostics[0];
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().Should().Contain("DependsOn");
    }

    [Fact]
    public void InjectFieldWithCustomName_DoesNotGenerateIOC035()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IAuditLogger { }
public interface IReportService { }

[Scoped]
public partial class CustomizedReportService : IReportService
{
    [Inject] private readonly IAuditLogger _primaryAuditLogger;

    public void Execute()
        => _primaryAuditLogger.ToString();
}
";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.GetDiagnosticsByCode("IOC035").Should().BeEmpty();
    }

    [Fact]
    public void InjectFieldWithoutReadonly_DoesNotGenerateIOC035()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IAuditLogger { }
public interface IReportService { }

[Scoped]
public partial class MutableReportService : IReportService
{
    [Inject] private IAuditLogger _auditLogger;

    public void Swap(IAuditLogger replacement)
        => _auditLogger = replacement;
}
";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.GetDiagnosticsByCode("IOC035").Should().BeEmpty();
    }
}
