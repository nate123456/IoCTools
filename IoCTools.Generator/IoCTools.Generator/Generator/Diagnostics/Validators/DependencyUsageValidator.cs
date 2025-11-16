namespace IoCTools.Generator.Generator.Diagnostics.Validators;

using System;
using System.Collections.Generic;
using System.Linq;

using IoCTools.Generator.Diagnostics;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Models;

using Utilities;

internal static class DependencyUsageValidator
{
    internal static void ValidateRedundantDependencies(
        SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        InheritanceHierarchyDependencies hierarchyDependencies)
    {
        var rawDependencies = hierarchyDependencies.RawAllDependencies;
        if (rawDependencies == null || rawDependencies.Count == 0) return;

        var grouped = rawDependencies
            .Where(d => d.Source == DependencySource.Inject || d.Source == DependencySource.DependsOn)
            .GroupBy(d => d.ServiceType, SymbolEqualityComparer.Default);

        foreach (var group in grouped)
        {
            var entries = group.ToList();
            if (entries.Count <= 1) continue;
            if (!entries.Any(e => e.Source == DependencySource.Inject)) continue;
            if (!entries.Any(e => e.Level == 0)) continue; // Current class must participate

            if (group.Key is not ITypeSymbol dependencyType) continue;
            var detail = BuildSourceSummary(entries);
            if (string.IsNullOrEmpty(detail)) continue;

            var location = ResolveRedundantLocation(classSymbol, classDeclaration, entries, dependencyType);
            var diagnostic = Diagnostic.Create(DiagnosticDescriptors.RedundantDependencyDeclarations,
                location,
                TypeHelpers.FormatTypeNameForDiagnostic(dependencyType),
                detail,
                classSymbol.Name);

            context.ReportDiagnostic(diagnostic);
        }
    }

    internal static void ValidateUnusedDependencies(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        SemanticModel? semanticModel,
        InheritanceHierarchyDependencies hierarchyDependencies)
    {
        if (semanticModel == null) return;

        var compilation = semanticModel.Compilation;
        var partialDeclarations = GetPartialDeclarations(classSymbol, compilation);
        if (partialDeclarations.Count == 0)
            partialDeclarations.Add((classDeclaration, semanticModel));

        var injectFields = classSymbol.GetMembers().OfType<IFieldSymbol>()
            .Where(IsInjectField)
            .ToDictionary(f => f.Name, f => f, StringComparer.Ordinal);

        var rawDependencies = hierarchyDependencies.RawAllDependencies;
        if (rawDependencies == null || rawDependencies.Count == 0) return;

        var currentDependencies = rawDependencies
            .Where(d => d.Level == 0 && (d.Source == DependencySource.Inject || d.Source == DependencySource.DependsOn))
            .GroupBy(d => d.FieldName)
            .Select(g => g.First())
            .ToList();

        foreach (var dependency in currentDependencies)
        {
            if (dependency.Source == DependencySource.Inject)
            {
                if (dependency.FieldName == null) continue;
                if (!injectFields.TryGetValue(dependency.FieldName, out var fieldSymbol)) continue;
                if (!ShouldCheckInjectField(fieldSymbol)) continue;
                if (IsFieldUsed(fieldSymbol, partialDeclarations)) continue;

                var location = fieldSymbol.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()?.GetLocation() ??
                               classDeclaration.Identifier.GetLocation();
                var diagnostic = Diagnostic.Create(DiagnosticDescriptors.UnusedDependency,
                    location,
                    dependency.FieldName,
                    TypeHelpers.FormatTypeNameForDiagnostic(dependency.ServiceType),
                    "the [Inject] field",
                    classSymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
            else if (dependency.Source == DependencySource.DependsOn)
            {
                if (dependency.FieldName == null) continue;
                if (classSymbol.IsAbstract) continue; // Generated fields are protected for abstract types
                if (IsGeneratedFieldReferenced(dependency.FieldName, partialDeclarations, classSymbol)) continue;

                var location = FindDependsOnAttributeLocation(classSymbol, dependency.ServiceType) ??
                               classDeclaration.Identifier.GetLocation();
                var diagnostic = Diagnostic.Create(DiagnosticDescriptors.UnusedDependency,
                    location,
                    dependency.FieldName,
                    TypeHelpers.FormatTypeNameForDiagnostic(dependency.ServiceType),
                    "a [DependsOn] attribute",
                    classSymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static bool IsInjectField(IFieldSymbol fieldSymbol) => fieldSymbol.GetAttributes()
        .Any(attr => attr.AttributeClass?.Name == "InjectAttribute");

    private static bool ShouldCheckInjectField(IFieldSymbol fieldSymbol)
    {
        return fieldSymbol.DeclaredAccessibility == Accessibility.Private;
    }

    private static bool IsFieldUsed(IFieldSymbol fieldSymbol,
        List<(TypeDeclarationSyntax TypeSyntax, SemanticModel Model)> partialDeclarations)
    {
        var declarationSpans = fieldSymbol.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax().Span)
            .ToList();

        foreach (var (typeSyntax, model) in partialDeclarations)
        {
            foreach (var identifier in typeSyntax.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                var symbol = model.GetSymbolInfo(identifier).Symbol;
                if (!SymbolEqualityComparer.Default.Equals(symbol, fieldSymbol)) continue;

                var span = identifier.Span;
                if (declarationSpans.Any(s => s.Equals(span))) continue;
                return true;
            }
        }

        return false;
    }

    private static bool IsGeneratedFieldReferenced(string fieldName,
        List<(TypeDeclarationSyntax TypeSyntax, SemanticModel Model)> partialDeclarations,
        INamedTypeSymbol classSymbol)
    {
        foreach (var (typeSyntax, model) in partialDeclarations)
            foreach (var identifier in typeSyntax.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                if (!string.Equals(identifier.Identifier.Text, fieldName, StringComparison.Ordinal)) continue;

                var symbol = model.GetSymbolInfo(identifier).Symbol;
                if (symbol == null) return true; // Referencing generated field before it exists in compilation model

                if (symbol is IFieldSymbol fieldSymbol &&
                    SymbolEqualityComparer.Default.Equals(fieldSymbol.ContainingType, classSymbol))
                    return true;
            }

        return false;
    }

    private static Location? FindDependsOnAttributeLocation(INamedTypeSymbol classSymbol, ITypeSymbol dependencyType)
    {
        foreach (var attribute in classSymbol.GetAttributes())
        {
            if (attribute.AttributeClass?.Name?.StartsWith("DependsOn", StringComparison.Ordinal) != true)
                continue;

            var typeArguments = attribute.AttributeClass?.TypeArguments ?? default;
            if (typeArguments.Length == 0) continue;

            if (!typeArguments.Any(arg => SymbolEqualityComparer.Default.Equals(arg, dependencyType))) continue;

            return attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation();
        }

        return null;
    }

    private static List<(TypeDeclarationSyntax TypeSyntax, SemanticModel Model)> GetPartialDeclarations(
        INamedTypeSymbol classSymbol,
        Compilation compilation)
    {
        var result = new List<(TypeDeclarationSyntax, SemanticModel)>();
        foreach (var syntaxReference in classSymbol.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax() is not TypeDeclarationSyntax typeSyntax) continue;
            var model = compilation.GetSemanticModel(typeSyntax.SyntaxTree);
            result.Add((typeSyntax, model));
        }

        return result;
    }

    private static string BuildSourceSummary(
        List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source, int Level)> entries)
    {
        var injectNames = entries.Where(e => e.Source == DependencySource.Inject && !string.IsNullOrEmpty(e.FieldName))
            .Select(e => FormatFieldName(e.FieldName!, e.Level))
            .Distinct()
            .ToList();
        var dependsOnNames = entries
            .Where(e => e.Source == DependencySource.DependsOn && !string.IsNullOrEmpty(e.FieldName))
            .Select(e => FormatFieldName(e.FieldName!, e.Level))
            .Distinct()
            .ToList();

        var parts = new List<string>();
        if (injectNames.Any()) parts.Add($"[Inject] fields {string.Join(", ", injectNames)}");
        if (dependsOnNames.Any()) parts.Add($"[DependsOn] attributes {string.Join(", ", dependsOnNames)}");

        return string.Join(" and ", parts);
    }

    private static string FormatFieldName(string fieldName, int level)
    {
        return level == 0 ? $"'{fieldName}'" : $"'{fieldName}' (base)";
    }

    private static Location ResolveRedundantLocation(INamedTypeSymbol classSymbol,
        TypeDeclarationSyntax classDeclaration,
        List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source, int Level)> entries,
        ITypeSymbol dependencyType)
    {
        var injectCurrent = entries.FirstOrDefault(e =>
            e.Source == DependencySource.Inject && e.Level == 0 && !string.IsNullOrEmpty(e.FieldName));
        if (!string.IsNullOrEmpty(injectCurrent.FieldName))
        {
            var fieldSymbol = classSymbol.GetMembers().OfType<IFieldSymbol>()
                .FirstOrDefault(f => f.Name == injectCurrent.FieldName);
            var location = fieldSymbol?.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax()?.GetLocation();
            if (location != null) return location;
        }

        var dependsOnCurrent = entries.FirstOrDefault(e =>
            e.Source == DependencySource.DependsOn && e.Level == 0);
        if (dependsOnCurrent.FieldName != null)
        {
            var attrLocation = FindDependsOnAttributeLocation(classSymbol, dependencyType);
            if (attrLocation != null) return attrLocation;
        }

        return classDeclaration.Identifier.GetLocation();
    }
}
