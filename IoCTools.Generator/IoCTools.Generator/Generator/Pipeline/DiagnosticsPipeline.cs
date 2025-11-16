namespace IoCTools.Generator.Generator.Pipeline;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;

using Models;

using Utilities;

internal static class DiagnosticsPipeline
{
    internal static void Attach(
        IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<ServiceClassInfo> serviceClasses)
    {
        var styleOptionsProvider = context.AnalyzerConfigOptionsProvider
            .Combine(context.CompilationProvider)
            .Select(static (input,
                _) => GeneratorStyleOptions.From(input.Left, input.Right));

        var referencedAssemblyTypes = context.CompilationProvider
            .Select(static (compilation,
                _) =>
            {
                var referencedTypes = new List<INamedTypeSymbol>();
                foreach (var reference in compilation.References)
                {
                    if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol asm) continue;
                    var name = asm.Name;
                    if (name.StartsWith("System", StringComparison.Ordinal) ||
                        name.StartsWith("Microsoft", StringComparison.Ordinal) ||
                        name.StartsWith("IoCTools.Generator", StringComparison.Ordinal))
                        continue;

                    var referencesAbstractions = asm.Modules.Any(m =>
                        m.ReferencedAssemblies.Any(ra => ra.Name == "IoCTools.Abstractions"));
                    if (!referencesAbstractions) continue;

                    DiagnosticScan.ScanNamespaceForTypes(asm.GlobalNamespace, referencedTypes);
                }

                return referencedTypes.ToImmutableArray();
            });

        var diagnosticsInput = serviceClasses
            .Collect()
            .Combine(referencedAssemblyTypes)
            .Combine(context.CompilationProvider)
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Combine(styleOptionsProvider)
            .Select(static (input,
                _) =>
            {
                var ((((services, referencedTypes), compilation), config), styleOptions) = input;
                return ((services, referencedTypes, compilation), config, styleOptions);
            });

        context.RegisterSourceOutput(diagnosticsInput, DiagnosticsRunner.EmitWithReferencedTypes);
    }
}
