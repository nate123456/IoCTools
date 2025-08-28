using IoCTools.Generator.Tests;

public class SkipAssignableTypesMediatRWithDependenciesTests
{
    [Fact]
    public void MediatR_Handler_With_DependsOn_And_Inject_Is_Skipped_But_Gets_Constructor()
    {
        var code = @"
using IoCTools.Abstractions.Annotations;
namespace MediatR { public interface IRequestHandler<TReq, TRes> {} }

namespace Test
{
    public interface ILogger<T> {}
    public sealed class Command {}
    public sealed class Result {}

    [DependsOn<ILogger<Handler>>]
    public partial class Handler : MediatR.IRequestHandler<Command, Result>
    {
        [Inject] private readonly ILogger<Handler> _logger;
    }
}
";
        // Add configuration to skip MediatR.* to match intended opt-in behavior
        code += @"
namespace IoCTools.Generator.Configuration { public static class GeneratorOptions { public const string SkipAssignableTypesAdd = ""MediatR.*""; } }
";
        var result = SourceGeneratorTestHelper.CompileWithGenerator(code);
        Assert.False(result.HasErrors);

        // Should not be registered
        var reg = result.GetServiceRegistrationSource();
        if (reg != null)
            Assert.DoesNotContain("Handler", reg.Content);

        // Should still have constructor
        var ctor = result.GetConstructorSource("Handler");
        Assert.NotNull(ctor);
        Assert.Contains("public Handler(", ctor!.Content);
    }
}
