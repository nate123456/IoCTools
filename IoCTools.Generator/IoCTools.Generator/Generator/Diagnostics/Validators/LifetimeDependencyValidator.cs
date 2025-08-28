namespace IoCTools.Generator.Generator.Diagnostics.Validators;

using System.Collections.Generic;
using System.Linq;

using IoCTools.Generator.Diagnostics;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Models;

using Utilities;

internal static class LifetimeDependencyValidator
{
    internal static void ValidateInheritanceChainLifetimesForSourceProduction(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        Dictionary<string, string> serviceLifetimes,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations)
    {
        var serviceLifetime = LifetimeUtilities.GetServiceLifetimeFromSymbol(classSymbol);
        if (serviceLifetime != "Singleton") return;

        var currentType = classSymbol.BaseType;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            var baseServiceLifetime =
                DependencyLifetimeResolver.GetDependencyLifetimeForSourceProduction(currentType, serviceLifetimes,
                    allImplementations);
            if (baseServiceLifetime == "Scoped")
            {
                var diagnostic = Diagnostic.Create(DiagnosticDescriptors.InheritanceChainLifetimeValidation,
                    classDeclaration.GetLocation(), classSymbol.Name, serviceLifetime, baseServiceLifetime);
                context.ReportDiagnostic(diagnostic);
                return;
            }

            var dependsOnAttributes = currentType.GetAttributes()
                .Where(attr => attr.AttributeClass?.Name == "DependsOnAttribute").ToList();
            foreach (var attr in dependsOnAttributes)
                if (attr.AttributeClass?.IsGenericType == true && attr.AttributeClass.TypeArguments.Length > 0)
                    foreach (var typeArg in attr.AttributeClass.TypeArguments)
                    {
                        var depLifetime =
                            DependencyLifetimeResolver.GetDependencyLifetimeForSourceProduction(typeArg,
                                serviceLifetimes, allImplementations);
                        if (depLifetime == "Scoped")
                        {
                            var diagnostic = Diagnostic.Create(DiagnosticDescriptors.InheritanceChainLifetimeValidation,
                                classDeclaration.GetLocation(), classSymbol.Name, serviceLifetime, depLifetime);
                            context.ReportDiagnostic(diagnostic);
                            return;
                        }
                    }

            currentType = currentType.BaseType;
        }
    }

    internal static void ValidateLifetimeDependencies(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        InheritanceHierarchyDependencies hierarchyDependencies,
        Dictionary<string, string> serviceLifetimes,
        HashSet<string> allRegisteredServices,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations,
        DiagnosticConfiguration diagnosticConfig,
        INamedTypeSymbol classSymbol)
    {
        var serviceLifetime = LifetimeUtilities.GetServiceLifetimeFromSymbol(classSymbol);
        if (serviceLifetime == null) return;

        foreach (var dependency in hierarchyDependencies.AllDependenciesWithExternalFlag)
        {
            if (dependency.IsExternal) continue;
            if (dependency.Source == DependencySource.ConfigurationInjection) continue;

            var dependencyTypeName = dependency.ServiceType.ToDisplayString();
            var enumerableTypeInfo = TypeHelpers.ExtractIEnumerableFromWrappedType(dependencyTypeName);
            if (enumerableTypeInfo != null)
            {
                ValidateIEnumerableLifetimes(context, classDeclaration, classSymbol, serviceLifetime,
                    enumerableTypeInfo.InnerType, enumerableTypeInfo.FullEnumerableType, serviceLifetimes,
                    allRegisteredServices, allImplementations);
                continue;
            }

            var (dependencyLifetime, implementationName) =
                DependencyLifetimeResolver.GetDependencyLifetimeWithGenericSupportAndImplementationName(
                    dependencyTypeName, serviceLifetimes, allRegisteredServices, allImplementations);
            if (dependencyLifetime == null) continue;

            if (serviceLifetime == "Singleton" && dependencyLifetime == "Scoped")
            {
                var diagnostic = Diagnostic.Create(DiagnosticDescriptors.SingletonDependsOnScoped,
                    classDeclaration.GetLocation(), classSymbol.Name, dependencyTypeName);
                context.ReportDiagnostic(diagnostic);
            }
            else if (serviceLifetime == "Singleton" && dependencyLifetime == "Transient")
            {
                var displayName = implementationName ??
                                  DependencyLifetimeResolver.FindImplementationNameForInterface(dependencyTypeName,
                                      allRegisteredServices) ??
                                  TypeHelpers.ExtractSimpleTypeNameFromFullName(dependencyTypeName);
                var diagnostic = Diagnostic.Create(DiagnosticDescriptors.SingletonDependsOnTransient,
                    classDeclaration.GetLocation(), classSymbol.Name, displayName);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    internal static void ValidateIEnumerableLifetimes(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        string serviceLifetime,
        string innerType,
        string dependencyTypeName,
        Dictionary<string, string> serviceLifetimes,
        HashSet<string> allRegisteredServices,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations)
    {
        var foundImplementations = false;
        var processed = new HashSet<string>();
        if (allImplementations.TryGetValue(innerType, out var direct))
        {
            foundImplementations = true;
            foreach (var implementation in direct)
            {
                if (!processed.Add(implementation.ToDisplayString())) continue;
                var implLifetime = LifetimeUtilities.GetServiceLifetimeFromSymbol(implementation);
                if (implLifetime == null) continue;
                if (serviceLifetime == "Singleton" && implLifetime == "Scoped")
                {
                    var diagnostic = Diagnostic.Create(DiagnosticDescriptors.SingletonDependsOnScoped,
                        classDeclaration.GetLocation(), classSymbol.Name,
                        $"{dependencyTypeName} -> {implementation.Name}");
                    context.ReportDiagnostic(diagnostic);
                }
                else if (serviceLifetime == "Singleton" && implLifetime == "Transient")
                {
                    var diagnostic = Diagnostic.Create(DiagnosticDescriptors.SingletonDependsOnTransient,
                        classDeclaration.GetLocation(), classSymbol.Name,
                        $"{dependencyTypeName} -> {implementation.Name}");
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        if (innerType.Contains('<') && innerType.Contains('>'))
        {
            var baseGeneric = TypeHelpers.ExtractBaseGenericInterface(innerType);
            if (baseGeneric != null && allImplementations.TryGetValue(baseGeneric, out var generics))
            {
                foundImplementations = true;
                foreach (var implementation in generics)
                {
                    if (!processed.Add(implementation.ToDisplayString())) continue;
                    var implLifetime = LifetimeUtilities.GetServiceLifetimeFromSymbol(implementation);
                    if (implLifetime == null) continue;
                    if (serviceLifetime == "Singleton" && implLifetime == "Scoped")
                    {
                        var diagnostic = Diagnostic.Create(DiagnosticDescriptors.SingletonDependsOnScoped,
                            classDeclaration.GetLocation(), classSymbol.Name,
                            $"{dependencyTypeName} -> {implementation.Name}");
                        context.ReportDiagnostic(diagnostic);
                    }
                    else if (serviceLifetime == "Singleton" && implLifetime == "Transient")
                    {
                        var diagnostic = Diagnostic.Create(DiagnosticDescriptors.SingletonDependsOnTransient,
                            classDeclaration.GetLocation(), classSymbol.Name,
                            $"{dependencyTypeName} -> {implementation.Name}");
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }

        if (!foundImplementations)
            foreach (var kvp in allImplementations)
            foreach (var implementation in kvp.Value)
            {
                if (!processed.Add(implementation.ToDisplayString())) continue;
                var interfaces = implementation.AllInterfaces.Select(i => i.ToDisplayString());
                if (!interfaces.Contains(innerType)) continue;
                var implLifetime = LifetimeUtilities.GetServiceLifetimeFromSymbol(implementation);
                if (implLifetime == null) continue;
                if (serviceLifetime == "Singleton" && implLifetime == "Scoped")
                {
                    var diagnostic = Diagnostic.Create(DiagnosticDescriptors.SingletonDependsOnScoped,
                        classDeclaration.GetLocation(), classSymbol.Name,
                        $"{dependencyTypeName} -> {implementation.Name}");
                    context.ReportDiagnostic(diagnostic);
                }
                else if (serviceLifetime == "Singleton" && implLifetime == "Transient")
                {
                    var diagnostic = Diagnostic.Create(DiagnosticDescriptors.SingletonDependsOnTransient,
                        classDeclaration.GetLocation(), classSymbol.Name,
                        $"{dependencyTypeName} -> {implementation.Name}");
                    context.ReportDiagnostic(diagnostic);
                }
            }
    }
}
