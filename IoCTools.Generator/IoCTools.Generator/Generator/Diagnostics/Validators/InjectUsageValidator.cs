namespace IoCTools.Generator.Generator.Diagnostics.Validators;

using System;
using System.Linq;

using IoCTools.Generator.Diagnostics;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Utilities;

internal static class InjectUsageValidator
{
    internal static void ValidatePreferDependsOn(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        foreach (var field in classSymbol.GetMembers().OfType<IFieldSymbol>())
        {
            if (!SymbolEqualityComparer.Default.Equals(field.ContainingType, classSymbol)) continue;
            if (!HasInjectAttribute(field)) continue;
            if (field.IsStatic) continue;
            if (!field.IsReadOnly) continue;
            if (field.DeclaredAccessibility != Accessibility.Private) continue;

            var defaultFieldName = AttributeParser.GenerateFieldName(
                TypeUtilities.GetMeaningfulTypeName(field.Type),
                "CamelCase",
                true,
                "_");

            if (!string.Equals(field.Name, defaultFieldName, StringComparison.Ordinal)) continue;

            var location = field.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()?.GetLocation() ??
                           classDeclaration.GetLocation();

            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.InjectFieldPreferDependsOn,
                location,
                field.Name,
                classSymbol.Name,
                field.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static bool HasInjectAttribute(IFieldSymbol fieldSymbol)
    {
        foreach (var attribute in fieldSymbol.GetAttributes())
        {
            var attributeName = attribute.AttributeClass?.Name;
            if (string.Equals(attributeName, "InjectAttribute", StringComparison.Ordinal)) return true;
        }

        return false;
    }
}
