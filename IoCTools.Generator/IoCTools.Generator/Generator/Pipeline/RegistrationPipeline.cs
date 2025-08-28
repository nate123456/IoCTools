namespace IoCTools.Generator.Generator.Pipeline;

using Microsoft.CodeAnalysis;

using Models;

internal static class RegistrationPipeline
{
    internal static void Attach(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<ServiceClassInfo> serviceClasses)
    {
        var registrationInput = serviceClasses
            .Collect()
            .Combine(context.CompilationProvider)
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select(static (input,
                _) =>
            {
                var ((services, compilation), config) = input;
                return (services, compilation, config);
            });

        context.RegisterSourceOutput(registrationInput,
            static (spc,
                input) => RegistrationEmitter.Emit(spc.AddSource, spc, input));
    }
}
