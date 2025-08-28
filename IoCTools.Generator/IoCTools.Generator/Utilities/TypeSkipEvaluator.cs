namespace IoCTools.Generator.Utilities;

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;

internal static class TypeSkipEvaluator
{
    public static bool ShouldSkipRegistration(INamedTypeSymbol classSymbol,
        Compilation compilation,
        GeneratorStyleOptions options)
    {
        // Project-level exceptions always allow registration
        var fullName = classSymbol.ToDisplayString();
        if (options.SkipAssignableExceptions.Contains(fullName) ||
            options.SkipAssignableExceptionPatterns.Any(p => Glob.IsMatch(fullName, p)))
            return false;

        if (options.SkipAssignableTypes.Count == 0 && options.SkipAssignableTypePatterns.Count == 0)
            return false;

        foreach (var metadataName in options.SkipAssignableTypes)
        {
            // Try exact metadata resolution first
            var target = compilation.GetTypeByMetadataName(metadataName);
            if (target != null)
            {
                if (IsAssignableTo(classSymbol, target))
                    return true;

                // If target is an open generic interface, check constructedFrom equality by name
                if (target.TypeKind == TypeKind.Interface && target.IsGenericType)
                    if (classSymbol.AllInterfaces.Any(i =>
                            SymbolEqualityComparer.Default.Equals(i.ConstructedFrom, target)))
                        return true;
            }
            else
            {
                // Fallback by name match (suffix or unbound generic arity notation)
                if (IsAssignableByName(classSymbol, metadataName))
                    return true;
            }
        }

        // Pattern-based assignable checks by name across inheritance and interfaces
        if (options.SkipAssignableTypePatterns.Count > 0)
        {
            // Build all candidate names to test patterns against
            foreach (var current in InheritanceChain(classSymbol))
            {
                var n = current.ToDisplayString();
                if (options.SkipAssignableTypePatterns.Any(p => Glob.IsMatch(n, p))) return true;
                if (current.IsGenericType)
                {
                    var open = current.ConstructedFrom.ToDisplayString();
                    if (options.SkipAssignableTypePatterns.Any(p => Glob.IsMatch(open, p))) return true;
                }
            }

            foreach (var iface in classSymbol.AllInterfaces)
            {
                var n = iface.ToDisplayString();
                if (options.SkipAssignableTypePatterns.Any(p => Glob.IsMatch(n, p))) return true;
                if (iface.IsGenericType)
                {
                    var open = iface.ConstructedFrom.ToDisplayString();
                    if (options.SkipAssignableTypePatterns.Any(p => Glob.IsMatch(open, p))) return true;
                }
            }
        }

        return false;
    }

    private static bool IsAssignableTo(INamedTypeSymbol type,
        INamedTypeSymbol target)
    {
        // Classes: walk base types
        var current = type;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, target))
                return true;
            if (SymbolEqualityComparer.Default.Equals(current.ConstructedFrom, target))
                return true;
            current = current.BaseType;
        }

        // Interfaces: check implemented interfaces
        foreach (var iface in type.AllInterfaces)
        {
            if (SymbolEqualityComparer.Default.Equals(iface, target))
                return true;
            if (SymbolEqualityComparer.Default.Equals(iface.ConstructedFrom, target))
                return true;
        }

        return false;
    }

    private static bool IsAssignableByName(INamedTypeSymbol type,
        string metadataName)
    {
        // Handle simple names and generic arity notations (e.g., Mediator.IRequestHandler`2)
        // Base type chain
        for (var current = type; current != null; current = current.BaseType)
            if (NameMatches(current, metadataName))
                return true;

        // Interfaces
        if (type.AllInterfaces.Any(i => NameMatches(i, metadataName)))
            return true;

        return false;
    }

    private static bool NameMatches(INamedTypeSymbol symbol,
        string metadataName)
    {
        var full = symbol.ToDisplayString();
        if (full.Equals(metadataName, StringComparison.Ordinal)) return true;
        if (full.EndsWith("." + metadataName, StringComparison.Ordinal)) return true;

        if (symbol.IsGenericType)
        {
            var constructedFromName = symbol.ConstructedFrom.ToDisplayString();
            if (constructedFromName.Equals(metadataName, StringComparison.Ordinal)) return true;
            if (constructedFromName.EndsWith("." + metadataName, StringComparison.Ordinal)) return true;

            // Support arity backtick suffix matching
            var nameWithArity = symbol.ConstructedFrom.Name + "`" + symbol.TypeArguments.Length;
            if (metadataName.Equals(nameWithArity, StringComparison.Ordinal)) return true;

            // Allow matching when only simple name with arity provided (e.g., IRequestHandler`2)
            if (metadataName.Contains('`'))
            {
                var simple = metadataName.Split('.').Last();
                if (nameWithArity.Equals(simple, StringComparison.Ordinal)) return true;
            }
        }

        return false;
    }

    private static IEnumerable<INamedTypeSymbol> InheritanceChain(INamedTypeSymbol type)
    {
        for (var current = type; current != null; current = current.BaseType)
            yield return current;
    }
}
