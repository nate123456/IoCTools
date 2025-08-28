namespace IoCTools.Generator.Tests;

using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;

/// <summary>
///     Critical test that exposes the Conditional Service logic failures.
///     This test demonstrates the impossible logic conditions generated like:
///     (config.GetValue("Flag") == "enabled" AND config.GetValue("Flag") != "enabled")
/// </summary>
public class CriticalConditionalLogicTest
{
    [Fact]
    public void Conflicting_Conditional_Services_Should_Not_Generate_Impossible_Logic()
    {
        // Arrange: Two services with conflicting conditions (reproduces LegacyPaymentProcessor issue)
        var sourceCode = @"
using System;
using IoCTools.Abstractions.Annotations;

namespace TestProject.Services;

public interface IPaymentProcessor 
{
    string ProcessPayment(decimal amount);
}
[ConditionalService(ConfigValue = ""FeatureFlags:NewPaymentProcessor"", Equals = ""enabled"")]
public partial class NewPaymentProcessor : IPaymentProcessor
{
    public string ProcessPayment(decimal amount) => $""New processor: {amount}"";
}
[ConditionalService(ConfigValue = ""FeatureFlags:NewPaymentProcessor"", NotEquals = ""enabled"")]
public partial class LegacyPaymentProcessor : IPaymentProcessor
{
    public string ProcessPayment(decimal amount) => $""Legacy processor: {amount}"";
}";

        // Act
        var (compilation, generatedCode) = GenerateServiceRegistrations(sourceCode);

        // Assert: Should NOT generate impossible logic conditions
        Assert.NotNull(generatedCode);
        // CRITICAL: This currently FAILS - generates impossible condition:
        // if ((config.GetValue("FeatureFlags:NewPaymentProcessor") ?? "") == "enabled" && 
        //     (config.GetValue("FeatureFlags:NewPaymentProcessor") ?? "") != "enabled")

        // CORE TEST: Should not generate impossible AND conditions
        // The critical bug was: (config.GetValue("Key") ?? "") == "enabled" && (config.GetValue("Key") ?? "") != "enabled"
        var normalizedCode = generatedCode.Replace("\n", " ").Replace("\r", " ");

        // Check for the specific impossible pattern
        var impossiblePattern = @"""enabled"" && \(configuration\.GetValue.*?!= ""enabled""";
        var hasImpossiblePattern = Regex.IsMatch(normalizedCode, impossiblePattern);
        Assert.False(hasImpossiblePattern,
            $"Found impossible AND condition pattern in generated code: {normalizedCode}");

        // Should not have the exact impossible sequence that was generated before
        Assert.DoesNotContain(@"== ""enabled"" && (configuration.GetValue", normalizedCode);

        // Should contain logical registration for each service based on condition
        Assert.Contains("NewPaymentProcessor", generatedCode);
        Assert.Contains("LegacyPaymentProcessor", generatedCode);

        // Should not have both services registered under impossible conditions
        var lines = generatedCode.Split('\n').Select(l => l.Trim()).ToArray();
        var impossibleConditions = lines.Where(l =>
            l.Contains("== \"enabled\"") && l.Contains("!= \"enabled\"")).ToArray();

        Assert.Empty(impossibleConditions);

        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.Empty(errors);
    }

    [Fact]
    public void Environment_Based_Conditional_Services_Should_Generate_Correct_Logic()
    {
        // Arrange: Environment-based conditional services
        var sourceCode = @"
using System;
using IoCTools.Abstractions.Annotations;

namespace TestProject.Services;

public interface IEmailService 
{
    void SendEmail(string to, string subject);
}
[ConditionalService(Environment = ""Development"")]
public partial class DevelopmentEmailService : IEmailService
{
    public void SendEmail(string to, string subject) => Console.WriteLine($""DEV: {subject}"");
}
[ConditionalService(Environment = ""Production"")]
public partial class ProductionEmailService : IEmailService
{
    public void SendEmail(string to, string subject) => Console.WriteLine($""PROD: {subject}"");
}";

        // Act
        var (compilation, generatedCode) = GenerateServiceRegistrations(sourceCode);

        // Assert: Should generate proper environment checks
        Assert.NotNull(generatedCode);

        // Should contain environment variable checks
        Assert.Contains("ASPNETCORE_ENVIRONMENT", generatedCode);
        Assert.Contains("Development", generatedCode);
        Assert.Contains("Production", generatedCode);

        // Should not have conflicting environment conditions
        var lines = generatedCode.Split('\n').Select(l => l.Trim()).ToArray();
        var environmentChecks = lines.Where(l => l.Contains("Environment.GetEnvironmentVariable")).ToArray();

        Assert.NotEmpty(environmentChecks);

        // Each service should have its own conditional block
        Assert.Contains("DevelopmentEmailService", generatedCode);
        Assert.Contains("ProductionEmailService", generatedCode);

        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.Empty(errors);
    }

    [Fact]
    public void Combined_Conditional_Services_Should_Generate_Valid_AND_Logic()
    {
        // Arrange: Service with combined environment and config conditions
        var sourceCode = @"
using System;
using IoCTools.Abstractions.Annotations;

namespace TestProject.Services;

public interface ISecurityService 
{
    bool ValidateToken(string token);
}
[ConditionalService(Environment = ""Production"", ConfigValue = ""Features:UseAdvancedSecurity"", Equals = ""true"")]
public partial class ProductionAdvancedSecurityService : ISecurityService
{
    public bool ValidateToken(string token) => true;
}";

        // Act
        var (compilation, generatedCode) = GenerateServiceRegistrations(sourceCode);

        // Assert: Should generate proper AND logic for combined conditions
        Assert.NotNull(generatedCode);

        // Should contain both environment and config checks
        Assert.Contains("ASPNETCORE_ENVIRONMENT", generatedCode);
        Assert.Contains("Production", generatedCode);
        Assert.Contains("Features:UseAdvancedSecurity", generatedCode);

        // Should use AND logic, not impossible conditions
        var containsAND = generatedCode.Contains("&&") || generatedCode.Contains(" AND ");
        Assert.True(containsAND, "Combined conditions should use AND logic");

        // Should not contain impossible logic
        var impossiblePatterns = new[]
        {
            @"== ""Production"" && != ""Production""", @"== ""true"" && != ""true""",
            @"!= ""Production"" && == ""Production""", @"!= ""true"" && == ""true"""
        };

        foreach (var pattern in impossiblePatterns)
            Assert.DoesNotContain(pattern, generatedCode.Replace(" ", "").Replace("\n", ""));

        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        Assert.Empty(errors);
    }

    private (Compilation compilation, string generatedCode) GenerateServiceRegistrations(string sourceCode)
    {
        // CRITICAL FIX: Use the proper test helper instead of incomplete reference setup
        // The original method was missing critical assemblies like netstandard and System.Runtime
        // which caused compilation to fail, resulting in no services being detected
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        var generatedCode = "";
        var registrationSource = result.GetServiceRegistrationSource();
        if (registrationSource != null) generatedCode = registrationSource.Content;

        return (result.Compilation, generatedCode);
    }
}
