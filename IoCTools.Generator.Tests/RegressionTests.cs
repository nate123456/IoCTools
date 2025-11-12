namespace IoCTools.Generator.Tests;

using Microsoft.CodeAnalysis;

using Xunit.Abstractions;

/// <summary>
///     CRITICAL REGRESSION TESTS: These tests verify the major regressions identified in 1.0 alpha
/// </summary>
public class RegressionTests
{
    private readonly ITestOutputHelper _output;

    public RegressionTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void SimpleService_ShouldGenerateExtensionMethod()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

[Scoped]
public partial class TestService
{
    public void DoSomething() => System.Console.WriteLine(""Hello from TestService!"");
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        _output.WriteLine($"Has Errors: {result.HasErrors}");
        _output.WriteLine($"Generated Sources Count: {result.GeneratedSources.Count}");

        foreach (var generatedSource in result.GeneratedSources)
        {
            _output.WriteLine($"Generated: {generatedSource.Hint}");
            _output.WriteLine(generatedSource.Content);
        }

        // This should pass in tests but fails in real MSBuild projects
        result.HasErrors.Should()
            .BeFalse(
                $"Compilation errors: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.GetMessage()))}");

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Verify extension method is generated
        registrationSource.Content.Should().Contain("AddTestAssemblyRegisteredServices");

        // Verify namespace (this shows the breaking change)
        // 0.4.1 would generate: namespace IoCTools.Extensions
        // 1.0 generates: namespace TestAssembly.Extensions (based on assembly name + .Extensions suffix)
        registrationSource.Content.Should().Contain("namespace TestAssembly.Extensions");
    }

    [Fact]
    public void SimpleService_NamespaceRegressionTest()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

[Scoped]
public partial class TestService
{
    public void DoSomething() => System.Console.WriteLine(""Hello from TestService!"");
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        _output.WriteLine("Generated Registration Code:");
        _output.WriteLine(registrationSource.Content);

        // BREAKING CHANGE: Namespace generation changed completely
        // 0.4.1 generated: namespace IoCTools.Extensions
        // 1.0 generates: namespace {AssemblyName}

        // This test documents the breaking change
        registrationSource.Content.Should().NotContain("namespace IoCTools.Extensions");
        registrationSource.Content.Should().Contain("namespace TestAssembly");

        // Extension method name also changed pattern
        // 0.4.1: Add{AssemblyName}RegisteredServices
        // 1.0: Add{AssemblyName}RegisteredServices (same pattern, but different namespace)
        registrationSource.Content.Should().Contain("AddTestAssemblyRegisteredServices");
    }

    [Fact]
    public void TransitiveGeneration_DocumentedLimitation()
    {
        // This test documents that transitive generation doesn't work with IIncrementalGenerator
        // In 0.4.1 (ISourceGenerator): If Project A references Project B with IoCTools,
        // the generator would run in Project B's context when building Project A.

        // In 1.0 (IIncrementalGenerator): The generator only runs in projects that directly
        // reference the generator package.

        // LIMITATION: We can't fully test this in our test framework because everything
        // is in the same compilation context. In real MSBuild projects, this is where
        // the transitive generation fails.

        const string serviceInReferencedProject = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace ProjectB;

[Scoped]
public partial class ServiceFromReferencedProject
{
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(serviceInReferencedProject);

        result.HasErrors.Should().BeFalse();
        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // In test framework: Service appears because generator runs
        // In real MSBuild: Service would NOT appear if this was in a referenced project
        // without direct IoCTools package references
        registrationSource.Content.Should().Contain("ServiceFromReferencedProject");

        _output.WriteLine(
            "TEST LIMITATION: This test passes in our framework but would fail in real MSBuild scenarios.");
        _output.WriteLine("Real issue: IIncrementalGenerator only runs in projects with direct package references.");
    }

    [Fact]
    public void ExtensionMethodSignature_CorrectGeneration()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

[Scoped]
public partial class TestService
{
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        _output.WriteLine("Generated Extension Method:");
        _output.WriteLine(registrationSource.Content);

        // Verify correct extension method signature
        registrationSource.Content.Should()
            .Contain(
                "public static IServiceCollection AddTestAssemblyRegisteredServices(this IServiceCollection services");

        // Verify it includes proper using statements
        registrationSource.Content.Should().Contain("using Microsoft.Extensions.DependencyInjection;");

        // Verify service registration (updated to match actual generator output)
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::TestNamespace.TestService, global::TestNamespace.TestService>();");
    }
}
