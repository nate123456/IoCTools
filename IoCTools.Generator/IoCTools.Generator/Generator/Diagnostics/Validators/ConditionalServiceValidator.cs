namespace IoCTools.Generator.Generator.Diagnostics.Validators;

using System.Collections.Generic;
using System.Linq;

using IoCTools.Generator.Diagnostics;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Utilities;

internal static class ConditionalServiceValidator
{
    internal static void ValidateAttributeCombinations(SourceProductionContext context,
        IEnumerable<INamedTypeSymbol> servicesWithAttributes)
    {
        var seen = new HashSet<string>();
        foreach (var classSymbol in servicesWithAttributes)
        {
            var key = classSymbol.ToDisplayString();
            if (!seen.Add(key)) continue;
            var syntaxRef = classSymbol.DeclaringSyntaxReferences.FirstOrDefault();
            if (syntaxRef == null) continue;
            if (syntaxRef.GetSyntax() is not TypeDeclarationSyntax classDeclaration) continue;

            ValidateConditionalServices(context, classSymbol, classDeclaration);
        }
    }

    private static void ValidateConditionalServices(SourceProductionContext context,
        INamedTypeSymbol classSymbol,
        TypeDeclarationSyntax classDeclaration)
    {
        var conditionalServiceAttributes = classSymbol.GetAttributes()
            .Where(attr => attr.AttributeClass?.ToDisplayString() ==
                           "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute")
            .ToList();

        if (!conditionalServiceAttributes.Any()) return;

        if (conditionalServiceAttributes.Count > 1)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.ConditionalServiceMultipleAttributes,
                classDeclaration.GetLocation(),
                classSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }

        foreach (var conditionalAttribute in conditionalServiceAttributes)
        {
            var validationResult = ConditionalServiceEvaluator.ValidateConditionsDetailed(conditionalAttribute);

            if (!validationResult.IsValid)
                foreach (var error in validationResult.Errors)
                    if (error.Contains("No conditions specified"))
                    {
                        var diagnostic = Diagnostic.Create(
                            DiagnosticDescriptors.ConditionalServiceEmptyConditions,
                            classDeclaration.GetLocation(),
                            classSymbol.Name);
                        context.ReportDiagnostic(diagnostic);
                    }
                    else if (error.Contains("conflict"))
                    {
                        var diagnostic = Diagnostic.Create(
                            DiagnosticDescriptors.ConditionalServiceConflictingConditions,
                            classDeclaration.GetLocation(),
                            classSymbol.Name,
                            error);
                        context.ReportDiagnostic(diagnostic);
                    }
                    else if (error.Contains("ConfigValue") && error.Contains("without Equals or NotEquals"))
                    {
                        var configValue = validationResult.ConfigValue ?? "unknown";
                        var diagnostic = Diagnostic.Create(
                            DiagnosticDescriptors.ConditionalServiceConfigValueWithoutComparison,
                            classDeclaration.GetLocation(),
                            classSymbol.Name,
                            configValue);
                        context.ReportDiagnostic(diagnostic);
                    }
                    else if (error.Contains("Equals or NotEquals") && error.Contains("without ConfigValue"))
                    {
                        var diagnostic = Diagnostic.Create(
                            DiagnosticDescriptors.ConditionalServiceComparisonWithoutConfigValue,
                            classDeclaration.GetLocation(),
                            classSymbol.Name);
                        context.ReportDiagnostic(diagnostic);
                    }
                    else if (error.Contains("ConfigValue is empty"))
                    {
                        var diagnostic = Diagnostic.Create(
                            DiagnosticDescriptors.ConditionalServiceEmptyConfigKey,
                            classDeclaration.GetLocation(),
                            classSymbol.Name);
                        context.ReportDiagnostic(diagnostic);
                    }
        }
    }
}
