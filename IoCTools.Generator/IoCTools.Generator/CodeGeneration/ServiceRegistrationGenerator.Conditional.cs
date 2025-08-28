namespace IoCTools.Generator.CodeGeneration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Models;

using Utilities;

internal static partial class ServiceRegistrationGenerator
{
    private static List<ConditionalServiceRegistration> DeduplicateConditionalServices(
        List<ConditionalServiceRegistration> conditionalServices)
    {
        var dedup = new Dictionary<string, ConditionalServiceRegistration>();
        foreach (var service in conditionalServices)
        {
            var conditionKey = service.Condition.ToString().Trim();
            if (!string.IsNullOrEmpty(service.Condition.ConfigValue))
                conditionKey =
                    $"config:{service.Condition.ConfigValue!}:{service.Condition.EqualsValue?.Trim()}:{service.Condition.NotEquals?.Trim()}";
            else if (!string.IsNullOrEmpty(service.Condition.Environment))
                conditionKey = $"env:{service.Condition.Environment!.Trim()}";

            var key =
                $"{service.ClassSymbol.ToDisplayString()}|{service.InterfaceSymbol.ToDisplayString()}|{conditionKey}|{service.Lifetime}|{service.UseSharedInstance}|{service.HasConfigurationInjection}";
            if (!dedup.ContainsKey(key)) dedup[key] = service;
        }

        return dedup.Values.ToList();
    }

    private static void GenerateConditionalServiceRegistrations(
        List<ConditionalServiceRegistration> conditionalServices,
        StringBuilder registrations,
        HashSet<string> uniqueNamespaces,
        bool hasConfigurationParameter)
    {
        if (!conditionalServices.Any()) return;

        var finalDedup = new Dictionary<string, ConditionalServiceRegistration>();
        foreach (var service in conditionalServices)
        {
            var conditionCode = service.GenerateConditionCode(hasConfigurationParameter)?.Trim() ?? "";
            var finalKey =
                $"{service.ClassSymbol.ToDisplayString()}|{service.InterfaceSymbol.ToDisplayString()}|{conditionCode}|{service.Lifetime}";
            if (!finalDedup.ContainsKey(finalKey)) finalDedup[finalKey] = service;
        }

        var deduped = finalDedup.Values.ToList();

        var requiresEnvironment = deduped.Any(cs => cs.Condition.RequiresEnvironment);
        var requiresConfiguration = deduped.Any(cs => cs.Condition.RequiresConfiguration);
        if (requiresEnvironment)
        {
            var environmentCode = ConditionalServiceEvaluator.GetEnvironmentDetectionCode();
            registrations.AppendLine($"         {environmentCode}");
        }

        if (requiresConfiguration && !hasConfigurationParameter)
            throw new InvalidOperationException(
                "Conditional services require IConfiguration parameter in extension method.");
        if (requiresEnvironment) registrations.AppendLine();

        var byInterface =
            deduped.GroupBy(cs => cs.InterfaceSymbol?.ToDisplayString() ?? cs.ClassSymbol.ToDisplayString());
        foreach (var interfaceGroup in byInterface)
        {
            var servicesForInterface = interfaceGroup.ToList();
            var isMutuallyExclusive = AreMutuallyExclusiveServices(servicesForInterface);
            if (isMutuallyExclusive)
            {
                var first = true;
                foreach (var service in servicesForInterface)
                {
                    var conditionCode = service.GenerateConditionCode(hasConfigurationParameter);
                    var ifKeyword = first ? "if" : "else if";
                    registrations.AppendLine($"         {ifKeyword} ({conditionCode})");
                    registrations.AppendLine("         {");
                    registrations.Append(GenerateServiceRegistrationCode(service, uniqueNamespaces, "             "));
                    registrations.AppendLine("         }");
                    first = false;
                }
            }
            else
            {
                var uniqueServiceConditions = new Dictionary<string, ConditionalServiceRegistration>();
                foreach (var service in servicesForInterface)
                {
                    var conditionCode = service.GenerateConditionCode(hasConfigurationParameter);
                    var key =
                        $"{conditionCode}|{service.ClassSymbol.ToDisplayString()}|{service.InterfaceSymbol.ToDisplayString()}|{service.Lifetime}";
                    if (!uniqueServiceConditions.ContainsKey(key)) uniqueServiceConditions[key] = service;
                }

                var byCondition = uniqueServiceConditions.Values
                    .GroupBy(s => (s.GenerateConditionCode(hasConfigurationParameter) ?? "").Trim().Replace("  ", " "));
                foreach (var kvp in byCondition)
                {
                    var conditionCode = kvp.Key;
                    var servicesForCondition = kvp.ToList();
                    registrations.AppendLine($"         if ({conditionCode})");
                    registrations.AppendLine("         {");
                    foreach (var service in servicesForCondition)
                        registrations.Append(
                            GenerateServiceRegistrationCode(service, uniqueNamespaces, "             "));
                    registrations.AppendLine("         }");
                }
            }
        }
    }

    private static bool AreMutuallyExclusiveServices(List<ConditionalServiceRegistration> services)
    {
        if (services.Count <= 1) return false;
        var uniqueClasses = services.Select(s => s.ClassSymbol.ToDisplayString()).Distinct().Count();
        if (uniqueClasses <= 1) return false;

        var environmentServices = services.Where(s => !string.IsNullOrEmpty(s.Condition.Environment)).ToList();
        if (environmentServices.Count > 1)
        {
            var envs = environmentServices.Select(s => s.Condition.Environment).Distinct().ToList();
            if (envs.Count > 1) return true;
        }

        var configServices = services.Where(s => !string.IsNullOrEmpty(s.Condition.ConfigValue)).ToList();
        if (configServices.Count > 1)
        {
            var groups = configServices.GroupBy(s => s.Condition.ConfigValue);
            foreach (var g in groups)
            {
                var equalsValues = g.Where(s => !string.IsNullOrEmpty(s.Condition.EqualsValue))
                    .Select(s => s.Condition.EqualsValue).Distinct().ToList();
                if (equalsValues.Count > 1) return true;
            }
        }

        return false;
    }
}
