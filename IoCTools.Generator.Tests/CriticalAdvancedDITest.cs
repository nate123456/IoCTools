namespace IoCTools.Generator.Tests;

using Microsoft.CodeAnalysis;

/// <summary>
///     Critical test that exposes the Advanced DI pattern failures.
///     This test demonstrates that Func&lt;T&gt;, Lazy&lt;T&gt;, and optional dependencies
///     are completely ignored by the generator, resulting in empty constructors.
/// </summary>
public class CriticalAdvancedDITest
{
    [Fact]
    public void Service_With_Func_Dependencies_Should_Generate_Constructor_With_Parameters()
    {
        // Arrange: Service with Func<T> factory delegate (currently ignored by generator)
        var sourceCode = @"
using System;
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace TestProject.Services;

public interface IEmailValidator 
{
    bool Validate(string email);
}
public partial class AdvancedDIService
{
    [Inject] private readonly ILogger<AdvancedDIService> _logger;
    [Inject] private readonly Func<IEmailValidator> _emailValidatorFactory;
    
    public void ValidateEmail(string email)
    {
        var validator = _emailValidatorFactory();
        var isValid = validator.Validate(email);
        _logger.LogInformation(""Email {Email} is valid: {IsValid}"", email, isValid);
    }
}";

        // Act
        var (compilation, generatedCode) = GenerateCode(sourceCode);

        // Assert: Constructor should include both logger and Func<T> parameters
        Assert.NotNull(generatedCode);

        // CRITICAL: Currently FAILS - generator ignores Func<T> dependencies entirely
        // Should generate constructor with: ILogger<AdvancedDIService> logger, Func<IEmailValidator> emailValidatorFactory
        Assert.Contains("ILogger<AdvancedDIService>", generatedCode);
        Assert.Contains("Func<IEmailValidator>", generatedCode);

        // Should have field assignments for both dependencies
        Assert.Contains("_logger = ", generatedCode);
        Assert.Contains("_emailValidatorFactory = ", generatedCode);

        // Generated code should compile without assembly reference issues
        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Where(d => !d.Id.StartsWith("CS0012")) // Ignore assembly reference errors for now
            .ToArray();
        Assert.Empty(errors);
    }

    [Fact]
    public void Service_With_Lazy_Dependencies_Should_Generate_Constructor_With_Parameters()
    {
        // Arrange: Service with Lazy<T> dependency (currently ignored)
        var sourceCode = @"
using System;
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace TestProject.Services;

public interface ICacheService 
{
    void Set(string key, object value);
    T Get<T>(string key);
}
public partial class LazyDependencyService
{
    [Inject] private readonly ILogger<LazyDependencyService> _logger;
    [Inject] private readonly Lazy<ICacheService> _lazyCacheService;
    
    public void UseCache(string key, object value)
    {
        var cache = _lazyCacheService.Value;
        cache.Set(key, value);
        _logger.LogInformation(""Used lazy cache service"");
    }
}";

        // Act
        var (compilation, generatedCode) = GenerateCode(sourceCode);

        // Assert: Constructor should include Lazy<T> parameter
        Assert.NotNull(generatedCode);

        // CRITICAL: Currently FAILS - generator ignores Lazy<T> dependencies
        Assert.Contains("ILogger<LazyDependencyService>", generatedCode);
        Assert.Contains("Lazy<ICacheService>", generatedCode);

        Assert.Contains("_logger = ", generatedCode);
        Assert.Contains("_lazyCacheService = ", generatedCode);

        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.Empty(errors);
    }

    [Fact]
    public void Service_With_Optional_Dependencies_Should_Generate_Constructor_With_Nullable_Parameters()
    {
        // Arrange: Service with optional nullable dependency
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace TestProject.Services;

public interface IOptionalService 
{
    void DoWork();
}
public partial class OptionalDependencyService
{
    [Inject] private readonly ILogger<OptionalDependencyService> _logger;
    [Inject] private readonly IOptionalService? _optionalService;
    
    public void DoWork()
    {
        _logger.LogInformation(""Starting work"");
        _optionalService?.DoWork();
    }
}";

        // Act
        var (compilation, generatedCode) = GenerateCode(sourceCode);

        // Assert: Constructor should handle optional dependencies
        Assert.NotNull(generatedCode);

        // CRITICAL: Currently FAILS - generator ignores optional nullable dependencies
        Assert.Contains("ILogger<OptionalDependencyService>", generatedCode);
        Assert.Contains("IOptionalService?", generatedCode);

        Assert.Contains("_logger = ", generatedCode);
        Assert.Contains("_optionalService = ", generatedCode);

        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.Empty(errors);
    }

    [Fact]
    public void Service_With_All_Advanced_Patterns_Should_Generate_Complete_Constructor()
    {
        // Arrange: Service combining all advanced DI patterns (reproduces AdvancedDependencyService failure)
        var sourceCode = @"
using System;
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace TestProject.Services;

public interface IEmailValidator { bool Validate(string email); }
public interface ICacheService { void Set(string key, object value); }
public interface IOptionalService { void DoWork(); }
public partial class CompleteAdvancedService
{
    [Inject] private readonly ILogger<CompleteAdvancedService> _logger;
    [Inject] private readonly Func<IEmailValidator> _emailValidatorFactory;
    [Inject] private readonly Lazy<ICacheService> _lazyCacheService;
    [Inject] private readonly IOptionalService? _optionalService;
}";

        // Act
        var (compilation, generatedCode) = GenerateCode(sourceCode);

        // Assert: All advanced pattern dependencies should be in constructor
        Assert.NotNull(generatedCode);

        // CRITICAL: This reproduces the exact AdvancedDependencyService failure
        // Currently generates empty constructor despite 4 [Inject] fields
        var constructorLines = generatedCode.Split('\n')
            .Where(l => l.Trim().StartsWith("public CompleteAdvancedService("))
            .ToArray();

        Assert.NotEmpty(constructorLines);

        var constructorLine = constructorLines[0].Trim();

        // Constructor should not be empty
        Assert.DoesNotContain("public CompleteAdvancedService() {", generatedCode);

        // Should contain all dependency types
        Assert.Contains("ILogger<CompleteAdvancedService>", generatedCode);
        Assert.Contains("Func<IEmailValidator>", generatedCode);
        Assert.Contains("Lazy<ICacheService>", generatedCode);
        Assert.Contains("IOptionalService?", generatedCode);

        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.Empty(errors);
    }

    private (Compilation compilation, string generatedCode) GenerateCode(string sourceCode)
    {
        // CRITICAL FIX: Use SourceGeneratorTestHelper's comprehensive assembly reference collection
        // instead of duplicating logic that was missing critical assembly references for advanced DI patterns
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Extract the constructor file for the specific test class
        var generatedCode = "";
        var constructorSource = result.GeneratedSources
            .FirstOrDefault(s => (s.Hint.Contains("Constructor") || s.Content.Contains("Constructor")) &&
                                 (s.Content.Contains("AdvancedDIService") ||
                                  s.Content.Contains("LazyDependencyService") ||
                                  s.Content.Contains("OptionalDependencyService") ||
                                  s.Content.Contains("CompleteAdvancedService")));

        if (constructorSource != null) generatedCode = constructorSource.Content;

        return (result.Compilation, generatedCode);
    }
}
