namespace IoCTools.Generator.Generator;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Analysis;

using IoCTools.Generator.Diagnostics.Configuration;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

using Models;

using Utilities;

internal static class DiagnosticsRunner
{
    public static void EmitWithReferencedTypes(
        SourceProductionContext context,
        ((ImmutableArray<ServiceClassInfo> Services, ImmutableArray<INamedTypeSymbol> ReferencedTypes, Compilation
            Compilation) Input,
            AnalyzerConfigOptionsProvider ConfigOptions) payload) =>
        ValidateAllServiceDiagnosticsWithReferencedTypes(context, payload);

    internal static void ValidateAllServiceDiagnostics(SourceProductionContext context,
        ((ImmutableArray<ServiceClassInfo> Services, Compilation Compilation) Input, AnalyzerConfigOptionsProvider
            ConfigOptions) input)
    {
        try
        {
            var ((services, compilation), configOptions) = input;
            if (!services.Any()) return;

            var allRegisteredServices = new HashSet<string>();
            var allImplementations = new Dictionary<string, List<INamedTypeSymbol>>();
            var serviceLifetimes = new Dictionary<string, string>();
            var processedClasses = new HashSet<string>(StringComparer.Ordinal);

            foreach (var serviceInfo in services)
            {
                var classKey = serviceInfo.ClassSymbol.ToDisplayString();
                if (!processedClasses.Add(classKey)) continue;
                if (serviceInfo.ClassDeclaration?.SyntaxTree != null && serviceInfo.SemanticModel != null)
                    DiagnosticScan.CollectServiceSymbolsOnce(serviceInfo.ClassDeclaration.SyntaxTree.GetRoot(),
                        serviceInfo.SemanticModel,
                        new List<INamedTypeSymbol>(), allRegisteredServices, allImplementations, serviceLifetimes,
                        new HashSet<string>());
            }

            var diagnosticConfig = DiagnosticConfigProvider.From(configOptions);

            var validatedClasses = new HashSet<string>(StringComparer.Ordinal);
            foreach (var serviceInfo in services)
            {
                var classKey = serviceInfo.ClassSymbol.ToDisplayString();
                if (!validatedClasses.Add(classKey)) continue;
                if (serviceInfo.SemanticModel == null) continue;

                var hierarchyDependencies = DependencyAnalyzer.GetInheritanceHierarchyDependenciesForDiagnostics(
                    serviceInfo.ClassSymbol, serviceInfo.SemanticModel, allRegisteredServices, allImplementations);

                if (serviceInfo.ClassDeclaration != null)
                    ValidateDependenciesComplete(context, serviceInfo.ClassDeclaration, hierarchyDependencies,
                        allRegisteredServices, allImplementations, serviceLifetimes, diagnosticConfig,
                        serviceInfo.SemanticModel, serviceInfo.ClassSymbol);
            }

            var allServiceSymbols = services.Select(s => s.ClassSymbol).ToList();
            DiagnosticRules.ValidateCircularDependenciesComplete(context, allServiceSymbols, allRegisteredServices,
                diagnosticConfig);
            DiagnosticRules.ValidateAttributeCombinations(context, services.Select(s => s.ClassSymbol));
        }
        catch (Exception ex)
        {
            GeneratorDiagnostics.Report(context, "IOC996", "Diagnostic validation pipeline error", ex.Message);
        }
    }

    internal static void ValidateDependenciesComplete(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        InheritanceHierarchyDependencies hierarchyDependencies,
        HashSet<string> allRegisteredServices,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations,
        Dictionary<string, string> serviceLifetimes,
        DiagnosticConfiguration diagnosticConfig,
        SemanticModel semanticModel,
        INamedTypeSymbol classSymbol)
    {
        if (!diagnosticConfig.DiagnosticsEnabled) return;

        var hasExternalServiceAttribute = classSymbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.ToDisplayString() ==
                         "IoCTools.Abstractions.Annotations.ExternalServiceAttribute");
        if (hasExternalServiceAttribute) return;

        // IOC012/IOC013
        DiagnosticRules.ValidateLifetimeDependencies(context, classDeclaration, hierarchyDependencies, serviceLifetimes,
            allRegisteredServices, allImplementations, diagnosticConfig, classSymbol);

        // IOC007/006/008/009 already in DiagnosticRules
        DiagnosticRules.ValidateDependsOnConflicts(context, classDeclaration, hierarchyDependencies, classSymbol);
        DiagnosticRules.ValidateDuplicateDependsOn(context, classDeclaration, classSymbol);
        DiagnosticRules.ValidateDuplicatesWithinSingleDependsOn(context, classDeclaration, classSymbol);
        DiagnosticRules.ValidateUnnecessarySkipRegistration(context, classDeclaration, classSymbol);

        // IOC016–IOC019
        DiagnosticRules.ValidateConfigurationInjection(context, classDeclaration, classSymbol);

        // IOC011
        DiagnosticRules.ValidateHostedServiceRequirements(context, classDeclaration, classSymbol);

        // IOC015
        var serviceLifetime = LifetimeUtilities.GetServiceLifetimeFromSymbol(classSymbol);
        if (serviceLifetime == "Singleton")
            DiagnosticRules.ValidateInheritanceChainLifetimesForSourceProduction(context, classDeclaration, classSymbol,
                serviceLifetimes, allImplementations);

        // IOC001/IOC002
        DiagnosticRules.ValidateMissingDependencies(context, classDeclaration, hierarchyDependencies,
            allRegisteredServices,
            allImplementations, serviceLifetimes, diagnosticConfig);
    }

    private static void ValidateAllServiceDiagnosticsWithReferencedTypes(SourceProductionContext context,
        ((ImmutableArray<ServiceClassInfo> Services, ImmutableArray<INamedTypeSymbol> ReferencedTypes, Compilation
            Compilation) Input, AnalyzerConfigOptionsProvider ConfigOptions) input)
    {
        try
        {
            var ((services, referencedTypes, compilation), configOptions) = input;
            if (!services.Any()) return;

            var allRegisteredServices = new HashSet<string>();
            var allImplementations = new Dictionary<string, List<INamedTypeSymbol>>();
            var serviceLifetimes = new Dictionary<string, string>();
            var processedClasses = new HashSet<string>(StringComparer.Ordinal);

            // Collect from current project
            foreach (var serviceInfo in services)
            {
                var classKey = serviceInfo.ClassSymbol.ToDisplayString();
                if (!processedClasses.Add(classKey)) continue;
                if (serviceInfo.ClassDeclaration != null && serviceInfo.SemanticModel != null)
                    DiagnosticScan.CollectServiceSymbolsOnce(
                        serviceInfo.ClassDeclaration.SyntaxTree.GetRoot(), serviceInfo.SemanticModel,
                        new List<INamedTypeSymbol>(), allRegisteredServices, allImplementations, serviceLifetimes,
                        new HashSet<string>());
            }

            // Scan current compilation types
            var currentCompilationTypes = new List<INamedTypeSymbol>();
            DiagnosticScan.ScanNamespaceForTypes(compilation.Assembly.GlobalNamespace, currentCompilationTypes);
            foreach (var currentType in currentCompilationTypes)
            {
                var typeName = currentType.ToDisplayString();
                if (!currentType.IsAbstract && currentType.TypeKind == TypeKind.Class)
                {
                    var hasConditionalServiceAttribute = currentType.GetAttributes().Any(attr =>
                        attr.AttributeClass?.ToDisplayString() ==
                        "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute");
                    var hasInjectFields = ServiceDiscovery.HasInjectFieldsAcrossPartialClasses(currentType);
                    var hasDependsOnAttribute = currentType.GetAttributes()
                        .Any(attr => attr.AttributeClass?.Name?.StartsWith("DependsOn") == true);
                    var hasRegisterAsAllAttribute = currentType.GetAttributes()
                        .Any(attr => attr.AttributeClass?.Name == "RegisterAsAllAttribute");
                    var hasRegisterAsAttribute = currentType.GetAttributes().Any(attr =>
                        attr.AttributeClass?.Name?.StartsWith("RegisterAsAttribute") == true &&
                        attr.AttributeClass?.IsGenericType == true);
                    var isHostedService = TypeAnalyzer.IsAssignableFromIHostedService(currentType);
                    var (hasLifetimeAttribute, _, _, _) = ServiceDiscovery.GetLifetimeAttributes(currentType);
                    var hasExplicitServiceIntent = hasConditionalServiceAttribute || hasRegisterAsAllAttribute ||
                                                   hasRegisterAsAttribute || hasLifetimeAttribute || isHostedService ||
                                                   hasInjectFields || hasDependsOnAttribute;
                    if (hasExplicitServiceIntent) allRegisteredServices.Add(typeName);
                    if (!allImplementations.ContainsKey(typeName))
                        allImplementations[typeName] = new List<INamedTypeSymbol>();
                    allImplementations[typeName].Add(currentType);
                }

                foreach (var interfaceType in currentType.Interfaces)
                {
                    var interfaceName = interfaceType.ToDisplayString();
                    if (!allImplementations.ContainsKey(interfaceName))
                        allImplementations[interfaceName] = new List<INamedTypeSymbol>();
                    allImplementations[interfaceName].Add(currentType);
                }
            }

            // Add referenced assembly types to implementations and registrations
            foreach (var referencedType in referencedTypes)
            {
                var typeName = referencedType.ToDisplayString();
                if (!allImplementations.ContainsKey(typeName))
                    allImplementations[typeName] = new List<INamedTypeSymbol>();
                allImplementations[typeName].Add(referencedType);
                foreach (var interfaceType in referencedType.Interfaces)
                {
                    var interfaceName = interfaceType.ToDisplayString();
                    if (!allImplementations.ContainsKey(interfaceName))
                        allImplementations[interfaceName] = new List<INamedTypeSymbol>();
                    allImplementations[interfaceName].Add(referencedType);
                }

                var hasServiceRelatedAttribute = referencedType.GetAttributes().Any(attr =>
                    attr.AttributeClass?.ToDisplayString() ==
                    "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute" ||
                    attr.AttributeClass?.ToDisplayString() == "IoCTools.Abstractions.Annotations.ScopedAttribute" ||
                    attr.AttributeClass?.ToDisplayString() == "IoCTools.Abstractions.Annotations.SingletonAttribute" ||
                    attr.AttributeClass?.ToDisplayString() == "IoCTools.Abstractions.Annotations.TransientAttribute" ||
                    attr.AttributeClass?.Name == "RegisterAsAllAttribute" ||
                    (attr.AttributeClass?.Name?.StartsWith("RegisterAsAttribute") == true &&
                     attr.AttributeClass?.IsGenericType == true));
                var isHostedService = TypeAnalyzer.IsAssignableFromIHostedService(referencedType);
                if (hasServiceRelatedAttribute || isHostedService) allRegisteredServices.Add(typeName);
                if (!referencedType.IsAbstract && referencedType.TypeKind == TypeKind.Class)
                    allRegisteredServices.Add(typeName);
            }

            var diagnosticConfig = DiagnosticConfigProvider.From(configOptions);

            foreach (var serviceInfo in services)
            {
                var classKey = serviceInfo.ClassSymbol.ToDisplayString();
                if (!processedClasses.Contains(classKey)) continue;

                if (serviceInfo.ClassDeclaration != null && serviceInfo.SemanticModel != null)
                {
                    var hierarchyDependencies = DependencyAnalyzer.GetInheritanceHierarchyDependenciesForDiagnostics(
                        serviceInfo.ClassSymbol, serviceInfo.SemanticModel, allRegisteredServices, allImplementations);

                    ValidateDependenciesComplete(context, serviceInfo.ClassDeclaration, hierarchyDependencies,
                        allRegisteredServices, allImplementations, serviceLifetimes, diagnosticConfig,
                        serviceInfo.SemanticModel, serviceInfo.ClassSymbol);
                }
            }

            var allServiceSymbols = services.Select(s => s.ClassSymbol).ToList();
            DiagnosticRules.ValidateCircularDependenciesComplete(context, allServiceSymbols, allRegisteredServices,
                diagnosticConfig);
            DiagnosticRules.ValidateAttributeCombinations(context, services.Select(s => s.ClassSymbol));
        }
        catch (Exception ex)
        {
            GeneratorDiagnostics.Report(context, "IOC997", "Cross-assembly diagnostic validation error", ex.Message);
        }
    }
}
