namespace IoCTools.Generator.Analysis;

using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;

/// <summary>
///     Determines whether a dependency type should be treated as external.
/// </summary>
internal static class ExternalServiceAnalyzer
{
    public static bool IsTypeExternal(
        ITypeSymbol dependencyType,
        HashSet<string>? allRegisteredServices,
        Dictionary<string, List<INamedTypeSymbol>>? allImplementations)
    {
        if (allImplementations == null || allRegisteredServices == null)
            return false;

        // Built-in DI helper patterns are never external
        if (IsAdvancedDIPattern(dependencyType))
            return false;

        var dependencyTypeName = dependencyType.ToDisplayString();
        if (!allImplementations.TryGetValue(dependencyTypeName, out var implementations))
            return false;

        return implementations.Any(impl => impl.GetAttributes()
            .Any(attr => attr.AttributeClass?.ToDisplayString() ==
                         "IoCTools.Abstractions.Annotations.ExternalServiceAttribute"));
    }

    private static bool IsAdvancedDIPattern(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType) return false;
        var typeName = namedType.OriginalDefinition.ToDisplayString();
        return typeName == "System.Func<>" ||
               typeName == "System.Lazy<>" ||
               typeName.StartsWith("System.Func<") ||
               typeName.StartsWith("System.Lazy<") ||
               (type.CanBeReferencedByName && type.NullableAnnotation == NullableAnnotation.Annotated);
    }
}
