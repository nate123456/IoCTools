namespace IoCTools.Generator.Tests;

using Microsoft.CodeAnalysis;

/// <summary>
///     Critical test that exposes Configuration Injection implementation gaps.
///     This test demonstrates that DefaultValue, Required, and SupportsReloading attributes
///     are completely ignored by the generator despite being documented features.
/// </summary>
public class CriticalConfigurationInjectionTest
{
    [Fact]
    public void Service_With_DefaultValue_Configuration_Should_Generate_Standard_Pattern()
    {
        // Arrange: Service with DefaultValue attribute should use standard .NET GetValue(key, defaultValue) pattern
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TestProject.Services;
public partial class ConfigurationService
{
    [Inject] private readonly ILogger<ConfigurationService> _logger;
    [Inject] private readonly IConfiguration _configuration;
    
    [InjectConfiguration(""Cache:ExpirationMinutes"", DefaultValue = ""60"")]
    private readonly int _cacheExpiration;
    
    [InjectConfiguration(""Features:EnableLogging"", DefaultValue = ""true"")]
    private readonly bool _enableLogging;
    
    public void LogCacheInfo()
    {
        _logger.LogInformation(""Cache expiration: {Expiration} minutes"", _cacheExpiration);
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert: Generated constructor should use standard .NET GetValue(key, defaultValue) pattern
        result.HasErrors.Should()
            .BeFalse(
                $"Compilation errors: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(e => e.ToString()))}");

        var constructorSource = result.GetRequiredConstructorSource("ConfigurationService");

        var generatedCode = constructorSource.Content;

        generatedCode.Should().Contain("Cache:ExpirationMinutes");
        generatedCode.Should().Contain("Features:EnableLogging");

        // Should use standard .NET GetValue overload with default values (NOT null-coalescing)
        // GetValue<int>("key", defaultValue) is the correct .NET pattern for value types
        generatedCode.Should().Contain("GetValue<int>(\"Cache:ExpirationMinutes\", 60)");
        generatedCode.Should().Contain("GetValue<bool>(\"Features:EnableLogging\", true)");

        // Should NOT use null-coalescing operator for value types (doesn't work with GetValue<T>)
        generatedCode.Should().NotContain("?? 60");
        generatedCode.Should().NotContain("?? true");

        // Should NOT use null-forgiving operator for fields with defaults
        generatedCode.Should().NotContain("GetValue<int>(\"Cache:ExpirationMinutes\")!");
        generatedCode.Should().NotContain("GetValue<bool>(\"Features:EnableLogging\")!");
    }

    [Fact]
    public void Service_With_Required_Configuration_Should_Generate_Proper_Validation()
    {
        // Arrange: Service with Required = true should generate proper .NET validation
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Configuration;

namespace TestProject.Services;
public partial class DatabaseService
{
    [Inject] private readonly IConfiguration _configuration;
    
    [InjectConfiguration(""Database:ConnectionString"", Required = true)]
    private readonly string _connectionString;
    
    [InjectConfiguration(""Optional:Feature"", Required = false)]
    private readonly string? _optionalFeature;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert: Required config should generate proper validation using .NET patterns
        result.HasErrors.Should()
            .BeFalse(
                $"Compilation errors: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(e => e.ToString()))}");

        var constructorSource = result.GetRequiredConstructorSource("DatabaseService");

        var generatedCode = constructorSource.Content;

        generatedCode.Should().Contain("Database:ConnectionString");
        generatedCode.Should().Contain("Optional:Feature");

        // Required configuration should use null-coalescing with exception for reference types
        // This is the correct pattern for string types (unlike value types)
        generatedCode.Should().Contain("GetValue<string>(\"Database:ConnectionString\")");
        generatedCode.Should().Contain("?? throw new");
        generatedCode.Should().Contain("ArgumentException"); // Use ArgumentException for missing required config
        generatedCode.Should().Contain("Required configuration");
        generatedCode.Should().Contain("Database:ConnectionString");

        // Optional configuration should not have validation
        generatedCode.Should().Contain("GetValue<string?>(\"Optional:Feature\")");
    }

    [Fact]
    public void Service_With_SupportsReloading_Should_Generate_Options_Pattern()
    {
        // Arrange: Service with SupportsReloading = true (currently ignored)
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Options;

namespace TestProject.Services;

public class ReloadableSettings
{
    public string Setting { get; set; } = """";
    public int Value { get; set; }
}
public partial class ReloadableService
{
    [InjectConfiguration(""HotReload"", SupportsReloading = true)]
    private readonly ReloadableSettings _settings;
    
    public string GetCurrentSetting() => _settings.Setting;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert: Should generate IOptionsSnapshot<T> or IOptionsMonitor<T> injection
        result.HasErrors.Should()
            .BeFalse(
                $"Compilation errors: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(e => e.ToString()))}");

        var constructorSource = result.GetRequiredConstructorSource("ReloadableService");

        var generatedCode = constructorSource.Content;

        // CRITICAL: Currently FAILS - SupportsReloading is ignored
        // Should generate IOptionsSnapshot<ReloadableSettings> injection instead of direct binding

        generatedCode.Should().Contain("ReloadableSettings");

        // Should use Options pattern for reloadable configuration
        var usesOptionsPattern = generatedCode.Contains("IOptionsSnapshot") ||
                                 generatedCode.Contains("IOptionsMonitor") ||
                                 generatedCode.Contains("IOptions");

        usesOptionsPattern.Should().BeTrue("SupportsReloading should use Options pattern");
    }

    [Fact]
    public void Service_With_Collection_Configuration_Should_Generate_Correct_Binding()
    {
        // Arrange: Service with collection configuration (interface binding currently fails)
        var sourceCode = @"
using System.Collections.Generic;
using System.Linq;
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Configuration;

namespace TestProject.Services;
public partial class CollectionService
{
    [Inject] private readonly IConfiguration _configuration;
    
    [InjectConfiguration(""AllowedHosts"", Required = false)]
    private readonly IReadOnlyList<string> _allowedHosts;
    
    [InjectConfiguration(""TrustedPorts"", Required = false)]
    private readonly int[] _trustedPorts;
    
    public bool IsHostAllowed(string host) => _allowedHosts.Contains(host);
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert: Should generate proper collection binding
        result.HasErrors.Should()
            .BeFalse(
                $"Compilation errors: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(e => e.ToString()))}");

        var constructorSource = result.GetRequiredConstructorSource("CollectionService");

        var generatedCode = constructorSource.Content;

        // Core functionality checks
        generatedCode.Should().Contain("AllowedHosts");
        generatedCode.Should().Contain("TrustedPorts");

        // CRITICAL FIX VERIFICATION: Should use concrete type binding for interface collections
        // IReadOnlyList<string> should bind as List<string> with .AsReadOnly() conversion
        generatedCode.Should().Contain("List<string>");
        generatedCode.Should().Contain("AsReadOnly");
        generatedCode.Should().Contain("GetSection(\"AllowedHosts\").Get<List<string>>()?.AsReadOnly()!");

        // Arrays should work directly without conversion
        generatedCode.Should().Contain("int[]");
        generatedCode.Should().Contain("GetSection(\"TrustedPorts\").Get<int[]>()!");

        // Should NOT contain the problematic direct interface binding anymore
        generatedCode.Should().NotContain("Get<IReadOnlyList<string>>()!");
    }
}
