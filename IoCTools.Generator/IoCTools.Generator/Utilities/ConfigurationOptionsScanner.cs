namespace IoCTools.Generator.Utilities;

using System.Collections.Generic;
using System.Linq;

using Analysis;

using Generator;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Models;

internal static class ConfigurationOptionsScanner
{
    public static List<ConfigurationOptionsRegistration> GetConfigurationOptionsToRegister(
        SemanticModel semanticModel,
        SyntaxNode root)
    {
        var configOptions = new List<ConfigurationOptionsRegistration>();
        var processedTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        var typeDeclarations = root.DescendantNodes().OfType<TypeDeclarationSyntax>().ToList();
        foreach (var classDeclaration in typeDeclarations)
        {
            var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;
            if (classSymbol == null) continue;
            if (classSymbol.IsAbstract || classSymbol.TypeKind == TypeKind.Interface) continue;

            var (hasLifetimeAttribute, _, _, _) = ServiceDiscovery.GetLifetimeAttributes(classSymbol);
            var hasConditionalServiceAttribute = classSymbol.GetAttributes().Any(attr =>
                attr.AttributeClass?.ToDisplayString() ==
                "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute");
            var isHostedServiceType = TypeAnalyzer.IsAssignableFromIHostedService(classSymbol);
            if (!hasLifetimeAttribute && !hasConditionalServiceAttribute && !isHostedServiceType) continue;

            var configFields = DependencyAnalyzer.GetConfigurationInjectedFieldsForType(classSymbol, semanticModel);
            foreach (var configField in configFields.Where(f => f.IsOptionsPattern))
            {
                var optionsInnerType = configField.GetOptionsInnerType();
                if (optionsInnerType != null && processedTypes.Add(optionsInnerType))
                {
                    var sectionName = configField.GetSectionName();
                    configOptions.Add(new ConfigurationOptionsRegistration(optionsInnerType, sectionName));
                }
            }

            var regularDependencies = DependencyAnalyzer.GetConstructorDependencies(classSymbol, semanticModel);
            foreach (var dependency in regularDependencies.AllDependencies)
                if (dependency.ServiceType is INamedTypeSymbol namedType &&
                    namedType.OriginalDefinition.ToDisplayString()
                        .StartsWith("Microsoft.Extensions.Options.IOptions<"))
                {
                    var optionsInnerType = namedType.TypeArguments.FirstOrDefault();
                    if (optionsInnerType != null && processedTypes.Add(optionsInnerType))
                    {
                        var sectionName = InferSectionNameFromType(optionsInnerType);
                        configOptions.Add(new ConfigurationOptionsRegistration(optionsInnerType, sectionName));
                    }
                }
        }

        return configOptions;
    }

    private static string InferSectionNameFromType(ITypeSymbol type)
    {
        var typeName = type.Name;
        if (typeName.EndsWith("Settings"))
            return typeName.Substring(0, typeName.Length - "Settings".Length);
        if (typeName.EndsWith("Configuration"))
            return typeName.Substring(0, typeName.Length - "Configuration".Length);
        if (typeName.EndsWith("Config"))
            return typeName.Substring(0, typeName.Length - "Config".Length);
        if (typeName.EndsWith("Options"))
            return typeName.Substring(0, typeName.Length - "Options".Length);
        if (typeName.EndsWith("Object"))
            return typeName.Substring(0, typeName.Length - "Object".Length);
        return typeName;
    }
}
