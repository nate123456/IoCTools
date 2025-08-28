namespace IoCTools.Generator.Utilities;

using System.Collections.Generic;
using System.Linq;
using System.Text;

using Analysis;

using Generator;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class DiagnosticScan
{
    internal static void CollectServiceSymbolsOnce(SyntaxNode root,
        SemanticModel semanticModel,
        List<INamedTypeSymbol> servicesWithAttributes,
        HashSet<string> allRegisteredServices,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations,
        Dictionary<string, string> serviceLifetimes,
        HashSet<string> globalProcessedClasses)
    {
        var typeDeclarations = root.DescendantNodes().OfType<TypeDeclarationSyntax>();
        foreach (var typeDeclaration in typeDeclarations)
        {
            var classSymbol = semanticModel.GetDeclaredSymbol(typeDeclaration) as INamedTypeSymbol;
            if (classSymbol == null) continue;
            if (classSymbol.TypeKind == TypeKind.Interface) continue;

            var classKey = classSymbol.ToDisplayString();

            foreach (var @interface in classSymbol.Interfaces)
            {
                var interfaceName = @interface.ToDisplayString();
                if (!allImplementations.ContainsKey(interfaceName))
                    allImplementations[interfaceName] = new List<INamedTypeSymbol>();
                allImplementations[interfaceName].Add(classSymbol);
                if (@interface is INamedTypeSymbol namedInterface && namedInterface.IsGenericType)
                {
                    var enhanced = ConvertToEnhancedOpenGenericFormForInterface(namedInterface);
                    if (enhanced != null && enhanced != interfaceName)
                    {
                        if (!allImplementations.ContainsKey(enhanced))
                            allImplementations[enhanced] = new List<INamedTypeSymbol>();
                        allImplementations[enhanced].Add(classSymbol);
                    }
                }
            }

            var hasConditionalServiceAttribute = classSymbol.GetAttributes().Any(attr =>
                attr.AttributeClass?.ToDisplayString() ==
                "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute");
            var hasInjectFields = classSymbol.GetMembers().OfType<IFieldSymbol>()
                .Any(field => field.GetAttributes().Any(attr => attr.AttributeClass?.Name == "InjectAttribute"));
            var hasInjectConfigurationFields =
                ServiceDiscovery.HasInjectConfigurationFieldsAcrossPartialClasses(classSymbol);
            var hasDependsOnAttribute = classSymbol.GetAttributes()
                .Any(attr => attr.AttributeClass?.Name?.StartsWith("DependsOn") == true);
            var hasRegisterAsAllAttribute = classSymbol.GetAttributes()
                .Any(attr => attr.AttributeClass?.Name == "RegisterAsAllAttribute");
            var hasRegisterAsAttribute = classSymbol.GetAttributes().Any(attr =>
                attr.AttributeClass?.Name?.StartsWith("RegisterAsAttribute") == true &&
                attr.AttributeClass?.IsGenericType == true);
            var isHostedService = TypeAnalyzer.IsAssignableFromIHostedService(classSymbol);

            var (hasLifetimeAttribute, _, _, _) = ServiceDiscovery.GetLifetimeAttributes(classSymbol);
            var hasExplicitServiceIntent = hasConditionalServiceAttribute || hasRegisterAsAllAttribute ||
                                           hasRegisterAsAttribute || isHostedService || hasLifetimeAttribute;

            if (hasExplicitServiceIntent || hasInjectFields || hasDependsOnAttribute || hasInjectConfigurationFields)
            {
                if (globalProcessedClasses.Add(classKey)) servicesWithAttributes.Add(classSymbol);
                else continue;
                allRegisteredServices.Add(classSymbol.ToDisplayString());
                var (hasAnyLifetime, _, _, _) = ServiceDiscovery.GetLifetimeAttributes(classSymbol);
                if (hasAnyLifetime)
                {
                    var lifetime = ServiceDiscovery.GetServiceLifetimeFromAttributes(classSymbol);
                    serviceLifetimes[classSymbol.ToDisplayString()] = lifetime;
                    foreach (var interfaceSymbol in classSymbol.AllInterfaces)
                    {
                        var ifDisplayString = interfaceSymbol.ToDisplayString();
                        serviceLifetimes[ifDisplayString] = lifetime;
                        allRegisteredServices.Add(ifDisplayString);
                    }
                }
            }
        }
    }

    internal static void ScanNamespaceForTypes(INamespaceSymbol namespaceSymbol,
        List<INamedTypeSymbol> types)
    {
        foreach (var typeSymbol in namespaceSymbol.GetTypeMembers())
            if (typeSymbol is INamedTypeSymbol namedType)
            {
                types.Add(namedType);
                ScanNestedTypesForTypes(namedType, types);
            }

        foreach (var nestedNamespace in namespaceSymbol.GetNamespaceMembers())
            ScanNamespaceForTypes(nestedNamespace, types);
    }

    private static void ScanNestedTypesForTypes(INamedTypeSymbol typeSymbol,
        List<INamedTypeSymbol> types)
    {
        foreach (var nestedType in typeSymbol.GetTypeMembers())
        {
            types.Add(nestedType);
            ScanNestedTypesForTypes(nestedType, types);
        }
    }

    private static string? ConvertToEnhancedOpenGenericFormForInterface(INamedTypeSymbol interfaceSymbol)
    {
        if (!interfaceSymbol.IsGenericType) return null;
        var interfaceName = interfaceSymbol.ToDisplayString();
        if (IsOpenGenericForm(interfaceName)) return interfaceName;
        return ConvertToEnhancedOpenGenericFormFromString(interfaceName);
    }

    private static bool IsOpenGenericForm(string typeName)
    {
        if (!typeName.Contains('<') || !typeName.Contains('>')) return false;
        var angleStart = typeName.IndexOf('<');
        var angleEnd = typeName.LastIndexOf('>');
        if (angleStart >= 0 && angleEnd > angleStart)
        {
            var typeArgsSection = typeName.Substring(angleStart + 1, angleEnd - angleStart - 1);
            var args = typeArgsSection.Split(',').Select(arg => arg.Trim()).ToArray();
            return args.All(arg =>
                arg == "T" || (arg.StartsWith("T") && arg.Length > 1 && arg.Substring(1).All(char.IsDigit)));
        }

        return false;
    }

    private static string? ConvertToEnhancedOpenGenericFormFromString(string constructedType)
    {
        if (!constructedType.Contains('<') || !constructedType.Contains('>')) return null;
        var angleStart = constructedType.IndexOf('<');
        var angleEnd = constructedType.LastIndexOf('>');
        if (angleStart >= 0 && angleEnd > angleStart)
        {
            var baseName = constructedType.Substring(0, angleStart);
            var typeArgsSection = constructedType.Substring(angleStart + 1, angleEnd - angleStart - 1);
            var typeArgs = ParseGenericTypeArgumentsFromString(typeArgsSection);
            var result = new List<string>();
            for (var i = 0; i < typeArgs.Count; i++)
            {
                var paramName = i == 0 ? "T" : $"T{i + 1}";
                result.Add(paramName);
            }

            return $"{baseName}<{string.Join(", ", result)}>";
        }

        return null;
    }

    private static List<string> ParseGenericTypeArgumentsFromString(string typeArgsSection)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var bracketLevel = 0;
        for (var i = 0; i < typeArgsSection.Length; i++)
        {
            var c = typeArgsSection[i];
            if (c == '<')
            {
                bracketLevel++;
                current.Append(c);
            }
            else if (c == '>')
            {
                bracketLevel--;
                current.Append(c);
            }
            else if (c == ',' && bracketLevel == 0)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0) result.Add(current.ToString().Trim());
        return result;
    }
}
