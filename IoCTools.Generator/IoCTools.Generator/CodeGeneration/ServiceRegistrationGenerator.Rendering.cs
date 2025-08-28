namespace IoCTools.Generator.CodeGeneration;

using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;

using Models;

using Utilities;

internal static partial class ServiceRegistrationGenerator
{
    private static string GenerateServiceRegistrationCode(ServiceRegistration service,
        HashSet<string> uniqueNamespaces,
        string indentation,
        bool shouldUseSimplifiedNamingForConsistency = false)
    {
        var registrationCode = new StringBuilder();

        var isConditionalService = service is ConditionalServiceRegistration;
        var hasConfigInjection = service.HasConfigurationInjection;

        var useGlobalQualifiedNames =
            (!isConditionalService || hasConfigInjection) && !shouldUseSimplifiedNamingForConsistency;

        var interfaceType = useGlobalQualifiedNames
            ? TypeNameSimplifier.SimplifySystemTypesForServiceRegistration(
                service.InterfaceSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            : TypeNameSimplifier.SimplifyTypesForConditionalServices(
                service.InterfaceSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
        var classType = useGlobalQualifiedNames
            ? TypeNameSimplifier.SimplifySystemTypesForServiceRegistration(
                service.ClassSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            : TypeNameSimplifier.SimplifyTypesForConditionalServices(
                service.ClassSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

        var interfaceTypeForRegistration = ConvertToOpenGeneric(interfaceType, service.InterfaceSymbol);
        var classTypeForRegistration = ConvertToOpenGeneric(classType, service.ClassSymbol);

        var lifetime = service.Lifetime;

        if (lifetime == "BackgroundService")
        {
            var backgroundServiceType = isConditionalService
                ? TypeNameSimplifier.SimplifyTypesForConditionalServices(
                    service.ClassSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                : TypeNameSimplifier.SimplifySystemTypesForServiceRegistration(
                    service.ClassSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

            registrationCode.AppendLine($"{indentation}services.AddHostedService<{backgroundServiceType}>();");
            return registrationCode.ToString();
        }

        var needsFactoryPattern =
            service.UseSharedInstance || (service.HasConfigurationInjection && !isConditionalService);

        if (needsFactoryPattern)
        {
            if (service.ClassSymbol.TypeParameters.Length > 0)
            {
                if (SymbolEqualityComparer.Default.Equals(service.ClassSymbol, service.InterfaceSymbol))
                    registrationCode.AppendLine(
                        $"{indentation}services.Add{lifetime}(typeof({classTypeForRegistration}));");
                else
                    registrationCode.AppendLine(
                        $"{indentation}services.Add{lifetime}(typeof({interfaceTypeForRegistration}), provider => provider.GetRequiredService(typeof({classTypeForRegistration})));");
            }
            else
            {
                if (SymbolEqualityComparer.Default.Equals(service.ClassSymbol, service.InterfaceSymbol))
                {
                    if (service.UseSharedInstance)
                        registrationCode.AppendLine($"{indentation}services.Add{lifetime}<{classType}>();");
                    else
                        registrationCode.AppendLine(
                            $"{indentation}services.Add{lifetime}<{classType}, {classType}>();");
                }
                else
                {
                    registrationCode.AppendLine(
                        $"{indentation}services.Add{lifetime}<{interfaceType}>(provider => provider.GetRequiredService<{classType}>());");
                }
            }
        }
        else
        {
            if (service.ClassSymbol.TypeParameters.Length > 0)
            {
                registrationCode.AppendLine(
                    $"{indentation}services.Add{lifetime}(typeof({interfaceTypeForRegistration}), typeof({classTypeForRegistration}));");
            }
            else
            {
                if (SymbolEqualityComparer.Default.Equals(service.ClassSymbol, service.InterfaceSymbol))
                    registrationCode.AppendLine($"{indentation}services.Add{lifetime}<{classType}, {classType}>();");
                else
                    registrationCode.AppendLine(
                        $"{indentation}services.Add{lifetime}<{interfaceType}, {classType}>();");
            }
        }

        return registrationCode.ToString();
    }

    private static string ConvertToOpenGeneric(string typeName,
        INamedTypeSymbol typeSymbol)
    {
        if (typeSymbol.TypeParameters.Length == 0)
            return typeName;

        var openBracketIndex = typeName.IndexOf('<');
        if (openBracketIndex >= 0)
        {
            var baseTypeName = typeName.Substring(0, openBracketIndex);
            var commaCount = typeSymbol.TypeParameters.Length - 1;
            var commas = commaCount > 0 ? new string(',', commaCount) : "";
            return $"{baseTypeName}<{commas}>";
        }

        return typeName;
    }

    private static string RemoveNamespacesAndDots(ISymbol serviceType,
        IEnumerable<string> uniqueNamespaces,
        bool forServiceRegistration = false) =>
        TypeDisplayUtilities.WithoutNamespaces(serviceType, uniqueNamespaces, forServiceRegistration);

    private static void GenerateCollectionWrapperRegistrations(
        List<ServiceRegistration> regularServices,
        StringBuilder registrations,
        HashSet<string> uniqueNamespaces)
    {
        uniqueNamespaces.Add("System.Collections.Generic");
        uniqueNamespaces.Add("System.Linq");

        var interfaceGroups = regularServices
            .Where(s => !SymbolEqualityComparer.Default.Equals(s.ClassSymbol, s.InterfaceSymbol))
            .GroupBy(s => s.InterfaceSymbol, SymbolEqualityComparer.Default)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var interfaceGroup in interfaceGroups)
        {
            var interfaceSymbol = interfaceGroup.First().InterfaceSymbol;
            var interfaceType = TypeNameSimplifier.SimplifySystemTypesForServiceRegistration(
                interfaceSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

            registrations.AppendLine(
                $"         services.AddTransient<IList<{interfaceType}>>(provider => provider.GetServices<{interfaceType}>().ToList());");
            registrations.AppendLine(
                $"         services.AddTransient<IReadOnlyList<{interfaceType}>>(provider => provider.GetServices<{interfaceType}>().ToList());");
        }
    }
}
