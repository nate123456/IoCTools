namespace IoCTools.Generator.CodeGeneration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;

using Models;

internal static partial class ServiceRegistrationGenerator
{
    public static string GenerateRegistrationExtensionMethod(
        List<ServiceRegistration> services,
        string extNameSpace,
        string methodNamePrefix,
        List<ConfigurationOptionsRegistration>? configOptions = null)
        => GenerateRegistrationExtensionMethodImpl(services, extNameSpace, methodNamePrefix, configOptions);

    // Moved implementation here for clarity (no behavior change)
    private static string GenerateRegistrationExtensionMethodImpl(List<ServiceRegistration> services,
        string extNameSpace,
        string methodNamePrefix,
        List<ConfigurationOptionsRegistration>? configOptions = null)
    {
        var uniqueNamespaces = new HashSet<string>();
        uniqueNamespaces.Add("Microsoft.Extensions.DependencyInjection");

        var hasConditionalServices = services.OfType<ConditionalServiceRegistration>().Any();
        if (hasConditionalServices)
        {
            uniqueNamespaces.Add("System");
            uniqueNamespaces.Add("Microsoft.Extensions.Configuration");
        }

        foreach (var service in services)
        {
            var classNs = service.ClassSymbol.ContainingNamespace;
            if (classNs != null && !classNs.IsGlobalNamespace)
            {
                var classNsName = classNs.ToDisplayString();
                if (!string.IsNullOrEmpty(classNsName)) uniqueNamespaces.Add(classNsName);
            }

            var interfaceNs = service.InterfaceSymbol.ContainingNamespace;
            if (interfaceNs != null && !interfaceNs.IsGlobalNamespace)
            {
                var interfaceNsName = interfaceNs.ToDisplayString();
                if (!string.IsNullOrEmpty(interfaceNsName)) uniqueNamespaces.Add(interfaceNsName);
            }

            if (service.UseSharedInstance) uniqueNamespaces.Add("System");
            if (service.Lifetime == "BackgroundService") uniqueNamespaces.Add("Microsoft.Extensions.Hosting");

            var interfaceDisplayString = service.InterfaceSymbol.ToDisplayString();
            if (interfaceDisplayString.Contains("System.Collections.Generic.List") ||
                interfaceDisplayString.Contains("System.Collections.Generic.Dictionary") ||
                interfaceDisplayString.Contains("System.Collections.Generic.IEnumerable") ||
                interfaceDisplayString.Contains("System.Collections.Generic.ICollection") ||
                interfaceDisplayString.Contains("System.Collections.Generic.IList"))
                uniqueNamespaces.Add("System.Collections.Generic");
        }

        var hasConfigurationInjection = services.Any(s =>
            s.ClassSymbol.DeclaringSyntaxReferences.Any(syntaxRef =>
                HasConfigurationInjectionFields(syntaxRef.GetSyntax())));

        var conditionalServices = services.OfType<ConditionalServiceRegistration>().ToList();
        var regularServices = services.Where(s => !(s is ConditionalServiceRegistration)).ToList();

        var hasSimpleConditionalServices = conditionalServices.Any(cs => !cs.HasConfigurationInjection);
        var hasRegularServicesInMethod = regularServices.Any();
        var shouldUseSimplifiedNamingForConsistency = hasSimpleConditionalServices && hasRegularServicesInMethod;

        var conditionalServicesNeedingConfig = conditionalServices.Any(cs => cs.Condition.RequiresConfiguration);
        var hasRegularServices = regularServices.Any();
        var onlyConditionalServices = conditionalServices.Any() && !hasRegularServices;

        var needsConfigParameter = (configOptions != null && configOptions.Any()) ||
                                   hasConfigurationInjection ||
                                   conditionalServicesNeedingConfig ||
                                   conditionalServices.Any();

        if (needsConfigParameter || conditionalServices.Any())
            uniqueNamespaces.Add("Microsoft.Extensions.Configuration");
        if (conditionalServices.Any()) uniqueNamespaces.Add("System");

        var registrations = new StringBuilder();

        var deduplicatedConditionalServices = DeduplicateConditionalServices(conditionalServices);
        try
        {
            GenerateConditionalServiceRegistrations(deduplicatedConditionalServices, registrations, uniqueNamespaces,
                needsConfigParameter);
        }
        catch (Exception ex)
        {
            if (deduplicatedConditionalServices.Any())
                throw new InvalidOperationException(
                    $"Failed to generate conditional service registrations. Conditional services count: {deduplicatedConditionalServices.Count}. Error: {ex.Message}",
                    ex);
        }

        if (configOptions != null && configOptions.Any())
        {
            uniqueNamespaces.Add("Microsoft.Extensions.Configuration");
            foreach (var configOption in configOptions)
            {
                var optionsTypeName = RemoveNamespacesAndDots(configOption.OptionsType, uniqueNamespaces);
                registrations.AppendLine(
                    $"         services.Configure<{optionsTypeName}>(options => configuration.GetSection(\"{configOption.SectionName}\").Bind(options));");
            }
        }

        var backgroundServices = regularServices.Where(s => s.Lifetime == "BackgroundService").ToList();
        var nonBackgroundServices = regularServices.Where(s => s.Lifetime != "BackgroundService").ToList();

        var uniqueBackgroundServices = backgroundServices
            .GroupBy(s => s.ClassSymbol.ToDisplayString())
            .Select(g => g.First())
            .ToList();

        if (backgroundServices.Count > 1)
        {
            uniqueNamespaces.Add("System.Collections.Generic");
            uniqueNamespaces.Add("System.Linq");
        }

        var uniqueNonBackgroundServices = new Dictionary<string, ServiceRegistration>();
        var registrationCounts = new Dictionary<string, int>();
        foreach (var service in nonBackgroundServices)
        {
            var isConcreteRegistration =
                SymbolEqualityComparer.Default.Equals(service.ClassSymbol, service.InterfaceSymbol);
            var registrationType = isConcreteRegistration ? "concrete" : "interface";
            var registrationKey =
                $"{service.InterfaceSymbol.ToDisplayString()}|{service.ClassSymbol.ToDisplayString()}|{service.Lifetime}|{service.UseSharedInstance}|{registrationType}";
            if (!uniqueNonBackgroundServices.ContainsKey(registrationKey))
            {
                uniqueNonBackgroundServices[registrationKey] = service;
                var serviceKey = $"{service.ClassSymbol.Name}_registrations";
                registrationCounts[serviceKey] = registrationCounts.ContainsKey(serviceKey)
                    ? registrationCounts[serviceKey] + 1
                    : 1;
            }
        }

        foreach (var service in uniqueNonBackgroundServices.Values)
        {
            var code = GenerateServiceRegistrationCode(service, uniqueNamespaces, "         ",
                shouldUseSimplifiedNamingForConsistency);
            registrations.Append(code);
        }

        foreach (var service in uniqueBackgroundServices)
        {
            var code = GenerateServiceRegistrationCode(service, uniqueNamespaces, "         ",
                shouldUseSimplifiedNamingForConsistency);
            registrations.Append(code);
        }

        GenerateCollectionWrapperRegistrations(regularServices, registrations, uniqueNamespaces);

        if (registrations.ToString().Contains("IList<") || registrations.ToString().Contains("IReadOnlyList<") ||
            registrations.ToString().Contains(".ToList()"))
        {
            uniqueNamespaces.Add("System.Collections.Generic");
            uniqueNamespaces.Add("System.Linq");
        }

        var usings = new StringBuilder();
        foreach (var ns in uniqueNamespaces) usings.AppendLine($"using {ns};");

        var configParameter = needsConfigParameter ? ", IConfiguration configuration" : "";

        return $$"""
                 #nullable enable
                 namespace {{extNameSpace}};

                 {{usings.ToString().Trim()}}

                 public static class GeneratedServiceCollectionExtensions
                 {
                     public static IServiceCollection Add{{methodNamePrefix}}RegisteredServices(this IServiceCollection services{{configParameter}})
                     {
                          {{registrations.ToString().Trim()}}
                          return services;
                     }
                 }
                 """;
    }
}
