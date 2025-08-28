namespace IoCTools.Generator.Analysis;

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Utilities;

/// <summary>
///     Focused logic for discovering [Inject] fields, with optional external-service flagging.
/// </summary>
internal static class InjectFieldAnalyzer
{
    public static List<(ITypeSymbol ServiceType, string FieldName)> GetInjectedFieldsForType(
        INamedTypeSymbol typeSymbol,
        SemanticModel semanticModel)
    {
        var fields = new List<(ITypeSymbol ServiceType, string FieldName)>();

        foreach (var declaringSyntaxRef in typeSymbol.DeclaringSyntaxReferences)
            try
            {
                if (declaringSyntaxRef.GetSyntax() is not TypeDeclarationSyntax typeDeclaration) continue;

                foreach (var fieldDeclaration in typeDeclaration.DescendantNodes().OfType<FieldDeclarationSyntax>())
                {
                    var modifiers = fieldDeclaration.Modifiers;
                    if (modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword) || m.IsKind(SyntaxKind.ConstKeyword)))
                        continue;

                    var hasInject = false;
                    foreach (var attributeList in fieldDeclaration.AttributeLists)
                    foreach (var attribute in attributeList.Attributes)
                    {
                        var name = attribute.Name.ToString();
                        if (name == "Inject" || name == "InjectAttribute" ||
                            (name.EndsWith("Inject") && !name.Contains("Configuration")) ||
                            (name.EndsWith("InjectAttribute") && !name.Contains("Configuration")))
                        {
                            hasInject = true;
                            break;
                        }
                    }

                    if (!hasInject) continue;

                    foreach (var variable in fieldDeclaration.Declaration.Variables)
                    {
                        var fieldName = variable.Identifier.Text;
                        if (fields.Any(f => f.FieldName == fieldName)) continue;

                        var fieldSymbol = semanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;
                        if (fieldSymbol?.Type != null)
                        {
                            var substituted = TypeSubstitution.SubstituteTypeParameters(fieldSymbol.Type, typeSymbol);
                            fields.Add((substituted, fieldName));
                        }
                        else
                        {
                            var fieldType = semanticModel.GetTypeInfo(fieldDeclaration.Declaration.Type).Type;
                            if (fieldType != null)
                            {
                                var substituted = TypeSubstitution.SubstituteTypeParameters(fieldType, typeSymbol);
                                fields.Add((substituted, fieldName));
                            }
                        }
                    }
                }
            }
            catch (ArgumentException)
            {
                // Fallback for generics where syntax walk can throw
                if (!typeSymbol.IsGenericType) continue;

                var symbolFields = typeSymbol.GetMembers().OfType<IFieldSymbol>()
                    .Where(f => !f.IsStatic && !f.IsConst)
                    .Where(f => f.GetAttributes().Any(a => a.AttributeClass?.Name == "InjectAttribute"));

                foreach (var field in symbolFields)
                {
                    var name = field.Name;
                    if (fields.Any(f => f.FieldName == name)) continue;
                    var substituted = TypeSubstitution.SubstituteTypeParameters(field.Type, typeSymbol);
                    fields.Add((substituted, name));
                }
            }

        // Extra safety for generics when syntax path finds nothing
        if (typeSymbol.IsGenericType && fields.Count == 0)
        {
            var symbolFields = typeSymbol.GetMembers().OfType<IFieldSymbol>()
                .Where(f => !f.IsStatic && !f.IsConst)
                .Where(f => f.GetAttributes().Any(a => a.AttributeClass?.Name == "InjectAttribute"));

            foreach (var field in symbolFields)
            {
                var name = field.Name;
                if (fields.Any(f => f.FieldName == name)) continue;
                var substituted = TypeSubstitution.SubstituteTypeParameters(field.Type, typeSymbol);
                fields.Add((substituted, name));
            }
        }

        return fields;
    }

    public static List<(ITypeSymbol ServiceType, string FieldName, bool IsExternal)>
        GetInjectedFieldsForTypeWithExternalFlag(
            INamedTypeSymbol typeSymbol,
            SemanticModel semanticModel,
            HashSet<string>? allRegisteredServices = null,
            Dictionary<string, List<INamedTypeSymbol>>? allImplementations = null)
    {
        var baseFields = GetInjectedFieldsForType(typeSymbol, semanticModel);
        var results = new List<(ITypeSymbol ServiceType, string FieldName, bool IsExternal)>();

        // Walk syntax again only to detect [ExternalService] attribute on fields if present
        var fieldsMarkedExternal = new HashSet<string>();
        foreach (var declaringSyntaxRef in typeSymbol.DeclaringSyntaxReferences)
            try
            {
                if (declaringSyntaxRef.GetSyntax() is not TypeDeclarationSyntax typeDeclaration) continue;
                foreach (var fieldDeclaration in typeDeclaration.DescendantNodes().OfType<FieldDeclarationSyntax>())
                {
                    var hasInject = false;
                    var hasExternalService = false;
                    foreach (var attributeList in fieldDeclaration.AttributeLists)
                    foreach (var attribute in attributeList.Attributes)
                    {
                        var name = attribute.Name.ToString();
                        if (name == "Inject" || name == "InjectAttribute" ||
                            (name.EndsWith("Inject") && !name.Contains("Configuration")))
                            hasInject = true;
                        if (name == "ExternalService" || name == "ExternalServiceAttribute" ||
                            name.EndsWith("ExternalService") || name.EndsWith("ExternalServiceAttribute"))
                            hasExternalService = true;
                    }

                    if (!hasInject) continue;
                    if (!hasExternalService) continue;

                    foreach (var variable in fieldDeclaration.Declaration.Variables)
                        fieldsMarkedExternal.Add(variable.Identifier.Text);
                }
            }
            catch (ArgumentException)
            {
                // ignore; best-effort annotation
            }

        foreach (var (service, name) in baseFields)
        {
            var isExternal = fieldsMarkedExternal.Contains(name) ||
                             ExternalServiceAnalyzer.IsTypeExternal(service, allRegisteredServices, allImplementations);
            results.Add((service, name, isExternal));
        }

        return results;
    }
}
