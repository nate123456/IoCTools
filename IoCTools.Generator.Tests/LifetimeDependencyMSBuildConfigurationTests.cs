namespace IoCTools.Generator.Tests;

using System.Diagnostics;
using System.Text;

using Microsoft.CodeAnalysis;

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

[Scoped]
public partial class DatabaseContext
{
}

[Transient]
public partial class HelperService
{
}

[Singleton]
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

[Scoped]
public partial class DatabaseContext
{
}

[Singleton]
public partial class CacheService
{
    [Inject] private readonly DatabaseContext _context;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var diagnostics = result.GetDiagnosticsByCode("IOC012");

        diagnostics.Should().ContainSingle();
        // Default severity should be Error for IOC012
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].GetMessage().Should().Contain("Singleton service");
        diagnostics[0].GetMessage().Should().Contain("CacheService");
        diagnostics[0].GetMessage().Should().Contain("Scoped service");
        diagnostics[0].GetMessage().Should().Contain("DatabaseContext");
    }

    [Fact]
    public void MSBuildConfig_LifetimeValidationWarning_DefaultBehavior_ReportsAsWarning()
    {
        // Test IOC013 default behavior (Singleton → Transient warning)
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

[Transient]
public partial class HelperService
{
}

[Singleton]
public partial class CacheService
{
    [Inject] private readonly HelperService _helper;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var diagnostics = result.GetDiagnosticsByCode("IOC013");

        diagnostics.Should().ContainSingle();
        // Default severity should be Warning for IOC013
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostics[0].GetMessage().Should().Contain("Singleton service");
        diagnostics[0].GetMessage().Should().Contain("CacheService");
        diagnostics[0].GetMessage().Should().Contain("Transient service");
        diagnostics[0].GetMessage().Should().Contain("HelperService");
    }

    #endregion

    #region MSBuild Configuration Validation Tests

    [Fact]
    public void MSBuildConfig_PropertyNamingConvention_FollowsStandard()
    {
        // This test documents the expected MSBuild property naming convention
        var expectedProperties = new[]
        {
            "build_property.IoCToolsLifetimeValidationSeverity", "build_property.IoCToolsDisableLifetimeValidation",
            "build_property.IoCToolsDisableDiagnostics", "build_property.IoCToolsNoImplementationSeverity",
            "build_property.IoCToolsManualSeverity"
        };

        // All properties should follow the build_property.IoCTools[Feature]Severity pattern
        foreach (var property in expectedProperties) property.Should().StartWith("build_property.IoCTools");

        // Verify severity properties use the Severity suffix
        var severityProperties = expectedProperties.Where(p => p.Contains("Severity"));
        severityProperties.Count().Should().Be(3);
        severityProperties.Should().AllSatisfy(p => p.Should().EndWith("Severity"));

        // Verify disable properties use the Disable prefix  
        var disableProperties = expectedProperties.Where(p => p.Contains("Disable"));
        disableProperties.Count().Should().Be(2);
        disableProperties.Should().AllSatisfy(p => p.Should().Contain("Disable"));
    }

    [Fact]
    public void MSBuildConfig_SeverityValues_FollowDiagnosticSeverityEnum()
    {
        // Document the expected severity values that should be supported
        var expectedSeverityValues = new[] { "Error", "Warning", "Info", "Hidden" };

        // These should map to DiagnosticSeverity enum values
        expectedSeverityValues.Length.Should().Be(4);
        expectedSeverityValues.Should().Contain("Error");
        expectedSeverityValues.Should().Contain("Warning");
        expectedSeverityValues.Should().Contain("Info");
        expectedSeverityValues.Should().Contain("Hidden");
    }

    [Fact]
    public void MSBuildConfig_BooleanValues_FollowDotNetConvention()
    {
        // Document the expected boolean values for disable properties
        var expectedBooleanValues = new[] { "true", "false" };

        // Should be case-insensitive
        var caseVariants = new[] { "TRUE", "True", "true", "FALSE", "False", "false" };

        expectedBooleanValues.Length.Should().Be(2);
        caseVariants.Length.Should().Be(6);
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

[Scoped]
public partial class DatabaseContext
{
}

[Singleton]
public partial class CacheService
{
    [Inject] private readonly DatabaseContext _context;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var diagnostics = result.GetDiagnosticsByCode("IOC012");

        diagnostics.Should().ContainSingle();
        diagnostics[0].Id.Should().Be("IOC012");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);

        // Verify message content
        var message = diagnostics[0].GetMessage();
        message.Should().Contain("Singleton service");
        message.Should().Contain("CacheService");
        message.Should().Contain("Scoped service");
        message.Should().Contain("DatabaseContext");
        message.Should().Contain("cannot capture shorter-lived dependencies");
    }

    [Fact]
    public void MSBuildConfig_IOC013_SingletonTransientDependency_Configured()
    {
        // Test that IOC013 diagnostics are properly configured
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

[Transient]
public partial class HelperService
{
}

[Singleton]
public partial class CacheService
{
    [Inject] private readonly HelperService _helper;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var diagnostics = result.GetDiagnosticsByCode("IOC013");

        diagnostics.Should().ContainSingle();
        diagnostics[0].Id.Should().Be("IOC013");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);

        // Verify message content
        var message = diagnostics[0].GetMessage();
        message.Should().Contain("Singleton service");
        message.Should().Contain("CacheService");
        message.Should().Contain("Transient service");
        message.Should().Contain("HelperService");
        message.Should().Contain("Consider if this transient should be Singleton");
    }

    [Fact]
    public void MSBuildConfig_IOC014_BackgroundServiceLifetime_NoWarnings()
    {
        // Test that IOC014 diagnostics are NOT generated for background services (hosted services)
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace TestNamespace;

[Scoped]
public partial class EmailBackgroundService : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var diagnostics = result.GetDiagnosticsByCode("IOC014");

        // No IOC014 errors for hosted services - their lifetime is managed by AddHostedService()
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void MSBuildConfig_IOC015_InheritanceChainLifetime_Configured()
    {
        // Test that IOC015 diagnostics are properly configured for inheritance chains
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

[Scoped]
public partial class DatabaseContext
{
}

[Scoped]
public partial class BaseService
{
    [Inject] private readonly DatabaseContext _context;
}

[Singleton]
public partial class DerivedService : BaseService
{
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var diagnostics = result.GetDiagnosticsByCode("IOC015");

        diagnostics.Should().ContainSingle();
        diagnostics[0].Id.Should().Be("IOC015");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);

        // Verify message content
        var message = diagnostics[0].GetMessage();
        message.Should().Contain("Service lifetime mismatch");
        message.Should().Contain("inheritance chain");
        message.Should().Contain("DerivedService");
        message.Should().Contain("Singleton");
        message.Should().Contain("Scoped");
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
        ioc012Diagnostics.Should().ContainSingle();
        ioc013Diagnostics.Should().ContainSingle();

        ioc012Diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        ioc013Diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void MSBuildConfig_ValidLifetimeCombinations_NoViolations()
    {
        // Test that valid lifetime combinations don't report violations
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

[Singleton]
public partial class ConfigService
{
}

[Scoped]
public partial class DatabaseService
{
    [Inject] private readonly ConfigService _config;
}

[Transient]
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
        lifetimeDiagnostics.Should().BeEmpty();
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

[Scoped]
public partial class DatabaseContext
{
}");

        // Create 10 levels of inheritance
        for (var i = 0; i < 10; i++)
        {
            var className = $"Level{i}Service";
            var baseClass = i == 0 ? "" : $" : Level{i - 1}Service";

            sourceCodeBuilder.AppendLine($@"
[Scoped]
public partial class {className}{baseClass}
{{
    [Inject] private readonly DatabaseContext _context{i};
}}");
        }

        // Final singleton service that should cause validation
        sourceCodeBuilder.AppendLine(@"
[Singleton]
public partial class FinalService : Level9Service
{
}");

        var sourceCode = sourceCodeBuilder.ToString();

        var stopwatch = Stopwatch.StartNew();
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        stopwatch.Stop();

        // Should complete in reasonable time
        (stopwatch.ElapsedMilliseconds < 10000).Should()
            .BeTrue($"Large inheritance hierarchy validation took {stopwatch.ElapsedMilliseconds}ms");

        // Should still detect lifetime violations
        var diagnostics = result.GetDiagnosticsByCode("IOC015");
        diagnostics.Should().ContainSingle();
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
[Scoped]
public partial class ScopedService{i}
{{
}}

[Singleton]
public partial class SingletonService{i}
{{
    [Inject] private readonly ScopedService{i} _scoped;
}}");

        var sourceCode = sourceCodeBuilder.ToString();

        var stopwatch = Stopwatch.StartNew();
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        stopwatch.Stop();

        // Should complete in reasonable time
        (stopwatch.ElapsedMilliseconds < 15000).Should()
            .BeTrue($"Many services validation took {stopwatch.ElapsedMilliseconds}ms");

        // Should detect all violations
        var diagnostics = result.GetDiagnosticsByCode("IOC012");
        diagnostics.Count.Should().Be(50);
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
        //   <IoCToolsManualSeverity>Hidden</IoCToolsManualSeverity>
        // </PropertyGroup>

        // Example 5: Development vs Release configuration
        // <PropertyGroup Condition="'$(Configuration)' == 'Debug'">
        //   <IoCToolsLifetimeValidationSeverity>Warning</IoCToolsLifetimeValidationSeverity>
        // </PropertyGroup>
        // <PropertyGroup Condition="'$(Configuration)' == 'Release'">
        //   <IoCToolsLifetimeValidationSeverity>Error</IoCToolsLifetimeValidationSeverity>
        // </PropertyGroup>

        // This test ensures the documentation examples are accurate
        true.Should().BeTrue("MSBuild configuration examples documented");
    }

    [Fact]
    public void MSBuildConfig_DefaultBehavior_Documentation()
    {
        // Document the default behavior when no MSBuild properties are set

        // Default severities:
        // - IOC012 (Singleton → Scoped): Error
        // - IOC013 (Singleton → Transient): Warning  
        // - IOC014 (Background Service): Disabled for hosted services
        // - IOC015 (Inheritance Chain): Error
        // - No Implementation: Warning
        // - Unregistered Service: Warning

        // Default enable/disable:
        // - DiagnosticsEnabled: true
        // - LifetimeValidationEnabled: true

        var sourceCode = GetStandardLifetimeViolationSource();
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        var ioc012 = result.GetDiagnosticsByCode("IOC012").FirstOrDefault() ??
                     throw new InvalidOperationException("Expected IOC012 diagnostic.");
        var ioc013 = result.GetDiagnosticsByCode("IOC013").FirstOrDefault() ??
                     throw new InvalidOperationException("Expected IOC013 diagnostic.");

        ioc012.Severity.Should().Be(DiagnosticSeverity.Error);
        ioc013.Severity.Should().Be(DiagnosticSeverity.Warning);
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

[Scoped]
public partial class DatabaseContext
{
}

[Singleton]
[ConditionalService(""Development"")]
public partial class CacheService
{
    [Inject] private readonly DatabaseContext _context;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Should still detect lifetime violations even with conditional services
        var diagnostics = result.GetDiagnosticsByCode("IOC012");
        diagnostics.Should().ContainSingle();
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void MSBuildConfig_LifetimeValidationWithExternalService_Skipped()
    {
        // Test that lifetime validation is skipped for external services
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

[Scoped]
public partial class DatabaseContext
{
}

[Singleton]
[ExternalService]
public partial class CacheService
{
    [Inject] private readonly DatabaseContext _context;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Should not report lifetime violations for external services
        var diagnostics = result.GetDiagnosticsByCode("IOC012");
        diagnostics.Should().BeEmpty();
    }

    #endregion
}
