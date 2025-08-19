using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IoCTools.Generator.Analysis;
using IoCTools.Generator.Models;
using IoCTools.Generator.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace IoCTools.Generator.Diagnostics;

internal static class DependencyValidator
{
    // Framework types that are commonly registered manually or by the framework
    private static readonly HashSet<string> FrameworkTypes = new()
    {
        "Microsoft.Extensions.Logging.ILogger",
        "Microsoft.Extensions.Logging.ILogger<>",
        "Microsoft.Extensions.Configuration.IConfiguration",
        "Microsoft.Extensions.Configuration.IConfigurationRoot",
        "Microsoft.Extensions.Configuration.IConfigurationSection",
        "System.Net.Http.HttpClient",
        "Microsoft.Extensions.DependencyInjection.IServiceProvider",
        "Microsoft.Extensions.Options.IOptions<>",
        "Microsoft.Extensions.Options.IOptionsMonitor<>",
        "Microsoft.Extensions.Options.IOptionsSnapshot<>",
        "Microsoft.Extensions.Hosting.IHostEnvironment",
        "Microsoft.Extensions.Hosting.IHostApplicationLifetime",
        "Microsoft.AspNetCore.Http.IHttpContextAccessor",
        "Microsoft.Extensions.Caching.Memory.IMemoryCache",
        "Microsoft.Extensions.Caching.Distributed.IDistributedCache",
        "System.IServiceProvider",
        "Mediator.IMediator",
        "Mediator.ISender",
        "Mediator.IPublisher",
        "Microsoft.Extensions.Hosting.IHostedService",
        "Microsoft.Extensions.FileProviders.IFileProvider",
        "Microsoft.Extensions.Primitives.IChangeToken",
        "System.ComponentModel.INotifyPropertyChanged",
        "System.IDisposable",
        "System.IAsyncDisposable"
    };

    // Diagnostic descriptors for dependency validation
    private static readonly DiagnosticDescriptor NoImplementationFound = new(
        "IOC001",
        "No implementation found for interface",
        "Service '{0}' depends on '{1}' but no implementation of this interface exists in the project",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Create an implementation, register manually in Program.cs, or mark as external dependency.");

    private static readonly DiagnosticDescriptor ImplementationNotRegistered = new(
        "IOC002",
        "Implementation exists but not registered",
        "Service '{0}' depends on '{1}' - implementation exists but lacks [Service] attribute",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Add [Service] attribute to the implementation or register manually in Program.cs.");

    public static void ValidateDependencies(GeneratorExecutionContext context,
        TypeDeclarationSyntax classDeclaration,
        InheritanceHierarchyDependencies hierarchyDependencies,
        HashSet<string> allRegisteredServices,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations,
        Dictionary<string, string> serviceLifetimes,
        DiagnosticConfiguration diagnosticConfig,
        SemanticModel semanticModel)
    {
        var symbol = semanticModel.GetDeclaredSymbol(classDeclaration);
        if (symbol is not INamedTypeSymbol classSymbol) return;

        // Skip all validation if diagnostics are disabled
        if (!diagnosticConfig.DiagnosticsEnabled) return;

        // Validate [Inject] field dependencies
        ValidateInjectFieldDependencies(context, classDeclaration, hierarchyDependencies, allRegisteredServices,
            allImplementations, diagnosticConfig, semanticModel);

        // Validate [DependsOn] attribute dependencies  
        ValidateDependsOnAttributeDependencies(context, classDeclaration, allRegisteredServices, allImplementations,
            diagnosticConfig, semanticModel);

        // Validate lifetime compatibility
        ValidateServiceLifetimes(context, classDeclaration, hierarchyDependencies, allRegisteredServices,
            allImplementations, serviceLifetimes, diagnosticConfig, semanticModel);

        // Validate redundancies and conflicts (IOC006, IOC007, IOC008, IOC009)
        DiagnosticUtilities.DetectAndReportRedundanciesWithHierarchy(classSymbol, hierarchyDependencies,
            context, semanticModel, diagnosticConfig);
    }

    /// <summary>
    ///     Validates circular dependencies across all services in the compilation.
    ///     This method should be called once per compilation with all services.
    /// </summary>
    public static void ValidateCircularDependencies(GeneratorExecutionContext context,
        List<INamedTypeSymbol> servicesWithAttributes,
        DiagnosticConfiguration diagnosticConfig)
    {
        // Skip all validation if diagnostics are disabled
        if (!diagnosticConfig.DiagnosticsEnabled) return;

        var detector = new CircularDependencyDetector();
        var serviceNameToSymbolMap = new Dictionary<string, INamedTypeSymbol>();
        var interfaceToImplementationMap = new Dictionary<string, string>();
        var processedServices = new HashSet<string>();

        // First pass: Build the interface to implementation mapping
        foreach (var serviceSymbol in servicesWithAttributes)
        {
            var serviceName = serviceSymbol.Name;

            // Skip if we've already processed this service type
            if (processedServices.Contains(serviceName))
                continue;

            processedServices.Add(serviceName);
            serviceNameToSymbolMap[serviceName] = serviceSymbol;

            // Check if this service has [ExternalService] attribute - if so, skip it
            var hasExternalServiceAttribute = serviceSymbol.GetAttributes()
                .Any(attr => attr.AttributeClass?.ToDisplayString() ==
                             "IoCTools.Abstractions.Annotations.ExternalServiceAttribute");

            if (hasExternalServiceAttribute) continue;

            // Map interfaces to this implementation
            foreach (var implementedInterface in serviceSymbol.AllInterfaces)
            {
                var interfaceTypeName = implementedInterface.ToDisplayString();
                var interfaceName = ExtractServiceNameFromType(interfaceTypeName);
                if (interfaceName != null) interfaceToImplementationMap[interfaceName] = serviceName;
            }
        }

        // Second pass: Build dependency graph
        foreach (var serviceSymbol in servicesWithAttributes)
        {
            var serviceName = serviceSymbol.Name;

            // Check if this service has [ExternalService] attribute - if so, skip it
            var hasExternalServiceAttribute = serviceSymbol.GetAttributes()
                .Any(attr => attr.AttributeClass?.ToDisplayString() ==
                             "IoCTools.Abstractions.Annotations.ExternalServiceAttribute");

            if (hasExternalServiceAttribute) continue;

            // Get all dependencies for this service
            var dependencies = GetAllDependenciesForService(serviceSymbol, context.Compilation);

            foreach (var dependency in dependencies)
            {
                // Skip collection types (they don't create circular dependencies)
                if (IsCollectionType(dependency))
                    continue;

                // Skip framework types
                if (FrameworkTypes.Contains(dependency) || IsFrameworkGenericType(dependency))
                    continue;

                // Extract service name from dependency type
                var dependencyInterfaceName = ExtractServiceNameFromType(dependency);
                if (dependencyInterfaceName != null)
                {
                    // Try to find the actual implementation for this interface
                    if (interfaceToImplementationMap.TryGetValue(dependencyInterfaceName, out var implementationName))
                        detector.AddDependency(serviceName, implementationName);
                    else
                        // If no implementation found, use the interface name directly
                        detector.AddDependency(serviceName, dependencyInterfaceName);
                }
            }
        }

        // Detect circular dependencies
        var circularDependencies = detector.DetectCircularDependencies();

        // Report diagnostics for each circular dependency
        foreach (var cycle in circularDependencies)
        {
            // Find the first service in the cycle that we have symbol information for
            var cycleServices = cycle.Split(new[] { " → " }, StringSplitOptions.RemoveEmptyEntries);
            var serviceForDiagnostic = cycleServices.FirstOrDefault(s => serviceNameToSymbolMap.ContainsKey(s));

            if (serviceForDiagnostic != null &&
                serviceNameToSymbolMap.TryGetValue(serviceForDiagnostic, out var serviceSymbol))
            {
                var location = serviceSymbol.Locations.FirstOrDefault() ?? Location.None;
                var diagnostic = Diagnostic.Create(
                    DiagnosticDescriptors.CircularDependency,
                    location,
                    cycle);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static void ValidateInjectFieldDependencies(GeneratorExecutionContext context,
        TypeDeclarationSyntax classDeclaration,
        InheritanceHierarchyDependencies hierarchyDependencies,
        HashSet<string> allRegisteredServices,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations,
        DiagnosticConfiguration diagnosticConfig,
        SemanticModel semanticModel)
    {
        // Get all [Inject] fields from the hierarchy
        var symbol = semanticModel.GetDeclaredSymbol(classDeclaration);
        if (symbol is not INamedTypeSymbol classSymbol) return;

        // Check if class has [ExternalService] attribute (makes all dependencies external)
        var classHasExternalServiceAttribute = classSymbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.ToDisplayString() ==
                         "IoCTools.Abstractions.Annotations.ExternalServiceAttribute");

        if (classHasExternalServiceAttribute) return; // Skip all validation for this class

        // Check if class has [UnregisteredService] attribute - if so, skip [Inject] validation
        var classHasUnregisteredServiceAttribute = classSymbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.ToDisplayString() ==
                         "IoCTools.Abstractions.Annotations.UnregisteredServiceAttribute");

        if (classHasUnregisteredServiceAttribute) return; // Skip [Inject] validation for [UnregisteredService] classes

        var allInjectFields = GetAllInjectFieldsFromHierarchy(classSymbol, semanticModel);

        foreach (var (fieldSymbol, fieldDeclaration) in allInjectFields)
        {
            // Check if field has [ExternalService] attribute
            var hasExternalServiceAttribute = fieldSymbol.GetAttributes()
                .Any(attr =>
                    attr.AttributeClass?.ToDisplayString() ==
                    "IoCTools.Abstractions.Annotations.ExternalServiceAttribute");

            if (hasExternalServiceAttribute) continue;

            var dependencyType = fieldSymbol.Type.ToDisplayString();

            // Check if dependency is valid (registered, framework type, or collection of valid types)
            if (!IsValidDependency(dependencyType, allRegisteredServices))
            {
                var location = fieldDeclaration.Declaration.Type.GetLocation();
                var diagnostic = CreateAppropriateDeficiencyDiagnostic(classSymbol.Name, fieldSymbol.Type,
                    allRegisteredServices, allImplementations, diagnosticConfig, location);
                if (diagnostic != null) context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static void ValidateDependsOnAttributeDependencies(GeneratorExecutionContext context,
        TypeDeclarationSyntax classDeclaration,
        HashSet<string> allRegisteredServices,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations,
        DiagnosticConfiguration diagnosticConfig,
        SemanticModel semanticModel)
    {
        var symbol = semanticModel.GetDeclaredSymbol(classDeclaration);
        if (symbol is not INamedTypeSymbol classSymbol) return;

        // Check if class has [ExternalService] attribute (makes all dependencies external)
        var classHasExternalServiceAttribute = classSymbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.ToDisplayString() ==
                         "IoCTools.Abstractions.Annotations.ExternalServiceAttribute");

        if (classHasExternalServiceAttribute) return; // Skip all validation for this class

        // Check if class has [UnregisteredService] attribute - if so, skip [DependsOn] validation
        var classHasUnregisteredServiceAttribute = classSymbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.ToDisplayString() ==
                         "IoCTools.Abstractions.Annotations.UnregisteredServiceAttribute");

        if (classHasUnregisteredServiceAttribute)
            return; // Skip [DependsOn] validation for [UnregisteredService] classes

        // Get all [DependsOn] attributes
        var dependsOnAttributes = classSymbol.GetAttributes()
            .Where(attr => attr.AttributeClass?.Name == "DependsOnAttribute")
            .ToList();

        foreach (var attribute in dependsOnAttributes)
        {
            // Check if this [DependsOn] attribute is marked as external
            var (_, _, _, isExternal) = AttributeParser.GetDependsOnOptionsFromAttribute(attribute);

            if (isExternal) continue; // Skip validation for external dependencies

            // Get the attribute syntax for better error location
            var attributeSyntax = attribute.ApplicationSyntaxReference?.GetSyntax() as AttributeSyntax;

            // Validate each type argument
            if (attribute.AttributeClass?.TypeArguments != null)
                for (var index = 0; index < attribute.AttributeClass.TypeArguments.Length; index++)
                {
                    var typeArg = attribute.AttributeClass.TypeArguments[index];
                    var dependencyType = typeArg.ToDisplayString();

                    if (!IsValidDependency(dependencyType, allRegisteredServices))
                    {
                        var location = GetDependsOnTypeArgumentLocation(attributeSyntax, index) ??
                                       classDeclaration.GetLocation();
                        var diagnostic = CreateAppropriateDeficiencyDiagnostic(classSymbol.Name, typeArg,
                            allRegisteredServices, allImplementations, diagnosticConfig, location);
                        if (diagnostic != null) context.ReportDiagnostic(diagnostic);
                    }
                }
        }
    }

    private static Diagnostic? CreateAppropriateDeficiencyDiagnostic(string serviceName,
        ITypeSymbol dependencyTypeSymbol,
        HashSet<string> allRegisteredServices,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations,
        DiagnosticConfiguration diagnosticConfig,
        Location location)
    {
        var dependencyType = dependencyTypeSymbol.ToDisplayString();
        var formattedDependencyType = FormatTypeNameForDiagnostic(dependencyTypeSymbol);

        // First check if it's a collection type - if so, extract the inner type
        var actualDependencyType = ExtractInnerTypeFromCollection(dependencyType) ?? dependencyType;

        // Check if there are ANY implementations of this interface in the project
        if (allImplementations.ContainsKey(actualDependencyType))
        {
            // Implementation exists but not registered with [Service]
            var descriptor = CreateDynamicDescriptor(ImplementationNotRegistered,
                diagnosticConfig.UnregisteredImplementationSeverity);
            return Diagnostic.Create(descriptor, location, serviceName, formattedDependencyType);
        }
        else
        {
            // No implementation found at all
            var descriptor = CreateDynamicDescriptor(NoImplementationFound, diagnosticConfig.NoImplementationSeverity);
            return Diagnostic.Create(descriptor, location, serviceName, formattedDependencyType);
        }
    }

    private static DiagnosticDescriptor CreateDynamicDescriptor(DiagnosticDescriptor baseDescriptor,
        DiagnosticSeverity severity) => new(
        baseDescriptor.Id,
        baseDescriptor.Title,
        baseDescriptor.MessageFormat,
        baseDescriptor.Category,
        severity,
        baseDescriptor.IsEnabledByDefault,
        baseDescriptor.Description,
        baseDescriptor.HelpLinkUri,
        baseDescriptor.CustomTags.ToArray());

    private static string? ExtractInnerTypeFromCollection(string dependencyType)
    {
        // Handle arrays first - simpler pattern
        if (dependencyType.EndsWith("[]")) return dependencyType.Substring(0, dependencyType.Length - 2);

        var collectionPrefixes = new[]
        {
            "System.Collections.Generic.IEnumerable<",
            "System.Collections.Generic.IList<",
            "System.Collections.Generic.ICollection<",
            "System.Collections.Generic.List<",
            "System.Collections.Generic.IReadOnlyList<",
            "System.Collections.Generic.IReadOnlyCollection<"
        };

        foreach (var prefix in collectionPrefixes)
            if (dependencyType.StartsWith(prefix) && dependencyType.EndsWith(">"))
                return dependencyType.Substring(prefix.Length, dependencyType.Length - prefix.Length - 1);

        return null;
    }

    private static List<(IFieldSymbol fieldSymbol, FieldDeclarationSyntax fieldDeclaration)>
        GetAllInjectFieldsFromHierarchy(
            INamedTypeSymbol classSymbol,
            SemanticModel semanticModel)
    {
        var result = new List<(IFieldSymbol, FieldDeclarationSyntax)>();
        var currentType = classSymbol;

        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            // Get [Inject] fields for current type
            foreach (var member in currentType.GetMembers().OfType<IFieldSymbol>())
            {
                var hasInjectAttribute = member.GetAttributes()
                    .Any(attr =>
                        attr.AttributeClass?.ToDisplayString() == "IoCTools.Abstractions.Annotations.InjectAttribute");

                if (hasInjectAttribute)
                {
                    // Find the corresponding syntax node
                    // DEFENSIVE: Only access syntax nodes from the current semantic model's syntax tree
                    var syntaxRef = member.DeclaringSyntaxReferences.FirstOrDefault();
                    if (syntaxRef != null)
                    {
                        var fieldSyntax = syntaxRef.GetSyntax();

                        // Make sure this syntax node is from the same semantic model
                        if (fieldSyntax.SyntaxTree == semanticModel.SyntaxTree &&
                            fieldSyntax is VariableDeclaratorSyntax declarator)
                        {
                            // Find the parent FieldDeclarationSyntax
                            var fieldDeclaration =
                                declarator.Ancestors().OfType<FieldDeclarationSyntax>().FirstOrDefault();
                            if (fieldDeclaration != null) result.Add((member, fieldDeclaration));
                        }
                    }
                }
            }

            currentType = currentType.BaseType;
        }

        return result;
    }

    private static Location? GetDependsOnTypeArgumentLocation(AttributeSyntax? attributeSyntax,
        int argumentIndex) =>
        // For now, return the whole attribute location
        // In a full implementation, we'd parse the generic type arguments to get precise locations
        attributeSyntax?.GetLocation();

    private static bool IsValidDependency(string dependencyType,
        HashSet<string> allRegisteredServices)
    {
        // Check if it's a registered service (direct match)
        if (allRegisteredServices.Contains(dependencyType))
            return true;

        // Check if it's a framework type
        if (FrameworkTypes.Contains(dependencyType))
            return true;

        // Check if it matches a generic framework type pattern
        foreach (var frameworkType in FrameworkTypes)
            if (frameworkType.EndsWith("<>") &&
                dependencyType.StartsWith(frameworkType.Substring(0, frameworkType.Length - 2)))
                return true;

        // For generic types, check if a matching open generic version is registered
        if (IsConstructedGenericType(dependencyType))
        {
            if (HasMatchingOpenGeneric(dependencyType, allRegisteredServices))
                return true;

            // CRITICAL FIX: Also check enhanced open generic matching
            var enhancedOpenGeneric = ConvertToEnhancedOpenGenericForm(dependencyType);
            if (enhancedOpenGeneric != null && allRegisteredServices.Contains(enhancedOpenGeneric))
                return true;
        }

        // Check if it's a collection type where the inner type is registered
        if (IsCollectionTypeWithRegisteredInnerType(dependencyType, allRegisteredServices))
            return true;

        return false;
    }

    private static bool IsCollectionTypeWithRegisteredInnerType(string dependencyType,
        HashSet<string> allRegisteredServices)
    {
        // Handle arrays first - simpler pattern
        if (dependencyType.EndsWith("[]"))
        {
            var innerType = dependencyType.Substring(0, dependencyType.Length - 2);

            // Check if the inner type is a valid dependency (this handles framework types, registered services, etc.)
            if (IsValidDependency(innerType, allRegisteredServices))
                return true;
        }

        // Handle IEnumerable<T>, IList<T>, ICollection<T>, etc.
        var collectionPrefixes = new[]
        {
            "System.Collections.Generic.IEnumerable<",
            "System.Collections.Generic.IList<",
            "System.Collections.Generic.ICollection<",
            "System.Collections.Generic.List<",
            "System.Collections.Generic.IReadOnlyList<",
            "System.Collections.Generic.IReadOnlyCollection<"
        };

        foreach (var prefix in collectionPrefixes)
            if (dependencyType.StartsWith(prefix) && dependencyType.EndsWith(">"))
            {
                // Extract the inner type
                var innerType = dependencyType.Substring(prefix.Length, dependencyType.Length - prefix.Length - 1);

                // Check if the inner type is directly registered
                if (allRegisteredServices.Contains(innerType))
                    return true;

                // CRITICAL FIX: Check if there are any implementations of the interface type registered
                // For IEnumerable<IMyInterface>, we need to check if any services implement IMyInterface
                if (HasImplementationsOfInterface(innerType, allRegisteredServices))
                    return true;

                // Recursively check if the inner type is also a valid collection
                if (IsCollectionTypeWithRegisteredInnerType(innerType, allRegisteredServices))
                    return true;
            }

        return false;
    }

    /// <summary>
    /// Checks if there are any implementations of the given interface type registered in the service collection.
    /// For example, if looking for IMyInterface, checks if any registered services implement IMyInterface.
    /// CRITICAL FIX: Enhanced to handle interface->implementation mappings for IEnumerable<T> validation.
    /// </summary>
    private static bool HasImplementationsOfInterface(string interfaceTypeName, HashSet<string> allRegisteredServices)
    {
        // First check if the interface itself is directly registered (exact match)
        if (allRegisteredServices.Contains(interfaceTypeName))
            return true;
        
        // CRITICAL FIX: Handle namespace prefix variations
        // The problem is that we're looking for "Test.INotificationProvider"
        // but allRegisteredServices contains "global::Test.INotificationProvider"
        
        // Check with global:: prefix
        var globalPrefixed = "global::" + interfaceTypeName;
        if (allRegisteredServices.Contains(globalPrefixed))
            return true;
            
        // Also check without any namespace prefixing for the interface itself
        var interfaceName = ExtractInterfaceNameFromType(interfaceTypeName);
        if (string.IsNullOrEmpty(interfaceName)) return false;
        
        // Look through all registered services for any that match this interface
        // This handles cases where the interface is registered as an interface mapping
        foreach (var registeredService in allRegisteredServices)
        {
            // Check various forms of the interface name
            if (DoesServiceMatchInterface(registeredService, interfaceTypeName, interfaceName))
                return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Checks if a registered service name matches the target interface in various forms
    /// </summary>
    private static bool DoesServiceMatchInterface(string registeredService, string interfaceTypeName, string interfaceName)
    {
        // Direct match
        if (registeredService.Equals(interfaceTypeName, StringComparison.Ordinal))
            return true;
            
        // Match with global:: prefix
        if (registeredService.Equals("global::" + interfaceTypeName, StringComparison.Ordinal))
            return true;
            
        // Match just the interface name (without namespace)
        if (registeredService.Equals(interfaceName, StringComparison.Ordinal))
            return true;
            
        // Match interface name with any namespace prefix
        if (registeredService.EndsWith("." + interfaceName))
            return true;
            
        return false;
    }
    
    /// <summary>
    /// Extracts the interface name from a fully qualified type name for matching purposes.
    /// Examples: "Test.INotificationProvider" -> "INotificationProvider"
    ///          "INotificationProvider" -> "INotificationProvider"
    /// </summary>
    private static string ExtractInterfaceNameFromType(string interfaceType)
    {
        if (string.IsNullOrEmpty(interfaceType)) return string.Empty;
        
        // Find the last dot to get the type name
        var lastDotIndex = interfaceType.LastIndexOf('.');
        if (lastDotIndex >= 0 && lastDotIndex < interfaceType.Length - 1)
        {
            return interfaceType.Substring(lastDotIndex + 1);
        }
        
        return interfaceType;
    }

    /// <summary>
    ///     Validates lifetime compatibility for a service and its dependencies
    /// </summary>
    private static void ValidateServiceLifetimes(GeneratorExecutionContext context,
        TypeDeclarationSyntax classDeclaration,
        InheritanceHierarchyDependencies hierarchyDependencies,
        HashSet<string> allRegisteredServices,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations,
        Dictionary<string, string> serviceLifetimes,
        DiagnosticConfiguration diagnosticConfig,
        SemanticModel semanticModel)
    {
        var symbol = semanticModel.GetDeclaredSymbol(classDeclaration);
        if (symbol is not INamedTypeSymbol classSymbol) return;

        // Skip all validation if lifetime validation is disabled
        if (!diagnosticConfig.LifetimeValidationEnabled) return;

        // Check if class has [ExternalService] attribute (skip validation)
        var classHasExternalServiceAttribute = classSymbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.ToDisplayString() ==
                         "IoCTools.Abstractions.Annotations.ExternalServiceAttribute");

        if (classHasExternalServiceAttribute) return;

        // Get the service lifetime for this class
        var serviceLifetime = GetServiceLifetime(classSymbol);
        if (serviceLifetime == null) return; // Not a service or no lifetime defined

        // Check if this is a background service
        var isBackgroundService = IsBackgroundService(classSymbol);

        // IOC014: Background service lifetime validation
        if (isBackgroundService && serviceLifetime != "Singleton")
        {
            // Check if SuppressLifetimeWarnings is enabled
            var backgroundServiceAttr = classSymbol.GetAttributes()
                .FirstOrDefault(attr =>
                    attr.AttributeClass?.ToDisplayString() ==
                    "IoCTools.Abstractions.Annotations.BackgroundServiceAttribute");

            var suppressWarnings = false;
            if (backgroundServiceAttr != null)
            {
                var suppressArg = backgroundServiceAttr.NamedArguments
                    .FirstOrDefault(kvp => kvp.Key == "SuppressLifetimeWarnings");
                if (suppressArg.Key != null && suppressArg.Value.Value is bool suppress) suppressWarnings = suppress;
            }

            // Only report diagnostic if not suppressed
            if (!suppressWarnings)
            {
                var descriptor = CreateDynamicDescriptor(DiagnosticDescriptors.BackgroundServiceLifetimeValidation,
                    diagnosticConfig.LifetimeValidationSeverity);
                var location = GetServiceAttributeLocation(classDeclaration, classSymbol);
                var diagnostic = Diagnostic.Create(descriptor, location,
                    classSymbol.Name, serviceLifetime);
                context.ReportDiagnostic(diagnostic);
            }
        }

        // First: Validate direct dependencies
        foreach (var dependency in hierarchyDependencies.AllDependenciesWithExternalFlag)
        {
            // Skip external dependencies
            if (dependency.IsExternal) continue;

            var dependencyType = dependency.ServiceType.ToDisplayString();

            // Check if this is a Lazy<T> dependency first
            if (IsLazyDependency(dependencyType))
            {
                ValidateLazyDependencyLifetimes(context, classDeclaration, classSymbol, serviceLifetime,
                    dependency.ServiceType, allRegisteredServices, serviceLifetimes, allImplementations,
                    diagnosticConfig);
                continue;
            }

            // Check if this is a collection dependency
            if (IsCollectionDependency(dependencyType))
            {
                ValidateCollectionDependencyLifetimes(context, classDeclaration, classSymbol, serviceLifetime,
                    dependency.ServiceType, allRegisteredServices, serviceLifetimes, allImplementations,
                    diagnosticConfig);
                continue;
            }

            // Skip framework types and unregistered dependencies
            if (!IsValidDependency(dependencyType, allRegisteredServices)) continue;

            var dependencyLifetime = GetDependencyLifetime(dependency.ServiceType, serviceLifetimes);
            if (dependencyLifetime == null) continue;

            // Validate lifetime compatibility for direct dependencies
            ValidateSpecificLifetimeRule(context, classDeclaration, classSymbol, serviceLifetime,
                dependency.ServiceType, dependencyLifetime, isBackgroundService, diagnosticConfig, allImplementations);
        }

        // Second: Validate transitive dependencies for Singleton services
        // This catches cases like Singleton -> Transient -> Scoped (should report IOC012)
        if (serviceLifetime == "Singleton")
        {
            // TODO: Implement ValidateTransitiveScopedDependencies method
            // ValidateTransitiveScopedDependencies(context, classDeclaration, classSymbol, 
            //     hierarchyDependencies.AllDependenciesWithExternalFlag, allRegisteredServices, serviceLifetimes,
            //     allImplementations, diagnosticConfig, new HashSet<string>(), 0);
        }

        // NOTE: Inheritance chain lifetime validation (IOC015) is now handled by the SourceProductionContext path
        // in ValidateInheritanceChainLifetimesForSourceProduction to avoid duplicate diagnostics.
        // This old path is kept for legacy purposes but IOC015 validation is disabled.
    }

    /// <summary>
    ///     Gets the service lifetime for a class symbol
    /// </summary>
    private static string? GetServiceLifetime(INamedTypeSymbol classSymbol)
    {
        var serviceAttribute = classSymbol.GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() ==
                                    "IoCTools.Abstractions.Annotations.ServiceAttribute");

        if (serviceAttribute == null) return null;

        // Check constructor arguments for lifetime
        if (serviceAttribute.ConstructorArguments.Length > 0)
        {
            var lifetimeValue = serviceAttribute.ConstructorArguments[0].Value;
            if (lifetimeValue != null)
            {
                // Convert enum value to string name
                var lifetimeInt = (int)lifetimeValue;
                return lifetimeInt switch
                {
                    0 => "Scoped",
                    1 => "Transient",
                    2 => "Singleton",
                    _ => "Scoped" // Default fallback
                };
            }
        }

        // Default lifetime is Scoped
        return "Scoped";
    }

    /// <summary>
    ///     Gets the lifetime for a dependency service type using the prebuilt serviceLifetimes dictionary
    /// </summary>
    private static string? GetDependencyLifetime(ITypeSymbol dependencyType,
        Dictionary<string, string> serviceLifetimes)
    {
        var dependencyTypeName = dependencyType.ToDisplayString();

        // First check for direct mapping
        if (serviceLifetimes.TryGetValue(dependencyTypeName, out var lifetime)) return lifetime;

        // If not found and this is a constructed generic type, try to find the open generic
        if (dependencyType is INamedTypeSymbol namedType && namedType.IsGenericType && !namedType.IsUnboundGenericType)
        {
            // Get the open generic type definition
            var openGenericType = namedType.ConstructedFrom.ToDisplayString();
            if (serviceLifetimes.TryGetValue(openGenericType, out var openGenericLifetime)) return openGenericLifetime;

            // CRITICAL FIX: Enhanced matching for complex generic types
            // Handle cases like IProcessor<List<string>> -> IProcessor<T>
            var enhancedOpenGeneric = ConvertToEnhancedOpenGenericForm(dependencyTypeName);
            if (enhancedOpenGeneric != null &&
                serviceLifetimes.TryGetValue(enhancedOpenGeneric, out var enhancedLifetime)) return enhancedLifetime;

            // Additional fallback: check if we can find a related service type 
            // This helps with cases where the exact dependency isn't in the dictionary
            // but we have related registrations that might match
            if (namedType.IsGenericType)
            {
                // Try to find any service with the same generic type structure
                var genericTypeName = namedType.Name;
                foreach (var kvp in serviceLifetimes)
                {
                    var serviceType = kvp.Key;
                    var serviceLifetime = kvp.Value;
                    if (serviceType.StartsWith(genericTypeName + "<") ||
                        serviceType.Contains("." + genericTypeName + "<"))
                        return serviceLifetime;
                }
            }
        }

        return null; // Unable to determine lifetime
    }

    /// <summary>
    ///     Converts a constructed generic type to enhanced open generic form for better matching
    ///     Examples:
    ///     "IProcessor<List
    ///     <string>
    ///         >" -> "IProcessor
    ///         <T>
    ///             "
    ///             "IRequestHandler
    ///             <GetUserQuery, string>
    ///                 " -> "IRequestHandler
    ///                 <T1, T2>
    ///                     "
    ///                     "ICache<Dictionary<int, string>>" -> "ICache<T>"
    /// </summary>
    private static string? ConvertToEnhancedOpenGenericForm(string constructedType)
    {
        if (!constructedType.Contains('<') || !constructedType.Contains('>'))
            return null;

        // If it's already an open generic (uses T, T1, T2, etc.), don't convert
        if (IsOpenGenericFormString(constructedType)) return constructedType;

        var angleStart = constructedType.IndexOf('<');
        var angleEnd = constructedType.LastIndexOf('>');

        if (angleStart >= 0 && angleEnd > angleStart)
        {
            var baseName = constructedType.Substring(0, angleStart);
            var typeArgsSection = constructedType.Substring(angleStart + 1, angleEnd - angleStart - 1);

            // Parse type arguments with proper bracket matching
            var typeArgs = ParseGenericTypeArguments(typeArgsSection);

            // Convert each type argument to open form (T, T1, T2, etc.)
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

    /// <summary>
    ///     Checks if a type name is already in open generic form (uses T, T1, T2, etc.)
    /// </summary>
    private static bool IsOpenGenericFormString(string typeName)
    {
        if (!typeName.Contains('<') || !typeName.Contains('>')) return false;

        var angleStart = typeName.IndexOf('<');
        var angleEnd = typeName.LastIndexOf('>');

        if (angleStart >= 0 && angleEnd > angleStart)
        {
            var typeArgsSection = typeName.Substring(angleStart + 1, angleEnd - angleStart - 1);
            var args = typeArgsSection.Split(',').Select(arg => arg.Trim()).ToArray();

            // Check if all arguments are simple type parameters (T, T1, T2, etc.)
            return args.All(arg =>
                arg == "T" || (arg.StartsWith("T") && arg.Length > 1 && arg.Substring(1).All(char.IsDigit)));
        }

        return false;
    }

    /// <summary>
    ///     Parses generic type arguments with proper bracket matching for nested generics
    /// </summary>
    private static List<string> ParseGenericTypeArguments(string typeArgsSection)
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
                // This comma is at the top level, so it separates arguments
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

    /// <summary>
    ///     Gets the lifetime for a dependency service type (legacy method)
    /// </summary>
    private static string? GetDependencyLifetime(ITypeSymbol dependencyType,
        HashSet<string> allRegisteredServices,
        IEnumerable<INamedTypeSymbol>? allServicesWithAttributes)
    {
        if (allServicesWithAttributes == null) return null;

        var dependencyTypeName = dependencyType.ToDisplayString();

        // Find the service that implements this dependency type
        foreach (var serviceSymbol in allServicesWithAttributes)
        {
            // Check if this service implements the dependency interface directly
            if (serviceSymbol.ToDisplayString() == dependencyTypeName) return GetServiceLifetime(serviceSymbol);

            // Check if this service implements the dependency interface
            var implementedInterfaces = serviceSymbol.AllInterfaces.Select(i => i.ToDisplayString());
            if (implementedInterfaces.Contains(dependencyTypeName)) return GetServiceLifetime(serviceSymbol);
        }

        return null; // Unable to determine lifetime
    }

    /// <summary>
    ///     Checks if a class inherits from BackgroundService or has the [BackgroundService] attribute
    /// </summary>
    private static bool IsBackgroundService(INamedTypeSymbol classSymbol)
    {
        // Check for [BackgroundService] attribute
        var hasBackgroundServiceAttribute = classSymbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.ToDisplayString() ==
                         "IoCTools.Abstractions.Annotations.BackgroundServiceAttribute");

        if (hasBackgroundServiceAttribute)
            return true;

        // Check inheritance from BackgroundService
        var currentType = classSymbol.BaseType;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            if (currentType.ToDisplayString() == "Microsoft.Extensions.Hosting.BackgroundService")
                return true;
            currentType = currentType.BaseType;
        }

        return false;
    }

    /// <summary>
    ///     Validates specific lifetime rules
    /// </summary>
    private static void ValidateSpecificLifetimeRule(GeneratorExecutionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        string serviceLifetime,
        ITypeSymbol dependencyType,
        string dependencyLifetime,
        bool isBackgroundService,
        DiagnosticConfiguration diagnosticConfig,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations)
    {
        var location = classDeclaration.GetLocation();
        var serviceName = classSymbol.Name;
        var dependencyName = FormatTypeNameForDiagnosticWithImplementation(dependencyType, allImplementations);

        // IOC012: Singleton → Scoped dependency errors
        if (serviceLifetime == "Singleton" && dependencyLifetime == "Scoped")
        {
            var descriptor = CreateDynamicDescriptor(DiagnosticDescriptors.SingletonDependsOnScoped,
                diagnosticConfig.LifetimeValidationSeverity);
            var diagnostic = Diagnostic.Create(descriptor, location, serviceName, dependencyName);
            context.ReportDiagnostic(diagnostic);
        }

        // IOC013: Singleton → Transient dependency warnings
        if (serviceLifetime == "Singleton" && dependencyLifetime == "Transient")
        {
            var descriptor = CreateDynamicDescriptor(DiagnosticDescriptors.SingletonDependsOnTransient,
                DiagnosticSeverity.Warning); // Always warning for this case
            var diagnostic = Diagnostic.Create(descriptor, location, serviceName, dependencyName);
            context.ReportDiagnostic(diagnostic);
        }
    }

    /// <summary>
    ///     Validates lifetime compatibility across inheritance chains using comprehensive analysis
    /// </summary>
    private static void ValidateInheritanceChainLifetimes(GeneratorExecutionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        InheritanceHierarchyDependencies hierarchyDependencies,
        Dictionary<string, string> serviceLifetimes,
        DiagnosticConfiguration diagnosticConfig)
    {
        var serviceLifetime = GetServiceLifetime(classSymbol);
        if (serviceLifetime == null) return; // Not a service

        // CRITICAL FIX: Check for direct inheritance lifetime violations first
        // This handles cases like ConcreteService (Singleton) : BaseService<string> (Scoped)
        if (serviceLifetime == "Singleton")
        {
            var currentType = classSymbol.BaseType;
            while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
            {
                var baseServiceLifetime = GetDependencyLifetime(currentType, serviceLifetimes);

                if (baseServiceLifetime == "Scoped")
                {
                    // Direct inheritance violation: Singleton inherits from Scoped
                    var descriptor = CreateDynamicDescriptor(DiagnosticDescriptors.InheritanceChainLifetimeValidation,
                        diagnosticConfig.LifetimeValidationSeverity);
                    var diagnostic = Diagnostic.Create(descriptor, classDeclaration.GetLocation(),
                        classSymbol.Name, serviceLifetime, baseServiceLifetime);
                    context.ReportDiagnostic(diagnostic);
                    return; // Report only the first violation
                }

                currentType = currentType.BaseType;
            }
        }

        // Use comprehensive inheritance chain lifetime analysis for dependency-based violations
        var lifetimeAnalysis = hierarchyDependencies.GetLifetimeAnalysis(serviceLifetimes);
        var compatibilityResult = lifetimeAnalysis.AnalyzeLifetimeCompatibility(serviceLifetime);

        if (!compatibilityResult.HasViolations) return;

        // Get the most significant violations to report
        var violationsByType = compatibilityResult.GetViolationsByType();

        // Report IOC015 for inheritance chain violations
        if (violationsByType.ContainsKey(LifetimeViolationType.SingletonDependsOnScoped))
        {
            var scopedViolations = violationsByType[LifetimeViolationType.SingletonDependsOnScoped];
            var firstViolation = scopedViolations.First();

            var descriptor = CreateDynamicDescriptor(DiagnosticDescriptors.InheritanceChainLifetimeValidation,
                diagnosticConfig.LifetimeValidationSeverity);
            var diagnostic = Diagnostic.Create(descriptor, classDeclaration.GetLocation(),
                classSymbol.Name, serviceLifetime, firstViolation.DependencyLifetime);
            context.ReportDiagnostic(diagnostic);
        }
        else if (violationsByType.ContainsKey(LifetimeViolationType.SingletonDependsOnTransient))
        {
            var transientViolations = violationsByType[LifetimeViolationType.SingletonDependsOnTransient];
            var firstViolation = transientViolations.First();

            var descriptor = CreateDynamicDescriptor(DiagnosticDescriptors.InheritanceChainLifetimeValidation,
                DiagnosticSeverity.Warning); // Transient violations are warnings
            var diagnostic = Diagnostic.Create(descriptor, classDeclaration.GetLocation(),
                classSymbol.Name, serviceLifetime, firstViolation.DependencyLifetime);
            context.ReportDiagnostic(diagnostic);
        }

        // Additionally report detailed violations for complex inheritance chains
        ReportDetailedInheritanceLifetimeViolations(context, classDeclaration, classSymbol,
            compatibilityResult, diagnosticConfig);
    }

    /// <summary>
    ///     Reports detailed lifetime violations for complex inheritance scenarios
    /// </summary>
    private static void ReportDetailedInheritanceLifetimeViolations(GeneratorExecutionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        InheritanceLifetimeCompatibilityResult compatibilityResult,
        DiagnosticConfiguration diagnosticConfig)
    {
        // For deep inheritance chains or complex scenarios, we might want to report 
        // additional specific diagnostics. For now, the main IOC015 diagnostic covers most cases.

        // Future enhancement: Could add specific diagnostics for different inheritance levels
        // or provide more detailed messages about which base class introduced the violation
    }

    /// <summary>
    ///     Checks if a type string represents a constructed generic type (e.g., "IRepository<string>")
    /// </summary>
    private static bool IsConstructedGenericType(string typeName) =>
        typeName.Contains('<') && typeName.Contains('>') && !typeName.EndsWith("<>");

    /// <summary>
    ///     Extracts the open generic type from a constructed generic type string.
    ///     For example: "TestNamespace.IService&lt;string&gt;" -> "TestNamespace.IService&lt;T&gt;"
    /// </summary>
    private static string? ExtractOpenGenericTypeFromConstructedType(string constructedType)
    {
        if (!IsConstructedGenericType(constructedType))
            return null;

        var angleIndex = constructedType.IndexOf('<');
        if (angleIndex == -1) return null;

        var baseName = constructedType.Substring(0, angleIndex);
        var typeParamsSection =
            constructedType.Substring(angleIndex + 1, constructedType.LastIndexOf('>') - angleIndex - 1);
        var typeParamCount = typeParamsSection.Split(',').Length;

        // Create the open generic type with T parameters
        var typeParams = new List<string>();
        for (var i = 0; i < typeParamCount; i++) typeParams.Add("T");

        return $"{baseName}<{string.Join(", ", typeParams)}>";
    }

    /// <summary>
    ///     Checks if any registered open generic matches the constructed type pattern.
    ///     This handles matching IRepository<string> with IRepository<T> or similar patterns.
    /// </summary>
    private static bool HasMatchingOpenGeneric(string constructedType,
        HashSet<string> allRegisteredServices)
    {
        if (!IsConstructedGenericType(constructedType))
            return false;

        var angleIndex = constructedType.IndexOf('<');
        if (angleIndex == -1)
            return false;

        var baseName = constructedType.Substring(0, angleIndex);

        // Count the number of type parameters by counting commas inside the angle brackets
        var typeParamsSection =
            constructedType.Substring(angleIndex + 1, constructedType.LastIndexOf('>') - angleIndex - 1);
        var typeParamCount = typeParamsSection.Split(',').Length;

        // Check if any registered service matches this pattern
        foreach (var registeredService in allRegisteredServices)
            if (registeredService.StartsWith(baseName + "<") && registeredService.EndsWith(">"))
            {
                // Check if the parameter count matches by counting commas
                var registeredAngleIndex = registeredService.IndexOf('<');
                if (registeredAngleIndex != -1)
                {
                    var registeredTypeParamsSection = registeredService.Substring(registeredAngleIndex + 1,
                        registeredService.LastIndexOf('>') - registeredAngleIndex - 1);
                    var registeredTypeParamCount = registeredTypeParamsSection.Split(',').Length;

                    if (registeredTypeParamCount == typeParamCount)
                        return true;
                }
            }

        return false;
    }

    /// <summary>
    ///     Formats type names for diagnostic messages to make them more readable.
    ///     Removes namespace prefixes to make messages more readable.
    /// </summary>
    private static string FormatTypeNameForDiagnostic(ITypeSymbol typeSymbol)
    {
        var displayString = typeSymbol.ToDisplayString();

        // For generic types like "Test.IRepository<Test.User>", simplify to "IRepository<User>"
        if (typeSymbol is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var typeName = namedType.Name;
            var typeArgs = namedType.TypeArguments
                .Select(arg => arg.Name) // Use just the name without namespace
                .ToArray();

            if (typeArgs.Length > 0) return $"{typeName}<{string.Join(", ", typeArgs)}>";
        }

        // For non-generic types, just use the name without namespace
        return typeSymbol.Name;
    }

    /// <summary>
    ///     Enhanced formatter that includes implementation names for better diagnostics
    /// </summary>
    private static string FormatTypeNameForDiagnosticWithImplementation(ITypeSymbol typeSymbol,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations)
    {
        var interfaceName = FormatTypeNameForDiagnostic(typeSymbol);
        var fullTypeName = typeSymbol.ToDisplayString();

        // Try to find the implementing class - first try exact match
        if (allImplementations.TryGetValue(fullTypeName, out var implementations) && implementations.Any())
        {
            var implementation = implementations.First();
            var implementationName = implementation.Name;
            return $"{implementationName}";
        }

        // For generic types, try to find by open generic version
        if (typeSymbol is INamedTypeSymbol namedType && namedType.IsGenericType && !namedType.IsUnboundGenericType)
        {
            // Get the open generic type definition
            var openGenericType = namedType.ConstructedFrom.ToDisplayString();
            if (allImplementations.TryGetValue(openGenericType, out var openGenericImplementations) &&
                openGenericImplementations.Any())
            {
                var implementation = openGenericImplementations.First();
                var implementationName = implementation.Name;
                return $"{implementationName}";
            }

            // CRITICAL FIX: Enhanced matching for complex generic types
            // Try to find implementations that implement this interface by checking all implementations
            foreach (var kvp in allImplementations)
            {
                var interfaceType = kvp.Key;
                var implementationList = kvp.Value;

                foreach (var impl in implementationList)
                {
                    // Check if this implementation implements the interface we're looking for
                    var implementedInterfaces = impl.AllInterfaces;
                    foreach (var implementedInterface in implementedInterfaces)
                        // Check if this implemented interface matches our target interface
                        if (implementedInterface.ConstructedFrom.Equals(namedType.ConstructedFrom,
                                SymbolEqualityComparer.Default))
                            return $"{impl.Name}";
                }
            }
        }

        return interfaceName;
    }

    /// <summary>
    ///     Checks if a dependency type is a collection type (IEnumerable, IList, array, etc.)
    /// </summary>
    private static bool IsCollectionDependency(string dependencyType)
    {
        // Handle arrays
        if (dependencyType.EndsWith("[]"))
            return true;

        // Handle collection interfaces
        var collectionPrefixes = new[]
        {
            "System.Collections.Generic.IEnumerable<",
            "System.Collections.Generic.IList<",
            "System.Collections.Generic.ICollection<",
            "System.Collections.Generic.List<",
            "System.Collections.Generic.IReadOnlyList<",
            "System.Collections.Generic.IReadOnlyCollection<"
        };

        return collectionPrefixes.Any(prefix => dependencyType.StartsWith(prefix) && dependencyType.EndsWith(">"));
    }

    /// <summary>
    ///     Validates lifetime compatibility for collection dependencies
    /// </summary>
    private static void ValidateCollectionDependencyLifetimes(GeneratorExecutionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        string serviceLifetime,
        ITypeSymbol collectionDependencyType,
        HashSet<string> allRegisteredServices,
        Dictionary<string, string> serviceLifetimes,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations,
        DiagnosticConfiguration diagnosticConfig)
    {
        var dependencyTypeString = collectionDependencyType.ToDisplayString();
        var innerType = ExtractInnerTypeFromCollection(dependencyTypeString);

        if (innerType == null) return;

        // Strategy 1: Find direct implementations of the inner interface type
        var foundImplementations = new List<INamedTypeSymbol>();

        // Look through all implementations to find services that implement the inner interface
        foreach (var kvp in allImplementations)
        {
            var interfaceType = kvp.Key;
            var implementations = kvp.Value;

            // Check if this interface type matches our inner type
            if (interfaceType == innerType)
            {
                foundImplementations.AddRange(implementations);
                continue;
            }

            // For generic interfaces, also check if any implementations implement the constructed generic
            if (IsConstructedGenericType(innerType))
            {
                var innerTypePattern = GetGenericTypePattern(innerType);
                var interfacePattern = GetGenericTypePattern(interfaceType);

                if (innerTypePattern != null && interfacePattern != null && innerTypePattern == interfacePattern)
                    foundImplementations.AddRange(implementations);
            }
        }

        // Strategy 2: Enhanced resolution for registered services that match the inner type
        if (allRegisteredServices.Contains(innerType))
            // Try to find the service symbol from serviceLifetimes
            if (serviceLifetimes.TryGetValue(innerType, out var innerLifetime))
            {
                // Report lifetime violation directly using string-based approach
                ReportCollectionLifetimeViolation(context, classDeclaration, classSymbol, serviceLifetime,
                    innerType, innerLifetime, diagnosticConfig);
                return; // Early return to avoid duplicate diagnostics
            }

        // Strategy 2.1: CRITICAL - Enhanced generic matching for inner types
        if (IsConstructedGenericType(innerType))
        {
            var enhancedOpenGeneric = ConvertToEnhancedOpenGenericForm(innerType);
            if (enhancedOpenGeneric != null &&
                serviceLifetimes.TryGetValue(enhancedOpenGeneric, out var enhancedLifetime))
            {
                ReportCollectionLifetimeViolation(context, classDeclaration, classSymbol, serviceLifetime,
                    innerType, enhancedLifetime, diagnosticConfig);
                return; // Early return to avoid duplicate diagnostics
            }

            // Also check allImplementations for the enhanced form
            if (enhancedOpenGeneric != null &&
                allImplementations.TryGetValue(enhancedOpenGeneric, out var enhancedImplementations))
                foreach (var implementation in enhancedImplementations)
                {
                    var implementationLifetime = GetServiceLifetime(implementation);
                    if (implementationLifetime == null) continue;

                    if (ShouldReportLifetimeViolation(serviceLifetime, implementationLifetime))
                    {
                        ReportCollectionLifetimeViolation(context, classDeclaration, classSymbol, serviceLifetime,
                            implementation.Name, implementationLifetime, diagnosticConfig);
                        return; // Report only first violation to avoid spam
                    }
                }
        }

        // Strategy 2.5: CRITICAL - Look for implementations using interface mapping approach
        // This handles cases like IEnumerable<IScopedService> where IScopedService is implemented by ScopedServiceImpl
        var interfaceKey = innerType; // e.g., "TestNamespace.IScopedService"
        if (allImplementations.TryGetValue(interfaceKey, out var interfaceImplementations))
            foreach (var implementation in interfaceImplementations)
            {
                var implementationLifetime = GetServiceLifetime(implementation);
                if (implementationLifetime == null) continue;

                // This is the core fix - validate the implementation's lifetime against the consumer
                if (ShouldReportLifetimeViolation(serviceLifetime, implementationLifetime))
                {
                    ReportCollectionLifetimeViolation(context, classDeclaration, classSymbol, serviceLifetime,
                        implementation.Name, implementationLifetime, diagnosticConfig);
                    return; // Report only first violation to avoid spam
                }
            }

        // Strategy 2.6: FALLBACK - Search through all services to find those that implement the interface
        // This is a more expensive but comprehensive approach
        foreach (var kvp in allImplementations)
        {
            var interfaceType = kvp.Key;
            var implementations = kvp.Value;

            // Skip if we already processed this exact match above
            if (interfaceType == interfaceKey) continue;

            // Look for implementations that implement our target interface
            foreach (var implementation in implementations)
            {
                // Check if this implementation implements the interface we're looking for
                var implementedInterfaces = implementation.AllInterfaces.Select(i => i.ToDisplayString());
                if (!implementedInterfaces.Contains(innerType)) continue;

                var implementationLifetime = GetServiceLifetime(implementation);
                if (implementationLifetime == null) continue;

                // Validate the implementation's lifetime against the consumer
                if (ShouldReportLifetimeViolation(serviceLifetime, implementationLifetime))
                {
                    ReportCollectionLifetimeViolation(context, classDeclaration, classSymbol, serviceLifetime,
                        implementation.Name, implementationLifetime, diagnosticConfig);
                    return; // Report only first violation to avoid spam
                }
            }
        }

        // Strategy 3: Validate each found implementation's lifetime against the consumer
        foreach (var implementation in foundImplementations)
        {
            var implementationLifetime = GetServiceLifetime(implementation);
            if (implementationLifetime == null) continue;

            // Validate lifetime compatibility for this implementation
            ValidateSpecificLifetimeRule(context, classDeclaration, classSymbol, serviceLifetime,
                implementation, implementationLifetime, false, diagnosticConfig, allImplementations);
        }

        // Strategy 4: Handle generic collection types by checking service registrations
        if (IsConstructedGenericType(innerType))
            ValidateGenericCollectionLifetimes(context, classDeclaration, classSymbol, serviceLifetime,
                innerType, allRegisteredServices, serviceLifetimes, diagnosticConfig);
    }

    /// <summary>
    ///     Determines if a lifetime violation should be reported based on service and dependency lifetimes
    /// </summary>
    private static bool ShouldReportLifetimeViolation(string serviceLifetime,
        string dependencyLifetime)
    {
        // Singleton services cannot depend on Scoped or Transient services
        if (serviceLifetime == "Singleton") return dependencyLifetime == "Scoped" || dependencyLifetime == "Transient";

        // Other combinations are generally valid
        return false;
    }

    /// <summary>
    ///     Reports a collection lifetime violation using string-based type information
    /// </summary>
    private static void ReportCollectionLifetimeViolation(GeneratorExecutionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        string serviceLifetime,
        string dependencyTypeName,
        string dependencyLifetime,
        DiagnosticConfiguration diagnosticConfig)
    {
        var location = classDeclaration.GetLocation();
        var serviceName = classSymbol.Name;
        var dependencyName = FormatTypeNameForDiagnosticFromString(dependencyTypeName);

        // IOC012: Singleton → Scoped dependency errors
        if (serviceLifetime == "Singleton" && dependencyLifetime == "Scoped")
        {
            var descriptor = CreateDynamicDescriptor(DiagnosticDescriptors.SingletonDependsOnScoped,
                diagnosticConfig.LifetimeValidationSeverity);
            var diagnostic = Diagnostic.Create(descriptor, location, serviceName, dependencyName);
            context.ReportDiagnostic(diagnostic);
        }

        // IOC013: Singleton → Transient dependency warnings
        if (serviceLifetime == "Singleton" && dependencyLifetime == "Transient")
        {
            var descriptor = CreateDynamicDescriptor(DiagnosticDescriptors.SingletonDependsOnTransient,
                DiagnosticSeverity.Warning); // Always warning for this case
            var diagnostic = Diagnostic.Create(descriptor, location, serviceName, dependencyName);
            context.ReportDiagnostic(diagnostic);
        }
    }

    /// <summary>
    ///     Validates generic collection lifetimes by checking service registrations
    /// </summary>
    private static void ValidateGenericCollectionLifetimes(GeneratorExecutionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        string serviceLifetime,
        string innerType,
        HashSet<string> allRegisteredServices,
        Dictionary<string, string> serviceLifetimes,
        DiagnosticConfiguration diagnosticConfig)
    {
        var angleIndex = innerType.IndexOf('<');
        if (angleIndex == -1) return;

        var baseName = innerType.Substring(0, angleIndex);
        var typeParamsSection = innerType.Substring(angleIndex + 1, innerType.LastIndexOf('>') - angleIndex - 1);
        var typeParamCount = typeParamsSection.Split(',').Length;

        // Look for matching open generic services in registrations
        foreach (var registeredService in allRegisteredServices)
        {
            if (!registeredService.StartsWith(baseName + "<") || !registeredService.EndsWith(">"))
                continue;

            var registeredAngleIndex = registeredService.IndexOf('<');
            if (registeredAngleIndex == -1) continue;

            var registeredTypeParamsSection = registeredService.Substring(registeredAngleIndex + 1,
                registeredService.LastIndexOf('>') - registeredAngleIndex - 1);
            var registeredTypeParamCount = registeredTypeParamsSection.Split(',').Length;

            if (registeredTypeParamCount != typeParamCount) continue;

            // Found a matching registration - check its lifetime
            if (serviceLifetimes.TryGetValue(registeredService, out var registeredLifetime))
            {
                ReportCollectionLifetimeViolation(context, classDeclaration, classSymbol, serviceLifetime,
                    registeredService, registeredLifetime, diagnosticConfig);
                return; // Report only first violation to avoid spam
            }
        }
    }

    /// <summary>
    ///     Gets a generic type pattern for comparison (e.g., "IRepository<T>")
    /// </summary>
    private static string? GetGenericTypePattern(string typeName)
    {
        if (!IsConstructedGenericType(typeName)) return null;

        var angleIndex = typeName.IndexOf('<');
        if (angleIndex == -1) return null;

        var baseName = typeName.Substring(0, angleIndex);
        var typeParamsSection = typeName.Substring(angleIndex + 1, typeName.LastIndexOf('>') - angleIndex - 1);
        var typeParamCount = typeParamsSection.Split(',').Length;

        // Create pattern like "IRepository<T>"
        var typeParams = new string[typeParamCount];
        for (var i = 0; i < typeParamCount; i++) typeParams[i] = "T";

        return $"{baseName}<{string.Join(", ", typeParams)}>";
    }

    /// <summary>
    ///     Formats a type name string for diagnostic messages
    /// </summary>
    private static string FormatTypeNameForDiagnosticFromString(string typeName)
    {
        // Remove namespace prefixes for readability
        var lastDot = typeName.LastIndexOf('.');
        if (lastDot >= 0) typeName = typeName.Substring(lastDot + 1);

        return typeName;
    }


    /// <summary>
    ///     Checks if a dependency type is a Lazy&lt;T&gt; type
    /// </summary>
    private static bool IsLazyDependency(string dependencyType) =>
        dependencyType.StartsWith("System.Lazy<") && dependencyType.EndsWith(">");

    /// <summary>
    ///     Validates lifetime compatibility for Lazy&lt;T&gt; dependencies by unwrapping the inner type
    /// </summary>
    private static void ValidateLazyDependencyLifetimes(GeneratorExecutionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        string serviceLifetime,
        ITypeSymbol lazyDependencyType,
        HashSet<string> allRegisteredServices,
        Dictionary<string, string> serviceLifetimes,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations,
        DiagnosticConfiguration diagnosticConfig)
    {
        var dependencyTypeString = lazyDependencyType.ToDisplayString();

        // Extract the inner type from Lazy<T>
        if (dependencyTypeString.StartsWith("System.Lazy<") && dependencyTypeString.EndsWith(">"))
        {
            var innerTypeString = dependencyTypeString.Substring("System.Lazy<".Length,
                dependencyTypeString.Length - "System.Lazy<".Length - 1);

            // Check if the inner type is a collection
            if (IsCollectionDependency(innerTypeString))
                // Create a mock dependency for the inner collection type and validate it
                ValidateCollectionDependencyLifetimesFromString(context, classDeclaration, classSymbol, serviceLifetime,
                    innerTypeString, allRegisteredServices, serviceLifetimes, allImplementations, diagnosticConfig);
            else
                // Validate the inner type directly
                ValidateSingleDependencyFromString(context, classDeclaration, classSymbol, serviceLifetime,
                    innerTypeString, allRegisteredServices, serviceLifetimes, allImplementations, diagnosticConfig);
        }
    }

    /// <summary>
    ///     Validates a single dependency from a type string
    /// </summary>
    private static void ValidateSingleDependencyFromString(GeneratorExecutionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        string serviceLifetime,
        string dependencyTypeString,
        HashSet<string> allRegisteredServices,
        Dictionary<string, string> serviceLifetimes,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations,
        DiagnosticConfiguration diagnosticConfig)
    {
        // Skip framework types and unregistered dependencies
        if (!IsValidDependency(dependencyTypeString, allRegisteredServices)) return;

        // Use the same generic type resolution logic as the main GetDependencyLifetime method
        string? dependencyLifetime = null;

        // First check for direct mapping
        if (serviceLifetimes.TryGetValue(dependencyTypeString, out var directLifetime))
        {
            dependencyLifetime = directLifetime;
        }
        // If not found and this looks like a constructed generic type, try to find the open generic
        else if (IsConstructedGenericType(dependencyTypeString))
        {
            var openGenericType = ExtractOpenGenericTypeFromConstructedType(dependencyTypeString);
            if (openGenericType != null && serviceLifetimes.TryGetValue(openGenericType, out var openGenericLifetime))
                dependencyLifetime = openGenericLifetime;

            // CRITICAL: Try enhanced matching if standard didn't work
            if (dependencyLifetime == null)
            {
                var enhancedOpenGeneric = ConvertToEnhancedOpenGenericForm(dependencyTypeString);
                if (enhancedOpenGeneric != null &&
                    serviceLifetimes.TryGetValue(enhancedOpenGeneric, out var enhancedLifetime))
                    dependencyLifetime = enhancedLifetime;
            }
        }

        if (dependencyLifetime != null)
        {
            // Use string-based validation for single dependencies
            ReportCollectionLifetimeViolation(context, classDeclaration, classSymbol, serviceLifetime,
                dependencyTypeString, dependencyLifetime, diagnosticConfig);

            // Also find implementations and validate those for completeness
            if (allImplementations.TryGetValue(dependencyTypeString, out var implementations))
                foreach (var implementation in implementations)
                {
                    var implementationLifetime = GetServiceLifetime(implementation);
                    if (implementationLifetime == null) continue;

                    ValidateSpecificLifetimeRule(context, classDeclaration, classSymbol, serviceLifetime,
                        implementation, implementationLifetime, false, diagnosticConfig, allImplementations);
                }

            // Also check for open generic implementations
            if (IsConstructedGenericType(dependencyTypeString))
            {
                var foundImplementations = FindOpenGenericImplementations(dependencyTypeString, allImplementations);
                foreach (var implementation in foundImplementations)
                {
                    var implementationLifetime = GetServiceLifetime(implementation);
                    if (implementationLifetime == null) continue;

                    ValidateSpecificLifetimeRule(context, classDeclaration, classSymbol, serviceLifetime,
                        implementation, implementationLifetime, false, diagnosticConfig, allImplementations);
                }
            }
        }
    }

    /// <summary>
    ///     Validates collection dependency lifetimes from a type string
    /// </summary>
    private static void ValidateCollectionDependencyLifetimesFromString(GeneratorExecutionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        string serviceLifetime,
        string collectionDependencyTypeString,
        HashSet<string> allRegisteredServices,
        Dictionary<string, string> serviceLifetimes,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations,
        DiagnosticConfiguration diagnosticConfig)
    {
        var innerType = ExtractInnerTypeFromCollection(collectionDependencyTypeString);
        if (innerType == null) return;

        ValidateSingleDependencyFromString(context, classDeclaration, classSymbol, serviceLifetime,
            innerType, allRegisteredServices, serviceLifetimes, allImplementations, diagnosticConfig);
    }

    /// <summary>
    ///     Finds open generic implementations that match a constructed generic type
    /// </summary>
    private static List<INamedTypeSymbol> FindOpenGenericImplementations(string constructedType,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations)
    {
        var foundImplementations = new List<INamedTypeSymbol>();

        if (!IsConstructedGenericType(constructedType)) return foundImplementations;

        var angleIndex = constructedType.IndexOf('<');
        if (angleIndex == -1) return foundImplementations;

        var baseName = constructedType.Substring(0, angleIndex);
        var typeParamsSection =
            constructedType.Substring(angleIndex + 1, constructedType.LastIndexOf('>') - angleIndex - 1);
        var typeParamCount = typeParamsSection.Split(',').Length;

        // Look for matching open generic implementations
        foreach (var kvp in allImplementations)
        {
            var registeredType = kvp.Key;
            if (registeredType.StartsWith(baseName + "<") && registeredType.EndsWith(">"))
            {
                var registeredAngleIndex = registeredType.IndexOf('<');
                if (registeredAngleIndex != -1)
                {
                    var registeredTypeParamsSection = registeredType.Substring(registeredAngleIndex + 1,
                        registeredType.LastIndexOf('>') - registeredAngleIndex - 1);
                    var registeredTypeParamCount = registeredTypeParamsSection.Split(',').Length;

                    if (registeredTypeParamCount == typeParamCount) foundImplementations.AddRange(kvp.Value);
                }
            }
        }

        return foundImplementations;
    }

    /// <summary>
    ///     Validates transitive Scoped dependencies for Singleton services.
    ///     Looks specifically for cases where a Singleton indirectly depends on Scoped services.
    /// </summary>
    private static void ValidateTransitiveScopedDependencies(GeneratorExecutionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source, bool IsExternal)> dependencies,
        HashSet<string> allRegisteredServices,
        Dictionary<string, string> serviceLifetimes,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations,
        DiagnosticConfiguration diagnosticConfig,
        HashSet<string> visitedServices,
        int depth)
    {
        // Avoid infinite recursion and limit depth to prevent performance issues
        if (depth > 10) return;

        var currentServiceKey = classSymbol.ToDisplayString();
        if (visitedServices.Contains(currentServiceKey)) return;
        visitedServices.Add(currentServiceKey);

        foreach (var dependency in dependencies)
        {
            // Skip external dependencies
            if (dependency.IsExternal) continue;

            var dependencyType = dependency.ServiceType.ToDisplayString();

            // Skip framework types, collections, and unregistered dependencies
            if (!IsValidDependency(dependencyType, allRegisteredServices) ||
                IsCollectionDependency(dependencyType) ||
                IsLazyDependency(dependencyType))
                continue;

            // Get the dependency's lifetime
            var dependencyLifetime = GetDependencyLifetime(dependency.ServiceType, serviceLifetimes);
            if (dependencyLifetime == null) continue;

            // If we found a direct Scoped dependency, report it
            if (dependencyLifetime == "Scoped")
            {
                var location = classDeclaration.GetLocation();
                var serviceName = classSymbol.Name;
                var dependencyName =
                    FormatTypeNameForDiagnosticWithImplementation(dependency.ServiceType, allImplementations);

                var descriptor = CreateDynamicDescriptor(DiagnosticDescriptors.SingletonDependsOnScoped,
                    diagnosticConfig.LifetimeValidationSeverity);
                var diagnostic = Diagnostic.Create(descriptor, location, serviceName, dependencyName);
                context.ReportDiagnostic(diagnostic);

                // Found a violation, stop here to avoid spam
                visitedServices.Remove(currentServiceKey);
                return;
            }

            // If this is a Transient dependency, recursively check its dependencies for Scoped ones
            if (dependencyLifetime == "Transient")
            {
                var dependencySymbol = GetDependencySymbol(dependency.ServiceType, allImplementations);
                if (dependencySymbol != null)
                    try
                    {
                        var syntaxRef = dependencySymbol.DeclaringSyntaxReferences.FirstOrDefault();
                        if (syntaxRef != null)
                        {
                            var semanticModel = context.Compilation.GetSemanticModel(syntaxRef.SyntaxTree);
                            var hierarchyDependencies =
                                DependencyAnalyzer.GetInheritanceHierarchyDependenciesForDiagnostics(dependencySymbol,
                                    semanticModel, allRegisteredServices, allImplementations);
                            // Recursively validate transitive dependencies
                            // TODO: Re-implement recursive validation
                            // ValidateTransitiveScopedDependencies(context, classDeclaration, classSymbol, 
                            //     hierarchyDependencies.AllDependenciesWithExternalFlag, allRegisteredServices, serviceLifetimes,
                            //     allImplementations, diagnosticConfig, visitedServices, depth + 1);
                        }
                    }
                    catch
                    {
                        // If we can't analyze this dependency, skip it
                    }
            }
        }

        visitedServices.Remove(currentServiceKey);
    }

    /// <summary>
    ///     Gets the symbol for a dependency type from the implementations dictionary
    /// </summary>
    private static INamedTypeSymbol? GetDependencySymbol(ITypeSymbol dependencyType,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations)
    {
        var dependencyTypeName = dependencyType.ToDisplayString();

        // First try exact match
        if (allImplementations.TryGetValue(dependencyTypeName, out var implementations) && implementations.Any())
            return implementations.First();

        // For generic types, try open generic version
        if (dependencyType is INamedTypeSymbol namedType && namedType.IsGenericType && !namedType.IsUnboundGenericType)
        {
            var openGenericType = namedType.ConstructedFrom.ToDisplayString();
            if (allImplementations.TryGetValue(openGenericType, out var openGenericImplementations) &&
                openGenericImplementations.Any()) return openGenericImplementations.First();
        }

        return null;
    }

    /// <summary>
    ///     Gets transitive dependencies for a service symbol
    /// </summary>
    private static List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source, bool IsExternal)>
        GetTransitiveDependenciesForService(
            INamedTypeSymbol serviceSymbol,
            Compilation compilation,
            HashSet<string> allRegisteredServices)
    {
        try
        {
            var syntaxRef = serviceSymbol.DeclaringSyntaxReferences.FirstOrDefault();
            if (syntaxRef == null) return new List<(ITypeSymbol, string, DependencySource, bool)>();

            var semanticModel = compilation.GetSemanticModel(syntaxRef.SyntaxTree);
            var hierarchyDependencies =
                DependencyAnalyzer.GetInheritanceHierarchyDependenciesForDiagnostics(serviceSymbol, semanticModel);

            return hierarchyDependencies.AllDependenciesWithExternalFlag;
        }
        catch
        {
            // If we can't get dependencies for any reason, return empty list
            return new List<(ITypeSymbol, string, DependencySource, bool)>();
        }
    }

    /// <summary>
    ///     Gets all dependencies for a service including [Inject] fields and [DependsOn] attributes
    /// </summary>
    private static List<string> GetAllDependenciesForService(INamedTypeSymbol serviceSymbol,
        Compilation compilation)
    {
        var dependencies = new HashSet<string>();
        var semanticModel = compilation.GetSemanticModel(serviceSymbol.DeclaringSyntaxReferences.First().SyntaxTree);

        // Get hierarchy dependencies using the existing analyzer
        var hierarchyDependencies =
            DependencyAnalyzer.GetInheritanceHierarchyDependenciesForDiagnostics(serviceSymbol, semanticModel);

        // Extract dependency types from all dependencies (including external ones for circular detection)
        foreach (var dependency in hierarchyDependencies.AllDependenciesWithExternalFlag)
        {
            // Skip external dependencies for circular dependency detection
            if (dependency.IsExternal) continue;

            var dependencyType = dependency.ServiceType.ToDisplayString();
            dependencies.Add(dependencyType);
        }

        return dependencies.ToList();
    }

    /// <summary>
    ///     Checks if a type is a collection type that should not participate in circular dependency detection
    /// </summary>
    private static bool IsCollectionType(string typeName)
    {
        // Handle arrays
        if (typeName.EndsWith("[]"))
            return true;

        // Handle collection interfaces and classes
        var collectionPrefixes = new[]
        {
            "System.Collections.Generic.IEnumerable<",
            "System.Collections.Generic.IList<",
            "System.Collections.Generic.ICollection<",
            "System.Collections.Generic.List<",
            "System.Collections.Generic.IReadOnlyList<",
            "System.Collections.Generic.IReadOnlyCollection<"
        };

        return collectionPrefixes.Any(prefix => typeName.StartsWith(prefix) && typeName.EndsWith(">"));
    }

    /// <summary>
    ///     Checks if a type is a framework generic type
    /// </summary>
    private static bool IsFrameworkGenericType(string typeName)
    {
        foreach (var frameworkType in FrameworkTypes)
            if (frameworkType.EndsWith("<>") &&
                typeName.StartsWith(frameworkType.Substring(0, frameworkType.Length - 2)))
                return true;
        return false;
    }

    /// <summary>
    ///     Extracts the service name from a dependency type for circular dependency detection
    ///     We need to map interface dependencies to their actual service implementations
    /// </summary>
    private static string? ExtractServiceNameFromType(string dependencyType)
    {
        // Remove namespace prefixes first
        var typeName = dependencyType;
        var lastDotIndex = typeName.LastIndexOf('.');
        if (lastDotIndex >= 0) typeName = typeName.Substring(lastDotIndex + 1);

        // Handle generic types by removing type parameters
        if (typeName.Contains('<'))
        {
            var genericIndex = typeName.IndexOf('<');
            typeName = typeName.Substring(0, genericIndex);
        }

        // For interfaces like IServiceA, we expect the implementation to be ServiceA
        // For concrete types, use the type name directly
        if (typeName.StartsWith("I") && typeName.Length > 1 && char.IsUpper(typeName[1]))
            // Remove the 'I' prefix for interface types
            return typeName.Substring(1);

        return typeName;
    }

    /// <summary>
    ///     Gets the most precise location for a service attribute diagnostic, pointing to the attribute or lifetime parameter
    /// </summary>
    private static Location GetServiceAttributeLocation(TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        // Try to find the [Service] attribute syntax to point to it specifically
        var serviceAttributeSyntax = classDeclaration.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(attr => attr.Name?.ToString().Contains("Service") == true);

        if (serviceAttributeSyntax != null)
        {
            // If there are arguments, point to the first argument (usually lifetime)
            if (serviceAttributeSyntax.ArgumentList?.Arguments.Count > 0)
                return serviceAttributeSyntax.ArgumentList.Arguments[0].GetLocation();
            // Otherwise point to the attribute itself
            return serviceAttributeSyntax.GetLocation();
        }

        // Fall back to class declaration
        return classDeclaration.GetLocation();
    }

    /// <summary>
    ///     Gets the location of an [Inject] field for more precise diagnostic reporting
    /// </summary>
    private static Location GetInjectFieldLocation(TypeDeclarationSyntax classDeclaration,
        string fieldTypeName)
    {
        // Try to find the specific [Inject] field with matching type
        var injectField = classDeclaration.Members
            .OfType<FieldDeclarationSyntax>()
            .Where(field => field.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(attr => attr.Name?.ToString().Contains("Inject") == true))
            .FirstOrDefault(field =>
                field.Declaration.Type?.ToString().Contains(fieldTypeName.Split('.').Last()) == true);

        return injectField?.GetLocation() ?? classDeclaration.GetLocation();
    }

    /// <summary>
    ///     Gets a more readable location description for diagnostics
    /// </summary>
    private static Location GetBestLocationForDependency(TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        string dependencyType)
    {
        // For [Inject] fields, try to point to the specific field
        var fieldLocation = GetInjectFieldLocation(classDeclaration, dependencyType);
        if (fieldLocation != classDeclaration.GetLocation()) return fieldLocation;

        // For [Service] attribute issues, point to the service attribute
        if (dependencyType.Contains("Lifetime") || dependencyType.Contains("Singleton") ||
            dependencyType.Contains("Scoped") || dependencyType.Contains("Transient"))
            return GetServiceAttributeLocation(classDeclaration, classSymbol);

        // Default to class declaration
        return classDeclaration.GetLocation();
    }

}