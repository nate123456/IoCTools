using Microsoft.CodeAnalysis;
using System.Linq;
using Xunit;

namespace IoCTools.Generator.Tests;

/// <summary>
/// Test to verify that [InjectConfiguration] fields generate proper configuration binding
/// instead of being treated as regular DI dependencies
/// </summary>
public class ConfigurationInjectionFixTest
{
    [Fact]
    public void InjectConfiguration_PrimitiveTypes_ShouldGenerateConfigurationBinding()
    {
        // Arrange: Service with configuration fields
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace TestProject.Services;

[Service]
public partial class ConfigTestService
{
    [Inject] private readonly ILogger<ConfigTestService> _logger;
    [InjectConfiguration(""Database:ConnectionString"")] private readonly string _connectionString;
    [InjectConfiguration(""Cache:TTL"")] private readonly int _cacheTtl;
    
    public void LogConfig()
    {
        _logger.LogInformation(""Connection: {Connection}, TTL: {TTL}"", _connectionString, _cacheTtl);
    }
}
";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert: Should compile without IOC001 errors for primitive types
        var ioc001Diagnostics = result.GetDiagnosticsByCode("IOC001");
        
        // Should NOT have IOC001 errors for string or int (they should be configuration binding, not DI)
        Assert.False(ioc001Diagnostics.Any(d => d.GetMessage().Contains("'string'")), 
            "String configuration field should not be treated as DI dependency");
        Assert.False(ioc001Diagnostics.Any(d => d.GetMessage().Contains("'int'")), 
            "Int configuration field should not be treated as DI dependency");
        
        // Should compile successfully
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(e => e.ToString()))}");

        // Should generate constructor with IConfiguration parameter
        var constructorSource = result.GetConstructorSource("ConfigTestService");
        Assert.NotNull(constructorSource);
        
        var generatedCode = constructorSource.Content;
        
        // Should have IConfiguration parameter
        Assert.Contains("IConfiguration", generatedCode);
        
        // Should NOT have primitive type parameters
        Assert.DoesNotContain("string connectionString", generatedCode);
        Assert.DoesNotContain("int cacheTtl", generatedCode);
        
        // Should have configuration binding in constructor body
        Assert.Contains("GetValue", generatedCode);
        Assert.Contains("Database:ConnectionString", generatedCode);
        Assert.Contains("Cache:TTL", generatedCode);
    }
}
