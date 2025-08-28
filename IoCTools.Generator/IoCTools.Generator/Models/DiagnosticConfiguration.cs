namespace IoCTools.Generator.Models;

using Microsoft.CodeAnalysis;

public class DiagnosticConfiguration
{
    public DiagnosticSeverity NoImplementationSeverity { get; set; } = DiagnosticSeverity.Warning;
    public DiagnosticSeverity ManualImplementationSeverity { get; set; } = DiagnosticSeverity.Warning;
    public DiagnosticSeverity LifetimeValidationSeverity { get; set; } = DiagnosticSeverity.Error;
    public bool DiagnosticsEnabled { get; set; } = true;
    public bool LifetimeValidationEnabled { get; set; } = true;

    // Specific diagnostic severity overrides for educational examples
    public DiagnosticSeverity ConditionalServiceValidationSeverity { get; set; } = DiagnosticSeverity.Warning;
    public DiagnosticSeverity PartialClassValidationSeverity { get; set; } = DiagnosticSeverity.Warning;
    public DiagnosticSeverity BackgroundServiceValidationSeverity { get; set; } = DiagnosticSeverity.Warning;
    public DiagnosticSeverity InheritanceChainValidationSeverity { get; set; } = DiagnosticSeverity.Warning;
}
