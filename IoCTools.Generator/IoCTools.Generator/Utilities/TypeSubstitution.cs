namespace IoCTools.Generator.Utilities;

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;

internal static class TypeSubstitution
{
    public static ITypeSymbol SubstituteTypeParameters(ITypeSymbol type,
        INamedTypeSymbol constructedType)
    {
        // If this is not a constructed generic type, return the type as-is
        // IMPORTANT: Preserve the original type including its nullable annotation
        if (constructedType.TypeArguments.IsEmpty ||
            constructedType.OriginalDefinition.Equals(constructedType, SymbolEqualityComparer.Default))
            return type; // Return the original type unchanged, preserving nullable annotations

        // Build a mapping from type parameters to type arguments
        var typeParameterMap = new Dictionary<ITypeParameterSymbol, ITypeSymbol>(SymbolEqualityComparer.Default);
        var originalDefinition = constructedType.OriginalDefinition;

        for (var i = 0; i < originalDefinition.TypeParameters.Length && i < constructedType.TypeArguments.Length; i++)
            typeParameterMap[originalDefinition.TypeParameters[i]] = constructedType.TypeArguments[i];

        return SubstituteTypeParametersRecursive(type, typeParameterMap);
    }

    public static ITypeSymbol ApplyInheritanceChainSubstitution(ITypeSymbol fieldType,
        INamedTypeSymbol sourceType,
        INamedTypeSymbol targetType)
    {
        if (sourceType.Equals(targetType, SymbolEqualityComparer.Default))
            // No substitution needed if source and target are the same
            return fieldType;

        // Build the substitution mapping by walking the inheritance chain
        var substitutionMap = BuildInheritanceChainSubstitutionMap(sourceType, targetType);

        // Apply the substitution
        return ApplyTypeSubstitution(fieldType, substitutionMap);
    }

    private static ITypeSymbol SubstituteTypeParametersRecursive(ITypeSymbol type,
        Dictionary<ITypeParameterSymbol, ITypeSymbol> typeParameterMap)
    {
        // If this is a type parameter, substitute it
        if (type is ITypeParameterSymbol typeParam && typeParameterMap.TryGetValue(typeParam, out var substitution))
            return substitution;

        // If this is a generic type, recursively substitute its type arguments
        if (type is INamedTypeSymbol namedType && namedType.TypeArguments.Length > 0)
        {
            var substitutedArguments = namedType.TypeArguments
                .Select(arg => SubstituteTypeParametersRecursive(arg, typeParameterMap))
                .ToArray();

            // Construct the substituted generic type and preserve nullable annotation
            var constructedType = namedType.OriginalDefinition.Construct(substitutedArguments);

            // CRITICAL FIX: Preserve nullable annotation from original type
            // The Construct method loses nullable annotations, so we need to restore them
            if (namedType.NullableAnnotation == NullableAnnotation.Annotated)
                return constructedType.WithNullableAnnotation(NullableAnnotation.Annotated);

            return constructedType;
        }

        // For array types, substitute the element type
        if (type is IArrayTypeSymbol arrayType)
        {
            var substitutedElementType = SubstituteTypeParametersRecursive(arrayType.ElementType, typeParameterMap);
            // Note: We can't easily construct array types in Roslyn, so we'll use a workaround
            // For now, return the original array type - this is a limitation we'll address if needed
            return type;
        }

        // For other types, return as-is
        return type;
    }

    private static Dictionary<ITypeParameterSymbol, ITypeSymbol> BuildInheritanceChainSubstitutionMap(
        INamedTypeSymbol sourceType,
        INamedTypeSymbol targetType)
    {
        var substitutionMap = new Dictionary<ITypeParameterSymbol, ITypeSymbol>(SymbolEqualityComparer.Default);

        // FIXED: Simplified approach using direct type parameter resolution
        // If types are the same, no substitution needed
        if (SymbolEqualityComparer.Default.Equals(sourceType, targetType))
            return substitutionMap;

        // Find the specific instantiation of sourceType in targetType's inheritance chain
        var current = targetType;
        while (current != null && current.SpecialType != SpecialType.System_Object)
        {
            // Check if this type or its base type matches sourceType
            if (current.BaseType != null &&
                SymbolEqualityComparer.Default.Equals(current.BaseType.OriginalDefinition,
                    sourceType.OriginalDefinition))
            {
                // Found the instantiation! Map sourceType's type parameters to the concrete types
                var concreteBaseType = current.BaseType;
                for (var i = 0;
                     i < Math.Min(sourceType.TypeParameters.Length, concreteBaseType.TypeArguments.Length);
                     i++)
                {
                    var sourceTypeParam = sourceType.TypeParameters[i];
                    var concreteTypeArg = concreteBaseType.TypeArguments[i];

                    // If the concrete type argument is itself a type parameter, 
                    // we need to resolve it in the context of targetType
                    if (concreteTypeArg is ITypeParameterSymbol concreteTypeParam)
                    {
                        // Find what this type parameter resolves to in targetType
                        var resolvedType = ResolveTypeParameterInContext(concreteTypeParam, current, targetType);
                        substitutionMap[sourceTypeParam] = resolvedType ?? concreteTypeArg;
                    }
                    else
                    {
                        substitutionMap[sourceTypeParam] = concreteTypeArg;
                    }
                }

                break;
            }

            current = current.BaseType;
        }

        return substitutionMap;
    }

    private static ITypeSymbol? ResolveTypeParameterInContext(ITypeParameterSymbol typeParam,
        INamedTypeSymbol contextType,
        INamedTypeSymbol targetType)
    {
        // Simple case: if the type parameter belongs to contextType, look up its concrete type in targetType
        if (SymbolEqualityComparer.Default.Equals(typeParam.ContainingType, contextType.OriginalDefinition))
        {
            var paramIndex = contextType.OriginalDefinition.TypeParameters.ToList().IndexOf(typeParam);
            if (paramIndex >= 0 && paramIndex < contextType.TypeArguments.Length)
                return contextType.TypeArguments[paramIndex];
        }

        return null; // Can't resolve, return original
    }

    private static ITypeSymbol ApplyTypeSubstitution(ITypeSymbol type,
        Dictionary<ITypeParameterSymbol, ITypeSymbol> substitutionMap)
    {
        // If this is a type parameter, substitute it
        if (type is ITypeParameterSymbol typeParam && substitutionMap.TryGetValue(typeParam, out var substitution))
            return substitution;

        // If this is a generic type, recursively substitute its type arguments
        if (type is INamedTypeSymbol namedType && namedType.TypeArguments.Length > 0)
        {
            var substitutedArguments = namedType.TypeArguments
                .Select(arg => ApplyTypeSubstitution(arg, substitutionMap))
                .ToArray();

            // Construct the substituted generic type and preserve nullable annotation
            var constructedType = namedType.OriginalDefinition.Construct(substitutedArguments);

            // Preserve nullable annotation from original type
            if (namedType.NullableAnnotation == NullableAnnotation.Annotated)
                return constructedType.WithNullableAnnotation(NullableAnnotation.Annotated);

            return constructedType;
        }

        // For array types, substitute the element type
        if (type is IArrayTypeSymbol arrayType)
        {
            var substitutedElementType = ApplyTypeSubstitution(arrayType.ElementType, substitutionMap);
            // Return the original array type for now - array construction is complex in Roslyn
            return type;
        }

        // For other types, return as-is
        return type;
    }
}
