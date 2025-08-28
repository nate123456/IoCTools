namespace IoCTools.Generator.Utilities;

using System.Collections.Generic;
using System.Linq;

using Analysis;

using Microsoft.CodeAnalysis;

using Models;

internal static class ServiceRegistrationScan
{
    internal static void ScanNamespaceForServices(INamespaceSymbol namespaceSymbol,
        List<ServiceClassInfo> services,
        Compilation compilation)
    {
        foreach (var typeSymbol in namespaceSymbol.GetTypeMembers())
        {
            if (typeSymbol is INamedTypeSymbol namedType && !namedType.IsStatic)
            {
                var hasConditionalServiceAttribute = namedType.GetAttributes().Any(attr =>
                    attr.AttributeClass?.ToDisplayString() ==
                    "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute");

                var hasInjectFields = namedType.GetMembers()
                    .OfType<IFieldSymbol>()
                    .Any(field => field.GetAttributes()
                        .Any(attr =>
                            attr.AttributeClass?.ToDisplayString() ==
                            "IoCTools.Abstractions.Annotations.InjectAttribute"));

                var hasInjectConfigurationFields = namedType.GetMembers()
                    .OfType<IFieldSymbol>()
                    .Any(field => field.GetAttributes()
                        .Any(attr =>
                            attr.AttributeClass?.ToDisplayString() ==
                            "IoCTools.Abstractions.Annotations.InjectConfigurationAttribute"));

                var hasDependsOnAttribute = namedType.GetAttributes()
                    .Any(attr => attr.AttributeClass?.Name?.StartsWith("DependsOn") == true);

                var hasRegisterAsAllAttribute = namedType.GetAttributes()
                    .Any(attr => attr.AttributeClass?.Name == "RegisterAsAllAttribute");

                var hasRegisterAsAttribute = namedType.GetAttributes()
                    .Any(attr =>
                        attr.AttributeClass?.Name?.StartsWith("RegisterAsAttribute") == true &&
                        attr.AttributeClass?.IsGenericType == true);

                var isHostedService = TypeAnalyzer.IsAssignableFromIHostedService(namedType);

                var hasExplicitServiceIntent = hasConditionalServiceAttribute || hasRegisterAsAllAttribute ||
                                               hasRegisterAsAttribute || isHostedService ||
                                               hasInjectFields || hasDependsOnAttribute;

                if (hasExplicitServiceIntent) services.Add(new ServiceClassInfo(namedType, null, null));
            }

            ScanNestedTypesForServices(typeSymbol, services, compilation);
        }

        foreach (var nestedNamespace in namespaceSymbol.GetNamespaceMembers())
            ScanNamespaceForServices(nestedNamespace, services, compilation);
    }

    internal static void ScanNestedTypesForServices(INamedTypeSymbol typeSymbol,
        List<ServiceClassInfo> services,
        Compilation compilation)
    {
        foreach (var nestedType in typeSymbol.GetTypeMembers())
        {
            if (!nestedType.IsStatic)
            {
                var hasConditionalServiceAttribute = nestedType.GetAttributes().Any(attr =>
                    attr.AttributeClass?.ToDisplayString() ==
                    "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute");

                var hasInjectFields = nestedType.GetMembers()
                    .OfType<IFieldSymbol>()
                    .Any(field => field.GetAttributes()
                        .Any(attr =>
                            attr.AttributeClass?.ToDisplayString() ==
                            "IoCTools.Abstractions.Annotations.InjectAttribute"));

                var hasInjectConfigurationFields = nestedType.GetMembers()
                    .OfType<IFieldSymbol>()
                    .Any(field => field.GetAttributes()
                        .Any(attr =>
                            attr.AttributeClass?.ToDisplayString() ==
                            "IoCTools.Abstractions.Annotations.InjectConfigurationAttribute"));

                var hasDependsOnAttribute = nestedType.GetAttributes()
                    .Any(attr => attr.AttributeClass?.Name?.StartsWith("DependsOn") == true);

                var hasRegisterAsAllAttribute = nestedType.GetAttributes()
                    .Any(attr => attr.AttributeClass?.Name == "RegisterAsAllAttribute");

                var hasRegisterAsAttribute = nestedType.GetAttributes()
                    .Any(attr =>
                        attr.AttributeClass?.Name?.StartsWith("RegisterAsAttribute") == true &&
                        attr.AttributeClass?.IsGenericType == true);

                var isHostedService = TypeAnalyzer.IsAssignableFromIHostedService(nestedType);

                var hasExplicitServiceIntent = hasConditionalServiceAttribute || hasRegisterAsAllAttribute ||
                                               hasRegisterAsAttribute || isHostedService ||
                                               hasInjectFields || hasDependsOnAttribute;

                if (hasExplicitServiceIntent) services.Add(new ServiceClassInfo(nestedType, null, null));
            }

            ScanNestedTypesForServices(nestedType, services, compilation);
        }
    }
}
