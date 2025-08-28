namespace IoCTools.Generator.Generator.Diagnostics.Validators;

using System;
using System.Collections.Generic;
using System.Linq;

using Analysis;

using IoCTools.Generator.Diagnostics;

using Microsoft.CodeAnalysis;

using Models;

using Utilities;

internal static class CircularDependencyValidator
{
    internal static void ValidateCircularDependenciesComplete(SourceProductionContext context,
        List<INamedTypeSymbol> servicesWithAttributes,
        HashSet<string> allRegisteredServices,
        DiagnosticConfiguration diagnosticConfig)
    {
        if (!diagnosticConfig.DiagnosticsEnabled) return;

        var detector = new CircularDependencyDetector();
        var serviceNameToSymbolMap = new Dictionary<string, INamedTypeSymbol>();
        var interfaceToImplementationMap = new Dictionary<string, string>();
        var processedServices = new HashSet<string>();

        foreach (var serviceSymbol in servicesWithAttributes)
        {
            var serviceName = serviceSymbol.Name;
            if (!processedServices.Add(serviceName)) continue;
            serviceNameToSymbolMap[serviceName] = serviceSymbol;

            var hasExternalServiceAttribute = serviceSymbol.GetAttributes()
                .Any(attr => attr.AttributeClass?.ToDisplayString() ==
                             "IoCTools.Abstractions.Annotations.ExternalServiceAttribute");
            if (hasExternalServiceAttribute) continue;

            foreach (var implementedInterface in serviceSymbol.AllInterfaces)
            {
                var interfaceTypeName = implementedInterface.ToDisplayString();
                var interfaceName = TypeHelpers.ExtractServiceNameFromType(interfaceTypeName);
                if (interfaceName != null) interfaceToImplementationMap[interfaceName] = serviceName;
            }
        }

        foreach (var serviceSymbol in servicesWithAttributes)
        {
            var serviceName = serviceSymbol.Name;
            var hasExternalServiceAttribute = serviceSymbol.GetAttributes()
                .Any(attr => attr.AttributeClass?.ToDisplayString() ==
                             "IoCTools.Abstractions.Annotations.ExternalServiceAttribute");
            if (hasExternalServiceAttribute) continue;

            var dependencies = ServiceDependencyUtilities.GetAllDependenciesForService(serviceSymbol);
            foreach (var dependency in dependencies)
            {
                if (TypeHelpers.IsCollectionTypeAdapted(dependency)) continue;
                if (TypeHelpers.IsFrameworkTypeAdapted(dependency)) continue;

                var dependencyInterfaceName = TypeHelpers.ExtractServiceNameFromType(dependency);
                if (dependencyInterfaceName != null)
                {
                    if (interfaceToImplementationMap.TryGetValue(dependencyInterfaceName, out var impl))
                        detector.AddDependency(serviceName, impl);
                    else
                        detector.AddDependency(serviceName, dependencyInterfaceName);
                }
            }
        }

        var circularDependencies = detector.DetectCircularDependencies();
        foreach (var cycle in circularDependencies)
        {
            var cycleServices = cycle.Split(new[] { " → " }, StringSplitOptions.RemoveEmptyEntries);
            var serviceForDiagnostic = cycleServices.FirstOrDefault(s => serviceNameToSymbolMap.ContainsKey(s));
            if (serviceForDiagnostic != null &&
                serviceNameToSymbolMap.TryGetValue(serviceForDiagnostic, out var symbol))
            {
                var location = symbol.Locations.FirstOrDefault() ?? Location.None;
                var diagnostic = Diagnostic.Create(DiagnosticDescriptors.CircularDependency, location, cycle);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
