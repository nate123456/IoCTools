using IoCTools.Generator.Diagnostics;
using Microsoft.CodeAnalysis;

namespace IoCTools.Generator.Tests;

/// <summary>
///     Simple validation tests for diagnostic improvements made
/// </summary>
public class SimpleDiagnosticValidationTests
{
    /// <summary>
    ///     Test that IOC014 diagnostic is generated for background services with wrong lifetime
    /// </summary>
    [Fact]
    public void IOC014_BackgroundServiceWithWrongLifetime_ShouldGenerateDiagnostic()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Hosting;

namespace Test;

[Service(Lifetime.Scoped)]
public partial class TestBackgroundService : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var ioc014Diagnostics = result.GetDiagnosticsByCode("IOC014");

        Assert.Single(ioc014Diagnostics);
        Assert.Contains("TestBackgroundService", ioc014Diagnostics[0].GetMessage());
        Assert.Contains("Scoped", ioc014Diagnostics[0].GetMessage());
    }

    /// <summary>
    ///     Test that IOC010 is deprecated and shows appropriate deprecation message
    /// </summary>
    [Fact]
    public void IOC010_ShouldBeDeprecatedWithMessage()
    {
        // We're testing the descriptor directly since IOC010 usage is removed from code
        var descriptor = DiagnosticDescriptors.BackgroundServiceLifetimeConflict;

        Assert.Equal("IOC010", descriptor.Id);
        Assert.Contains("deprecated", descriptor.Title.ToString());
        Assert.Contains("IOC014", descriptor.Description.ToString());
    }

    /// <summary>
    ///     Test that IOC001 diagnostic has improved help text with actionable suggestions
    /// </summary>
    [Fact]
    public void IOC001_ShouldHaveActionableFixSuggestions()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

[Service]
public partial class TestService
{
    [Inject] IUnknownService unknownService;
}

public interface IUnknownService { }";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var ioc001Diagnostics = result.GetDiagnosticsByCode("IOC001");

        Assert.Single(ioc001Diagnostics);
        var helpText = ioc001Diagnostics[0].Descriptor.Description.ToString();
        Assert.Contains("Fix options:", helpText);
        Assert.Contains("1)", helpText);
        Assert.Contains("2)", helpText);
        Assert.Contains("3)", helpText);
    }

    /// <summary>
    ///     Test that diagnostic messages have improved formatting and suggestions
    /// </summary>
    [Fact]
    public void DiagnosticMessages_ShouldHaveConsistentActionableFormat()
    {
        var keyDiagnostics = new[]
        {
            DiagnosticDescriptors.NoImplementationFound,
            DiagnosticDescriptors.ImplementationNotRegistered,
            DiagnosticDescriptors.SingletonDependsOnScoped,
            DiagnosticDescriptors.BackgroundServiceLifetimeValidation
        };

        foreach (var diagnostic in keyDiagnostics)
        {
            if (diagnostic.Id == "IOC010") continue; // Skip deprecated diagnostic

            var helpText = diagnostic.Description.ToString();
            Assert.True(!string.IsNullOrWhiteSpace(helpText),
                $"Diagnostic {diagnostic.Id} should have help text");

            // Should contain action words or numbered options
            var hasActionableContent = helpText.Contains("Fix") || helpText.Contains("1)") ||
                                       helpText.Contains("Add") || helpText.Contains("Change") ||
                                       helpText.Contains("options:");

            Assert.True(hasActionableContent,
                $"Diagnostic {diagnostic.Id} should have actionable fix suggestions. Help text: {helpText}");
        }
    }

    /// <summary>
    ///     Test that severity levels are appropriate
    /// </summary>
    [Fact]
    public void DiagnosticSeverityLevels_ShouldBeAppropriate()
    {
        // Error severity for critical issues
        Assert.Equal(DiagnosticSeverity.Error,
            DiagnosticDescriptors.SingletonDependsOnScoped.DefaultSeverity);
        Assert.Equal(DiagnosticSeverity.Error,
            DiagnosticDescriptors.BackgroundServiceLifetimeValidation.DefaultSeverity);
        Assert.Equal(DiagnosticSeverity.Error,
            DiagnosticDescriptors.BackgroundServiceNotPartial.DefaultSeverity);

        // Warning severity for best practices
        Assert.Equal(DiagnosticSeverity.Warning,
            DiagnosticDescriptors.NoImplementationFound.DefaultSeverity);
        Assert.Equal(DiagnosticSeverity.Warning,
            DiagnosticDescriptors.ImplementationNotRegistered.DefaultSeverity);
        Assert.Equal(DiagnosticSeverity.Warning,
            DiagnosticDescriptors.SingletonDependsOnTransient.DefaultSeverity);
    }
}