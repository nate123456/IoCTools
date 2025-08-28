namespace IoCTools.Generator.Utilities;

using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;

internal static class DependencyLifetimeResolver
{
    internal static (string? lifetime, string? implementationName)
        GetDependencyLifetimeWithGenericSupportAndImplementationName(
            string dependencyTypeName,
            Dictionary<string, string> serviceLifetimes,
            HashSet<string> allRegisteredServices,
            Dictionary<string, List<INamedTypeSymbol>>? allImplementations = null)
    {
        if (serviceLifetimes.TryGetValue(dependencyTypeName, out var lifetime)) return (lifetime, null);

        if (allImplementations != null)
            foreach (var kvp in allImplementations)
            foreach (var implementation in kvp.Value)
            {
                var implTypeName = implementation.ToDisplayString();
                if (implTypeName == dependencyTypeName)
                {
                    var implLifetime = LifetimeUtilities.GetServiceLifetimeFromSymbol(implementation);
                    if (implLifetime != null) return (implLifetime, implementation.Name);
                }
            }

        if (TypeHelpers.IsConstructedGenericTypeSimple(dependencyTypeName))
        {
            var baseName = TypeHelpers.ExtractBaseTypeNameFromConstructed(dependencyTypeName);
            var typeParamCount = TypeHelpers.CountTypeParameters(dependencyTypeName);
            foreach (var registeredService in allRegisteredServices)
                if (TypeHelpers.IsMatchingOpenGeneric(baseName, typeParamCount, registeredService))
                    if (serviceLifetimes.TryGetValue(registeredService, out var openLifetime))
                        return (openLifetime, null);

            if (allImplementations != null)
            {
                foreach (var kvp in allImplementations)
                {
                    var interfaceKey = kvp.Key;
                    var implementations = kvp.Value;
                    if (TypeHelpers.IsMatchingGenericInterface(dependencyTypeName, interfaceKey))
                        foreach (var impl in implementations)
                        {
                            var implTypeName = impl.ToDisplayString();
                            if (serviceLifetimes.TryGetValue(implTypeName, out var implLifetime))
                                return (implLifetime, TypeHelpers.FormatTypeNameForDiagnostic(impl));
                            var symbolLifetime = LifetimeUtilities.GetServiceLifetimeFromSymbol(impl);
                            if (symbolLifetime != null)
                                return (symbolLifetime, TypeHelpers.FormatTypeNameForDiagnostic(impl));
                        }
                }

                foreach (var kvp in allImplementations)
                {
                    var implementations = kvp.Value;
                    foreach (var impl in implementations)
                    foreach (var implementedInterface in impl.AllInterfaces)
                    {
                        var implementedInterfaceName = implementedInterface.ToDisplayString();
                        if (TypeHelpers.IsMatchingGenericInterface(dependencyTypeName, implementedInterfaceName))
                        {
                            var implTypeName = impl.ToDisplayString();
                            if (serviceLifetimes.TryGetValue(implTypeName, out var implLifetime))
                                return (implLifetime, TypeHelpers.FormatTypeNameForDiagnostic(impl));
                            var symbolLifetime = LifetimeUtilities.GetServiceLifetimeFromSymbol(impl);
                            if (symbolLifetime != null)
                                return (symbolLifetime, TypeHelpers.FormatTypeNameForDiagnostic(impl));
                        }
                    }
                }

                foreach (var kvp in allImplementations)
                {
                    var interfaceName = kvp.Key;
                    var implementations = kvp.Value;
                    if (TypeHelpers.IsMatchingOpenGeneric(baseName, typeParamCount, interfaceName))
                        foreach (var impl in implementations)
                        {
                            var implTypeName = impl.ToDisplayString();
                            if (serviceLifetimes.TryGetValue(implTypeName, out var implLifetime))
                                return (implLifetime, TypeHelpers.FormatTypeNameForDiagnostic(impl));
                            var symbolLifetime = LifetimeUtilities.GetServiceLifetimeFromSymbol(impl);
                            if (symbolLifetime != null)
                                return (symbolLifetime, TypeHelpers.FormatTypeNameForDiagnostic(impl));
                        }
                }
            }
        }

        return (null, null);
    }

    internal static string? GetDependencyLifetimeForSourceProduction(ITypeSymbol dependencyType,
        Dictionary<string, string> serviceLifetimes,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations)
    {
        var dependencyTypeName = dependencyType.ToDisplayString();
        if (serviceLifetimes.TryGetValue(dependencyTypeName, out var lifetime)) return lifetime;
        if (allImplementations.TryGetValue(dependencyTypeName, out var implementations) && implementations.Any())
        {
            var implementation = implementations.First();
            var implementationLifetime = LifetimeUtilities.GetServiceLifetimeFromSymbol(implementation);
            if (implementationLifetime != null) return implementationLifetime;
        }

        if (dependencyType is INamedTypeSymbol namedType && namedType.IsGenericType && !namedType.IsUnboundGenericType)
        {
            var openGenericType = namedType.ConstructedFrom.ToDisplayString();
            if (serviceLifetimes.TryGetValue(openGenericType, out var openGenericLifetime)) return openGenericLifetime;
            var namespaceName = namedType.ContainingNamespace?.ToDisplayString();
            var genericTypeName = namedType.Name;
            var typeParameterCount = namedType.TypeArguments.Length;
            foreach (var kvp in serviceLifetimes)
            {
                var serviceType = kvp.Key;
                var serviceLifetime = kvp.Value;
                if (serviceType.Contains(genericTypeName + "<") &&
                    (namespaceName == null || serviceType.StartsWith(namespaceName + ".")))
                {
                    var angleStart = serviceType.IndexOf('<');
                    var angleEnd = serviceType.LastIndexOf('>');
                    if (angleStart >= 0 && angleEnd > angleStart)
                    {
                        var typeParamSection = serviceType.Substring(angleStart + 1, angleEnd - angleStart - 1);
                        var paramCount = typeParamSection.Split(',').Length;
                        if (paramCount == typeParameterCount) return serviceLifetime;
                    }
                }
            }

            foreach (var kvp in serviceLifetimes)
            {
                var serviceType = kvp.Key;
                var serviceLifetime = kvp.Value;
                if (serviceType.StartsWith(genericTypeName + "<") || serviceType.Contains("." + genericTypeName + "<"))
                    return serviceLifetime;
            }
        }

        return null;
    }

    internal static string? FindImplementationNameForInterface(string interfaceTypeName,
        HashSet<string> allRegisteredServices)
    {
        var interfaceBaseName = TypeHelpers.ExtractSimpleTypeNameFromFullName(interfaceTypeName);
        foreach (var registeredService in allRegisteredServices)
        {
            var serviceBaseName = TypeHelpers.ExtractSimpleTypeNameFromFullName(registeredService);
            if (interfaceBaseName.StartsWith("I") && interfaceBaseName.Length > 1 &&
                serviceBaseName.EndsWith("Service") && serviceBaseName.Contains(interfaceBaseName.Substring(1)))
                return serviceBaseName;
            if (interfaceBaseName.StartsWith("I") && serviceBaseName.EndsWith("Service"))
            {
                var interfaceRoot = interfaceBaseName.Substring(1);
                if (serviceBaseName.Contains(interfaceRoot)) return serviceBaseName;
            }
        }

        return null;
    }
}
