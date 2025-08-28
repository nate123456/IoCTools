namespace IoCTools.Generator.Analysis;

using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;

using Utilities;

/// <summary>
///     Focused logic for [DependsOn] attribute processing and field-name generation.
/// </summary>
internal static class DependsOnFieldAnalyzer
{
    public static List<(ITypeSymbol ServiceType, string FieldName)> GetRawDependsOnFieldsForType(
        INamedTypeSymbol typeSymbol)
    {
        var fields = new List<(ITypeSymbol ServiceType, string FieldName)>();

        var originalTypeDefinition = typeSymbol.OriginalDefinition;
        var dependsOnAttributes = originalTypeDefinition.GetAttributes()
            .Where(attr => attr.AttributeClass?.Name?.StartsWith("DependsOn") == true)
            .ToList();

        foreach (var attribute in dependsOnAttributes)
        {
            var genericTypeArguments = attribute.AttributeClass?.TypeArguments.ToList();
            if (genericTypeArguments == null) continue;

            var (namingConvention, stripI, prefix) = AttributeParser.GetNamingConventionOptionsFromAttribute(attribute);
            foreach (var genericTypeArgument in genericTypeArguments)
            {
                var substitutedType = TypeSubstitution.SubstituteTypeParameters(genericTypeArgument, typeSymbol);
                var fieldName = AttributeParser.GenerateFieldName(
                    TypeUtilities.GetMeaningfulTypeName(substitutedType), namingConvention, stripI, prefix);
                fields.Add((substitutedType, fieldName));
            }
        }

        return fields;
    }

    public static List<(ITypeSymbol ServiceType, string FieldName)> GetRawDependsOnFieldsForTypeWithSubstitution(
        INamedTypeSymbol typeSymbol,
        INamedTypeSymbol targetTypeForSubstitution)
    {
        var fields = new List<(ITypeSymbol ServiceType, string FieldName)>();

        var dependsOnAttributes = typeSymbol.GetAttributes()
            .Where(attr => attr.AttributeClass?.ToDisplayString()
                .StartsWith("IoCTools.Abstractions.Annotations.DependsOnAttribute") == true)
            .ToList();

        foreach (var attribute in dependsOnAttributes)
        {
            if (attribute.AttributeClass?.TypeArguments == null) continue;
            var (namingConvention, stripI, prefix) = AttributeParser.GetNamingConventionOptionsFromAttribute(attribute);
            foreach (var genericTypeArgument in attribute.AttributeClass.TypeArguments)
            {
                var substitutedType = TypeSubstitution.ApplyInheritanceChainSubstitution(
                    genericTypeArgument, typeSymbol, targetTypeForSubstitution);
                var fieldName = AttributeParser.GenerateFieldName(
                    TypeUtilities.GetMeaningfulTypeName(substitutedType), namingConvention, stripI, prefix);
                fields.Add((substitutedType, fieldName));
            }
        }

        return fields;
    }

    public static List<(ITypeSymbol ServiceType, string FieldName, bool IsExternal)>
        GetRawDependsOnFieldsForTypeWithExternalFlag(
            INamedTypeSymbol typeSymbol,
            HashSet<string>? allRegisteredServices = null,
            Dictionary<string, List<INamedTypeSymbol>>? allImplementations = null)
    {
        var fields = new List<(ITypeSymbol ServiceType, string FieldName, bool IsExternal)>();

        var originalTypeDefinition = typeSymbol.OriginalDefinition;
        var dependsOnAttributes = originalTypeDefinition.GetAttributes()
            .Where(attr => attr.AttributeClass?.Name?.StartsWith("DependsOn") == true)
            .ToList();

        foreach (var attribute in dependsOnAttributes)
        {
            var genericTypeArguments = attribute.AttributeClass?.TypeArguments.ToList();
            if (genericTypeArguments == null) continue;

            var (namingConvention, stripI, prefix, external) =
                AttributeParser.GetDependsOnOptionsFromAttribute(attribute);

            foreach (var genericTypeArgument in genericTypeArguments)
            {
                var substitutedType = TypeSubstitution.SubstituteTypeParameters(genericTypeArgument, typeSymbol);
                var fieldName = AttributeParser.GenerateFieldName(
                    TypeUtilities.GetMeaningfulTypeName(substitutedType), namingConvention, stripI, prefix);
                var isExternal = external ||
                                 ExternalServiceAnalyzer.IsTypeExternal(substitutedType, allRegisteredServices,
                                     allImplementations);
                fields.Add((substitutedType, fieldName, isExternal));
            }
        }

        return fields;
    }
}
