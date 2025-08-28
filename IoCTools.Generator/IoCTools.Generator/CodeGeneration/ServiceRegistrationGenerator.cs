namespace IoCTools.Generator.CodeGeneration;

using System.Linq;

using Generator;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

// Thin coordination partial: diagnostics + tiny helpers only.
internal static partial class ServiceRegistrationGenerator
{
    // Tiny helper used by some partials.
    private static string ExtractLifetimeFromAttributes(INamedTypeSymbol classSymbol)
        => ServiceDiscovery.GetServiceLifetimeFromAttributes(classSymbol);

    // Helper used by RegistrationCode to detect config-injection on syntax nodes
    private static bool HasConfigurationInjectionFields(SyntaxNode classDeclaration)
    {
        if (classDeclaration is not TypeDeclarationSyntax typeDeclaration)
            return false;
        foreach (var field in typeDeclaration.DescendantNodes().OfType<FieldDeclarationSyntax>())
        {
            var mods = field.Modifiers;
            if (mods.Any(m => m.IsKind(SyntaxKind.StaticKeyword) ||
                              m.IsKind(SyntaxKind.ConstKeyword)))
                continue;
            foreach (var attrList in field.AttributeLists)
            foreach (var attr in attrList.Attributes)
            {
                var name = attr.Name.ToString();
                if (name == "InjectConfiguration" || name == "InjectConfigurationAttribute" ||
                    name.EndsWith("InjectConfiguration") || name.EndsWith("InjectConfigurationAttribute"))
                    return true;
            }
        }

        return false;
    }
}
