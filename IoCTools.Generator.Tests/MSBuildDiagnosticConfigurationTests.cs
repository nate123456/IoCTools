namespace IoCTools.Generator.Tests;

using Microsoft.CodeAnalysis;

/// <summary>
///     Comprehensive tests for MSBuild diagnostic configuration system.
///     Tests the new MSBuild property support: IoCToolsNoImplementationSeverity,
///     IoCToolsManualSeverity, and IoCToolsDisableDiagnostics.
///     These tests validate that MSBuild properties correctly configure diagnostic severity
///     and enable/disable functionality for dependency validation features.
/// </summary>
public class MSBuildDiagnosticConfigurationTests
{
    #region Integration Tests with Real Source Generator

    [Fact]
    public void MSBuildDiagnostics_RealIntegrationTest_NoImplementationError()
    {
        // Integration test using the actual source generator pipeline
        // This tests that MSBuild properties are correctly parsed and applied
        var sourceCode = GetMissingImplementationSource();
        var properties = new Dictionary<string, string>(); // No MSBuild properties = default behavior

        var (compilation, diagnostics) = CompileWithMSBuildProperties(sourceCode, properties);
        var ioc001Diagnostics = diagnostics.Where(d => d.Id == "IOC001").ToList();

        ioc001Diagnostics.Should().ContainSingle();
        ioc001Diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning); // Default severity
    }

    #endregion

    #region Test Infrastructure

    private static (Compilation compilation, List<Diagnostic> diagnostics) CompileWithMSBuildProperties(
        string sourceCode,
        Dictionary<string, string> msbuildProperties)
    {
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode, true, msbuildProperties);
        return (result.Compilation, result.Diagnostics.ToList());
    }

    /// <summary>
    ///     Creates standard test source code with missing implementation
    /// </summary>
    private static string GetMissingImplementationSource() => @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace
{
    public interface IMissingService { }

    
    [Scoped]
    public partial class TestService
    {
        [Inject] private readonly IMissingService _missingService;
    }
}";

    /// <summary>
    ///     Creates standard test source code with unregistered service
    /// </summary>
    private static string GetUnmanagedServiceSource() => @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace
{
    public interface IUnmanagedService { }
    
    // NOTE: UnmanagedService deliberately lacks lifetime attribute and is non-partial to trigger IOC002
    public class UnmanagedService : IUnmanagedService { }

    
    [Scoped]
    public partial class TestService
    {
        [Inject] private readonly IUnmanagedService _unmanagedService;
    }
}";

    #endregion

    #region Default Behavior Tests

    [Fact]
    public void MSBuildDiagnostics_DefaultBehavior_NoImplementation_ReportsWarning()
    {
        // Test default behavior for IOC001 - No implementation found
        var sourceCode = GetMissingImplementationSource();
        var properties = new Dictionary<string, string>(); // No MSBuild properties = default behavior

        var (compilation, diagnostics) = CompileWithMSBuildProperties(sourceCode, properties);
        var ioc001Diagnostics = diagnostics.Where(d => d.Id == "IOC001").ToList();

        ioc001Diagnostics.Should().ContainSingle();
        ioc001Diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);
        ioc001Diagnostics[0].GetMessage().Should().Contain("but no implementation of this interface exists");
        ioc001Diagnostics[0].GetMessage().Should().Contain("IMissingService");
    }

    [Fact]
    public void MSBuildDiagnostics_DefaultBehavior_UnmanagedService_ReportsWarning()
    {
        // Test default behavior for IOC002 - Unregistered implementation
        var sourceCode = GetUnmanagedServiceSource();
        var properties = new Dictionary<string, string>(); // No MSBuild properties = default behavior

        var (compilation, diagnostics) = CompileWithMSBuildProperties(sourceCode, properties);
        var ioc002Diagnostics = diagnostics.Where(d => d.Id == "IOC002").ToList();

        ioc002Diagnostics.Should().ContainSingle();
        ioc002Diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);
        ioc002Diagnostics[0].GetMessage().Should().Contain("implementation exists but lacks lifetime attribute");
        ioc002Diagnostics[0].GetMessage().Should().Contain("UnmanagedService");
    }

    #endregion

    #region IoCToolsNoImplementationSeverity Configuration Tests

    [Theory]
    [InlineData("Error", DiagnosticSeverity.Error)]
    [InlineData("Warning", DiagnosticSeverity.Warning)]
    [InlineData("Info", DiagnosticSeverity.Info)]
    [InlineData("Hidden", DiagnosticSeverity.Hidden)]
    public void MSBuildDiagnostics_NoImplementationSeverity_ConfiguresCorrectly(string severityValue,
        DiagnosticSeverity expectedSeverity)
    {
        var sourceCode = GetMissingImplementationSource();
        var properties = new Dictionary<string, string>
        {
            ["build_property.IoCToolsNoImplementationSeverity"] = severityValue
        };

        var (compilation, diagnostics) = CompileWithMSBuildProperties(sourceCode, properties);

        var ioc001Diagnostics = diagnostics.Where(d => d.Id == "IOC001").ToList();
        ioc001Diagnostics.Should().ContainSingle();
        ioc001Diagnostics[0].Severity.Should().Be(expectedSeverity);
    }

    [Theory]
    [InlineData("error")]
    [InlineData("WARNING")]
    [InlineData("Info")]
    [InlineData("HIDDEN")]
    public void MSBuildDiagnostics_NoImplementationSeverity_CaseInsensitive(string severityValue)
    {
        var sourceCode = GetMissingImplementationSource();
        var properties = new Dictionary<string, string>
        {
            ["build_property.IoCToolsNoImplementationSeverity"] = severityValue
        };

        var (compilation, diagnostics) = CompileWithMSBuildProperties(sourceCode, properties);

        var ioc001Diagnostics = diagnostics.Where(d => d.Id == "IOC001").ToList();
        ioc001Diagnostics.Should().ContainSingle();

        // Should parse case-insensitively
        var expectedSeverity = severityValue.ToLowerInvariant() switch
        {
            "error" => DiagnosticSeverity.Error,
            "warning" => DiagnosticSeverity.Warning,
            "info" => DiagnosticSeverity.Info,
            "hidden" => DiagnosticSeverity.Hidden,
            _ => DiagnosticSeverity.Warning
        };

        ioc001Diagnostics[0].Severity.Should().Be(expectedSeverity);
    }

    [Fact]
    public void MSBuildDiagnostics_NoImplementationSeverity_InvalidValue_UsesDefault()
    {
        var sourceCode = GetMissingImplementationSource();
        var properties = new Dictionary<string, string>
        {
            ["build_property.IoCToolsNoImplementationSeverity"] = "InvalidValue"
        };

        var (compilation, diagnostics) = CompileWithMSBuildProperties(sourceCode, properties);

        var ioc001Diagnostics = diagnostics.Where(d => d.Id == "IOC001").ToList();
        ioc001Diagnostics.Should().ContainSingle();
        // Should fallback to default Warning severity for invalid values
        ioc001Diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    #endregion

    #region IoCToolsManualSeverity Configuration Tests

    [Theory]
    [InlineData("Error", DiagnosticSeverity.Error)]
    [InlineData("Warning", DiagnosticSeverity.Warning)]
    [InlineData("Info", DiagnosticSeverity.Info)]
    [InlineData("Hidden", DiagnosticSeverity.Hidden)]
    public void MSBuildDiagnostics_UnregisteredSeverity_ConfiguresCorrectly(string severityValue,
        DiagnosticSeverity expectedSeverity)
    {
        var sourceCode = GetUnmanagedServiceSource();
        var properties = new Dictionary<string, string> { ["build_property.IoCToolsManualSeverity"] = severityValue };

        var (compilation, diagnostics) = CompileWithMSBuildProperties(sourceCode, properties);

        var ioc002Diagnostics = diagnostics.Where(d => d.Id == "IOC002").ToList();
        ioc002Diagnostics.Should().ContainSingle();
        ioc002Diagnostics[0].Severity.Should().Be(expectedSeverity);
    }

    [Theory]
    [InlineData("error")]
    [InlineData("WARNING")]
    [InlineData("Info")]
    [InlineData("HIDDEN")]
    public void MSBuildDiagnostics_UnregisteredSeverity_CaseInsensitive(string severityValue)
    {
        var sourceCode = GetUnmanagedServiceSource();
        var properties = new Dictionary<string, string> { ["build_property.IoCToolsManualSeverity"] = severityValue };

        var (compilation, diagnostics) = CompileWithMSBuildProperties(sourceCode, properties);

        var ioc002Diagnostics = diagnostics.Where(d => d.Id == "IOC002").ToList();
        ioc002Diagnostics.Should().ContainSingle();

        // Should parse case-insensitively
        var expectedSeverity = severityValue.ToLowerInvariant() switch
        {
            "error" => DiagnosticSeverity.Error,
            "warning" => DiagnosticSeverity.Warning,
            "info" => DiagnosticSeverity.Info,
            "hidden" => DiagnosticSeverity.Hidden,
            _ => DiagnosticSeverity.Warning
        };

        ioc002Diagnostics[0].Severity.Should().Be(expectedSeverity);
    }

    #endregion

    #region IoCToolsDisableDiagnostics Configuration Tests

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    public void MSBuildDiagnostics_DisableDiagnostics_True_DisablesAllDiagnostics(string booleanValue)
    {
        var sourceCode = GetMissingImplementationSource();
        var properties = new Dictionary<string, string>
        {
            ["build_property.IoCToolsDisableDiagnostics"] = booleanValue
        };

        var (compilation, diagnostics) = CompileWithMSBuildProperties(sourceCode, properties);

        // When diagnostics are disabled, no IOC001 or IOC002 diagnostics should be reported
        var iocDiagnostics = diagnostics.Where(d => d.Id.StartsWith("IOC")).ToList();
        iocDiagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData("false")]
    [InlineData("False")]
    [InlineData("FALSE")]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("no")]
    public void MSBuildDiagnostics_DisableDiagnostics_False_EnablesDiagnostics(string booleanValue)
    {
        var sourceCode = GetMissingImplementationSource();
        var properties = new Dictionary<string, string>
        {
            ["build_property.IoCToolsDisableDiagnostics"] = booleanValue
        };

        var (compilation, diagnostics) = CompileWithMSBuildProperties(sourceCode, properties);

        // When diagnostics are enabled (false or invalid values), IOC001 should be reported
        var ioc001Diagnostics = diagnostics.Where(d => d.Id == "IOC001").ToList();
        ioc001Diagnostics.Should().ContainSingle();
    }

    #endregion

    #region Combined Configuration Tests

    [Fact]
    public void MSBuildDiagnostics_CombinedConfiguration_DifferentSeverities()
    {
        // Test configuring different severities for different diagnostic types
        var combinedSource = @"
using IoCTools.Abstractions.Annotations;

namespace TestNamespace
{
    public interface IMissingService { }
    public interface IUnmanagedService { }
    
    // UnmanagedService lacks lifetime attribute to trigger IOC002
    public class UnmanagedService : IUnmanagedService { }

    [Scoped]
    public partial class TestService
    {
        [Inject] private readonly IMissingService _missingService;
        [Inject] private readonly IUnmanagedService _unmanagedService;
    }
}";

        var properties = new Dictionary<string, string>
        {
            ["build_property.IoCToolsNoImplementationSeverity"] = "Error",
            ["build_property.IoCToolsManualSeverity"] = "Info"
        };

        var (compilation, diagnostics) = CompileWithMSBuildProperties(combinedSource, properties);

        var ioc001Diagnostics = diagnostics.Where(d => d.Id == "IOC001").ToList();
        var ioc002Diagnostics = diagnostics.Where(d => d.Id == "IOC002").ToList();

        ioc001Diagnostics.Should().ContainSingle();
        ioc002Diagnostics.Should().ContainSingle();

        ioc001Diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        ioc002Diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Info);
    }

    [Fact]
    public void MSBuildDiagnostics_CombinedConfiguration_DisableTakesPrecedence()
    {
        // Test that IoCToolsDisableDiagnostics takes precedence over severity settings
        var sourceCode = GetMissingImplementationSource();
        var properties = new Dictionary<string, string>
        {
            ["build_property.IoCToolsDisableDiagnostics"] = "true",
            ["build_property.IoCToolsNoImplementationSeverity"] = "Error",
            ["build_property.IoCToolsManualSeverity"] = "Error"
        };

        var (compilation, diagnostics) = CompileWithMSBuildProperties(sourceCode, properties);

        // Even with Error severity configured, disable should take precedence
        var iocDiagnostics = diagnostics.Where(d => d.Id.StartsWith("IOC")).ToList();
        iocDiagnostics.Should().BeEmpty();
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public void MSBuildDiagnostics_EmptyValues_UsesDefaults()
    {
        // Test behavior when MSBuild properties are set to empty values
        var sourceCode = GetMissingImplementationSource();
        var properties = new Dictionary<string, string>
        {
            ["build_property.IoCToolsNoImplementationSeverity"] = "",
            ["build_property.IoCToolsManualSeverity"] = "",
            ["build_property.IoCToolsDisableDiagnostics"] = ""
        };

        var (compilation, diagnostics) = CompileWithMSBuildProperties(sourceCode, properties);

        var ioc001Diagnostics = diagnostics.Where(d => d.Id == "IOC001").ToList();
        ioc001Diagnostics.Should().ContainSingle();
        // Empty values should fall back to default Warning severity
        ioc001Diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void MSBuildDiagnostics_WhitespaceValues_UsesDefaults()
    {
        // Test behavior when MSBuild properties are set to whitespace
        var sourceCode = GetMissingImplementationSource();
        var properties = new Dictionary<string, string>
        {
            ["build_property.IoCToolsNoImplementationSeverity"] = "   ",
            ["build_property.IoCToolsDisableDiagnostics"] = "\t\n"
        };

        var (compilation, diagnostics) = CompileWithMSBuildProperties(sourceCode, properties);

        var ioc001Diagnostics = diagnostics.Where(d => d.Id == "IOC001").ToList();
        ioc001Diagnostics.Should().ContainSingle();
        // Whitespace values should fall back to default Warning severity
        ioc001Diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    #endregion

    #region Documentation and Usage Examples

    [Fact]
    public void MSBuildDiagnostics_PropertyNamingConvention_Documentation()
    {
        // Document the MSBuild property naming convention
        var expectedProperties = new[]
        {
            "build_property.IoCToolsNoImplementationSeverity", "build_property.IoCToolsManualSeverity",
            "build_property.IoCToolsDisableDiagnostics"
        };

        // All properties should follow the build_property.IoCTools[Feature] pattern
        foreach (var property in expectedProperties) property.Should().StartWith("build_property.IoCTools");

        // Severity properties should end with "Severity"
        var severityProperties = expectedProperties.Where(p => p.Contains("Severity"));
        severityProperties.Count().Should().Be(2);
        severityProperties.Should().AllSatisfy(p => p.Should().EndWith("Severity"));
    }

    [Fact]
    public void MSBuildDiagnostics_UsageExamples_Documentation()
    {
        // Example MSBuild configuration:
        // <PropertyGroup>
        //   <!-- Configure severity for missing implementations (default: Warning) -->
        //   <IoCToolsNoImplementationSeverity>Error</IoCToolsNoImplementationSeverity>
        //   
        //   <!-- Configure severity for unregistered implementations (default: Warning) -->
        //   <IoCToolsManualSeverity>Info</IoCToolsManualSeverity>
        //   
        //   <!-- Disable all dependency validation diagnostics (default: false) -->
        //   <IoCToolsDisableDiagnostics>true</IoCToolsDisableDiagnostics>
        // </PropertyGroup>

        true.Should().BeTrue("MSBuild configuration examples documented in CLAUDE.md");
    }

    #endregion
}
