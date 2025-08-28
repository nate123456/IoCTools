namespace IoCTools.Generator;

using Generator;
using Generator.Pipeline;

using Microsoft.CodeAnalysis;

[Generator]
public sealed class DependencyInjectionGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var serviceClasses = ServiceClassPipeline.Build(context);

        RegistrationPipeline.Attach(context, serviceClasses);

        context.RegisterSourceOutput(serviceClasses, ConstructorEmitter.EmitSingleConstructor);

        DiagnosticsPipeline.Attach(context, serviceClasses);
    }
}
