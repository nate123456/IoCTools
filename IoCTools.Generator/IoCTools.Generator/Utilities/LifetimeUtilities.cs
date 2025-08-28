namespace IoCTools.Generator.Utilities;

using System.Linq;

using Generator;

using Microsoft.CodeAnalysis;

internal static class LifetimeUtilities
{
    internal static string? GetServiceLifetimeFromSymbol(INamedTypeSymbol classSymbol)
    {
        var (hasLifetimeAttribute, _, _, _) = ServiceDiscovery.GetLifetimeAttributes(classSymbol);
        var conditionalAttribute = classSymbol.GetAttributes()
            .FirstOrDefault(attr =>
                attr.AttributeClass?.ToDisplayString() ==
                "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute");
        if (hasLifetimeAttribute) return ServiceDiscovery.GetServiceLifetimeFromAttributes(classSymbol);
        if (conditionalAttribute?.ConstructorArguments.Length > 1)
        {
            var lifetimeValue = conditionalAttribute.ConstructorArguments[1].Value;
            if (lifetimeValue != null)
            {
                var lifetimeInt = (int)lifetimeValue;
                return lifetimeInt switch
                {
                    0 => "Scoped",
                    1 => "Transient",
                    2 => "Singleton",
                    _ => "Scoped"
                };
            }
        }

        return "Scoped";
    }
}
