using IoCTools.Generator.Tests;

public class SkipAssignableTypesMediatorWithDependenciesTests
{
    [Fact]
    public void Mediator_Handler_With_DependsOn_And_Inject_Is_Skipped_But_Gets_Constructor()
    {
        var code = @"
using IoCTools.Abstractions.Annotations;
namespace Mediator { public interface IRequestHandler<TReq, TRes> {} }

namespace Test
{
    public interface ILogger<T> {}
    public sealed class Command {}
    public sealed class Result {}

    [DependsOn<ILogger<Handler>>]
    public partial class Handler : Mediator.IRequestHandler<Command, Result>
    {
        [Inject] private readonly ILogger<Handler> _logger;
    }
}
";
        // Add configuration to skip Mediator.* to match intended opt-in behavior
        code += @"
namespace IoCTools.Generator.Configuration { public static class GeneratorOptions { public const string SkipAssignableTypesAdd = ""Mediator.*""; } }
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(code);
        Assert.False(result.HasErrors);

        // Registration should not contain the handler at all
        var reg = result.GetServiceRegistrationSource();
        if (reg != null)
            Assert.DoesNotContain("Handler", reg.Content);

        // Constructor should still be generated (DI works for consuming code)
        var ctor = result.GetConstructorSource("Handler");
        Assert.NotNull(ctor);
        Assert.Contains("public Handler(", ctor!.Content);
    }
}
