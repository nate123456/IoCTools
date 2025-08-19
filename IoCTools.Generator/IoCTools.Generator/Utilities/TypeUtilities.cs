using System.Linq;
using Microsoft.CodeAnalysis;

namespace IoCTools.Generator.Utilities;

internal static class TypeUtilities
{
    public static string GetMeaningfulTypeName(ITypeSymbol typeSymbol)
    {
        // For collection types, extract the inner type argument for better field naming
        if (typeSymbol is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var typeName = namedType.Name;

            // Check if it's a common collection type that should use its type argument for naming
            var collectionTypes = new[]
            {
                "IEnumerable", "IList", "ICollection", "List",
                "IReadOnlyList", "IReadOnlyCollection", "Array"
            };

            if (collectionTypes.Contains(typeName) && namedType.TypeArguments.Length > 0)
            {
                // Use the first type argument for field naming
                var innerType = namedType.TypeArguments[0];
                return GetMeaningfulTypeName(innerType); // Recursive for nested generics
            }
        }

        // For non-collection types or non-generic types, use the type name itself
        return typeSymbol.Name;
    }
}