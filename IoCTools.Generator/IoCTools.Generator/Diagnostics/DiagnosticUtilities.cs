namespace IoCTools.Generator.Diagnostics;

using Helpers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

using Models;

internal static class DiagnosticUtilities
{
    public static DiagnosticConfiguration GetDiagnosticConfiguration(GeneratorExecutionContext context)
    {
        var config = new DiagnosticConfiguration();
        if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.IoCToolsNoImplementationSeverity",
                out var noImplSeverity))
            config.NoImplementationSeverity = ParseDiagnosticSeverity(noImplSeverity);
        if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.IoCToolsManualSeverity",
                out var manualSeverity))
            config.ManualImplementationSeverity = ParseDiagnosticSeverity(manualSeverity);
        if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.IoCToolsDisableDiagnostics",
                out var disableStr) && bool.TryParse(disableStr, out var disable))
            config.DiagnosticsEnabled = !disable;
        if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.IoCToolsLifetimeValidationSeverity",
                out var lifetimeSeverity) && !string.IsNullOrWhiteSpace(lifetimeSeverity))
            config.LifetimeValidationSeverity = ParseDiagnosticSeverity(lifetimeSeverity);
        if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.IoCToolsDisableLifetimeValidation",
                out var disableLifetimeStr) && bool.TryParse(disableLifetimeStr, out var disableLifetime))
            config.LifetimeValidationEnabled = !disableLifetime;
        return config;
    }

    public static DiagnosticConfiguration GetDiagnosticConfiguration(Compilation _)
        => new()
        {
            DiagnosticsEnabled = true,
            NoImplementationSeverity = DiagnosticSeverity.Warning,
            ManualImplementationSeverity = DiagnosticSeverity.Warning,
            LifetimeValidationEnabled = true,
            LifetimeValidationSeverity = DiagnosticSeverity.Warning
        };

    public static DiagnosticConfiguration GetDiagnosticConfiguration(AnalyzerConfigOptionsProvider options)
    {
        var config = new DiagnosticConfiguration();
        if (options.GlobalOptions.TryGetValue("build_property.IoCToolsNoImplementationSeverity",
                out var noImplSeverity))
            config.NoImplementationSeverity = ParseDiagnosticSeverity(noImplSeverity);
        if (options.GlobalOptions.TryGetValue("build_property.IoCToolsManualSeverity", out var manualSeverity))
            config.ManualImplementationSeverity = ParseDiagnosticSeverity(manualSeverity);
        if (options.GlobalOptions.TryGetValue("build_property.IoCToolsDisableDiagnostics", out var disableStr) &&
            bool.TryParse(disableStr, out var disable))
            config.DiagnosticsEnabled = !disable;
        if (options.GlobalOptions.TryGetValue("build_property.IoCToolsLifetimeValidationSeverity",
                out var lifetimeSeverity) && !string.IsNullOrWhiteSpace(lifetimeSeverity))
            config.LifetimeValidationSeverity = ParseDiagnosticSeverity(lifetimeSeverity);
        if (options.GlobalOptions.TryGetValue("build_property.IoCToolsDisableLifetimeValidation",
                out var disableLifetimeStr) && bool.TryParse(disableLifetimeStr, out var disableLifetime))
            config.LifetimeValidationEnabled = !disableLifetime;
        return config;
    }

    private static DiagnosticSeverity ParseDiagnosticSeverity(string severity)
        => severity.ToLowerInvariant() switch
        {
            "error" => DiagnosticSeverity.Error,
            "warning" => DiagnosticSeverity.Warning,
            "info" => DiagnosticSeverity.Info,
            "hidden" => DiagnosticSeverity.Hidden,
            _ => DiagnosticSeverity.Warning
        };

    public static DiagnosticDescriptor CreateDynamicDescriptor(DiagnosticDescriptor baseDescriptor,
        DiagnosticSeverity severity)
        => DiagnosticDescriptorFactory.WithSeverity(baseDescriptor, severity);
}
