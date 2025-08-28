namespace IoCTools.Generator.CodeGeneration;

using System.Collections.Generic;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Utilities;

internal static partial class ConstructorGenerator
{
    private static void CollectNamespaces(ITypeSymbol typeSymbol,
        HashSet<string> uniqueNamespaces)
    {
        if (typeSymbol == null) return;

        var ns = typeSymbol.ContainingNamespace;
        if (ns != null && !ns.IsGlobalNamespace)
        {
            var nsName = ns.ToDisplayString();
            if (!string.IsNullOrEmpty(nsName)) uniqueNamespaces.Add(nsName);
        }

        if (typeSymbol is INamedTypeSymbol namedType && namedType.TypeArguments.Length > 0)
            foreach (var typeArg in namedType.TypeArguments)
                CollectNamespaces(typeArg, uniqueNamespaces);

        if (typeSymbol is IArrayTypeSymbol arrayType) CollectNamespaces(arrayType.ElementType, uniqueNamespaces);
    }

    private static void CollectNamespacesFromConstraints(TypeParameterConstraintClauseSyntax constraintClause,
        SemanticModel semanticModel,
        HashSet<string> uniqueNamespaces)
    {
        foreach (var constraint in constraintClause.Constraints)
            if (constraint is TypeConstraintSyntax typeConstraint)
            {
                var typeInfo = semanticModel.GetTypeInfo(typeConstraint.Type);
                if (typeInfo.Type != null) CollectNamespaces(typeInfo.Type, uniqueNamespaces);
            }
    }

    private static string? GetClassNamespace(TypeDeclarationSyntax classDeclaration)
    {
        var parent = classDeclaration.Parent;
        while (parent != null)
        {
            if (parent is BaseNamespaceDeclarationSyntax namespaceDeclaration)
                return namespaceDeclaration.Name.ToString();
            parent = parent.Parent;
        }

        return null;
    }

    private static string GetClassAccessibilityModifier(INamedTypeSymbol classSymbol)
    {
        return classSymbol.DeclaredAccessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Protected => "protected",
            Accessibility.Private => "private",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            _ => "public"
        };
    }

    private static string GetTypeDeclarationKeyword(TypeDeclarationSyntax typeDeclaration)
    {
        return typeDeclaration switch
        {
            ClassDeclarationSyntax => "class",
            RecordDeclarationSyntax => "record",
            StructDeclarationSyntax => "struct",
            InterfaceDeclarationSyntax => "interface",
            _ => "class"
        };
    }

    private static string RemoveNamespacesAndDots(ITypeSymbol typeSymbol,
        HashSet<string> namespacesToStrip) =>
        TypeDisplayUtilities.WithoutNamespaces(typeSymbol, namespacesToStrip, true);
}
