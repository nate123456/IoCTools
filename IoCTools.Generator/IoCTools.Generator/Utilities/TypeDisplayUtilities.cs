namespace IoCTools.Generator.Utilities;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;

internal static class TypeDisplayUtilities
{
    // Mirrors ConstructorGenerator.Namespaces.RemoveNamespacesAndDots behavior
    public static string WithoutNamespaces(ITypeSymbol typeSymbol,
        IEnumerable<string> namespacesToStrip,
        bool includeNullable = true)
    {
        if (typeSymbol == null) return "object";

        if (typeSymbol is IArrayTypeSymbol arrayType)
        {
            var elementTypeName = WithoutNamespaces(arrayType.ElementType, namespacesToStrip, includeNullable);
            var ranks = new string(',', arrayType.Rank - 1);
            return $"{elementTypeName}[{ranks}]";
        }

        var misc = includeNullable
            ? SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
              SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            : SymbolDisplayMiscellaneousOptions.UseSpecialTypes;

        var format = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            delegateStyle: SymbolDisplayDelegateStyle.NameOnly,
            miscellaneousOptions: misc);

        var fullTypeName = typeSymbol.ToDisplayString(format);
        var sortedNamespaces = (namespacesToStrip ?? Array.Empty<string>()).Where(ns => !string.IsNullOrEmpty(ns))
            .OrderByDescending(ns => ns.Length);

        foreach (var ns in sortedNamespaces)
        {
            if (fullTypeName.StartsWith($"{ns}.", StringComparison.Ordinal))
                fullTypeName = fullTypeName.Substring(ns.Length + 1);
            fullTypeName = fullTypeName.Replace($"{ns}.", "");
        }

        return fullTypeName;
    }

    // Mirrors ServiceRegistrationGenerator.Rendering.RemoveNamespacesAndDots behavior
    public static string WithoutNamespaces(ISymbol serviceType,
        IEnumerable<string> uniqueNamespaces,
        bool forServiceRegistration)
    {
        if (serviceType == null) throw new ArgumentNullException(nameof(serviceType));

        if (serviceType is IArrayTypeSymbol arrayType)
        {
            var elementTypeName = WithoutNamespaces(arrayType.ElementType, uniqueNamespaces, forServiceRegistration);
            var ranks = new string(',', arrayType.Rank - 1);
            return $"{elementTypeName}[{ranks}]";
        }

        var qualificationStyle = forServiceRegistration
            ? SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces
            : SymbolDisplayTypeQualificationStyle.NameAndContainingTypes;

        var miscOptions = forServiceRegistration
            ? SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            : SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
              SymbolDisplayMiscellaneousOptions.UseSpecialTypes;

        var format = new SymbolDisplayFormat(
            typeQualificationStyle: qualificationStyle,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            delegateStyle: SymbolDisplayDelegateStyle.NameOnly,
            miscellaneousOptions: miscOptions);

        var fullTypeName = serviceType.ToDisplayString(format);
        var sortedNamespaces = (uniqueNamespaces ?? Array.Empty<string>()).Where(ns => !string.IsNullOrEmpty(ns))
            .OrderByDescending(ns => ns.Length);

        foreach (var ns in sortedNamespaces)
        {
            if (fullTypeName.StartsWith($"{ns}.", StringComparison.Ordinal))
                fullTypeName = fullTypeName.Substring(ns.Length + 1);
            fullTypeName = Regex.Replace(fullTypeName, $@"\b{Regex.Escape(ns)}\.", "");
        }

        return fullTypeName;
    }
}
