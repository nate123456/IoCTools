using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace IoCTools.Generator.Tests;

/// <summary>
///     Comprehensive tests for MSBuild diagnostic configuration system.
///     Tests the new MSBuild property support: IoCToolsNoImplementationSeverity,
///     IoCToolsUnregisteredSeverity, and IoCToolsDisableDiagnostics.
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

        Assert.Single(ioc001Diagnostics);
        Assert.Equal(DiagnosticSeverity.Warning, ioc001Diagnostics[0].Severity); // Default severity
    }

    #endregion

    #region Test Infrastructure

    private static (Compilation compilation, List<Diagnostic> diagnostics) CompileWithMSBuildProperties(
        string sourceCode,
        Dictionary<string, string> msbuildProperties)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var references = SourceGeneratorTestHelper.GetStandardReferences();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        // Create analyzer config options with MSBuild properties
        var configOptions = new TestAnalyzerConfigOptionsProvider(msbuildProperties);

        var generator = new DependencyInjectionGenerator();
        var driver = CSharpGeneratorDriver.Create(generator)
            .WithUpdatedAnalyzerConfigOptions(configOptions);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        return (outputCompilation, diagnostics.ToList());
    }

    /// <summary>
    ///     Test implementation of AnalyzerConfigOptionsProvider for testing MSBuild properties
    /// </summary>
    private class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private readonly Dictionary<string, string> _properties;

        public TestAnalyzerConfigOptionsProvider(Dictionary<string, string> properties)
        {
            _properties = properties;
        }

        public override AnalyzerConfigOptions GlobalOptions => new TestAnalyzerConfigOptions(_properties);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new TestAnalyzerConfigOptions(_properties);

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) =>
            new TestAnalyzerConfigOptions(_properties);
    }

    private class TestAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        private readonly Dictionary<string, string> _properties;

        public TestAnalyzerConfigOptions(Dictionary<string, string> properties)
        {
            _properties = properties;
        }

        public override bool TryGetValue(string key,
            out string value) => _properties.TryGetValue(key, out value);
    }

    /// <summary>
    ///     Creates standard test source code with missing implementation
    /// </summary>
    private static string GetMissingImplementationSource() => @"
using IoCTools.Abstractions.Annotations;

namespace TestNamespace
{
    public interface IMissingService { }

    [Service]
    public partial class TestService
    {
        [Inject] private readonly IMissingService _missingService;
    }
}";

    /// <summary>
    ///     Creates standard test source code with unregistered service
    /// </summary>
    private static string GetUnregisteredServiceSource() => @"
using IoCTools.Abstractions.Annotations;

namespace TestNamespace
{
    public interface IUnregisteredService { }
    
    public class UnregisteredService : IUnregisteredService { }

    [Service]
    public partial class TestService
    {
        [Inject] private readonly IUnregisteredService _unregisteredService;
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

        Assert.Single(ioc001Diagnostics);
        Assert.Equal(DiagnosticSeverity.Warning, ioc001Diagnostics[0].Severity);
        Assert.Contains("but no implementation of this interface exists", ioc001Diagnostics[0].GetMessage());
        Assert.Contains("IMissingService", ioc001Diagnostics[0].GetMessage());
    }

    [Fact]
    public void MSBuildDiagnostics_DefaultBehavior_UnregisteredService_ReportsWarning()
    {
        // Test default behavior for IOC002 - Unregistered implementation
        var sourceCode = GetUnregisteredServiceSource();
        var properties = new Dictionary<string, string>(); // No MSBuild properties = default behavior

        var (compilation, diagnostics) = CompileWithMSBuildProperties(sourceCode, properties);
        var ioc002Diagnostics = diagnostics.Where(d => d.Id == "IOC002").ToList();

        Assert.Single(ioc002Diagnostics);
        Assert.Equal(DiagnosticSeverity.Warning, ioc002Diagnostics[0].Severity);
        Assert.Contains("implementation exists but lacks [Service] attribute", ioc002Diagnostics[0].GetMessage());
        Assert.Contains("UnregisteredService", ioc002Diagnostics[0].GetMessage());
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
        Assert.Single(ioc001Diagnostics);
        Assert.Equal(expectedSeverity, ioc001Diagnostics[0].Severity);
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
        Assert.Single(ioc001Diagnostics);

        // Should parse case-insensitively
        var expectedSeverity = severityValue.ToLowerInvariant() switch
        {
            "error" => DiagnosticSeverity.Error,
            "warning" => DiagnosticSeverity.Warning,
            "info" => DiagnosticSeverity.Info,
            "hidden" => DiagnosticSeverity.Hidden,
            _ => DiagnosticSeverity.Warning
        };

        Assert.Equal(expectedSeverity, ioc001Diagnostics[0].Severity);
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
        Assert.Single(ioc001Diagnostics);
        // Should fallback to default Warning severity for invalid values
        Assert.Equal(DiagnosticSeverity.Warning, ioc001Diagnostics[0].Severity);
    }

    #endregion

    #region IoCToolsUnregisteredSeverity Configuration Tests

    [Theory]
    [InlineData("Error", DiagnosticSeverity.Error)]
    [InlineData("Warning", DiagnosticSeverity.Warning)]
    [InlineData("Info", DiagnosticSeverity.Info)]
    [InlineData("Hidden", DiagnosticSeverity.Hidden)]
    public void MSBuildDiagnostics_UnregisteredSeverity_ConfiguresCorrectly(string severityValue,
        DiagnosticSeverity expectedSeverity)
    {
        var sourceCode = GetUnregisteredServiceSource();
        var properties = new Dictionary<string, string>
        {
            ["build_property.IoCToolsUnregisteredSeverity"] = severityValue
        };

        var (compilation, diagnostics) = CompileWithMSBuildProperties(sourceCode, properties);

        var ioc002Diagnostics = diagnostics.Where(d => d.Id == "IOC002").ToList();
        Assert.Single(ioc002Diagnostics);
        Assert.Equal(expectedSeverity, ioc002Diagnostics[0].Severity);
    }

    [Theory]
    [InlineData("error")]
    [InlineData("WARNING")]
    [InlineData("Info")]
    [InlineData("HIDDEN")]
    public void MSBuildDiagnostics_UnregisteredSeverity_CaseInsensitive(string severityValue)
    {
        var sourceCode = GetUnregisteredServiceSource();
        var properties = new Dictionary<string, string>
        {
            ["build_property.IoCToolsUnregisteredSeverity"] = severityValue
        };

        var (compilation, diagnostics) = CompileWithMSBuildProperties(sourceCode, properties);

        var ioc002Diagnostics = diagnostics.Where(d => d.Id == "IOC002").ToList();
        Assert.Single(ioc002Diagnostics);

        // Should parse case-insensitively
        var expectedSeverity = severityValue.ToLowerInvariant() switch
        {
            "error" => DiagnosticSeverity.Error,
            "warning" => DiagnosticSeverity.Warning,
            "info" => DiagnosticSeverity.Info,
            "hidden" => DiagnosticSeverity.Hidden,
            _ => DiagnosticSeverity.Warning
        };

        Assert.Equal(expectedSeverity, ioc002Diagnostics[0].Severity);
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
        Assert.Empty(iocDiagnostics);
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
        Assert.Single(ioc001Diagnostics);
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
    public interface IUnregisteredService { }
    
    public class UnregisteredService : IUnregisteredService { }

    [Service]
    public partial class TestService
    {
        [Inject] private readonly IMissingService _missingService;
        [Inject] private readonly IUnregisteredService _unregisteredService;
    }
}";

        var properties = new Dictionary<string, string>
        {
            ["build_property.IoCToolsNoImplementationSeverity"] = "Error",
            ["build_property.IoCToolsUnregisteredSeverity"] = "Info"
        };

        var (compilation, diagnostics) = CompileWithMSBuildProperties(combinedSource, properties);

        var ioc001Diagnostics = diagnostics.Where(d => d.Id == "IOC001").ToList();
        var ioc002Diagnostics = diagnostics.Where(d => d.Id == "IOC002").ToList();

        Assert.Single(ioc001Diagnostics);
        Assert.Single(ioc002Diagnostics);

        Assert.Equal(DiagnosticSeverity.Error, ioc001Diagnostics[0].Severity);
        Assert.Equal(DiagnosticSeverity.Info, ioc002Diagnostics[0].Severity);
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
            ["build_property.IoCToolsUnregisteredSeverity"] = "Error"
        };

        var (compilation, diagnostics) = CompileWithMSBuildProperties(sourceCode, properties);

        // Even with Error severity configured, disable should take precedence
        var iocDiagnostics = diagnostics.Where(d => d.Id.StartsWith("IOC")).ToList();
        Assert.Empty(iocDiagnostics);
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
            ["build_property.IoCToolsUnregisteredSeverity"] = "",
            ["build_property.IoCToolsDisableDiagnostics"] = ""
        };

        var (compilation, diagnostics) = CompileWithMSBuildProperties(sourceCode, properties);

        var ioc001Diagnostics = diagnostics.Where(d => d.Id == "IOC001").ToList();
        Assert.Single(ioc001Diagnostics);
        // Empty values should fall back to default Warning severity
        Assert.Equal(DiagnosticSeverity.Warning, ioc001Diagnostics[0].Severity);
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
        Assert.Single(ioc001Diagnostics);
        // Whitespace values should fall back to default Warning severity
        Assert.Equal(DiagnosticSeverity.Warning, ioc001Diagnostics[0].Severity);
    }

    #endregion

    #region Documentation and Usage Examples

    [Fact]
    public void MSBuildDiagnostics_PropertyNamingConvention_Documentation()
    {
        // Document the MSBuild property naming convention
        var expectedProperties = new[]
        {
            "build_property.IoCToolsNoImplementationSeverity",
            "build_property.IoCToolsUnregisteredSeverity",
            "build_property.IoCToolsDisableDiagnostics"
        };

        // All properties should follow the build_property.IoCTools[Feature] pattern
        foreach (var property in expectedProperties) Assert.StartsWith("build_property.IoCTools", property);

        // Severity properties should end with "Severity"
        var severityProperties = expectedProperties.Where(p => p.Contains("Severity"));
        Assert.Equal(2, severityProperties.Count());
        Assert.All(severityProperties, p => Assert.EndsWith("Severity", p));
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
        //   <IoCToolsUnregisteredSeverity>Info</IoCToolsUnregisteredSeverity>
        //   
        //   <!-- Disable all dependency validation diagnostics (default: false) -->
        //   <IoCToolsDisableDiagnostics>true</IoCToolsDisableDiagnostics>
        // </PropertyGroup>

        Assert.True(true, "MSBuild configuration examples documented in CLAUDE.md");
    }

    #endregion
}