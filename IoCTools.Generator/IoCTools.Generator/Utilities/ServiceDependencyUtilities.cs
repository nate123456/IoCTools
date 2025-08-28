namespace IoCTools.Generator.Utilities;

using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;

internal static class ServiceDependencyUtilities
{
    internal static List<string> GetAllDependenciesForService(INamedTypeSymbol serviceSymbol)
    {
        var dependencies = new List<string>();
        dependencies.AddRange(GetDependsOnTypes(serviceSymbol));
        foreach (var member in serviceSymbol.GetMembers().OfType<IFieldSymbol>())
        {
            var hasInjectAttribute = member.GetAttributes().Any(attr => attr.AttributeClass?.Name == "InjectAttribute");
            if (hasInjectAttribute) dependencies.Add(member.Type.ToDisplayString());
        }

        return dependencies;
    }

    private static List<string> GetDependsOnTypes(INamedTypeSymbol classSymbol)
    {
        var types = new List<string>();
        foreach (var attr in classSymbol.GetAttributes())
            if (attr.AttributeClass?.Name == "DependsOnAttribute" && attr.AttributeClass?.TypeArguments != null)
                foreach (var typeArg in attr.AttributeClass.TypeArguments)
                    types.Add(typeArg.ToDisplayString());
        return types;
    }
}
