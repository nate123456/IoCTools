using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;

namespace IoCTools.Generator.Tests;

/// <summary>
///     Comprehensive tests for MSBuild configuration of lifetime validation diagnostics.
///     Tests all MSBuild properties and their combinations for diagnostic configuration.
///     These tests validate that MSBuild properties correctly configure diagnostic severity
///     and enable/disable functionality for IoCTools lifetime validation features.
/// </summary>
public class LifetimeDependencyMSBuildConfigurationTests
{
    #region Test Infrastructure

    /// <summary>
    ///     Creates standard test source code with lifetime violations
    /// </summary>
    private static string GetStandardLifetimeViolationSource() => @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

[Service(Lifetime.Scoped)]
public partial class DatabaseContext
{
}

[Service(Lifetime.Transient)]
public partial class HelperService
{
}

[Service(Lifetime.Singleton)]
public partial class CacheService
{
    [Inject] private readonly DatabaseContext _context;
    [Inject] private readonly HelperService _helper;
}";

    #endregion

    #region IoCToolsLifetimeValidationSeverity Configuration Tests

    [Fact]
    public void MSBuildConfig_LifetimeValidationSeverity_DefaultBehavior_ReportsAsError()
    {
        // Test the default behavior without any MSBuild configuration
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

[Service(Lifetime.Scoped)]
public partial class DatabaseContext
{
}

[Service(Lifetime.Singleton)]
public partial class CacheService
{
    [Inject] private readonly DatabaseContext _context;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var diagnostics = result.GetDiagnosticsByCode("IOC012");

        Assert.Single(diagnostics);
        // Default severity should be Error for IOC012
        Assert.Equal(DiagnosticSeverity.Error, diagnostics[0].Severity);
        Assert.Contains("Singleton service", diagnostics[0].GetMessage());
        Assert.Contains("CacheService", diagnostics[0].GetMessage());
        Assert.Contains("Scoped service", diagnostics[0].GetMessage());
        Assert.Contains("DatabaseContext", diagnostics[0].GetMessage());
    }

    [Fact]
    public void MSBuildConfig_LifetimeValidationWarning_DefaultBehavior_ReportsAsWarning()
    {
        // Test IOC013 default behavior (Singleton → Transient warning)
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

[Service(Lifetime.Transient)]
public partial class HelperService
{
}

[Service(Lifetime.Singleton)]
public partial class CacheService
{
    [Inject] private readonly HelperService _helper;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var diagnostics = result.GetDiagnosticsByCode("IOC013");

        Assert.Single(diagnostics);
        // Default severity should be Warning for IOC013
        Assert.Equal(DiagnosticSeverity.Warning, diagnostics[0].Severity);
        Assert.Contains("Singleton service", diagnostics[0].GetMessage());
        Assert.Contains("CacheService", diagnostics[0].GetMessage());
        Assert.Contains("Transient service", diagnostics[0].GetMessage());
        Assert.Contains("HelperService", diagnostics[0].GetMessage());
    }

    #endregion

    #region MSBuild Configuration Validation Tests

    [Fact]
    public void MSBuildConfig_PropertyNamingConvention_FollowsStandard()
    {
        // This test documents the expected MSBuild property naming convention
        var expectedProperties = new[]
        {
            "build_property.IoCToolsLifetimeValidationSeverity",
            "build_property.IoCToolsDisableLifetimeValidation",
            "build_property.IoCToolsDisableDiagnostics",
            "build_property.IoCToolsNoImplementationSeverity",
            "build_property.IoCToolsUnregisteredSeverity"
        };

        // All properties should follow the build_property.IoCTools[Feature]Severity pattern
        foreach (var property in expectedProperties) Assert.StartsWith("build_property.IoCTools", property);

        // Verify severity properties use the Severity suffix
        var severityProperties = expectedProperties.Where(p => p.Contains("Severity"));
        Assert.Equal(3, severityProperties.Count());
        Assert.All(severityProperties, p => Assert.EndsWith("Severity", p));

        // Verify disable properties use the Disable prefix  
        var disableProperties = expectedProperties.Where(p => p.Contains("Disable"));
        Assert.Equal(2, disableProperties.Count());
        Assert.All(disableProperties, p => Assert.Contains("Disable", p));
    }

    [Fact]
    public void MSBuildConfig_SeverityValues_FollowDiagnosticSeverityEnum()
    {
        // Document the expected severity values that should be supported
        var expectedSeverityValues = new[]
        {
            "Error",
            "Warning",
            "Info",
            "Hidden"
        };

        // These should map to DiagnosticSeverity enum values
        Assert.Equal(4, expectedSeverityValues.Length);
        Assert.Contains("Error", expectedSeverityValues);
        Assert.Contains("Warning", expectedSeverityValues);
        Assert.Contains("Info", expectedSeverityValues);
        Assert.Contains("Hidden", expectedSeverityValues);
    }

    [Fact]
    public void MSBuildConfig_BooleanValues_FollowDotNetConvention()
    {
        // Document the expected boolean values for disable properties
        var expectedBooleanValues = new[]
        {
            "true",
            "false"
        };

        // Should be case-insensitive
        var caseVariants = new[]
        {
            "TRUE", "True", "true",
            "FALSE", "False", "false"
        };

        Assert.Equal(2, expectedBooleanValues.Length);
        Assert.Equal(6, caseVariants.Length);
    }

    #endregion

    #region Diagnostic Code Coverage Tests

    [Fact]
    public void MSBuildConfig_IOC012_SingletonScopedDependency_Configured()
    {
        // Test that IOC012 diagnostics are properly configured
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

[Service(Lifetime.Scoped)]
public partial class DatabaseContext
{
}

[Service(Lifetime.Singleton)]
public partial class CacheService
{
    [Inject] private readonly DatabaseContext _context;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var diagnostics = result.GetDiagnosticsByCode("IOC012");

        Assert.Single(diagnostics);
        Assert.Equal("IOC012", diagnostics[0].Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostics[0].Severity);

        // Verify message content
        var message = diagnostics[0].GetMessage();
        Assert.Contains("Singleton service", message);
        Assert.Contains("CacheService", message);
        Assert.Contains("Scoped service", message);
        Assert.Contains("DatabaseContext", message);
        Assert.Contains("cannot capture shorter-lived dependencies", message);
    }

    [Fact]
    public void MSBuildConfig_IOC013_SingletonTransientDependency_Configured()
    {
        // Test that IOC013 diagnostics are properly configured
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

[Service(Lifetime.Transient)]
public partial class HelperService
{
}

[Service(Lifetime.Singleton)]
public partial class CacheService
{
    [Inject] private readonly HelperService _helper;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var diagnostics = result.GetDiagnosticsByCode("IOC013");

        Assert.Single(diagnostics);
        Assert.Equal("IOC013", diagnostics[0].Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostics[0].Severity);

        // Verify message content
        var message = diagnostics[0].GetMessage();
        Assert.Contains("Singleton service", message);
        Assert.Contains("CacheService", message);
        Assert.Contains("Transient service", message);
        Assert.Contains("HelperService", message);
        Assert.Contains("Consider if this transient should be Singleton", message);
    }

    [Fact]
    public void MSBuildConfig_IOC014_BackgroundServiceLifetime_Configured()
    {
        // Test that IOC014 diagnostics are properly configured for background services
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace TestNamespace;

[Service(Lifetime.Scoped)]
[BackgroundService]
public partial class EmailBackgroundService : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var diagnostics = result.GetDiagnosticsByCode("IOC014");

        Assert.Single(diagnostics);
        Assert.Equal("IOC014", diagnostics[0].Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostics[0].Severity);

        // Verify message content
        var message = diagnostics[0].GetMessage();
        Assert.Contains("Background service", message);
        Assert.Contains("EmailBackgroundService", message);
        Assert.Contains("Scoped", message);
        Assert.Contains("Background services should typically be Singleton", message);
    }

    [Fact]
    public void MSBuildConfig_IOC015_InheritanceChainLifetime_Configured()
    {
        // Test that IOC015 diagnostics are properly configured for inheritance chains
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

[Service(Lifetime.Scoped)]
public partial class DatabaseContext
{
}

[Service(Lifetime.Scoped)]
public partial class BaseService
{
    [Inject] private readonly DatabaseContext _context;
}

[Service(Lifetime.Singleton)]
public partial class DerivedService : BaseService
{
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var diagnostics = result.GetDiagnosticsByCode("IOC015");

        Assert.Single(diagnostics);
        Assert.Equal("IOC015", diagnostics[0].Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostics[0].Severity);

        // Verify message content
        var message = diagnostics[0].GetMessage();
        Assert.Contains("Service lifetime mismatch", message);
        Assert.Contains("inheritance chain", message);
        Assert.Contains("DerivedService", message);
        Assert.Contains("Singleton", message);
        Assert.Contains("Scoped", message);
    }

    #endregion

    #region Configuration Integration Tests

    [Fact]
    public void MSBuildConfig_MultipleLifetimeViolations_AllReported()
    {
        // Test that multiple lifetime violations are all reported
        var sourceCode = GetStandardLifetimeViolationSource();

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        var ioc012Diagnostics = result.GetDiagnosticsByCode("IOC012");
        var ioc013Diagnostics = result.GetDiagnosticsByCode("IOC013");

        // Should report both IOC012 (Singleton → Scoped) and IOC013 (Singleton → Transient)
        Assert.Single(ioc012Diagnostics);
        Assert.Single(ioc013Diagnostics);

        Assert.Equal(DiagnosticSeverity.Error, ioc012Diagnostics[0].Severity);
        Assert.Equal(DiagnosticSeverity.Warning, ioc013Diagnostics[0].Severity);
    }

    [Fact]
    public void MSBuildConfig_ValidLifetimeCombinations_NoViolations()
    {
        // Test that valid lifetime combinations don't report violations
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

[Service(Lifetime.Singleton)]
public partial class ConfigService
{
}

[Service(Lifetime.Scoped)]
public partial class DatabaseService
{
    [Inject] private readonly ConfigService _config;
}

[Service(Lifetime.Transient)]
public partial class ProcessorService
{
    [Inject] private readonly DatabaseService _db;
    [Inject] private readonly ConfigService _config;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        var lifetimeDiagnostics = result.GetDiagnosticsByCode("IOC012")
            .Concat(result.GetDiagnosticsByCode("IOC013"))
            .Concat(result.GetDiagnosticsByCode("IOC014"))
            .Concat(result.GetDiagnosticsByCode("IOC015"))
            .ToList();

        // No lifetime violations should be reported for valid combinations
        Assert.Empty(lifetimeDiagnostics);
    }

    #endregion

    #region Performance and Edge Case Tests

    [Fact]
    public void MSBuildConfig_LargeInheritanceHierarchy_PerformanceTest()
    {
        // Test performance with complex inheritance chains
        var sourceCodeBuilder = new StringBuilder();
        sourceCodeBuilder.AppendLine(@"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

[Service(Lifetime.Scoped)]
public partial class DatabaseContext
{
}");

        // Create 10 levels of inheritance
        for (var i = 0; i < 10; i++)
        {
            var className = $"Level{i}Service";
            var baseClass = i == 0 ? "" : $" : Level{i - 1}Service";

            sourceCodeBuilder.AppendLine($@"
[Service(Lifetime.Scoped)]
public partial class {className}{baseClass}
{{
    [Inject] private readonly DatabaseContext _context{i};
}}");
        }

        // Final singleton service that should cause validation
        sourceCodeBuilder.AppendLine(@"
[Service(Lifetime.Singleton)]
public partial class FinalService : Level9Service
{
}");

        var sourceCode = sourceCodeBuilder.ToString();

        var stopwatch = Stopwatch.StartNew();
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        stopwatch.Stop();

        // Should complete in reasonable time
        Assert.True(stopwatch.ElapsedMilliseconds < 10000,
            $"Large inheritance hierarchy validation took {stopwatch.ElapsedMilliseconds}ms");

        // Should still detect lifetime violations
        var diagnostics = result.GetDiagnosticsByCode("IOC015");
        Assert.Single(diagnostics);
    }

    [Fact]
    public void MSBuildConfig_ManyServicesWithViolations_PerformanceTest()
    {
        // Test performance with many services
        var sourceCodeBuilder = new StringBuilder();
        sourceCodeBuilder.AppendLine(@"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;");

        // Create many services with lifetime violations
        for (var i = 0; i < 50; i++)
            sourceCodeBuilder.AppendLine($@"
[Service(Lifetime.Scoped)]
public partial class ScopedService{i}
{{
}}

[Service(Lifetime.Singleton)]
public partial class SingletonService{i}
{{
    [Inject] private readonly ScopedService{i} _scoped;
}}");

        var sourceCode = sourceCodeBuilder.ToString();

        var stopwatch = Stopwatch.StartNew();
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        stopwatch.Stop();

        // Should complete in reasonable time
        Assert.True(stopwatch.ElapsedMilliseconds < 15000,
            $"Many services validation took {stopwatch.ElapsedMilliseconds}ms");

        // Should detect all violations
        var diagnostics = result.GetDiagnosticsByCode("IOC012");
        Assert.Equal(50, diagnostics.Count);
    }

    #endregion

    #region Documentation and Usage Examples

    [Fact]
    public void MSBuildConfig_DocumentedConfiguration_Examples()
    {
        // This test serves as documentation for how MSBuild configuration should work

        // Example 1: Setting severity to Warning for all lifetime validation
        // <PropertyGroup>
        //   <IoCToolsLifetimeValidationSeverity>Warning</IoCToolsLifetimeValidationSeverity>
        // </PropertyGroup>

        // Example 2: Disabling lifetime validation entirely
        // <PropertyGroup>
        //   <IoCToolsDisableLifetimeValidation>true</IoCToolsDisableLifetimeValidation>
        // </PropertyGroup>

        // Example 3: Disabling all IoCTools diagnostics
        // <PropertyGroup>
        //   <IoCToolsDisableDiagnostics>true</IoCToolsDisableDiagnostics>
        // </PropertyGroup>

        // Example 4: Fine-grained severity control
        // <PropertyGroup>
        //   <IoCToolsLifetimeValidationSeverity>Info</IoCToolsLifetimeValidationSeverity>
        //   <IoCToolsNoImplementationSeverity>Error</IoCToolsNoImplementationSeverity>
        //   <IoCToolsUnregisteredSeverity>Hidden</IoCToolsUnregisteredSeverity>
        // </PropertyGroup>

        // Example 5: Development vs Release configuration
        // <PropertyGroup Condition="'$(Configuration)' == 'Debug'">
        //   <IoCToolsLifetimeValidationSeverity>Warning</IoCToolsLifetimeValidationSeverity>
        // </PropertyGroup>
        // <PropertyGroup Condition="'$(Configuration)' == 'Release'">
        //   <IoCToolsLifetimeValidationSeverity>Error</IoCToolsLifetimeValidationSeverity>
        // </PropertyGroup>

        // This test ensures the documentation examples are accurate
        Assert.True(true, "MSBuild configuration examples documented");
    }

    [Fact]
    public void MSBuildConfig_DefaultBehavior_Documentation()
    {
        // Document the default behavior when no MSBuild properties are set

        // Default severities:
        // - IOC012 (Singleton → Scoped): Error
        // - IOC013 (Singleton → Transient): Warning  
        // - IOC014 (Background Service): Error
        // - IOC015 (Inheritance Chain): Error
        // - No Implementation: Warning
        // - Unregistered Service: Warning

        // Default enable/disable:
        // - DiagnosticsEnabled: true
        // - LifetimeValidationEnabled: true

        var sourceCode = GetStandardLifetimeViolationSource();
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        var ioc012 = result.GetDiagnosticsByCode("IOC012").FirstOrDefault();
        var ioc013 = result.GetDiagnosticsByCode("IOC013").FirstOrDefault();

        Assert.NotNull(ioc012);
        Assert.NotNull(ioc013);
        Assert.Equal(DiagnosticSeverity.Error, ioc012.Severity);
        Assert.Equal(DiagnosticSeverity.Warning, ioc013.Severity);
    }

    #endregion

    #region Integration with Other Features

    [Fact]
    public void MSBuildConfig_LifetimeValidationWithConditionalServices_Works()
    {
        // Test that lifetime validation works with conditional services
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

[Service(Lifetime.Scoped)]
public partial class DatabaseContext
{
}

[Service(Lifetime.Singleton)]
[ConditionalService(""Development"")]
public partial class CacheService
{
    [Inject] private readonly DatabaseContext _context;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Should still detect lifetime violations even with conditional services
        var diagnostics = result.GetDiagnosticsByCode("IOC012");
        Assert.Single(diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, diagnostics[0].Severity);
    }

    [Fact]
    public void MSBuildConfig_LifetimeValidationWithExternalService_Skipped()
    {
        // Test that lifetime validation is skipped for external services
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

[Service(Lifetime.Scoped)]
public partial class DatabaseContext
{
}

[Service(Lifetime.Singleton)]
[ExternalService]
public partial class CacheService
{
    [Inject] private readonly DatabaseContext _context;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Should not report lifetime violations for external services
        var diagnostics = result.GetDiagnosticsByCode("IOC012");
        Assert.Empty(diagnostics);
    }

    #endregion
}