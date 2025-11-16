using System.Collections.Generic;

namespace IoCTools.Generator.Tests;

public class ImplicitLifetimeConfigurationTests
{
    private const string PartialServiceSource = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService { }

public partial class ModuleProvider : IService
{
}

[Singleton]
public partial class Consumer
{
    [Inject] private readonly IService _service;
}
";

    [Theory]
    [InlineData("Singleton")]
    [InlineData("singleton")]
    public void ImplicitLifetime_ConfiguredSingleton_SuppressesSingletonWarnings(string configuredValue)
    {
        var result = SourceGeneratorTestHelper.CompileWithGenerator(PartialServiceSource, true,
            new Dictionary<string, string>
            {
                ["IoCToolsDefaultServiceLifetime"] = configuredValue
            });

        var registrationText = result.GetServiceRegistrationText();
        registrationText.Should().Contain("AddSingleton", "implicit singleton lifetime should be applied");

        result.GetDiagnosticsByCode("IOC012").Should().BeEmpty();
        result.GetDiagnosticsByCode("IOC013").Should().BeEmpty();
        result.GetDiagnosticsByCode("IOC033").Should().BeEmpty();
    }

    [Fact]
    public void ImplicitLifetime_ConfiguredTransient_TriggersSingletonDependsOnTransient()
    {
        var result = SourceGeneratorTestHelper.CompileWithGenerator(PartialServiceSource, true,
            new Dictionary<string, string>
            {
                ["IoCToolsDefaultServiceLifetime"] = "Transient"
            });

        result.GetDiagnosticsByCode("IOC012").Should().BeEmpty();
        result.GetDiagnosticsByCode("IOC013").Should().ContainSingle();
    }

    [Fact]
    public void ImplicitLifetime_ConfiguredScoped_MatchesOriginalBehavior()
    {
        var result = SourceGeneratorTestHelper.CompileWithGenerator(PartialServiceSource, true,
            new Dictionary<string, string>
            {
                ["IoCToolsDefaultServiceLifetime"] = "Scoped"
            });

        result.GetDiagnosticsByCode("IOC012").Should().ContainSingle();
    }
}
