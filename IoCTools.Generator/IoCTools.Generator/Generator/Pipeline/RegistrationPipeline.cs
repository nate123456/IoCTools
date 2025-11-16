namespace IoCTools.Generator.Generator.Pipeline;

using Microsoft.CodeAnalysis;

using Models;

using Utilities;

internal static class RegistrationPipeline
{
    internal static void Attach(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<ServiceClassInfo> serviceClasses)
    {
        var styleOptionsProvider = context.AnalyzerConfigOptionsProvider
            .Combine(context.CompilationProvider)
            .Select(static (input,
                _) => GeneratorStyleOptions.From(input.Left, input.Right));

        var registrationInput = serviceClasses
            .Collect()
            .Combine(context.CompilationProvider)
            .Combine(styleOptionsProvider)
            .Select(static (input,
                _) => (input.Left.Left, input.Left.Right, input.Right));

        context.RegisterSourceOutput(registrationInput,
            static (spc,
                input) => RegistrationEmitter.Emit(spc.AddSource, spc, input));
    }
}
