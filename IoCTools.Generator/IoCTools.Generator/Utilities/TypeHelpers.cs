namespace IoCTools.Generator.Utilities;

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;

internal static class TypeHelpers
{
    internal static bool IsFrameworkTypeAdapted(string typeName) => FrameworkTypeUtilities.IsFrameworkType(typeName);

    internal static bool IsCollectionTypeAdapted(string typeName) =>
        CollectionUtilities.IsCollectionTypeAdapted(typeName);

    internal static string? ExtractServiceNameFromType(string dependencyType) =>
        TypeNameUtilities.ExtractServiceNameFromType(dependencyType);

    internal static string FormatTypeNameForDiagnostic(ITypeSymbol typeSymbol) =>
        TypeNameUtilities.FormatTypeNameForDiagnostic(typeSymbol);

    internal static string? ExtractInnerTypeFromIEnumerable(string enumerableTypeName) =>
        CollectionUtilities.ExtractInnerTypeFromIEnumerable(enumerableTypeName);

    internal static EnumerableTypeInfo? ExtractIEnumerableFromGenericArguments(string typeName) =>
        CollectionUtilities.ExtractIEnumerableFromGenericArguments(typeName);

    internal static EnumerableTypeInfo? ExtractIEnumerableFromWrappedType(string typeName) =>
        CollectionUtilities.ExtractIEnumerableFromWrappedType(typeName);

    internal static bool IsConstructedGenericTypeSimple(string typeName) =>
        typeName.Contains('<') && typeName.Contains('>') && !typeName.EndsWith("<>", StringComparison.Ordinal);

    internal static string ExtractBaseTypeNameFromConstructed(string constructedType)
    {
        var angleIndex = constructedType.IndexOf('<');
        return angleIndex >= 0 ? constructedType.Substring(0, angleIndex) : constructedType;
    }

    internal static string? ExtractBaseGenericInterface(string constructedType)
    {
        if (!constructedType.Contains('<') || !constructedType.Contains('>')) return null;
        return ConvertToOpenGenericForm(constructedType);
    }

    internal static string ConvertToOpenGenericForm(string constructedType)
    {
        if (!constructedType.Contains('<') || !constructedType.Contains('>')) return constructedType;
        var angleStart = constructedType.IndexOf('<');
        var angleEnd = constructedType.LastIndexOf('>');
        if (angleStart >= 0 && angleEnd > angleStart)
        {
            var baseName = constructedType.Substring(0, angleStart);
            var typeArgsString = constructedType.Substring(angleStart + 1, angleEnd - angleStart - 1);
            var convertedTypeArgs = ConvertTypeArgumentsToOpenForm(typeArgsString);
            return $"{baseName}<{string.Join(", ", convertedTypeArgs)}>";
        }

        return constructedType;
    }

    internal static List<string> ConvertTypeArgumentsToOpenForm(string typeArgsString)
    {
        var result = new List<string>();
        var typeArgs = SplitTopLevelTypeArguments(typeArgsString);
        var simpleTypeCounter = 1;
        foreach (var typeArg in typeArgs)
        {
            var trimmedArg = typeArg.Trim();
            if (trimmedArg.Contains('<') && trimmedArg.Contains('>'))
            {
                result.Add(ConvertToOpenGenericForm(trimmedArg));
            }
            else
            {
                var paramName = simpleTypeCounter == 1 ? "T" : $"T{simpleTypeCounter}";
                result.Add(paramName);
                simpleTypeCounter++;
            }
        }

        return result;
    }

    internal static List<string> SplitTopLevelTypeArguments(string typeArgsString)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(typeArgsString)) return result;
        var depth = 0;
        var lastStart = 0;
        for (var i = 0; i < typeArgsString.Length; i++)
        {
            var c = typeArgsString[i];
            if (c == '<')
            {
                depth++;
            }
            else if (c == '>')
            {
                depth--;
            }
            else if (c == ',' && depth == 0)
            {
                result.Add(typeArgsString.Substring(lastStart, i - lastStart));
                lastStart = i + 1;
            }
        }

        if (lastStart < typeArgsString.Length) result.Add(typeArgsString.Substring(lastStart));
        return result;
    }

    internal static int CountTypeParameters(string constructedType)
    {
        var start = constructedType.IndexOf('<');
        var end = constructedType.LastIndexOf('>');
        if (start >= 0 && end > start)
        {
            var typeParams = constructedType.Substring(start + 1, end - start - 1);
            return CountTopLevelTypeParameters(typeParams);
        }

        return 0;
    }

    internal static int CountTopLevelTypeParameters(string typeParamsString)
    {
        if (string.IsNullOrWhiteSpace(typeParamsString)) return 0;
        var count = 0;
        var depth = 0;
        var lastStart = 0;
        for (var i = 0; i < typeParamsString.Length; i++)
        {
            var c = typeParamsString[i];
            if (c == '<')
            {
                depth++;
            }
            else if (c == '>')
            {
                depth--;
            }
            else if (c == ',' && depth == 0)
            {
                count++;
                lastStart = i + 1;
            }
        }

        if (lastStart < typeParamsString.Length && typeParamsString.Substring(lastStart).Trim().Length > 0) count++;
        return count;
    }

    internal static bool IsMatchingOpenGeneric(string baseName,
        int typeParamCount,
        string registeredService)
    {
        if (!registeredService.StartsWith(baseName + "<", StringComparison.Ordinal)) return false;
        var registeredTypeParamCount = CountTypeParameters(registeredService);
        return registeredTypeParamCount == typeParamCount;
    }

    internal static bool IsMatchingGenericInterface(string constructedType,
        string implementedInterfaceType)
    {
        if (!constructedType.Contains('<') || !implementedInterfaceType.Contains('<')) return false;
        var constructedBaseName = ExtractBaseTypeNameFromConstructed(constructedType);
        var constructedParamCount = CountTypeParameters(constructedType);
        var implementedBaseName = ExtractBaseTypeNameFromConstructed(implementedInterfaceType);
        var implementedParamCount = CountTypeParameters(implementedInterfaceType);
        return constructedBaseName == implementedBaseName && constructedParamCount == implementedParamCount;
    }

    internal static string ExtractSimpleTypeNameFromFullName(string fullTypeName) =>
        TypeNameUtilities.ExtractSimpleTypeNameFromFullName(fullTypeName);

    internal class EnumerableTypeInfo
    {
        public EnumerableTypeInfo(string innerType,
            string fullEnumerableType)
        {
            InnerType = innerType;
            FullEnumerableType = fullEnumerableType;
        }

        public string InnerType { get; }
        public string FullEnumerableType { get; }
    }
}
