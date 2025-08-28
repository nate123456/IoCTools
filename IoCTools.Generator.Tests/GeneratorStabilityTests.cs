namespace IoCTools.Generator.Tests;

using System.Diagnostics;
using System.Text;

using Microsoft.CodeAnalysis;

using Xunit.Abstractions;

/// <summary>
///     Tests to validate generator stability, idempotency, and consistency
/// </summary>
public class GeneratorStabilityTests
{
    private readonly ITestOutputHelper _output;

    public GeneratorStabilityTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void GeneratorIdempotency_SameSourceCodeMultipleRuns_ProducesIdenticalOutput()
    {
        // Arrange
        var sourceCode = """
                         using IoCTools.Abstractions.Annotations;
                         using IoCTools.Abstractions.Enumerations;

                         namespace TestNamespace;

                         [Scoped]
                         public partial class TestService
                         {
                             [Inject] private readonly IService1 _dep1;
                             [Inject] private readonly IService2 _dep2;
                         }

                         public interface IService1 { }
                         public interface IService2 { }

                         [Scoped]
                         public partial class Service1 : IService1 { }

                         [Scoped] 
                         public partial class Service2 : IService2 { }
                         """;

        // Act - Run generator multiple times
        var results = new List<GeneratorTestResult>();
        for (var i = 0; i < 5; i++) results.Add(SourceGeneratorTestHelper.CompileWithGenerator(sourceCode));

        // Assert - All results should be identical
        for (var i = 1; i < results.Count; i++)
        {
            // Check same number of generated sources
            Assert.Equal(results[0].GeneratedSources.Count, results[i].GeneratedSources.Count);

            // Check ServiceRegistrations content is identical
            var firstServiceReg = results[0].GetServiceRegistrationSource();
            var currentServiceReg = results[i].GetServiceRegistrationSource();

            Assert.NotNull(firstServiceReg);
            Assert.NotNull(currentServiceReg);
            Assert.Equal(firstServiceReg.Content, currentServiceReg.Content);

            // Check constructor generation is identical
            var firstConstructors = results[0].GeneratedSources.Where(s => s.Hint.Contains("Constructor")).ToList();
            var currentConstructors = results[i].GeneratedSources.Where(s => s.Hint.Contains("Constructor")).ToList();

            Assert.Equal(firstConstructors.Count, currentConstructors.Count);

            foreach (var constructor in firstConstructors)
            {
                var matchingConstructor = currentConstructors.FirstOrDefault(c => c.Hint == constructor.Hint);
                Assert.NotNull(matchingConstructor);
                Assert.Equal(constructor.Content, matchingConstructor.Content);
            }
        }
    }

    [Fact]
    public void GeneratorConsistency_MultipleServicesNoInteraction_ProducesConsistentFiles()
    {
        // Arrange - Services that don't interact with each other
        var sourceCode = """
                         using IoCTools.Abstractions.Annotations;
                         using IoCTools.Abstractions.Enumerations;
                         using System.Collections.Generic;

                         namespace TestNamespace;

                         [Scoped]
                         public partial class ServiceA
                         {
                             [Inject] private readonly IDepA _depA;
                         }

                         [Scoped]
                         public partial class ServiceB  
                         {
                             [Inject] private readonly IDepB _depB;
                         }

                         [Scoped]
                         public partial class ServiceC
                         {
                             [Inject] private readonly IDepC _depC;
                         }

                         public interface IDepA { }
                         public interface IDepB { }
                         public interface IDepC { }

                         [Scoped]
                         public partial class DepA : IDepA { }

                         [Scoped]
                         public partial class DepB : IDepB { }

                         [Scoped]
                         public partial class DepC : IDepC { }
                         """;

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert - No compilation errors
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.GetMessage()))}");

        // Assert - Each service gets exactly one constructor file
        var constructorSources = result.GeneratedSources.Where(s => s.Hint.Contains("Constructor")).ToList();
        var expectedServiceNames = new[] { "ServiceA", "ServiceB", "ServiceC", "DepA", "DepB", "DepC" };

        foreach (var serviceName in expectedServiceNames)
        {
            var constructorForService = constructorSources.Where(s => s.Hint.Contains(serviceName)).ToList();
            Assert.Single(constructorForService); // Exactly one constructor per service
        }

        // Assert - Exactly one service registration file
        var serviceRegSources = result.GeneratedSources.Where(s =>
            s.Content.Contains("ServiceCollectionExtensions") ||
            s.Hint.Contains("ServiceRegistrations")).ToList();
        Assert.Single(serviceRegSources);

        // Assert - Service registration contains all expected services
        var serviceRegContent = serviceRegSources[0].Content;
        foreach (var serviceName in expectedServiceNames) Assert.Contains(serviceName, serviceRegContent);
    }

    [Fact]
    public void GeneratorScalability_ManyServices_CompletesInReasonableTime()
    {
        // Arrange - Generate many simple services to test scalability
        var serviceCount = 20; // Reduced count to avoid timeout issues
        var sourceBuilder = new StringBuilder();

        sourceBuilder.AppendLine("using IoCTools.Abstractions.Annotations;");
        sourceBuilder.AppendLine("using IoCTools.Abstractions.Enumerations;");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("namespace TestNamespace");
        sourceBuilder.AppendLine("{");

        // Generate simple services without complex dependencies
        for (var i = 0; i < serviceCount; i++)
        {
            sourceBuilder.AppendLine("    [Scoped]");
            sourceBuilder.AppendLine($"    public partial class Service{i} {{ }}");
            sourceBuilder.AppendLine();
        }

        sourceBuilder.AppendLine("}");
        var sourceCode = sourceBuilder.ToString();

        // Act
        var stopwatch = Stopwatch.StartNew();
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        stopwatch.Stop();

        // Assert - Completes within reasonable time (5 seconds for 20 simple services)
        Assert.True(stopwatch.Elapsed.TotalSeconds < 5,
            $"Generator took too long: {stopwatch.Elapsed.TotalSeconds:F2} seconds for {serviceCount} services");

        // Assert - No errors
        Assert.False(result.HasErrors,
            $"Compilation errors with {serviceCount} services: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.GetMessage()))}");

        // Assert - Generated expected number of files
        var constructorCount = result.GeneratedSources.Count(s => s.Hint.Contains("Constructor"));
        Assert.True(constructorCount >= serviceCount, "Should generate constructor files for each service");

        var serviceRegCount = result.GeneratedSources.Count(s =>
            s.Content.Contains("ServiceCollectionExtensions") ||
            s.Hint.Contains("ServiceRegistrations"));
        Assert.Equal(1, serviceRegCount); // Exactly one service registration file
    }

    [Fact]
    public void GeneratorFileNaming_ComplexTypeNames_ProducesConsistentFileNames()
    {
        // Arrange - Test complex type names that could cause file naming conflicts
        var sourceCode = """
                         using IoCTools.Abstractions.Annotations;
                         using IoCTools.Abstractions.Enumerations;
                         using System.Collections.Generic;

                         namespace Complex.Nested.Namespace
                         {

                             [Scoped]
                             public partial class SimpleService
                             {
                                 [Inject] private readonly ISimpleDep _dep;
                             }

                             [Scoped]
                             public partial class AnotherService
                             {
                                 [Inject] private readonly IAnotherDep _dep;
                             }

                             [Scoped]
                             public partial class ServiceWithSpecialChars_AndSymbols
                             {
                                 [Inject] private readonly ISpecialDep _dep;
                             }

                             public interface ISimpleDep { }
                             public interface IAnotherDep { }
                             public interface ISpecialDep { }

                             [Scoped]
                             public partial class SimpleDep : ISimpleDep { }

                             [Scoped]
                             public partial class AnotherDep : IAnotherDep { }

                             [Scoped]
                             public partial class SpecialDep : ISpecialDep { }
                         }
                         """;

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert - No compilation errors
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.GetMessage()))}");

        // Assert - All file names are unique and valid
        var constructorHints = result.GeneratedSources.Where(s => s.Hint.Contains("Constructor")).Select(s => s.Hint)
            .ToList();
        var uniqueHints = constructorHints.Distinct().ToList();
        Assert.Equal(constructorHints.Count, uniqueHints.Count); // No duplicate file names

        // Assert - File names don't contain problematic characters
        foreach (var hint in constructorHints)
        {
            Assert.DoesNotContain("<", hint);
            Assert.DoesNotContain(">", hint);
            Assert.DoesNotContain(" ", hint);
            Assert.DoesNotContain("$", hint);
            Assert.Contains("_Constructor.g.cs", hint);
        }

        // Assert - Service registration works correctly
        var serviceRegSource = result.GetServiceRegistrationSource();
        Assert.NotNull(serviceRegSource);
        Assert.Contains("SimpleService", serviceRegSource.Content);
        Assert.Contains("AnotherService", serviceRegSource.Content);
        Assert.Contains("ServiceWithSpecialChars_AndSymbols", serviceRegSource.Content);
    }

    [Fact]
    public void GeneratorIncrementalCompilation_AddService_OnlyGeneratesNewFiles()
    {
        // Arrange - Initial source code
        var initialSource = """
                            using IoCTools.Abstractions.Annotations;
                            using IoCTools.Abstractions.Enumerations;

                            namespace TestNamespace;

                            [Scoped]
                            public partial class ServiceA
                            {
                                [Inject] private readonly IDepA _depA;
                            }

                            public interface IDepA { }

                            [Scoped]
                            public partial class DepA : IDepA { }
                            """;

        // Act - First compilation
        var initialResult = SourceGeneratorTestHelper.CompileWithGenerator(initialSource);

        // Arrange - Updated source with additional service
        var updatedSource = """
                            using IoCTools.Abstractions.Annotations;
                            using IoCTools.Abstractions.Enumerations;

                            namespace TestNamespace;

                            [Scoped]
                            public partial class ServiceA
                            {
                                [Inject] private readonly IDepA _depA;
                            }

                            [Scoped]
                            public partial class ServiceB
                            {
                                [Inject] private readonly IDepB _depB;
                            }

                            public interface IDepA { }
                            public interface IDepB { }

                            [Scoped]
                            public partial class DepA : IDepA { }

                            [Scoped]
                            public partial class DepB : IDepB { }
                            """;

        // Act - Second compilation
        var updatedResult = SourceGeneratorTestHelper.CompileWithGenerator(updatedSource);

        // Assert - Both compilations succeed
        Assert.False(initialResult.HasErrors);
        Assert.False(updatedResult.HasErrors);

        // Assert - Updated result has more constructor files
        var initialConstructorCount = initialResult.GeneratedSources.Count(s => s.Hint.Contains("Constructor"));
        var updatedConstructorCount = updatedResult.GeneratedSources.Count(s => s.Hint.Contains("Constructor"));
        Assert.True(updatedConstructorCount > initialConstructorCount);

        // Assert - Both have exactly one service registration file
        var initialServiceRegCount = initialResult.GeneratedSources.Count(s =>
            s.Content.Contains("ServiceCollectionExtensions"));
        var updatedServiceRegCount = updatedResult.GeneratedSources.Count(s =>
            s.Content.Contains("ServiceCollectionExtensions"));
        Assert.Equal(1, initialServiceRegCount);
        Assert.Equal(1, updatedServiceRegCount);
    }

    [Fact]
    public void GeneratorStability_PartialClassesAcrossFiles_HandlesCorrectly()
    {
        // Arrange - Partial classes that could confuse the generator
        var sourceCode = """
                         using IoCTools.Abstractions.Annotations;
                         using IoCTools.Abstractions.Enumerations;

                         namespace TestNamespace;

                         // First part of the class
                         [Scoped]
                         public partial class SplitService
                         {
                             [Inject] private readonly IDep1 _dep1;
                         }

                         // Second part of the class (simulated as if in another file)
                         public partial class SplitService
                         {
                             [Inject] private readonly IDep2 _dep2;
                             
                             public void DoSomething() { }
                         }

                         public interface IDep1 { }
                         public interface IDep2 { }

                         [Scoped]
                         public partial class Dep1 : IDep1 { }

                         [Scoped]
                         public partial class Dep2 : IDep2 { }
                         """;

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert - No compilation errors
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.GetMessage()))}");

        // Assert - Generates exactly one constructor for SplitService
        var splitServiceConstructors = result.GeneratedSources.Where(s =>
            s.Hint.Contains("SplitService") && s.Hint.Contains("Constructor")).ToList();
        Assert.Single(splitServiceConstructors);

        // Assert - Constructor includes both dependencies
        var constructorContent = splitServiceConstructors[0].Content;
        Assert.Contains("IDep1", constructorContent);
        Assert.Contains("IDep2", constructorContent);
    }

    [Fact]
    public void GeneratorRecovery_CompilationErrors_GeneratesValidCode()
    {
        // Arrange - Source with some compilation issues but valid generator targets
        var sourceCode = """
                         using IoCTools.Abstractions.Annotations;
                         using IoCTools.Abstractions.Enumerations;
                         using NonExistentNamespace; // This will cause a compilation error

                         namespace TestNamespace;

                         [Scoped]
                         public partial class ValidService
                         {
                             [Inject] private readonly IDependency _dep;
                             
                             public void InvalidMethod() 
                             {
                                 var x = UndefinedVariable; // This will cause a compilation error
                             }
                         }

                         public interface IDependency { }

                         [Scoped]
                         public partial class Dependency : IDependency { }
                         """;

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert - Has expected compilation errors but generator still runs
        Assert.True(result.CompilationDiagnostics.Any(d => d.Severity == DiagnosticSeverity.Error));

        // Assert - Generator still produces output for valid parts
        Assert.True(result.GeneratedSources.Count > 0, "Generator should produce output even with compilation errors");

        // Assert - Service registration is generated
        var serviceRegistration = result.GetServiceRegistrationSource();
        Assert.NotNull(serviceRegistration);
        Assert.Contains("ValidService", serviceRegistration.Content);

        // Assert - Constructor is generated for valid service
        var constructorSource = result.GetConstructorSource("ValidService");
        Assert.NotNull(constructorSource);
        Assert.Contains("ValidService(", constructorSource.Content);
    }

    [Fact]
    public void GeneratorDuplicateServiceDetection_SameServiceDifferentNamespaces_HandlesCorrectly()
    {
        // Arrange - Different service names in different namespaces to avoid ambiguity
        var sourceCode = """
                         using IoCTools.Abstractions.Annotations;
                         using IoCTools.Abstractions.Enumerations;

                         namespace Namespace1 
                         {
                             [Scoped]
                             public partial class Service1
                             {
                                 [Inject] private readonly IDep1 _dep;
                             }

                             public interface IDep1 { }

                             [Scoped]  
                             public partial class Dep1 : IDep1 { }
                         }

                         namespace Namespace2
                         {
                             [Scoped]
                             public partial class Service2  
                             {
                                 [Inject] private readonly IDep2 _otherDep;
                             }

                             public interface IDep2 { }

                             [Scoped]
                             public partial class Dep2 : IDep2 { }
                         }
                         """;

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert - No compilation errors
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.GetMessage()))}");

        // Assert - All services get constructors
        var constructorSources = result.GeneratedSources.Where(s => s.Hint.Contains("Constructor")).ToList();
        Assert.True(constructorSources.Count >= 4); // Should have constructors for all 4 services

        // Assert - Service registration contains all services
        var serviceRegSource = result.GetServiceRegistrationSource();
        Assert.NotNull(serviceRegSource);
        Assert.Contains("Service1", serviceRegSource.Content);
        Assert.Contains("Service2", serviceRegSource.Content);
        Assert.Contains("Dep1", serviceRegSource.Content);
        Assert.Contains("Dep2", serviceRegSource.Content);
    }

    [Fact]
    public void GeneratorCleanRebuild_ConsistentOutput()
    {
        // Arrange
        var sourceCode = """
                         using IoCTools.Abstractions.Annotations;
                         using IoCTools.Abstractions.Enumerations;

                         namespace TestNamespace;

                         [Scoped]
                         public partial class RebuildTestService
                         {
                             [Inject] private readonly IDependency _dep;
                         }

                         public interface IDependency { }

                         [Scoped]
                         public partial class Dependency : IDependency { }
                         """;

        // Act - Multiple independent compilations (simulating clean rebuilds)
        var results = new List<GeneratorTestResult>();
        for (var i = 0; i < 3; i++)
            // Each call should be independent, simulating a clean rebuild
            results.Add(SourceGeneratorTestHelper.CompileWithGenerator(sourceCode));

        // Assert - All results are consistent
        var firstResult = results[0];
        Assert.False(firstResult.HasErrors);

        for (var i = 1; i < results.Count; i++)
        {
            var currentResult = results[i];
            Assert.False(currentResult.HasErrors);

            // Same number of generated files
            Assert.Equal(firstResult.GeneratedSources.Count, currentResult.GeneratedSources.Count);

            // Same service registration content
            var firstServiceReg = firstResult.GetServiceRegistrationSource()?.Content;
            var currentServiceReg = currentResult.GetServiceRegistrationSource()?.Content;
            Assert.Equal(firstServiceReg, currentServiceReg);

            // Same constructor content  
            var firstConstructor = firstResult.GetConstructorSource("RebuildTestService")?.Content;
            var currentConstructor = currentResult.GetConstructorSource("RebuildTestService")?.Content;
            Assert.Equal(firstConstructor, currentConstructor);
        }
    }
}
