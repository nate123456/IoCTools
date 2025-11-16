namespace IoCTools.Generator.Generator;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

using CodeGeneration;

using IoCTools.Generator.Diagnostics;
using IoCTools.Generator.Utilities;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

using Models;

internal static class RegistrationEmitter
{
    public static void Emit(Action<string, SourceText> addSource,
        SourceProductionContext context,
        (ImmutableArray<ServiceClassInfo> Services, Compilation Compilation, GeneratorStyleOptions StyleOptions)
            input)
    {
        try
        {
            var (services, compilation, styleOptions) = input;
            if (!services.Any()) return;

            var allServiceRegistrations = new List<ServiceRegistration>();
            var allConfigOptions = new List<ConfigurationOptionsRegistration>();
            var processedClasses = new HashSet<string>(StringComparer.Ordinal);

            var implicitLifetime = styleOptions.DefaultImplicitLifetime;

            foreach (var serviceInfo in services)
            {
                var classKey = serviceInfo.ClassSymbol.ToDisplayString();
                if (!processedClasses.Add(classKey)) continue;
                if (serviceInfo.ClassDeclaration == null || serviceInfo.SemanticModel == null) continue;
                if (TypeSkipEvaluator.ShouldSkipRegistration(serviceInfo.ClassSymbol, compilation, styleOptions))
                    continue;

                var treeServices = RegistrationSelector.GetServicesToRegisterForSingleClass(
                    serviceInfo.SemanticModel, serviceInfo.ClassDeclaration,
                    serviceInfo.ClassSymbol, context, implicitLifetime).ToList();
                allServiceRegistrations.AddRange(treeServices);

                var treeConfigOptions = ConfigurationOptionsScanner.GetConfigurationOptionsToRegister(
                    serviceInfo.SemanticModel, serviceInfo.ClassDeclaration.SyntaxTree.GetRoot()).ToList();
                allConfigOptions.AddRange(treeConfigOptions);
            }

            allServiceRegistrations = DeduplicateServiceRegistrations(allServiceRegistrations);
            allConfigOptions = DeduplicateConfigurationOptions(allConfigOptions);

            if (allServiceRegistrations.Any() || allConfigOptions.Any())
            {
                var assemblyName = compilation.AssemblyName ?? "UnknownAssembly";
                var safeRootNamespace = assemblyName.Replace("-", "_").Replace(" ", "_");
                var extensionNamespace = safeRootNamespace + ".Extensions.Generated";
                var safeAssemblyName = safeRootNamespace.Replace(".", "");

                var extensionCode = ServiceRegistrationGenerator.GenerateRegistrationExtensionMethod(
                    allServiceRegistrations, extensionNamespace, safeAssemblyName, allConfigOptions);

                var registrationFileName = $"ServiceRegistrations_{safeAssemblyName}.g.cs";
                addSource(registrationFileName, SourceText.From(extensionCode, Encoding.UTF8));
            }
        }
        catch (Exception ex)
        {
            GeneratorDiagnostics.Report(context, "IOC999", "Service registration generation failed", ex.Message);
        }
    }

    private static List<ServiceRegistration> DeduplicateServiceRegistrations(
        List<ServiceRegistration> serviceRegistrations)
    {
        var deduplicationMap = new Dictionary<string, ServiceRegistration>();
        foreach (var service in serviceRegistrations)
        {
            var isConditional = service is ConditionalServiceRegistration;
            var classTypeRaw = service.ClassSymbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "";
            var interfaceTypeRaw = service.InterfaceSymbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ??
                                   "";

            var classType = isConditional
                ? TypeNameSimplifier.SimplifyTypesForConditionalServices(classTypeRaw)
                : TypeNameSimplifier.SimplifySystemTypesForServiceRegistration(classTypeRaw);
            var interfaceType = isConditional
                ? TypeNameSimplifier.SimplifyTypesForConditionalServices(interfaceTypeRaw)
                : TypeNameSimplifier.SimplifySystemTypesForServiceRegistration(interfaceTypeRaw);

            var conditionKey = "";
            if (service is ConditionalServiceRegistration conditional)
            {
                conditionKey = conditional.Condition?.ToString()?.Trim()?.Replace("  ", " ") ?? "";
                if (!string.IsNullOrEmpty(conditional.Condition?.ConfigValue))
                {
                    var config = conditional.Condition;
                    conditionKey =
                        $"config:{config?.ConfigValue?.Trim()}:{config?.EqualsValue?.Trim()}:{config?.NotEquals?.Trim()}";
                }
                else if (!string.IsNullOrEmpty(conditional.Condition?.Environment))
                {
                    conditionKey = $"env:{conditional.Condition?.Environment?.Trim()}";
                }
            }

            var isConcreteRegistration =
                SymbolEqualityComparer.Default.Equals(service.ClassSymbol, service.InterfaceSymbol);
            var registrationType = isConcreteRegistration ? "concrete" : "interface";

            var key =
                $"{classType}|{interfaceType}|{service.Lifetime}|{service.UseSharedInstance}|{isConditional}|{conditionKey}|{registrationType}|{service.HasConfigurationInjection}";

            if (!deduplicationMap.ContainsKey(key)) deduplicationMap[key] = service;
        }

        return deduplicationMap.Values.ToList();
    }

    // Type simplification moved to Utilities/TypeNameSimplifier to avoid duplication

    private static List<ConfigurationOptionsRegistration> DeduplicateConfigurationOptions(
        List<ConfigurationOptionsRegistration> configOptions)
    {
        return configOptions
            .GroupBy(c => new { OptionsType = c.OptionsType?.ToDisplayString() ?? "", c.SectionName })
            .Select(g => g.First())
            .ToList();
    }

    // per-class selection now delegated to existing generator method for correctness


    private static void ValidateUnnecessarySkipRegistration(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        var registerAsAllAttribute = classSymbol.GetAttributes().FirstOrDefault(attr =>
            attr.AttributeClass?.ToDisplayString() == "IoCTools.Abstractions.Annotations.RegisterAsAllAttribute");
        if (registerAsAllAttribute == null) return;

        var skipRegistrationAttributes = classSymbol.GetAttributes()
            .Where(attr =>
                attr.AttributeClass?.ToDisplayString()
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
                            FormatTypeNameForDiagnostic(typeArg),
                            classSymbol.Name);
                        context.ReportDiagnostic(diagnostic);
                    }
    }

    private static string FormatTypeNameForDiagnostic(ITypeSymbol typeSymbol)
        => typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
}
