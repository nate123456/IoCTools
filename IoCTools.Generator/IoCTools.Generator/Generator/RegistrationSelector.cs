namespace IoCTools.Generator.Generator;

using System;
using System.Collections.Generic;
using System.Linq;

using Analysis;

using CodeGeneration;

using Intent;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Models;

using Utilities;

internal static class RegistrationSelector
{
    internal static IEnumerable<ServiceRegistration> GetRegisterAsAllRegistrations(
        INamedTypeSymbol classSymbol,
        AttributeData registerAsAllAttribute,
        string lifetime) =>
        ServiceRegistrationGenerator.GetMultiInterfaceRegistrations(classSymbol, registerAsAllAttribute, lifetime);

    internal static IEnumerable<ServiceRegistration> GetRegisterAsRegistrationsWithSourceContext(
        INamedTypeSymbol classSymbol,
        AttributeData registerAsAttribute,
        string lifetime,
        SourceProductionContext context) => ServiceRegistrationGenerator.GetRegisterAsRegistrationsWithSourceContext(
        classSymbol, registerAsAttribute, lifetime, context);

    internal static IEnumerable<ServiceRegistration> GetDefaultRegistrations(
        INamedTypeSymbol classSymbol,
        string lifetime,
        SourceProductionContext context)
    {
        var results = new List<ServiceRegistration>();

        // Skip all registrations if non-generic SkipRegistration attribute is present
        var hasNonGenericSkipRegistration = classSymbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.ToDisplayString() ==
                         "IoCTools.Abstractions.Annotations.SkipRegistrationAttribute");
        if (hasNonGenericSkipRegistration)
            return results;

        var hasConfigInjection = ServiceDiscovery.HasInjectConfigurationFieldsAcrossPartialClasses(classSymbol);

        // Concrete class registration
        results.Add(new ServiceRegistration(classSymbol, classSymbol, lifetime, false, hasConfigInjection));

        // Interface registrations using utility discovery
        var interfaces = InterfaceDiscovery.GetAllInterfacesForService(classSymbol);
        foreach (var @interface in interfaces)
            results.Add(new ServiceRegistration(classSymbol, @interface, lifetime, false, hasConfigInjection));

        return results;
    }

    internal static IEnumerable<ServiceRegistration> GetConditionalServiceRegistrations(
        SemanticModel semanticModel,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        SourceProductionContext context)
    {
        var results = new List<ServiceRegistration>();

        // Check for SkipRegistration attribute - skip all registrations if present
        var hasNonGenericSkipRegistration = classSymbol.GetAttributes()
            .Any(attr =>
                attr.AttributeClass?.ToDisplayString() ==
                "IoCTools.Abstractions.Annotations.SkipRegistrationAttribute");
        if (hasNonGenericSkipRegistration)
            return results;

        // Validate conditional service attributes
        var conditionalServiceAttrs = classSymbol.GetAttributes()
            .Where(attr =>
                attr.AttributeClass?.ToDisplayString() ==
                "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute")
            .ToList();

        foreach (var conditionalAttr in conditionalServiceAttrs)
        {
            var validationResult = ConditionalServiceEvaluator.ValidateConditionsDetailed(conditionalAttr);
            if (!validationResult.IsValid)
                foreach (var error in validationResult.Errors)
                    GeneratorDiagnostics.Report(context, "IOC020",
                        "ConditionalService validation error",
                        $"Class '{classSymbol.Name}': {error}");
        }

        var (hasLifetimeAttribute, _, _, _) = ServiceDiscovery.GetLifetimeAttributes(classSymbol);
        var lifetime = hasLifetimeAttribute ? ServiceDiscovery.GetServiceLifetimeFromAttributes(classSymbol) : "Scoped";

        var isHostedService = TypeAnalyzer.IsAssignableFromIHostedService(classSymbol);
        if (isHostedService) lifetime = "BackgroundService";

        var serviceRegisterAsAllAttr = classSymbol.GetAttributes().FirstOrDefault(attr =>
            attr.AttributeClass?.ToDisplayString() ==
            "IoCTools.Abstractions.Annotations.RegisterAsAllAttribute");

        foreach (var conditionalAttr in conditionalServiceAttrs)
        {
            var condition = ConditionalServiceEvaluator.ExtractCondition(conditionalAttr);
            if (condition == null) continue;

            if (serviceRegisterAsAllAttr != null)
            {
                var multiInterfaceRegistrations =
                    ServiceRegistrationGenerator.GetMultiInterfaceRegistrationsForConditionalServices(
                        classSymbol, serviceRegisterAsAllAttr, lifetime);
                foreach (var registration in multiInterfaceRegistrations)
                    results.Add(new ConditionalServiceRegistration(
                        registration.ClassSymbol,
                        registration.InterfaceSymbol,
                        registration.Lifetime,
                        condition,
                        registration.UseSharedInstance,
                        registration.HasConfigurationInjection));
            }
            else
            {
                var hasConfigInjection = ServiceDiscovery.HasInjectConfigurationFieldsAcrossPartialClasses(classSymbol);
                var useSharedInstance = hasConfigInjection;
                results.Add(new ConditionalServiceRegistration(classSymbol, classSymbol, lifetime,
                    condition, useSharedInstance, hasConfigInjection));

                if (lifetime != "BackgroundService")
                {
                    var allInterfaces = InterfaceDiscovery.GetAllInterfacesForService(classSymbol);
                    foreach (var interfaceSymbol in allInterfaces)
                        results.Add(new ConditionalServiceRegistration(classSymbol, interfaceSymbol,
                            lifetime, condition, useSharedInstance, hasConfigInjection));
                }
            }
        }

        return results;
    }

    internal static IEnumerable<ServiceRegistration> GetServicesToRegisterForSingleClass(
        SemanticModel semanticModel,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        SourceProductionContext context)
    {
        var serviceRegistrations = new List<ServiceRegistration>();

        try
        {
            if (classSymbol.IsStatic) return serviceRegistrations;
            if (classSymbol.IsAbstract) return serviceRegistrations;

            // Global opt-out: honor non-generic [SkipRegistration] on any service kind (including BackgroundService)
            var hasNonGenericSkipRegistration = classSymbol.GetAttributes()
                .Any(attr => attr.AttributeClass?.ToDisplayString() ==
                             "IoCTools.Abstractions.Annotations.SkipRegistrationAttribute");
            if (hasNonGenericSkipRegistration)
                return serviceRegistrations;

            var hasRegisterAsAll = classSymbol.GetAttributes().Any(attr =>
                attr.AttributeClass?.ToDisplayString() ==
                "IoCTools.Abstractions.Annotations.RegisterAsAllAttribute");

            var hasRegisterAs = classSymbol.GetAttributes().Any(attr =>
                attr.AttributeClass?.ToDisplayString()
                    ?.StartsWith("IoCTools.Abstractions.Annotations.RegisterAsAttribute") == true &&
                attr.AttributeClass.IsGenericType);

            var (hasLifetimeAttribute, _, _, _) = ServiceDiscovery.GetLifetimeAttributes(classSymbol);
            var hasInjectFields = ServiceDiscovery.HasInjectFieldsAcrossPartialClasses(classSymbol);
            var hasDependsOnAttribute = classSymbol.GetAttributes().Any(attr =>
                attr.AttributeClass?.Name?.StartsWith("DependsOn") == true);
            var hasInjectConfigurationFields =
                ServiceDiscovery.HasInjectConfigurationFieldsAcrossPartialClasses(classSymbol);
            var hasConditionalServiceAttribute = classSymbol.GetAttributes().Any(attr =>
                attr.AttributeClass?.ToDisplayString() ==
                "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute");

            var isHostedService = TypeAnalyzer.IsAssignableFromIHostedService(classSymbol);

            var hasExplicitServiceIntent = ServiceIntentEvaluator.HasExplicitServiceIntent(
                classSymbol,
                hasInjectFields,
                hasInjectConfigurationFields,
                hasDependsOnAttribute,
                hasConditionalServiceAttribute,
                hasRegisterAsAll,
                hasRegisterAs,
                hasLifetimeAttribute,
                isHostedService,
                classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)) &&
                classSymbol.Interfaces.Any());

            // Partial-with-interfaces case covered in ServiceIntentEvaluator

            if (!hasExplicitServiceIntent)
                return serviceRegistrations;

            var (hasLifetimeAttr, _, _, _) = ServiceDiscovery.GetLifetimeAttributes(classSymbol);

            var conditionalServiceAttrs = classSymbol.GetAttributes()
                .Where(attr =>
                    attr.AttributeClass?.ToDisplayString() ==
                    "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute")
                .ToList();

            if (conditionalServiceAttrs.Any())
            {
                foreach (var conditionalAttr in conditionalServiceAttrs)
                {
                    var validationResult = ConditionalServiceEvaluator.ValidateConditionsDetailed(conditionalAttr);
                    if (!validationResult.IsValid)
                        foreach (var error in validationResult.Errors)
                            GeneratorDiagnostics.Report(context, "IOC020",
                                "ConditionalService validation error",
                                $"Class '{classSymbol.Name}': {error}");
                }

                var lifetime = hasLifetimeAttribute
                    ? ServiceDiscovery.GetServiceLifetimeFromAttributes(classSymbol)
                    : "Scoped";

                if (isHostedService) lifetime = "BackgroundService";

                var serviceRegisterAsAllAttr = classSymbol.GetAttributes().FirstOrDefault(attr =>
                    attr.AttributeClass?.ToDisplayString() ==
                    "IoCTools.Abstractions.Annotations.RegisterAsAllAttribute");

                foreach (var conditionalAttr in conditionalServiceAttrs)
                {
                    var condition = ConditionalServiceEvaluator.ExtractCondition(conditionalAttr);
                    if (condition == null) continue;

                    if (serviceRegisterAsAllAttr != null)
                    {
                        var multiInterfaceRegistrations =
                            ServiceRegistrationGenerator.GetMultiInterfaceRegistrationsForConditionalServices(
                                classSymbol, serviceRegisterAsAllAttr, lifetime);
                        foreach (var registration in multiInterfaceRegistrations)
                            serviceRegistrations.Add(new ConditionalServiceRegistration(
                                registration.ClassSymbol,
                                registration.InterfaceSymbol,
                                registration.Lifetime,
                                condition,
                                registration.UseSharedInstance,
                                registration.HasConfigurationInjection));
                    }
                    else
                    {
                        var hasConfigInjection =
                            ServiceDiscovery.HasInjectConfigurationFieldsAcrossPartialClasses(classSymbol);
                        var useSharedInstance = hasConfigInjection;
                        serviceRegistrations.Add(new ConditionalServiceRegistration(classSymbol, classSymbol, lifetime,
                            condition, useSharedInstance, hasConfigInjection));

                        if (lifetime != "BackgroundService")
                        {
                            var allInterfaces = InterfaceDiscovery.GetAllInterfacesForService(classSymbol);
                            foreach (var interfaceSymbol in allInterfaces)
                                serviceRegistrations.Add(new ConditionalServiceRegistration(classSymbol,
                                    interfaceSymbol,
                                    lifetime, condition, useSharedInstance, hasConfigInjection));
                        }
                    }
                }

                return serviceRegistrations;
            }

            var registerAsAllAttribute = classSymbol.GetAttributes().FirstOrDefault(attr =>
                attr.AttributeClass?.ToDisplayString() == "IoCTools.Abstractions.Annotations.RegisterAsAllAttribute");

            var hasInjectConfigurationOnly =
                ServiceDiscovery.HasInjectConfigurationFieldsAcrossPartialClasses(classSymbol);

            var hasConditionalAttributes = classSymbol.GetAttributes()
                .Any(attr =>
                    attr.AttributeClass?.ToDisplayString() ==
                    "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute");
            if (hasConditionalAttributes) return serviceRegistrations;

            var lifetimeValue = hasLifetimeAttribute
                ? ServiceDiscovery.GetServiceLifetimeFromAttributes(classSymbol)
                : "Scoped";
            if (isHostedService) lifetimeValue = "BackgroundService";

            var hasConfig = ServiceDiscovery.HasInjectConfigurationFieldsAcrossPartialClasses(classSymbol);

            if (registerAsAllAttribute != null)
            {
                // Use non-conditional multi-interface path for regular RegisterAsAll
                var multiRegs = ServiceRegistrationGenerator.GetMultiInterfaceRegistrations(
                    classSymbol, registerAsAllAttribute, lifetimeValue);
                foreach (var reg in multiRegs)
                    serviceRegistrations.Add(reg);
            }
            else
            {
                var registerAsAttributes = classSymbol.GetAttributes()
                    .Where(attr => attr.AttributeClass?.Name?.StartsWith("RegisterAsAttribute") == true &&
                                   attr.AttributeClass?.IsGenericType == true)
                    .ToList();

                if (registerAsAttributes.Any())
                {
                    // Route through diagnostic-aware helper to emit IOC029/IOC030/IOC031 and correct sharing behavior
                    foreach (var registerAsAttr in registerAsAttributes)
                    {
                        var regs = GetRegisterAsRegistrationsWithSourceContext(classSymbol, registerAsAttr,
                            lifetimeValue, context);
                        serviceRegistrations.AddRange(regs);
                    }
                }
                else
                {
                    // Default behavior when no RegisterAs attributes are present: concrete + all interfaces
                    serviceRegistrations.Add(new ServiceRegistration(classSymbol, classSymbol, lifetimeValue, false,
                        hasConfig));
                    var interfaces = InterfaceDiscovery.GetAllInterfacesForService(classSymbol);
                    foreach (var @interface in interfaces)
                        serviceRegistrations.Add(new ServiceRegistration(classSymbol, @interface, lifetimeValue,
                            false, hasConfig));
                }
            }
        }
        catch (Exception ex)
        {
            GeneratorDiagnostics.Report(context, "IOC998", "Service registration processing error", ex.Message);
        }

        return serviceRegistrations;
    }
}
