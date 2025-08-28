namespace IoCTools.Generator.Diagnostics.Configuration;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using Models;

internal static class DiagnosticConfigProvider
{
    public static DiagnosticConfiguration From(GeneratorExecutionContext context)
        => DiagnosticUtilities.GetDiagnosticConfiguration(context);

    public static DiagnosticConfiguration From(Compilation compilation)
        => DiagnosticUtilities.GetDiagnosticConfiguration(compilation);

    public static DiagnosticConfiguration From(AnalyzerConfigOptionsProvider options)
        => DiagnosticUtilities.GetDiagnosticConfiguration(options);
}
