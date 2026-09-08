namespace IoCTools.Generator.Tests;

using System;
using Microsoft.Extensions.DependencyInjection;

public class ManualRegistrationMigrationTests
{
    [Fact]
    public void ManualRegistrationMigration_AttributeAndExtensionCall_ResolvesSameSingleton()
    {
        var result = CompileMigration("services.AddTestAssemblyRegisteredServices();");
        result.HasErrors.Should().BeFalse("{0}", string.Join(Environment.NewLine, result.CompilationDiagnostics));
        var context = SourceGeneratorTestHelper.CreateRuntimeContext(result);
        using var provider = CreateProvider(context);
        var serviceType = context.Assembly.GetType("Migration.Probe", throwOnError: true)!;

        var first = provider.GetRequiredService(serviceType);
        var second = provider.GetRequiredService(serviceType);

        first.Should().BeSameAs(second);
    }

    [Fact]
    public void ManualRegistrationMigration_AttributeWithoutExtensionCall_CompilesButResolutionThrows()
    {
        var result = CompileMigration("");
        result.HasErrors.Should().BeFalse();
        result.GetRequiredServiceRegistrationSource().Content.Should()
            .Contain("AddTestAssemblyRegisteredServices");
        var context = SourceGeneratorTestHelper.CreateRuntimeContext(result);
        using var provider = CreateProvider(context);
        var serviceType = context.Assembly.GetType("Migration.Probe", throwOnError: true)!;

        var resolve = () => provider.GetRequiredService(serviceType);

        resolve.Should().Throw<InvalidOperationException>()
            .WithMessage("*Migration.Probe*");
    }

    private static GeneratorTestResult CompileMigration(string registrationCall) =>
        SourceGeneratorTestHelper.CompileWithGenerator($$"""
            using IoCTools.Abstractions.Annotations;
            using Microsoft.Extensions.DependencyInjection;
            using TestAssembly.Extensions.Generated;

            namespace Migration;

            [Singleton]
            public sealed class Probe { }

            public static class CompositionRoot
            {
                public static ServiceProvider CreateProvider()
                {
                    var services = new ServiceCollection();
                    {{registrationCall}}
                    return services.BuildServiceProvider();
                }
            }
            """);

    private static ServiceProvider CreateProvider(RuntimeTestContext context) =>
        (ServiceProvider)context.Assembly.GetType("Migration.CompositionRoot", throwOnError: true)!
            .GetMethod("CreateProvider")!.Invoke(null, null)!;
}
