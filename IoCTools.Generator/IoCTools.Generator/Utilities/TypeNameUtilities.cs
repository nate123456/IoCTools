namespace IoCTools.Generator.Utilities;

using System.Linq;

using Microsoft.CodeAnalysis;

internal static class TypeNameUtilities
{
    internal static string? ExtractServiceNameFromType(string dependencyType)
    {
        var typeName = dependencyType;
        var lastDotIndex = typeName.LastIndexOf('.');
        if (lastDotIndex >= 0) typeName = typeName.Substring(lastDotIndex + 1);
        if (typeName.StartsWith("I") && typeName.Length > 1 && char.IsUpper(typeName[1]))
            return typeName.Substring(1);
        return typeName;
    }

    internal static string FormatTypeNameForDiagnostic(ITypeSymbol typeSymbol)
    {
        if (typeSymbol is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var typeName = namedType.Name;
            var typeArgs = namedType.TypeArguments.Select(FormatTypeNameForDiagnostic).ToArray();
            return typeArgs.Length > 0 ? $"{typeName}<{string.Join(", ", typeArgs)}>" : typeName;
        }

        return typeSymbol.Name;
    }

    internal static string ExtractSimpleTypeNameFromFullName(string fullTypeName)
    {
        var genericStart = fullTypeName.IndexOf('<');
        var searchEnd = genericStart >= 0 ? genericStart : fullTypeName.Length;
        var lastDotIndex = fullTypeName.LastIndexOf('.', searchEnd - 1);
        var typeName = lastDotIndex >= 0 ? fullTypeName.Substring(lastDotIndex + 1) : fullTypeName;
        var angleIndex = typeName.IndexOf('<');
        if (angleIndex >= 0) typeName = typeName.Substring(0, angleIndex);
        return typeName;
    }
}
