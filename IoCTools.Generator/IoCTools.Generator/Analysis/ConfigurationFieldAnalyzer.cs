namespace IoCTools.Generator.Analysis;

using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Models;

using Utilities;

/// <summary>
///     Focused logic for discovering [InjectConfiguration] fields and parsing their options.
/// </summary>
internal static class ConfigurationFieldAnalyzer
{
    public static List<ConfigurationInjectionInfo> GetConfigurationInjectedFieldsForType(
        INamedTypeSymbol typeSymbol,
        SemanticModel semanticModel)
    {
        var configFields = new List<ConfigurationInjectionInfo>();

        foreach (var declaringSyntaxRef in typeSymbol.DeclaringSyntaxReferences)
            try
            {
                if (declaringSyntaxRef.GetSyntax() is not TypeDeclarationSyntax typeDeclaration)
                    continue;

                foreach (var fieldDeclaration in typeDeclaration.DescendantNodes().OfType<FieldDeclarationSyntax>())
                {
                    // Skip static and const fields
                    var modifiers = fieldDeclaration.Modifiers;
                    if (modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword) || m.IsKind(SyntaxKind.ConstKeyword)))
                        continue;

                    // Check for [InjectConfiguration]
                    AttributeSyntax? injectConfigAttribute = null;
                    foreach (var attributeList in fieldDeclaration.AttributeLists)
                        foreach (var attribute in attributeList.Attributes)
                        {
                            var attributeText = attribute.Name.ToString();
                            if (attributeText == "InjectConfiguration" ||
                                attributeText == "InjectConfigurationAttribute" ||
                                attributeText.EndsWith("InjectConfiguration") ||
                                attributeText.EndsWith("InjectConfigurationAttribute"))
                            {
                                injectConfigAttribute = attribute;
                                break;
                            }
                        }

                    if (injectConfigAttribute == null) continue;

                    foreach (var variable in fieldDeclaration.Declaration.Variables)
                    {
                        var fieldName = variable.Identifier.Text;

                        // Prefer symbol to preserve nullability
                        var fieldSymbol = semanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;
                        if (fieldSymbol?.Type == null) continue;

                        var substitutedType = TypeSubstitution.SubstituteTypeParameters(fieldSymbol.Type, typeSymbol);
                        var (configKey, defaultValue, required, supportsReloading) =
                            ParseInjectConfigurationAttribute(injectConfigAttribute, semanticModel);

                        configFields.Add(new ConfigurationInjectionInfo(
                            fieldName,
                            substitutedType,
                            configKey,
                            defaultValue,
                            required,
                            supportsReloading));
                    }
                }
            }
            catch
            {
                // Continue with what we have
            }

        return configFields;
    }

    private static (string? configKey, object? defaultValue, bool required, bool supportsReloading)
        ParseInjectConfigurationAttribute(
            AttributeSyntax attributeSyntax,
            SemanticModel semanticModel)
    {
        string? configKey = null;
        object? defaultValue = null;
        var required = true;
        var supportsReloading = false;

        if (attributeSyntax.ArgumentList != null)
            foreach (var argument in attributeSyntax.ArgumentList.Arguments)
                if (argument.NameEquals != null)
                {
                    var parameterName = argument.NameEquals.Name.Identifier.ValueText;
                    switch (parameterName)
                    {
                        case "DefaultValue":
                            defaultValue = GetConstantValue(argument.Expression, semanticModel);
                            break;
                        case "Required":
                            if (GetConstantValue(argument.Expression, semanticModel) is bool req)
                                required = req;
                            break;
                        case "SupportsReloading":
                            if (GetConstantValue(argument.Expression, semanticModel) is bool reload)
                                supportsReloading = reload;
                            break;
                    }
                }
                else
                {
                    if (configKey == null && GetConstantValue(argument.Expression, semanticModel) is string key)
                        configKey = key;
                }

        return (configKey, defaultValue, required, supportsReloading);
    }

    private static object? GetConstantValue(ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        try
        {
            var constantValue = semanticModel.GetConstantValue(expression);
            if (constantValue.HasValue) return constantValue.Value;

            if (expression is LiteralExpressionSyntax literal)
            {
                if (literal.Token.IsKind(SyntaxKind.TrueKeyword) || literal.Token.IsKind(SyntaxKind.FalseKeyword))
                    return literal.Token.IsKind(SyntaxKind.TrueKeyword);
                if (literal.Token.IsKind(SyntaxKind.NumericLiteralToken))
                {
                    var t = literal.Token.ValueText;
                    if (int.TryParse(t, out var i)) return i;
                    if (double.TryParse(t, out var d)) return d;
                    if (decimal.TryParse(t, out var m)) return m;
                }

                return literal.Token.ValueText;
            }
        }
        catch
        {
        }

        return null;
    }
}
