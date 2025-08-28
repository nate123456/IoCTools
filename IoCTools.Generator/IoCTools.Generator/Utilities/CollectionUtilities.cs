namespace IoCTools.Generator.Utilities;

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;

internal static class CollectionUtilities
{
    internal static bool IsCollectionTypeAdapted(string typeName) =>
        typeName.EndsWith("[]", StringComparison.Ordinal) ||
        typeName.StartsWith("System.Collections.Generic.IEnumerable<", StringComparison.Ordinal) ||
        typeName.StartsWith("System.Collections.Generic.IList<", StringComparison.Ordinal) ||
        typeName.StartsWith("System.Collections.Generic.List<", StringComparison.Ordinal) ||
        typeName.StartsWith("System.Collections.Generic.ICollection<", StringComparison.Ordinal) ||
        typeName.StartsWith("System.Collections.Generic.IReadOnlyList<", StringComparison.Ordinal) ||
        typeName.StartsWith("System.Collections.Generic.IReadOnlyCollection<", StringComparison.Ordinal) ||
        typeName.Contains("IEnumerable<") ||
        typeName.Contains("IList<") ||
        typeName.Contains("ICollection<") ||
        typeName.Contains("List<") ||
        typeName.StartsWith("IEnumerable<", StringComparison.Ordinal) ||
        typeName.StartsWith("IList<", StringComparison.Ordinal) ||
        typeName.StartsWith("ICollection<", StringComparison.Ordinal) ||
        typeName.StartsWith("List<", StringComparison.Ordinal);

    internal static string? ExtractInnerTypeFromIEnumerable(string enumerableTypeName)
    {
        const string prefix = "System.Collections.Generic.IEnumerable<";
        if (enumerableTypeName.StartsWith(prefix, StringComparison.Ordinal) &&
            enumerableTypeName.EndsWith(">", StringComparison.Ordinal))
        {
            var innerType = enumerableTypeName.Substring(prefix.Length, enumerableTypeName.Length - prefix.Length - 1);
            return innerType.Trim();
        }

        return null;
    }

    internal static TypeHelpers.EnumerableTypeInfo? ExtractIEnumerableFromGenericArguments(string typeName)
    {
        var genericStart = typeName.IndexOf('<');
        if (genericStart == -1) return null;
        var depth = 0;
        var start = genericStart + 1;
        var argumentStart = start;
        for (var i = start; i < typeName.Length; i++)
            if (typeName[i] == '<')
            {
                depth++;
            }
            else if (typeName[i] == '>')
            {
                if (depth == 0)
                {
                    var argument = typeName.Substring(argumentStart, i - argumentStart).Trim();
                    if (argument.StartsWith("System.Collections.Generic.IEnumerable<", StringComparison.Ordinal) &&
                        argument.EndsWith(">", StringComparison.Ordinal))
                    {
                        var innerType = ExtractInnerTypeFromIEnumerable(argument);
                        if (innerType != null) return new TypeHelpers.EnumerableTypeInfo(innerType, argument);
                    }

                    var nested = ExtractIEnumerableFromGenericArguments(argument);
                    if (nested != null) return nested;
                    break;
                }

                depth--;
            }
            else if (typeName[i] == ',' && depth == 0)
            {
                var argument = typeName.Substring(argumentStart, i - argumentStart).Trim();
                if (argument.StartsWith("System.Collections.Generic.IEnumerable<", StringComparison.Ordinal) &&
                    argument.EndsWith(">", StringComparison.Ordinal))
                {
                    var innerType = ExtractInnerTypeFromIEnumerable(argument);
                    if (innerType != null) return new TypeHelpers.EnumerableTypeInfo(innerType, argument);
                }

                var nested = ExtractIEnumerableFromGenericArguments(argument);
                if (nested != null) return nested;
                argumentStart = i + 1;
            }

        return null;
    }

    internal static TypeHelpers.EnumerableTypeInfo? ExtractIEnumerableFromWrappedType(string typeName)
    {
        if (typeName.StartsWith("System.Collections.Generic.IEnumerable<", StringComparison.Ordinal) &&
            typeName.EndsWith(">", StringComparison.Ordinal))
        {
            var innerType = ExtractInnerTypeFromIEnumerable(typeName);
            if (innerType != null) return new TypeHelpers.EnumerableTypeInfo(innerType, typeName);
        }

        return ExtractIEnumerableFromGenericArguments(typeName);
    }

    // Symbol-based helpers used by constructor/config-binding generation
    internal static bool IsCollectionInterfaceType(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType) return false;
        var typeName = namedType.OriginalDefinition.ToDisplayString();
        return typeName.StartsWith("System.Collections.Generic.IEnumerable<", StringComparison.Ordinal) ||
               typeName.StartsWith("System.Collections.Generic.IReadOnlyList<", StringComparison.Ordinal) ||
               typeName.StartsWith("System.Collections.Generic.IReadOnlyCollection<", StringComparison.Ordinal) ||
               typeName.StartsWith("System.Collections.Generic.IList<", StringComparison.Ordinal) ||
               typeName.StartsWith("System.Collections.Generic.ICollection<", StringComparison.Ordinal);
    }

    internal static bool IsCollectionType(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol) return true;
        if (type is not INamedTypeSymbol namedType) return false;
        var typeName = namedType.OriginalDefinition.ToDisplayString();
        return typeName.StartsWith("System.Collections.Generic.List<", StringComparison.Ordinal) ||
               typeName.StartsWith("System.Collections.Generic.IEnumerable<", StringComparison.Ordinal) ||
               typeName.StartsWith("System.Collections.Generic.IList<", StringComparison.Ordinal) ||
               typeName.StartsWith("System.Collections.Generic.ICollection<", StringComparison.Ordinal) ||
               typeName.StartsWith("System.Collections.Generic.Dictionary<", StringComparison.Ordinal) ||
               typeName.StartsWith("System.Collections.Generic.IDictionary<", StringComparison.Ordinal) ||
               typeName.StartsWith("System.Collections.Generic.IReadOnlyList<", StringComparison.Ordinal) ||
               typeName.StartsWith("System.Collections.Generic.IReadOnlyCollection<", StringComparison.Ordinal);
    }

    internal static (string concreteTypeName, string conversionMethod) GetConcreteCollectionBinding(
        ITypeSymbol fieldType,
        IEnumerable<string> namespacesForStripping)
    {
        if (fieldType is not INamedTypeSymbol namedType)
            return (TypeDisplayUtilities.WithoutNamespaces(fieldType, namespacesForStripping, true), "");

        var typeName = namedType.OriginalDefinition.ToDisplayString();

        if (typeName.StartsWith("System.Collections.Generic.IReadOnlyList<", StringComparison.Ordinal) ||
            typeName.StartsWith("System.Collections.Generic.IReadOnlyCollection<", StringComparison.Ordinal))
        {
            var elementType = namedType.TypeArguments.FirstOrDefault();
            if (elementType != null)
            {
                var elementTypeName = TypeDisplayUtilities.WithoutNamespaces(elementType, namespacesForStripping, true);
                return ($"List<{elementTypeName}>", "?.AsReadOnly()");
            }
        }
        else if (typeName.StartsWith("System.Collections.Generic.IEnumerable<", StringComparison.Ordinal))
        {
            return (TypeDisplayUtilities.WithoutNamespaces(fieldType, namespacesForStripping, true), "");
        }
        else if (typeName.StartsWith("System.Collections.Generic.IList<", StringComparison.Ordinal))
        {
            return (TypeDisplayUtilities.WithoutNamespaces(fieldType, namespacesForStripping, true), "");
        }
        else if (typeName.StartsWith("System.Collections.Generic.ICollection<", StringComparison.Ordinal))
        {
            var elementType = namedType.TypeArguments.FirstOrDefault();
            if (elementType != null)
            {
                var elementTypeName = TypeDisplayUtilities.WithoutNamespaces(elementType, namespacesForStripping, true);
                return ($"List<{elementTypeName}>", "");
            }
        }

        return (TypeDisplayUtilities.WithoutNamespaces(fieldType, namespacesForStripping, true), "");
    }
}
