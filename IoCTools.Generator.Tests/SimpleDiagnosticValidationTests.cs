namespace IoCTools.Generator.Tests;

using Diagnostics;

using Microsoft.CodeAnalysis;

/// <summary>
///     Simple validation tests for diagnostic improvements made
/// </summary>
public class SimpleDiagnosticValidationTests
{
    /// <summary>
    ///     Test that IOC014 diagnostic is NOT generated for background services - their lifetime is managed by
    ///     AddHostedService()
    /// </summary>
    [Fact]
    public void IOC014_BackgroundServiceWithAnyLifetime_NoError()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Hosting;

namespace Test;

[Scoped]
public partial class TestBackgroundService : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.CompletedTask;
    }
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var ioc014Diagnostics = result.GetDiagnosticsByCode("IOC014");

        // No IOC014 errors for hosted services - their lifetime is managed by AddHostedService()
        ioc014Diagnostics.Should().BeEmpty();
    }

    /// <summary>
    ///     Test that IOC010 is deprecated and shows appropriate deprecation message
    /// </summary>
    [Fact]
    public void IOC010_ShouldBeDeprecatedWithMessage()
    {
        // We're testing the descriptor directly since IOC010 usage is removed from code
#pragma warning disable CS0618 // Intentional usage to verify deprecation surface
        var descriptor = DiagnosticDescriptors.BackgroundServiceLifetimeConflict;
#pragma warning restore CS0618

        descriptor.Id.Should().Be("IOC010");
        descriptor.Title.ToString().Should().Contain("deprecated");
        descriptor.Description.ToString().Should().Contain("IOC014");
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
public partial class TestService
{
    [Inject] IUnknownService unknownService;
}

public interface IUnknownService { }";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var ioc001Diagnostics = result.GetDiagnosticsByCode("IOC001");

        ioc001Diagnostics.Should().ContainSingle();
        var helpText = ioc001Diagnostics[0].Descriptor.Description.ToString();
        helpText.Should().Contain("Fix options:");
        helpText.Should().Contain("1)");
        helpText.Should().Contain("2)");
        helpText.Should().Contain("3)");
    }

    /// <summary>
    ///     Test that diagnostic messages have improved formatting and suggestions
    /// </summary>
    [Fact]
    public void DiagnosticMessages_ShouldHaveConsistentActionableFormat()
    {
        var keyDiagnostics = new[]
        {
            DiagnosticDescriptors.NoImplementationFound, DiagnosticDescriptors.ImplementationNotRegistered,
            DiagnosticDescriptors.SingletonDependsOnScoped,
            DiagnosticDescriptors.BackgroundServiceLifetimeValidation
        };

        foreach (var diagnostic in keyDiagnostics)
        {
            if (diagnostic.Id == "IOC010") continue; // Skip deprecated diagnostic

            var helpText = diagnostic.Description.ToString();
            helpText.Should().NotBeNullOrWhiteSpace(
                $"Diagnostic {diagnostic.Id} should have help text");

            // Should contain action words or numbered options
            var hasActionableContent = helpText.Contains("Fix") || helpText.Contains("1)") ||
                                       helpText.Contains("Add") || helpText.Contains("Change") ||
                                       helpText.Contains("options:");

            hasActionableContent.Should().BeTrue(
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
        DiagnosticDescriptors.SingletonDependsOnScoped.DefaultSeverity.Should()
            .Be(DiagnosticSeverity.Error);
        DiagnosticDescriptors.BackgroundServiceLifetimeValidation.DefaultSeverity.Should()
            .Be(DiagnosticSeverity.Error);
        DiagnosticDescriptors.BackgroundServiceNotPartial.DefaultSeverity.Should()
            .Be(DiagnosticSeverity.Error);

        // Warning severity for best practices
        DiagnosticDescriptors.NoImplementationFound.DefaultSeverity.Should()
            .Be(DiagnosticSeverity.Warning);
        DiagnosticDescriptors.ImplementationNotRegistered.DefaultSeverity.Should()
            .Be(DiagnosticSeverity.Warning);
        DiagnosticDescriptors.SingletonDependsOnTransient.DefaultSeverity.Should()
            .Be(DiagnosticSeverity.Warning);
    }
}
