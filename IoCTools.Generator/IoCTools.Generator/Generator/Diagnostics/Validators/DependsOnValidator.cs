namespace IoCTools.Generator.Generator.Diagnostics.Validators;

using System.Collections.Generic;
using System.Linq;

using IoCTools.Generator.Diagnostics;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Models;

using Utilities;

internal static class DependsOnValidator
{
    internal static void ValidateDependsOnConflicts(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        InheritanceHierarchyDependencies hierarchyDependencies,
        INamedTypeSymbol classSymbol)
    {
        var dependsOnTypes = new HashSet<string>(hierarchyDependencies.RawAllDependencies
            .Where(d => d.Source == DependencySource.DependsOn)
            .Select(d => d.ServiceType.ToDisplayString()));

        var injectTypes = new HashSet<string>(hierarchyDependencies.RawAllDependencies
            .Where(d => d.Source == DependencySource.Inject)
            .Select(d => d.ServiceType.ToDisplayString()));

        foreach (var dependsOnType in dependsOnTypes)
            if (injectTypes.Contains(dependsOnType))
            {
                var diagnostic = Diagnostic.Create(DiagnosticDescriptors.DependsOnConflictsWithInject,
                    classDeclaration.GetLocation(), dependsOnType, classSymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
    }

    internal static void ValidateDuplicateDependsOn(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        var dependsOnTypeSymbols = GetDependsOnTypeSymbolsFromInheritanceChain(classSymbol);
        var seenTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var duplicates = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var typeSymbol in dependsOnTypeSymbols)
            if (!seenTypes.Add(typeSymbol))
                duplicates.Add(typeSymbol);

        foreach (var duplicate in duplicates)
        {
            var displayName = TypeHelpers.FormatTypeNameForDiagnostic(duplicate);
            var diagnostic = Diagnostic.Create(DiagnosticDescriptors.DuplicateDependsOnType,
                classDeclaration.GetLocation(), displayName, classSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    internal static void ValidateDuplicatesWithinSingleDependsOn(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        foreach (var attr in classSymbol.GetAttributes())
            if (attr.AttributeClass?.Name == "DependsOnAttribute" && attr.AttributeClass?.TypeArguments != null)
            {
                var typeArguments = attr.AttributeClass.TypeArguments.ToList();
                var duplicates = typeArguments.GroupBy(t => t, SymbolEqualityComparer.Default)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .Cast<ITypeSymbol>()
                    .ToList();

                foreach (var duplicate in duplicates)
                {
                    var displayName = TypeHelpers.FormatTypeNameForDiagnostic(duplicate);
                    var diagnostic = Diagnostic.Create(
                        DiagnosticDescriptors.DuplicateTypeInSingleDependsOn,
                        classDeclaration.GetLocation(),
                        displayName,
                        classSymbol.Name);
                    context.ReportDiagnostic(diagnostic);
                }
            }
    }

    internal static void ValidateUnnecessarySkipRegistration(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        var registerAsAllAttribute = classSymbol.GetAttributes().FirstOrDefault(attr =>
            attr.AttributeClass?.ToDisplayString() == "IoCTools.Abstractions.Annotations.RegisterAsAllAttribute");
        if (registerAsAllAttribute == null) return;

        var skipRegistrationAttributes = classSymbol.GetAttributes()
            .Where(attr => attr.AttributeClass?.ToDisplayString()
                .StartsWith("IoCTools.Abstractions.Annotations.SkipRegistrationAttribute") == true)
            .ToList();
        if (!skipRegistrationAttributes.Any()) return;

        var allInterfaces = classSymbol.AllInterfaces.ToList();
        foreach (var attr in skipRegistrationAttributes)
            if (attr.AttributeClass?.TypeArguments != null)
                foreach (var typeArg in attr.AttributeClass.TypeArguments)
                    if (!allInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, typeArg)))
                    {
                        var diagnostic = Diagnostic.Create(
                            DiagnosticDescriptors.SkipRegistrationForNonRegisteredInterface,
                            attr.ApplicationSyntaxReference?.GetSyntax()?.GetLocation() ??
                            classDeclaration.GetLocation(),
                            typeArg.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                            classSymbol.Name);
                        context.ReportDiagnostic(diagnostic);
                    }
    }

    private static List<ITypeSymbol> GetDependsOnTypeSymbolsFromInheritanceChain(INamedTypeSymbol classSymbol)
    {
        var types = new List<ITypeSymbol>();
        var currentType = classSymbol;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            foreach (var attr in currentType.GetAttributes())
                if (attr.AttributeClass?.Name == "DependsOnAttribute" && attr.AttributeClass?.TypeArguments != null)
                    foreach (var typeArg in attr.AttributeClass.TypeArguments)
                        types.Add(typeArg);
            currentType = currentType.BaseType;
        }

        return types;
    }
}
