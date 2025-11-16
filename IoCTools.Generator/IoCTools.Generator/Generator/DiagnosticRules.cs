namespace IoCTools.Generator.Generator;

using System.Collections.Generic;
using System.Linq;

using Analysis;

using Diagnostics.Validators;

using IoCTools.Generator.Diagnostics;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Models;

using Utilities;

internal static class DiagnosticRules
{
    // Attribute combinations (IOC02x family; current focus on ConditionalService rules)
    public static void ValidateAttributeCombinations(SourceProductionContext context,
        IEnumerable<INamedTypeSymbol> servicesWithAttributes)
    {
        ConditionalServiceValidator
            .ValidateAttributeCombinations(context, servicesWithAttributes);
    }

    // Conditional services handled by ConditionalServiceValidator

    public static void ValidateCircularDependenciesComplete(SourceProductionContext context,
        List<INamedTypeSymbol> servicesWithAttributes,
        HashSet<string> allRegisteredServices,
        DiagnosticConfiguration diagnosticConfig)
    {
        CircularDependencyValidator
            .ValidateCircularDependenciesComplete(context, servicesWithAttributes, allRegisteredServices,
                diagnosticConfig);
    }

    // IOC016-IOC019: Validate configuration injection
    public static void ValidateConfigurationInjection(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        ConfigurationInjectionValidator
            .ValidateConfigurationInjection(context, classDeclaration, classSymbol);
    }

    public static void ValidateRedundantServiceConfigurations(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        RedundantConfigurationValidator
            .Validate(context, classDeclaration, classSymbol);
    }

    // IOC011: Validate HostedService requirements (partial class requirement)
    public static void ValidateHostedServiceRequirements(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        var isHostedService = TypeAnalyzer.IsAssignableFromIHostedService(classSymbol);
        if (!isHostedService) return;

        var isPartial = classDeclaration.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PartialKeyword));

        var hasInjectFields = classSymbol.GetMembers().OfType<IFieldSymbol>()
            .Any(field => field.GetAttributes().Any(attr => attr.AttributeClass?.Name == "InjectAttribute"));
        var hasDependsOnAttributes = classSymbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.Name?.StartsWith("DependsOn") == true);

        if ((hasInjectFields || hasDependsOnAttributes) && !isPartial)
        {
            var diagnostic = Diagnostic.Create(DiagnosticDescriptors.BackgroundServiceNotPartial,
                classDeclaration.GetLocation(), classSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    // IOC001/IOC002: Validate missing/unregistered dependencies
    public static void ValidateMissingDependencies(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        InheritanceHierarchyDependencies hierarchyDependencies,
        HashSet<string> allRegisteredServices,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations,
        Dictionary<string, string> serviceLifetimes,
        DiagnosticConfiguration diagnosticConfig,
        string implicitLifetime)
    {
        foreach (var dependency in hierarchyDependencies.AllDependenciesWithExternalFlag)
        {
            if (dependency.IsExternal) continue;

            if (dependency.Source == DependencySource.ConfigurationInjection &&
                dependency.FieldName != "_configuration")
                continue;

            var dependencyType = dependency.ServiceType.ToDisplayString();

            if (TypeHelpers.IsFrameworkTypeAdapted(dependencyType) ||
                allRegisteredServices.Contains(dependencyType))
                continue;

            var serviceTypeName = dependency.ServiceType.ToDisplayString();
            var knownCrossAssemblyInterfaces = new[]
            {
                "Delta.Domain.BoundedContexts.Consumers.Dependencies.IConsumerRepository",
                "Delta.Domain.BoundedContexts.Events.Dependencies.IEventRepository",
                "Delta.Domain.BoundedContexts.Store.Dependencies.IStoreItemRepository",
                "Delta.Common.Abstractions.Interfaces.IClock",
                "Delta.Common.Abstractions.Interfaces.IEnvironmentProvider",
                "Delta.Application.Abstractions.Interfaces.IConsumerConnectionService",
                "Delta.Application.Abstractions.Interfaces.ISerializationService",
                "Delta.Application.Abstractions.Interfaces.IEventDistributionService",
                "Delta.Application.Abstractions.Interfaces.IUnitOfWorkService",
                "Delta.Application.Abstractions.Interfaces.IHealthMonitoringService",
                "Delta.Application.Abstractions.Interfaces.IMetadataAwareCache",
                "Delta.Application.Abstractions.Interfaces.IIntegrationEventSchedulingService",
                "Delta.Application.Abstractions.Interfaces.IIntegrationEventService",
                "Delta.Domain.Abstractions.Interfaces.ISchemaFactory",
                "Delta.Domain.Abstractions.Interfaces.IEventPatternMatcher"
            };
            var isGenericLoggerService =
                serviceTypeName.StartsWith("Delta.Application.Abstractions.Interfaces.ILoggerService<") &&
                serviceTypeName.EndsWith(">");
            if (knownCrossAssemblyInterfaces.Contains(serviceTypeName) || isGenericLoggerService) continue;

            // Special handling for IEnumerable<T> dependencies
            var enumerableTypeInfo = TypeHelpers.ExtractIEnumerableFromWrappedType(dependencyType);
            if (enumerableTypeInfo != null)
            {
                var innerType = enumerableTypeInfo.InnerType;
                var hasInnerTypeImplementations = allImplementations.ContainsKey(innerType) ||
                                                  allRegisteredServices.Contains(innerType) ||
                                                  serviceLifetimes.ContainsKey(innerType);
                if (!hasInnerTypeImplementations)
                {
                    var descriptor = DiagnosticUtilities.CreateDynamicDescriptor(
                        DiagnosticDescriptors.NoImplementationFound, diagnosticConfig.NoImplementationSeverity);
                    var diagnostic = Diagnostic.Create(descriptor,
                        classDeclaration.GetLocation(), classDeclaration.Identifier.ValueText, dependencyType);
                    context.ReportDiagnostic(diagnostic);
                }

                continue;
            }

            var (dependencyLifetime, _) =
                DependencyLifetimeResolver.GetDependencyLifetimeWithGenericSupportAndImplementationName(
                    dependencyType, serviceLifetimes, allRegisteredServices, allImplementations, implicitLifetime);

            var implementationExists = dependencyLifetime != null || allImplementations.ContainsKey(dependencyType);
            if (implementationExists)
            {
                if (dependencyLifetime != null) continue; // registered

                if (allImplementations.ContainsKey(dependencyType))
                {
                    var implementations = allImplementations[dependencyType];
                    var hasRegisteredImplementation = implementations.Any(impl =>
                        allRegisteredServices.Contains(impl.ToDisplayString()));

                    if (!hasRegisteredImplementation)
                    {
                        var descriptor = DiagnosticUtilities.CreateDynamicDescriptor(
                            DiagnosticDescriptors.ImplementationNotRegistered,
                            diagnosticConfig.ManualImplementationSeverity);
                        var diagnostic = Diagnostic.Create(descriptor,
                            classDeclaration.GetLocation(), classDeclaration.Identifier.ValueText, dependencyType);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
            else
            {
                var descriptor = DiagnosticUtilities.CreateDynamicDescriptor(
                    DiagnosticDescriptors.NoImplementationFound, diagnosticConfig.NoImplementationSeverity);
                var diagnostic = Diagnostic.Create(descriptor,
                    classDeclaration.GetLocation(), classDeclaration.Identifier.ValueText, dependencyType);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static List<IFieldSymbol> GetConfigurationFieldsFromHierarchy(INamedTypeSymbol classSymbol)
    {
        var result = new List<IFieldSymbol>();
        var currentType = classSymbol;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            foreach (var member in currentType.GetMembers().OfType<IFieldSymbol>())
            {
                var hasInjectConfigurationAttribute = member.GetAttributes()
                    .Any(attr => attr.AttributeClass?.ToDisplayString() ==
                                 "IoCTools.Abstractions.Annotations.InjectConfigurationAttribute");
                if (hasInjectConfigurationAttribute) result.Add(member);
            }

            currentType = currentType.BaseType;
        }

        return result;
    }

    private static bool IsSupportedConfigurationType(ITypeSymbol type)
    {
        if (type.SpecialType != SpecialType.None && type.SpecialType != SpecialType.System_Object) return true;
        if (type.SpecialType == SpecialType.System_String) return true;

        if (type.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T &&
            type is INamedTypeSymbol nullableType)
            return IsSupportedConfigurationType(nullableType.TypeArguments.First());

        if (type is IArrayTypeSymbol arrayType) return IsSupportedConfigurationType(arrayType.ElementType);

        if (type is INamedTypeSymbol namedType)
        {
            var typeName = namedType.ToDisplayString();
            if (namedType.TypeKind == TypeKind.Enum) return true;

            var supportedBuiltinTypes = new[]
            {
                "System.DateTime", "System.DateTimeOffset", "System.TimeSpan", "System.Guid", "System.Uri",
                "System.Decimal"
            };
            if (supportedBuiltinTypes.Contains(typeName)) return true;

            if (typeName.StartsWith("Microsoft.Extensions.Options.IOptions") ||
                typeName.StartsWith("Microsoft.Extensions.Options.IOptionsSnapshot") ||
                typeName.StartsWith("Microsoft.Extensions.Options.IOptionsMonitor"))
                return true;

            if (typeName.StartsWith("System.Collections.Generic.List<") ||
                typeName.StartsWith("System.Collections.Generic.IList<") ||
                typeName.StartsWith("System.Collections.Generic.ICollection<") ||
                typeName.StartsWith("System.Collections.Generic.IEnumerable<") ||
                typeName.StartsWith("System.Collections.Generic.Dictionary<") ||
                typeName.StartsWith("System.Collections.Generic.IDictionary<"))
                if (namedType.TypeArguments.Length > 0)
                    return namedType.TypeArguments.All(arg => IsSupportedConfigurationType(arg));

            if (namedType.TypeKind == TypeKind.Interface) return false;
            if (namedType.IsAbstract) return false;
            return namedType.Constructors.Any(c => c.Parameters.Length == 0);
        }

        return false;
    }

    private static string GetUnsupportedTypeReason(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Interface)
        {
            if (type is INamedTypeSymbol interfaceType)
            {
                var typeName = interfaceType.ToDisplayString();
                if (typeName.StartsWith("Microsoft.Extensions.Options."))
                    return "requires parameterless constructor";
            }

            return "Interfaces cannot be bound from configuration";
        }

        if (type.IsAbstract) return "Abstract types cannot be bound from configuration";
        if (type is IArrayTypeSymbol) return "Array element type is not supported for configuration binding";
        if (type is INamedTypeSymbol namedType2)
        {
            var typeName2 = namedType2.ToDisplayString();
            if (typeName2.StartsWith("System.Collections.Generic."))
                return "Collection element type is not supported for configuration binding";
        }

        return "requires parameterless constructor";
    }

    private static ConfigurationKeyValidationResult ValidateConfigurationKey(string? key)
    {
        if (string.IsNullOrEmpty(key))
            return new ConfigurationKeyValidationResult { IsValid = false, ErrorMessage = "empty or whitespace-only" };
        if (string.IsNullOrWhiteSpace(key))
            return new ConfigurationKeyValidationResult { IsValid = false, ErrorMessage = "empty or whitespace-only" };
        if (key!.Contains("::"))
            return new ConfigurationKeyValidationResult
            {
                IsValid = false,
                ErrorMessage = "contains double colons (::)"
            };
        if (key!.StartsWith(":") || key.EndsWith(":"))
            return new ConfigurationKeyValidationResult
            {
                IsValid = false,
                ErrorMessage = "cannot start or end with a colon (:)"
            };
        if (key.Any(c => c == '\0' || c == '\r' || c == '\n' || c == '\t'))
            return new ConfigurationKeyValidationResult
            {
                IsValid = false,
                ErrorMessage = "contains invalid characters (null, carriage return, newline, or tab)"
            };
        return new ConfigurationKeyValidationResult { IsValid = true, ErrorMessage = string.Empty };
    }

    public static void ValidateDependencyRedundancy(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        InheritanceHierarchyDependencies hierarchyDependencies)
    {
        DependencyUsageValidator
            .ValidateRedundantDependencies(context, classDeclaration, classSymbol, hierarchyDependencies);
    }

    public static void ValidateUnusedDependencies(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        SemanticModel? semanticModel,
        InheritanceHierarchyDependencies hierarchyDependencies) =>
        DependencyUsageValidator.ValidateUnusedDependencies(context, classDeclaration, classSymbol, semanticModel,
            hierarchyDependencies);

    public static void ValidateDuplicateDependsOn(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        DependsOnValidator
            .ValidateDuplicateDependsOn(context, classDeclaration, classSymbol);
    }

    public static void ValidateDuplicatesWithinSingleDependsOn(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        DependsOnValidator
            .ValidateDuplicatesWithinSingleDependsOn(context, classDeclaration, classSymbol);
    }

    // IOC009: Validates unnecessary SkipRegistration attributes for interfaces not registered by RegisterAsAll
    public static void ValidateUnnecessarySkipRegistration(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        DependsOnValidator
            .ValidateUnnecessarySkipRegistration(context, classDeclaration, classSymbol);
    }

    public static void ValidateInjectFieldPreferences(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol) =>
        InjectUsageValidator.ValidatePreferDependsOn(context, classDeclaration, classSymbol);

    // IOC015: Validate inheritance chain lifetime violations (SourceProductionContext)
    public static void ValidateInheritanceChainLifetimesForSourceProduction(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        Dictionary<string, string> serviceLifetimes,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations,
        string implicitLifetime)
    {
        LifetimeDependencyValidator
            .ValidateInheritanceChainLifetimesForSourceProduction(context, classDeclaration, classSymbol,
                serviceLifetimes, allImplementations, implicitLifetime);
    }

    // IOC012/IOC013: Validate service lifetime dependencies
    public static void ValidateLifetimeDependencies(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        InheritanceHierarchyDependencies hierarchyDependencies,
        Dictionary<string, string> serviceLifetimes,
        HashSet<string> allRegisteredServices,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations,
        DiagnosticConfiguration diagnosticConfig,
        INamedTypeSymbol classSymbol,
        string implicitLifetime)
    {
        LifetimeDependencyValidator
            .ValidateLifetimeDependencies(context, classDeclaration, hierarchyDependencies, serviceLifetimes,
                allRegisteredServices, allImplementations, diagnosticConfig, classSymbol, implicitLifetime);
    }

    internal static void ValidateIEnumerableLifetimes(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        string serviceLifetime,
        string innerType,
        string dependencyTypeName,
        Dictionary<string, string> serviceLifetimes,
        HashSet<string> allRegisteredServices,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations,
        string implicitLifetime)
    {
        var foundImplementations = false;
        var processedImplementations = new HashSet<string>();
        if (allImplementations.TryGetValue(innerType, out var directImplementations))
        {
            foundImplementations = true;
            foreach (var implementation in directImplementations)
            {
                if (!processedImplementations.Add(implementation.ToDisplayString())) continue;
                var implementationLifetime = LifetimeUtilities.GetServiceLifetimeFromSymbol(implementation,
                    implicitLifetime);
                if (implementationLifetime == null) continue;
                if (serviceLifetime == "Singleton" && implementationLifetime == "Scoped")
                {
                    var diagnostic = Diagnostic.Create(DiagnosticDescriptors.SingletonDependsOnScoped,
                        classDeclaration.GetLocation(), classSymbol.Name,
                        $"{dependencyTypeName} -> {implementation.Name}");
                    context.ReportDiagnostic(diagnostic);
                }
                else if (serviceLifetime == "Singleton" && implementationLifetime == "Transient")
                {
                    var diagnostic = Diagnostic.Create(DiagnosticDescriptors.SingletonDependsOnTransient,
                        classDeclaration.GetLocation(), classSymbol.Name,
                        $"{dependencyTypeName} -> {implementation.Name}");
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        if (innerType.Contains('<') && innerType.Contains('>'))
        {
            var baseGenericType = TypeHelpers.ExtractBaseGenericInterface(innerType);
            if (baseGenericType != null &&
                allImplementations.TryGetValue(baseGenericType, out var genericImplementations))
            {
                foundImplementations = true;
                foreach (var implementation in genericImplementations)
                {
                    if (!processedImplementations.Add(implementation.ToDisplayString())) continue;
                    var implementationLifetime = LifetimeUtilities.GetServiceLifetimeFromSymbol(implementation,
                        implicitLifetime);
                    if (implementationLifetime == null) continue;
                    if (serviceLifetime == "Singleton" && implementationLifetime == "Scoped")
                    {
                        var diagnostic = Diagnostic.Create(DiagnosticDescriptors.SingletonDependsOnScoped,
                            classDeclaration.GetLocation(), classSymbol.Name,
                            $"{dependencyTypeName} -> {implementation.Name}");
                        context.ReportDiagnostic(diagnostic);
                    }
                    else if (serviceLifetime == "Singleton" && implementationLifetime == "Transient")
                    {
                        var diagnostic = Diagnostic.Create(DiagnosticDescriptors.SingletonDependsOnTransient,
                            classDeclaration.GetLocation(), classSymbol.Name,
                            $"{dependencyTypeName} -> {implementation.Name}");
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
        }

        if (!foundImplementations)
            foreach (var kvp in allImplementations)
            {
                var implementations = kvp.Value;
                foreach (var implementation in implementations)
                {
                    if (!processedImplementations.Add(implementation.ToDisplayString())) continue;
                    var implementedInterfaces = implementation.AllInterfaces.Select(i => i.ToDisplayString());
                    if (!implementedInterfaces.Contains(innerType)) continue;
                    var implementationLifetime = LifetimeUtilities.GetServiceLifetimeFromSymbol(implementation,
                        implicitLifetime);
                    if (implementationLifetime == null) continue;
                    if (serviceLifetime == "Singleton" && implementationLifetime == "Scoped")
                    {
                        var diagnostic = Diagnostic.Create(DiagnosticDescriptors.SingletonDependsOnScoped,
                            classDeclaration.GetLocation(), classSymbol.Name,
                            $"{dependencyTypeName} -> {implementation.Name}");
                        context.ReportDiagnostic(diagnostic);
                    }
                    else if (serviceLifetime == "Singleton" && implementationLifetime == "Transient")
                    {
                        var diagnostic = Diagnostic.Create(DiagnosticDescriptors.SingletonDependsOnTransient,
                            classDeclaration.GetLocation(), classSymbol.Name,
                            $"{dependencyTypeName} -> {implementation.Name}");
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
    }

    private static List<ITypeSymbol> GetDependsOnTypeSymbolsFromInheritanceChain(INamedTypeSymbol classSymbol)
    {
        var types = new List<ITypeSymbol>();
        var currentType = classSymbol;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            foreach (var attr in currentType.GetAttributes())
                if (attr.AttributeClass?.Name == "DependsOnAttribute" &&
                    attr.AttributeClass?.TypeArguments != null)
                    foreach (var typeArg in attr.AttributeClass.TypeArguments)
                        types.Add(typeArg);
            currentType = currentType.BaseType;
        }

        return types;
    }

    // Helpers (scoped to diagnostics rules)
    private struct ConfigurationKeyValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
    }
}
