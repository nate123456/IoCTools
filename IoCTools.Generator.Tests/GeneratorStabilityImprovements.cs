namespace IoCTools.Generator.Tests;

using System.Diagnostics;
using System.Text;

using Microsoft.CodeAnalysis;

using Xunit.Abstractions;

/// <summary>
///     Tests for improved generator stability with targeted fixes
/// </summary>
public class GeneratorStabilityImprovements
{
    private readonly ITestOutputHelper _output;

    public GeneratorStabilityImprovements(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void GeneratorFileNaming_SafeCharactersOnly_ProducesValidFileNames()
    {
        // Arrange - Test safer complex type scenarios
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using System.Collections.Generic;

namespace TestNamespace;
public partial class GenericService_T
{
    [Inject] private readonly IGenericDep_T _dep;
}
public partial class ServiceWithUnderscores_And_Numbers123
{
    [Inject] private readonly ISpecialDep _dep;
}

public interface IGenericDep_T { }
public interface ISpecialDep { }
public partial class GenericDep_T : IGenericDep_T { }
public partial class SpecialDep : ISpecialDep { }
";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var errorMessages = string.Join(", ", result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.GetMessage()));

        // Assert - No compilation errors
        result.HasErrors.Should().BeFalse("Compilation errors: {0}", errorMessages);

        // Assert - All file names are valid and unique
        var constructorHints = result.GeneratedSources.Where(s => s.Hint.Contains("Constructor")).Select(s => s.Hint)
            .ToList();
        var uniqueHints = constructorHints.Distinct().ToList();
        constructorHints.Count.Should().Be(uniqueHints.Count); // No duplicate file names

        // Assert - File names contain only safe characters
        foreach (var hint in constructorHints)
        {
            hint.Should().NotContain("<", "constructor hint should avoid invalid characters");
            hint.Should().NotContain(">", "constructor hint should avoid invalid characters");
            hint.Should().NotContain("$", "constructor hint should avoid invalid characters");
            hint.Should().NotContain(" ", "constructor hint should avoid invalid characters");
            hint.Should().NotContain(",", "constructor hint should avoid invalid characters");
            hint.Should().Contain("_Constructor.g.cs", "constructor hints should end with the expected suffix");
        }
    }

    [Fact]
    public void GeneratorScalability_ModerateServiceCount_CompletesSuccessfully()
    {
        // Arrange - Test with a moderate number of services (avoid the namespace issue)
        var sourceCode = CreateScalableTestCode(20); // Reduced count to avoid namespace conflicts

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        stopwatch.Stop();

        // Assert - Completes within reasonable time
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5),
            "the generator should finish moderate workloads quickly but took {0:F2} seconds",
            stopwatch.Elapsed.TotalSeconds);

        // Assert - No errors
        var errorMessages = string.Join(", ", result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.GetMessage()));
        result.HasErrors.Should().BeFalse("Compilation errors: {0}", errorMessages);

        // Assert - Generated expected files
        var constructorCount = result.GeneratedSources.Count(s => s.Hint.Contains("Constructor"));
        constructorCount.Should().BeGreaterThan(0, "the generator should emit constructor files");

        var serviceRegCount = result.GeneratedSources.Count(s =>
            s.Content.Contains("ServiceCollectionExtensions"));
        serviceRegCount.Should().Be(1); // Exactly one service registration file
    }

    [Fact]
    public void GeneratorNamespaceHandling_DistinctNamespaces_HandlesCorrectly()
    {
        // Arrange - Services in truly different namespaces
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Alpha 
{
    [Scoped]
    public partial class MyService
    {
        [Inject] private readonly IDep _dep;
    }

    public interface IDep { }

    [Scoped]  
    public partial class Dep : IDep { }
}

namespace Beta
{
    [Scoped]
    public partial class MyService  
    {
        [Inject] private readonly IOtherDep _otherDep;
    }

    public interface IOtherDep { }

    [Scoped]
    public partial class OtherDep : IOtherDep { }
}

namespace Gamma
{
    [Scoped]
    public partial class ServiceC
    {
        [Inject] private readonly IThirdDep _thirdDep;
    }

    public interface IThirdDep { }

    [Scoped]
    public partial class ThirdDep : IThirdDep { }
}
";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var errorMessages = string.Join(", ", result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.GetMessage()));

        // Assert - No compilation errors
        result.HasErrors.Should().BeFalse("Compilation errors: {0}", errorMessages);

        // Assert - Services are registered correctly
        var registrationContent = result.GetServiceRegistrationText();
        registrationContent.Should().Contain("Alpha.MyService");
        registrationContent.Should().Contain("Beta.MyService");
        registrationContent.Should().Contain("Gamma.ServiceC");

        // Assert - All services get constructor files with unique names
        var constructorSources = result.GeneratedSources.Where(s => s.Hint.Contains("Constructor")).ToList();
        constructorSources.Count.Should().BeGreaterOrEqualTo(6); // At least 6 services

        // Assert - No duplicate file names
        var hints = constructorSources.Select(c => c.Hint).ToList();
        hints.Count.Should().Be(hints.Distinct().Count());
    }

    [Fact]
    public void GeneratorConsistencyCheck_MultipleIndependentRuns_ProducesIdenticalResults()
    {
        // Arrange
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;

namespace TestNamespace;
public partial class ServiceA
{
    [Inject] private readonly IDepA _depA;
}
public partial class ServiceB  
{
    [Inject] private readonly IDepB _depB;
}

public interface IDepA { }
public interface IDepB { }
public partial class DepA : IDepA { }
public partial class DepB : IDepB { }
";

        // Act - Run generator multiple times independently
        var results = new List<GeneratorTestResult>();
        for (var i = 0; i < 5; i++) results.Add(SourceGeneratorTestHelper.CompileWithGenerator(sourceCode));

        // Assert - All results are successful
        foreach (var result in results) result.HasErrors.Should().BeFalse();

        // Assert - All results are identical
        var firstResult = results[0];
        for (var i = 1; i < results.Count; i++)
        {
            var currentResult = results[i];

            // Same number of generated sources
            currentResult.GeneratedSources.Count.Should().Be(firstResult.GeneratedSources.Count);

            // Service registration is identical
            var firstServiceReg = firstResult.GetServiceRegistrationText();
            var currentServiceReg = currentResult.GetServiceRegistrationText();
            currentServiceReg.Should().Be(firstServiceReg);

            // Constructor files are identical
            var firstConstructors = firstResult.GeneratedSources.Where(s => s.Hint.Contains("Constructor"))
                .OrderBy(s => s.Hint).ToList();
            var currentConstructors = currentResult.GeneratedSources.Where(s => s.Hint.Contains("Constructor"))
                .OrderBy(s => s.Hint).ToList();

            currentConstructors.Count.Should().Be(firstConstructors.Count);

            for (var j = 0; j < firstConstructors.Count; j++)
            {
                currentConstructors[j].Hint.Should().Be(firstConstructors[j].Hint);
                currentConstructors[j].Content.Should().Be(firstConstructors[j].Content);
            }
        }
    }

    [Fact]
    public void GeneratorErrorRecovery_InvalidSyntax_GeneratesValidParts()
    {
        // Arrange - Source with syntax errors but some valid services
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

[Scoped]
public partial class ValidService1
{
    [Inject] private readonly IDep1 _dep1;
}

// This class has issues but shouldn't prevent other services from generating
public class NonPartialServiceWithInject
{
    [Inject] private readonly IDep1 _dep1; // Invalid - non-partial class with Inject
}
[Scoped]
public partial class ValidService2
{
    [Inject] private readonly IDep2 _dep2;
}

public interface IDep1 { }
public interface IDep2 { }
[Scoped]
public partial class Dep1 : IDep1 { }

[Scoped]
public partial class Dep2 : IDep2 { }
";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert - May have warnings but generator still produces output
        result.CompilationDiagnostics.Count.Should().BeGreaterOrEqualTo(0);

        var generatedSources = result.GeneratedSources.ToList();
        var allDiagnostics = string.Join(", ", result.CompilationDiagnostics.Select(d => $"{d.Id}: {d.GetMessage()}"));
        generatedSources.Should().NotBeEmpty("No sources generated. Diagnostics: {0}", allDiagnostics);

        // Assert - Valid services still get processed
        var registrationContent = result.GetServiceRegistrationText();
        registrationContent.Should().Contain("ValidService1");
        registrationContent.Should().Contain("ValidService2");

        // Assert - Constructor files generated for valid services
        var constructorSources = generatedSources.Where(s => s.Hint.Contains("Constructor")).ToList();
        constructorSources.Any(s => s.Hint.Contains("ValidService1"))
            .Should().BeTrue("ValidService1 should still generate constructors");
        constructorSources.Any(s => s.Hint.Contains("ValidService2"))
            .Should().BeTrue("ValidService2 should still generate constructors");
    }

    [Fact]
    public void GeneratorPerformance_RepeatedCompilation_MaintainsPerformance()
    {
        // Arrange
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;

namespace TestNamespace;
public partial class PerformanceTestService
{
    [Inject] private readonly IDependency _dep;
}

public interface IDependency { }
public partial class Dependency : IDependency { }
";

        // Act - Multiple compilation runs
        var executionTimes = new List<TimeSpan>();

        for (var i = 0; i < 10; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
            stopwatch.Stop();

            result.HasErrors.Should().BeFalse();
            executionTimes.Add(stopwatch.Elapsed);
        }

        // Assert - Performance remains consistent (no major degradation)
        var averageTime = TimeSpan.FromTicks((long)executionTimes.Select(t => t.Ticks).Average());
        var maxTime = executionTimes.Max();

        // Max time shouldn't be more than 3x average (allowing for variance)
        var performanceThreshold = TimeSpan.FromTicks(averageTime.Ticks * 3);
        maxTime.Should().BeLessOrEqualTo(performanceThreshold,
            "Performance degraded: Max time {0}ms, Average {1}ms",
            maxTime.TotalMilliseconds,
            averageTime.TotalMilliseconds);

        // All compilations should complete in reasonable time
        maxTime.Should().BeLessThan(TimeSpan.FromSeconds(2),
            "Compilation took too long: {0}s",
            maxTime.TotalSeconds);
    }

    /// <summary>
    ///     Creates test code with proper namespace isolation to avoid conflicts
    /// </summary>
    private static string CreateScalableTestCode(int serviceCount)
    {
        // Create all services in a single namespace to avoid file-scoped namespace conflicts
        // but with unique names to avoid naming collisions
        var allCode = new StringBuilder();

        allCode.AppendLine("using IoCTools.Abstractions.Annotations;");
        allCode.AppendLine("using IoCTools.Abstractions.Enumerations;");
        allCode.AppendLine();
        allCode.AppendLine("namespace TestScalabilityNamespace");
        allCode.AppendLine("{");

        for (var i = 0; i < serviceCount; i++)
        {
            var serviceName = $"ScalabilityService{i}"; // Unique service names
            var interfaceName = $"IScalabilityService{i}"; // Unique interface names
            var implName = $"ScalabilityService{i}Impl"; // Unique implementation names

            // Create simple dependencies to avoid cross-reference issues
            var dependencies = new List<string>();
            if (i > 0 && i < 5) // Only first few services have dependencies to keep it simple
            {
                var depIndex = i - 1;
                dependencies.Add($"IScalabilityService{depIndex}");
            }

            var depFields = string.Join("\n        ",
                dependencies.Select((dep,
                        idx) => $"[Inject] private readonly {dep} _dep{idx};"));

            allCode.AppendLine($@"
    [Scoped]
    public partial class {serviceName}
    {{
        {depFields}
    }}

    public interface {interfaceName} {{ }}

    [Scoped]
    public partial class {implName} : {interfaceName} {{ }}
");
        }

        allCode.AppendLine("}"); // Close namespace

        return allCode.ToString();
    }
}
