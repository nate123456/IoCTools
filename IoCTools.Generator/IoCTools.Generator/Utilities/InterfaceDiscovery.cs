namespace IoCTools.Generator.Utilities;

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;

internal static class InterfaceDiscovery
{
    internal static List<INamedTypeSymbol> GetAllInterfacesForService(INamedTypeSymbol classSymbol)
    {
        try
        {
            var allInterfaces = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            CollectAllInterfacesRecursive(classSymbol, allInterfaces);

            return allInterfaces
                .Where(i => i?.ContainingNamespace?.ToDisplayString()?.StartsWith("System", StringComparison.Ordinal) !=
                            true)
                .ToList();
        }
        catch
        {
            return new List<INamedTypeSymbol>();
        }
    }

    internal static void CollectAllInterfacesRecursive(INamedTypeSymbol typeSymbol,
        HashSet<INamedTypeSymbol> allInterfaces)
    {
        try
        {
            if (typeSymbol == null) return;

            foreach (var interfaceSymbol in typeSymbol.Interfaces)
                if (interfaceSymbol != null && allInterfaces.Add(interfaceSymbol))
                    CollectAllInterfacesRecursive(interfaceSymbol, allInterfaces);

            var baseType = typeSymbol.BaseType;
            if (baseType != null && baseType.SpecialType != SpecialType.System_Object)
                CollectAllInterfacesRecursive(baseType, allInterfaces);

            foreach (var interfaceSymbol in typeSymbol.AllInterfaces)
                if (interfaceSymbol != null)
                    allInterfaces.Add(interfaceSymbol);
        }
        catch
        {
            // Swallow to keep generator resilient; worst case we miss an interface registration.
        }
    }
}
