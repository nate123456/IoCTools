using Microsoft.CodeAnalysis;

namespace IoCTools.Generator.Tests;

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

[Service]
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
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(e => e.ToString()))}");

        var constructorSource = result.GetConstructorSource("ConfigurationService");
        Assert.NotNull(constructorSource);

        var generatedCode = constructorSource.Content;

        Assert.Contains("Cache:ExpirationMinutes", generatedCode);
        Assert.Contains("Features:EnableLogging", generatedCode);

        // Should use standard .NET GetValue overload with default values (NOT null-coalescing)
        // GetValue<int>("key", defaultValue) is the correct .NET pattern for value types
        Assert.Contains("GetValue<int>(\"Cache:ExpirationMinutes\", 60)", generatedCode);
        Assert.Contains("GetValue<bool>(\"Features:EnableLogging\", true)", generatedCode);

        // Should NOT use null-coalescing operator for value types (doesn't work with GetValue<T>)
        Assert.DoesNotContain("?? 60", generatedCode);
        Assert.DoesNotContain("?? true", generatedCode);

        // Should NOT use null-forgiving operator for fields with defaults
        Assert.DoesNotContain("GetValue<int>(\"Cache:ExpirationMinutes\")!", generatedCode);
        Assert.DoesNotContain("GetValue<bool>(\"Features:EnableLogging\")!", generatedCode);
    }

    [Fact]
    public void Service_With_Required_Configuration_Should_Generate_Proper_Validation()
    {
        // Arrange: Service with Required = true should generate proper .NET validation
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Configuration;

namespace TestProject.Services;

[Service]
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
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(e => e.ToString()))}");

        var constructorSource = result.GetConstructorSource("DatabaseService");
        Assert.NotNull(constructorSource);

        var generatedCode = constructorSource.Content;

        Assert.Contains("Database:ConnectionString", generatedCode);
        Assert.Contains("Optional:Feature", generatedCode);

        // Required configuration should use null-coalescing with exception for reference types
        // This is the correct pattern for string types (unlike value types)
        Assert.Contains("GetValue<string>(\"Database:ConnectionString\")", generatedCode);
        Assert.Contains("?? throw new", generatedCode);
        Assert.Contains("ArgumentException", generatedCode); // Use ArgumentException for missing required config
        Assert.Contains("Required configuration", generatedCode);
        Assert.Contains("Database:ConnectionString", generatedCode);

        // Optional configuration should not have validation
        Assert.Contains("GetValue<string?>(\"Optional:Feature\")", generatedCode);
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

[Service]
public partial class ReloadableService
{
    [InjectConfiguration(""HotReload"", SupportsReloading = true)]
    private readonly ReloadableSettings _settings;
    
    public string GetCurrentSetting() => _settings.Setting;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert: Should generate IOptionsSnapshot<T> or IOptionsMonitor<T> injection
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(e => e.ToString()))}");

        var constructorSource = result.GetConstructorSource("ReloadableService");
        Assert.NotNull(constructorSource);

        var generatedCode = constructorSource.Content;

        // CRITICAL: Currently FAILS - SupportsReloading is ignored
        // Should generate IOptionsSnapshot<ReloadableSettings> injection instead of direct binding

        Assert.Contains("ReloadableSettings", generatedCode);

        // Should use Options pattern for reloadable configuration
        var usesOptionsPattern = generatedCode.Contains("IOptionsSnapshot") ||
                                 generatedCode.Contains("IOptionsMonitor") ||
                                 generatedCode.Contains("IOptions");

        Assert.True(usesOptionsPattern, "SupportsReloading should use Options pattern");
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

[Service]
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
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(e => e.ToString()))}");

        var constructorSource = result.GetConstructorSource("CollectionService");
        Assert.NotNull(constructorSource);

        var generatedCode = constructorSource.Content;

        // Core functionality checks
        Assert.Contains("AllowedHosts", generatedCode);
        Assert.Contains("TrustedPorts", generatedCode);

        // CRITICAL FIX VERIFICATION: Should use concrete type binding for interface collections
        // IReadOnlyList<string> should bind as List<string> with .AsReadOnly() conversion
        Assert.Contains("List<string>", generatedCode);
        Assert.Contains("AsReadOnly", generatedCode);
        Assert.Contains("GetSection(\"AllowedHosts\").Get<List<string>>()?.AsReadOnly()!", generatedCode);

        // Arrays should work directly without conversion
        Assert.Contains("int[]", generatedCode);
        Assert.Contains("GetSection(\"TrustedPorts\").Get<int[]>()!", generatedCode);

        // Should NOT contain the problematic direct interface binding anymore
        Assert.DoesNotContain("Get<IReadOnlyList<string>>()!", generatedCode);
    }
}