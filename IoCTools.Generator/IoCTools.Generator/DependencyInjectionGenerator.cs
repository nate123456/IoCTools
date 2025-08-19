using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using IoCTools.Generator.Analysis;
using IoCTools.Generator.CodeGeneration;
using IoCTools.Generator.Diagnostics;
using IoCTools.Generator.Models;
using IoCTools.Generator.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace IoCTools.Generator;

[Generator]
public class DependencyInjectionGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // REMOVED: Global state clearing - this breaks incremental generator pipeline
        // The static collections are used by transform methods executing later

        // CRITICAL FIX: Proper IIncrementalGenerator architecture to prevent duplicate generation

        // Pipeline Stage 1: Service-attributed classes only (with proper partial class handling)
        var serviceClasses = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node,
                    _) => node is TypeDeclarationSyntax,
                static (ctx,
                    _) =>
                {
                    var classDeclaration = (TypeDeclarationSyntax)ctx.Node;
                    var classSymbol = ctx.SemanticModel.GetDeclaredSymbol(classDeclaration);

                    if (classSymbol == null) return null;

                    // CRITICAL FIX: Static classes cannot be used for dependency injection - filter them out immediately
                    if (classSymbol.IsStatic) return null;

                    // CRITICAL FIX: For partial classes, attributes and inject fields might be scattered across multiple parts
                    // We need to check the COMPLETE symbol (which aggregates all partial parts), not just the current syntax node

                    // Check for service-related attributes on the complete symbol
                    var hasServiceAttribute = classSymbol.GetAttributes().Any(attr =>
                        attr.AttributeClass?.ToDisplayString() == "IoCTools.Abstractions.Annotations.ServiceAttribute");

                    var hasConditionalServiceAttribute = classSymbol.GetAttributes().Any(attr =>
                        attr.AttributeClass?.ToDisplayString() ==
                        "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute");


                    // CRITICAL FIX: Check for [Inject] fields across ALL partial declarations
                    var hasInjectFields = HasInjectFieldsAcrossPartialClasses(classSymbol);


                    // CRITICAL FIX: Check for [InjectConfiguration] fields across ALL partial declarations
                    var hasInjectConfigurationFields = HasInjectConfigurationFieldsAcrossPartialClasses(classSymbol);

                    var hasDependsOnAttribute = classSymbol.GetAttributes()
                        .Any(attr => attr.AttributeClass?.Name?.StartsWith("DependsOn") == true);

                    // CRITICAL FIX: Also check for RegisterAsAll attribute to catch validation cases
                    var hasRegisterAsAllAttribute = classSymbol.GetAttributes()
                        .Any(attr => attr.AttributeClass?.Name == "RegisterAsAllAttribute");

                    // Check for BackgroundService inheritance
                    var isBackgroundService = false;
                    var currentServiceType = classSymbol;
                    while (currentServiceType != null && !isBackgroundService)
                    {
                        if (currentServiceType.ToDisplayString() == "Microsoft.Extensions.Hosting.BackgroundService")
                        {
                            isBackgroundService = true;
                            break;
                        }

                        currentServiceType = currentServiceType.BaseType;
                    }

                    // Check for IHostedService interface implementation
                    var isHostedService = classSymbol.Interfaces.Any(i =>
                        i.ToDisplayString() == "Microsoft.Extensions.Hosting.IHostedService");

                    if (hasServiceAttribute || hasConditionalServiceAttribute || hasInjectFields ||
                        hasInjectConfigurationFields || hasDependsOnAttribute || hasRegisterAsAllAttribute ||
                        isBackgroundService ||
                        isHostedService) return new ServiceClassInfo(classSymbol, classDeclaration, ctx.SemanticModel);
                    return (ServiceClassInfo?)null;
                })
            .Where(static x => x.HasValue)
            .Select(static (x,
                _) => x!.Value)
            // CRITICAL FIX: Deduplicate by symbol to handle partial classes (multiple TypeDeclarationSyntax -> one Symbol)
            .Collect()
            .SelectMany(static (services,
                _) =>
            {
                // Group by symbol and keep only one ServiceClassInfo per unique symbol
                return services.GroupBy(s => s.ClassSymbol, SymbolEqualityComparer.Default)
                    .Select(g => g.First()) // Take the first occurrence of each unique symbol
                    .ToImmutableArray();
            });

        // Pipeline Stage 2: Collect all services with compilation context ONLY  
        // CRITICAL FIX: Remove AnalyzerConfigOptionsProvider from registration pipeline to prevent multiple triggers
        // Diagnostics can access config options separately
        var allServicesForRegistration = serviceClasses
            .Collect()
            .Combine(context.CompilationProvider)
            .Select(static (input, _) =>
            {
                var (services, compilation) = input;
                return (services, compilation);
            });

        // Pipeline Stage 3: Generate service registrations (ONCE per unique compilation)
        context.RegisterSourceOutput(allServicesForRegistration, static (context,
            input) => GenerateServiceRegistrations(context, input));

        // Pipeline Stage 4: Generate constructors (ONCE per service class)
        context.RegisterSourceOutput(serviceClasses, static (context,
            serviceInfo) => GenerateSingleConstructor(context, serviceInfo));

        // Pipeline Stage 5: Validate diagnostics (ONCE per compilation with all services)  
        // Use a separate pipeline combining services with config options for diagnostics
        var allServicesForDiagnostics = serviceClasses
            .Collect()
            .Combine(context.CompilationProvider)
            .Combine(context.AnalyzerConfigOptionsProvider)
            .Select(static (input, _) =>
            {
                var ((services, compilation), configOptions) = input;
                return ((services, compilation), configOptions);
            });
            
        context.RegisterSourceOutput(allServicesForDiagnostics, static (context,
            input) => ValidateAllServiceDiagnostics(context, input));
    }

    /// <summary>
    ///     Helper method to check for [Inject] fields across all partial class declarations
    /// </summary>
    private static bool HasInjectFieldsAcrossPartialClasses(INamedTypeSymbol classSymbol)
    {
        // Check all syntax references for this symbol (handles partial classes)
        foreach (var declaringSyntaxRef in classSymbol.DeclaringSyntaxReferences)
            if (declaringSyntaxRef.GetSyntax() is TypeDeclarationSyntax typeDeclaration)
                foreach (var fieldDeclaration in typeDeclaration.DescendantNodes().OfType<FieldDeclarationSyntax>())
                    // Check for [Inject] attribute
                foreach (var attributeList in fieldDeclaration.AttributeLists)
                foreach (var attribute in attributeList.Attributes)
                {
                    var attributeText = attribute.Name.ToString();
                    if (attributeText == "Inject" || attributeText == "InjectAttribute" ||
                        attributeText.EndsWith("Inject") || attributeText.EndsWith("InjectAttribute"))
                        return true;
                }

        // Also check using symbol-based approach as fallback
        return classSymbol.GetMembers().OfType<IFieldSymbol>()
            .Any(field => field.GetAttributes().Any(attr => attr.AttributeClass?.Name == "InjectAttribute"));
    }

    /// <summary>
    ///     Helper method to check for [InjectConfiguration] fields across all partial class declarations
    /// </summary>
    private static bool HasInjectConfigurationFieldsAcrossPartialClasses(INamedTypeSymbol classSymbol)
    {
        // Check all syntax references for this symbol (handles partial classes)
        foreach (var declaringSyntaxRef in classSymbol.DeclaringSyntaxReferences)
            if (declaringSyntaxRef.GetSyntax() is TypeDeclarationSyntax typeDeclaration)
                foreach (var fieldDeclaration in typeDeclaration.DescendantNodes().OfType<FieldDeclarationSyntax>())
                    // Check for [InjectConfiguration] attribute
                foreach (var attributeList in fieldDeclaration.AttributeLists)
                foreach (var attribute in attributeList.Attributes)
                {
                    var attributeText = attribute.Name.ToString();
                    if (attributeText == "InjectConfiguration" || attributeText == "InjectConfigurationAttribute" ||
                        attributeText.EndsWith("InjectConfiguration") ||
                        attributeText.EndsWith("InjectConfigurationAttribute"))
                        return true;
                }

        // Also check using symbol-based approach as fallback
        return classSymbol.GetMembers().OfType<IFieldSymbol>()
            .Any(field =>
                field.GetAttributes().Any(attr => attr.AttributeClass?.Name == "InjectConfigurationAttribute"));
    }

    /// <summary>
    ///     Generate service registrations for all services (called once per compilation)
    /// </summary>
    private static void GenerateServiceRegistrations(SourceProductionContext context,
        (ImmutableArray<ServiceClassInfo> Services, Compilation Compilation) input)
    {
        try
        {
            var (services, compilation) = input;
            
            if (!services.Any()) return; // No services found

            var allServiceRegistrations = new List<ServiceRegistration>();
            var allConfigOptions = new List<ConfigurationOptionsRegistration>();
            var processedClasses = new HashSet<string>(StringComparer.Ordinal);

            // Generate service registrations from collected services
            foreach (var serviceInfo in services)
            {
                var classKey = serviceInfo.ClassSymbol.ToDisplayString();
                if (!processedClasses.Add(classKey)) continue; // Skip duplicates

                var treeServices = GetServicesToRegisterForSingleClass(
                    serviceInfo.SemanticModel, serviceInfo.ClassDeclaration,
                    serviceInfo.ClassSymbol, context).ToList();
                allServiceRegistrations.AddRange(treeServices);

                var treeConfigOptions = ServiceRegistrationGenerator.GetConfigurationOptionsToRegister(
                    serviceInfo.SemanticModel, serviceInfo.ClassDeclaration.SyntaxTree.GetRoot()).ToList();
                allConfigOptions.AddRange(treeConfigOptions);
            }

            // Deduplicate service registrations
            allServiceRegistrations = DeduplicateServiceRegistrations(allServiceRegistrations);
            // Deduplicate configuration options registrations
            allConfigOptions = DeduplicateConfigurationOptions(allConfigOptions);

            // Generate service registration extension method with deduplication
            if (allServiceRegistrations.Any() || allConfigOptions.Any())
            {
                var assemblyName = compilation.AssemblyName ?? "UnknownAssembly";
                var safeAssemblyName = assemblyName.Replace(".", "").Replace("-", "").Replace(" ", "");

                // CRITICAL FIX: Use stable filename based on assembly name only
                var extensionCode = ServiceRegistrationGenerator.GenerateRegistrationExtensionMethod(
                    allServiceRegistrations, safeAssemblyName, allConfigOptions);

                // Use stable filename that doesn't change between builds of the same source
                var registrationFileName = $"ServiceRegistrations_{safeAssemblyName}.g.cs";
                context.AddSource(registrationFileName, extensionCode);
            }
        }
        catch (Exception ex)
        {
            ReportGeneratorError(context, "IOC999", "Service registration generation failed", ex.Message);
        }
    }

    /// <summary>
    ///     Validate diagnostics for all services (called once per compilation)
    /// </summary>
    private static void ValidateAllServiceDiagnostics(SourceProductionContext context,
        ((ImmutableArray<ServiceClassInfo> Services, Compilation Compilation) Input, AnalyzerConfigOptionsProvider
            ConfigOptions) input)
    {
        try
        {
            var ((services, compilation), configOptions) = input;

            if (!services.Any()) return; // No services found

            // Build service lifetime map and registered services set
            var allRegisteredServices = new HashSet<string>();
            var allImplementations = new Dictionary<string, List<INamedTypeSymbol>>();
            var serviceLifetimes = new Dictionary<string, string>();
            var processedClasses = new HashSet<string>(StringComparer.Ordinal);

            // First pass: collect all service information
            foreach (var serviceInfo in services)
            {
                var classKey = serviceInfo.ClassSymbol.ToDisplayString();
                if (!processedClasses.Add(classKey)) continue; // Skip duplicates

                CollectServiceSymbolsOnce(serviceInfo.ClassDeclaration.SyntaxTree.GetRoot(), serviceInfo.SemanticModel,
                    new List<INamedTypeSymbol>(), allRegisteredServices, allImplementations, serviceLifetimes,
                    new HashSet<string>());
            }

            // FIXED: Get MSBuild diagnostic configuration once for all validations
            var diagnosticConfig = DiagnosticUtilities.GetDiagnosticConfiguration(configOptions);

            // Second pass: validate each service (with proper deduplication)
            var validatedClasses = new HashSet<string>(StringComparer.Ordinal);
            foreach (var serviceInfo in services)
            {
                var classKey = serviceInfo.ClassSymbol.ToDisplayString();
                if (!validatedClasses.Add(classKey))
                    continue; // CRITICAL FIX: Skip duplicate validation to prevent duplicate diagnostics

                var hierarchyDependencies = DependencyAnalyzer.GetInheritanceHierarchyDependenciesForDiagnostics(
                    serviceInfo.ClassSymbol, serviceInfo.SemanticModel, allRegisteredServices, allImplementations);

                // Call diagnostic validation
                ValidateDependenciesComplete(context, serviceInfo.ClassDeclaration, hierarchyDependencies,
                    allRegisteredServices, allImplementations, serviceLifetimes, diagnosticConfig,
                    serviceInfo.SemanticModel, serviceInfo.ClassSymbol);
            }

            // CRITICAL FIX: Add circular dependency validation (IOC003)
            var allServiceSymbols = services.Select(s => s.ClassSymbol).ToList();
            ValidateCircularDependenciesComplete(context, allServiceSymbols, allRegisteredServices, diagnosticConfig);

            // CRITICAL FIX: Add attribute combination validation (IOC004 and IOC005)
            ValidateAttributeCombinations(context, services.Select(s => s.ClassSymbol));
        }
        catch (Exception ex)
        {
            ReportGeneratorError(context, "IOC996", "Diagnostic validation pipeline error", ex.Message);
        }
    }

    // REMOVED: Global state collections that interfered with incremental generation
    // Proper IIncrementalGenerator architecture eliminates need for manual deduplication

    /// <summary>
    ///     Generate constructor for a single service class
    /// </summary>
    private static void GenerateSingleConstructor(SourceProductionContext context,
        ServiceClassInfo serviceInfo)
    {
        try
        {
            // REMOVED: Global deduplication that breaks incremental generation
            // IIncrementalGenerator handles deduplication through proper pipeline design

            // NOTE: External services still get constructors generated, they only skip diagnostic validation

            var hierarchyDependencies = DependencyAnalyzer.GetConstructorDependencies(
                serviceInfo.ClassSymbol, serviceInfo.SemanticModel);

            // CRITICAL FIX: Always generate constructors for services, even with no dependencies
            // This handles edge cases like empty compilation units and ensures consistent behavior
            var constructorCode = GenerateInheritanceAwareConstructorCodeWithContext(
                serviceInfo.ClassDeclaration, hierarchyDependencies, serviceInfo.SemanticModel, context);

            if (!string.IsNullOrEmpty(constructorCode))
            {
                // CRITICAL FIX: Use canonical type representation for consistent filename generation
                var canonicalKey = serviceInfo.ClassSymbol.ToDisplayString();
                var sanitizedTypeName = canonicalKey.Replace("<", "_").Replace(">", "_")
                    .Replace(".", "_").Replace(",", "_").Replace(" ", "_");
                var fileName = $"{sanitizedTypeName}_Constructor.g.cs";

                context.AddSource(fileName, constructorCode);
            }
        }
        catch (Exception ex)
        {
            var typeName = serviceInfo.ClassSymbol.ToDisplayString();
            ReportGeneratorError(context, "IOC995", "Constructor generation error",
                $"Failed to generate constructor for {typeName}: {ex.Message}");
        }
    }


    // REMOVED: GetCanonicalTypeKey method - no longer needed after removing global deduplication

    /// <summary>
    ///     Deduplicate service registrations using comprehensive key-based deduplication
    /// </summary>
    private static List<ServiceRegistration> DeduplicateServiceRegistrations(
        List<ServiceRegistration> serviceRegistrations)
    {
        // CRITICAL FIX: Enhanced deduplication that uses same type formatting as generated code
        var deduplicationMap = new Dictionary<string, ServiceRegistration>();
        
        foreach (var service in serviceRegistrations)
        {
            // CRITICAL FIX: Use the same type name formatting that will be used in generated code
            // This ensures deduplication keys match actual generated service type names
            var isConditional = service is ConditionalServiceRegistration;
            var classTypeRaw = service.ClassSymbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "";
            var interfaceTypeRaw = service.InterfaceSymbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "";
            
            // Apply the same formatting logic used in ServiceRegistrationGenerator
            var classType = isConditional 
                ? SimplifyTypesForConditionalServicesDedup(classTypeRaw)
                : SimplifySystemTypesInServiceRegistrationDedup(classTypeRaw);
            var interfaceType = isConditional 
                ? SimplifyTypesForConditionalServicesDedup(interfaceTypeRaw)
                : SimplifySystemTypesInServiceRegistrationDedup(interfaceTypeRaw);
                
            var conditionKey = "";
            
            if (service is ConditionalServiceRegistration conditional)
            {
                // CRITICAL FIX: Normalize condition key for better duplicate detection
                conditionKey = conditional.Condition?.ToString()?.Trim()?.Replace("  ", " ") ?? "";
                
                // Additional normalization for configuration conditions
                if (!string.IsNullOrEmpty(conditional.Condition?.ConfigValue))
                {
                    conditionKey = $"config:{conditional.Condition.ConfigValue?.Trim()}:{conditional.Condition.EqualsValue?.Trim()}:{conditional.Condition.NotEquals?.Trim()}";
                }
                else if (!string.IsNullOrEmpty(conditional.Condition?.Environment))
                {
                    conditionKey = $"env:{conditional.Condition.Environment?.Trim()}";
                }
            }
            
            // CRITICAL FIX: Include registration type in key to differentiate concrete vs interface registrations
            var isConcreteRegistration = SymbolEqualityComparer.Default.Equals(service.ClassSymbol, service.InterfaceSymbol);
            var registrationType = isConcreteRegistration ? "concrete" : "interface";
            
            var key = $"{classType}|{interfaceType}|{service.Lifetime}|{service.UseSharedInstance}|{isConditional}|{conditionKey}|{registrationType}|{service.HasConfigurationInjection}";
            
            if (!deduplicationMap.ContainsKey(key))
            {
                deduplicationMap[key] = service;
            }
            else
            {
                // Duplicate found - using existing registration
            }
        }
        
        var deduplicated = deduplicationMap.Values.ToList();        
        return deduplicated;
    }

    /// <summary>
    /// Deduplication helper: Simplifies system types in service registration while keeping user types fully qualified
    /// MUST match logic in ServiceRegistrationGenerator.SimplifySystemTypesInServiceRegistration
    /// </summary>
    private static string SimplifySystemTypesInServiceRegistrationDedup(string fullyQualifiedTypeName)
    {
        if (string.IsNullOrEmpty(fullyQualifiedTypeName))
            return fullyQualifiedTypeName;

        // Replace common System types with their simplified forms
        // This matches test expectations: global::Test.IRepository<List<string>> instead of global::Test.IRepository<global::System.Collections.Generic.List<string>>
        var simplified = fullyQualifiedTypeName
            .Replace("global::System.Collections.Generic.List<", "List<")
            .Replace("global::System.Collections.Generic.Dictionary<", "Dictionary<")
            .Replace("global::System.Collections.Generic.IEnumerable<", "IEnumerable<")
            .Replace("global::System.Collections.Generic.ICollection<", "ICollection<")
            .Replace("global::System.Collections.Generic.IList<", "IList<")
            .Replace("global::System.Collections.Generic.HashSet<", "HashSet<")
            .Replace("global::System.String", "string")
            .Replace("global::System.Int32", "int")
            .Replace("global::System.Boolean", "bool")
            .Replace("global::System.Double", "double")
            .Replace("global::System.Decimal", "decimal")
            .Replace("global::System.DateTime", "DateTime")
            .Replace("global::System.TimeSpan", "TimeSpan")
            .Replace("global::System.Guid", "Guid");

        return simplified;
    }

    /// <summary>
    /// Deduplication helper: Simplifies types for conditional service registration - removes global:: prefix completely
    /// MUST match logic in ServiceRegistrationGenerator.SimplifyTypesForConditionalServices
    /// </summary>
    private static string SimplifyTypesForConditionalServicesDedup(string fullyQualifiedTypeName)
    {
        if (string.IsNullOrEmpty(fullyQualifiedTypeName))
            return fullyQualifiedTypeName;

        // First apply system type simplification
        var simplified = SimplifySystemTypesInServiceRegistrationDedup(fullyQualifiedTypeName);
        
        // Then remove global:: prefix completely for conditional services
        if (simplified.StartsWith("global::"))
            simplified = simplified.Substring("global::".Length);

        return simplified;
    }

    private static List<ConfigurationOptionsRegistration> DeduplicateConfigurationOptions(
        List<ConfigurationOptionsRegistration> configOptions)
    {
        // Deduplicate configuration options by OptionsType and SectionName
        return configOptions
            .GroupBy(c => new
            {
                OptionsType = c.OptionsType?.ToDisplayString() ?? "",
                c.SectionName
            })
            .Select(g => g.First()) // Take first registration from each unique group
            .ToList();
    }

    /// <summary>
    ///     Generate constructors using existing ConstructorGenerator with comprehensive duplicate prevention
    /// </summary>
    private static void GenerateConstructorsOnce(SourceProductionContext context,
        Compilation compilation,
        List<INamedTypeSymbol> uniqueServicesWithAttributes,
        HashSet<string> globalProcessedFiles)
    {
        try
        {
            // CRITICAL FIX: Process each unique service symbol exactly once
            foreach (var serviceSymbol in uniqueServicesWithAttributes)
            {
                var fullTypeName = serviceSymbol.ToDisplayString();

                // NOTE: External services still get constructors generated, they only skip diagnostic validation

                // CRITICAL FIX: Process only FIRST syntax reference to prevent duplicate generation
                var firstSyntaxRef = serviceSymbol.DeclaringSyntaxReferences.FirstOrDefault();
                if (firstSyntaxRef != null)
                {
                    var syntax = firstSyntaxRef.GetSyntax();
                    if (syntax is TypeDeclarationSyntax classDeclaration)
                    {
                        var semanticModel = compilation.GetSemanticModel(syntax.SyntaxTree);
                        var hierarchyDependencies =
                            DependencyAnalyzer.GetConstructorDependencies(serviceSymbol, semanticModel);

                        // CRITICAL FIX: Use the working adapter that generates fields AND constructor
                        var constructorCode = GenerateInheritanceAwareConstructorCodeWithContext(
                            classDeclaration, hierarchyDependencies, semanticModel, context);

                        if (!string.IsNullOrEmpty(constructorCode))
                        {
                            var sanitizedTypeName = fullTypeName.Replace("<", "_").Replace(">", "_")
                                .Replace(".", "_").Replace(",", "_").Replace(" ", "_");
                            var fileName = $"{sanitizedTypeName}_Constructor.g.cs";

                            // CRITICAL FIX: Use global file tracking to prevent ANY duplicate files
                            if (globalProcessedFiles.Add(fileName)) context.AddSource(fileName, constructorCode);
                            // Else: Skip - file already generated by this generator run
                        }
                        else
                        {
                            // Log when constructor code is empty 
                            ReportGeneratorError(context, "IOC990", "Constructor generation warning",
                                $"Empty constructor code generated for {fullTypeName}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ReportGeneratorError(context, "IOC995", "Constructor generation error", ex.Message);
        }
    }

    /// <summary>
    ///     Collect service symbols and related information for validation with global deduplication
    /// </summary>
    private static void CollectServiceSymbolsOnce(SyntaxNode root,
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
            var classSymbol = semanticModel.GetDeclaredSymbol(typeDeclaration);
            if (classSymbol == null) continue;

            // Skip interfaces - we only collect concrete classes
            if (classSymbol.TypeKind == TypeKind.Interface) continue;

            var classKey = classSymbol.ToDisplayString();

            // CRITICAL FIX: Collect ALL class implementations for IOC002 diagnostic
            // This is needed to detect when implementations exist but lack [Service] attributes
            foreach (var @interface in classSymbol.Interfaces)
            {
                var interfaceName = @interface.ToDisplayString();
                if (!allImplementations.ContainsKey(interfaceName))
                    allImplementations[interfaceName] = new List<INamedTypeSymbol>();
                allImplementations[interfaceName].Add(classSymbol);

                // CRITICAL FIX: Also register the enhanced open generic form for better matching
                if (@interface is INamedTypeSymbol namedInterface && namedInterface.IsGenericType)
                {
                    var enhancedOpenGeneric = ConvertToEnhancedOpenGenericFormForInterface(namedInterface);
                    if (enhancedOpenGeneric != null && enhancedOpenGeneric != interfaceName)
                    {
                        if (!allImplementations.ContainsKey(enhancedOpenGeneric))
                            allImplementations[enhancedOpenGeneric] = new List<INamedTypeSymbol>();
                        allImplementations[enhancedOpenGeneric].Add(classSymbol);
                    }
                }
            }

            // Check for service-related attributes
            var hasServiceAttribute = classSymbol.GetAttributes().Any(attr =>
                attr.AttributeClass?.ToDisplayString() == "IoCTools.Abstractions.Annotations.ServiceAttribute");

            var hasConditionalServiceAttribute = classSymbol.GetAttributes().Any(attr =>
                attr.AttributeClass?.ToDisplayString() ==
                "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute");

            var hasInjectFields = classSymbol.GetMembers().OfType<IFieldSymbol>()
                .Any(field => field.GetAttributes().Any(attr => attr.AttributeClass?.Name == "InjectAttribute"));

            var hasDependsOnAttribute = classSymbol.GetAttributes()
                .Any(attr => attr.AttributeClass?.Name?.StartsWith("DependsOn") == true);

            if (hasServiceAttribute || hasConditionalServiceAttribute || hasInjectFields || hasDependsOnAttribute)
            {
                // CRITICAL FIX: Only add if not already processed globally
                if (globalProcessedClasses.Add(classKey))
                    servicesWithAttributes.Add(classSymbol);
                else
                    // Skip - already processed this class symbol
                    continue;

                // Collect for validation - only services with attributes are "registered"
                allRegisteredServices.Add(classSymbol.ToDisplayString());

                // Get lifetime from service attribute if present
                if (hasServiceAttribute)
                {
                    var serviceAttr = classSymbol.GetAttributes().First(attr =>
                        attr.AttributeClass?.ToDisplayString() == "IoCTools.Abstractions.Annotations.ServiceAttribute");

                    var lifetime = "Scoped"; // default
                    if (serviceAttr.ConstructorArguments.Length > 0)
                    {
                        var lifetimeValue = serviceAttr.ConstructorArguments[0].Value;
                        if (lifetimeValue != null)
                            lifetime = (int)lifetimeValue switch
                            {
                                0 => "Scoped",
                                1 => "Transient",
                                2 => "Singleton",
                                _ => "Scoped"
                            };
                    }

                    serviceLifetimes[classSymbol.ToDisplayString()] = lifetime;

                    // CRITICAL FIX: Also add all interfaces implemented by this class to serviceLifetimes
                    // This is essential for lifetime validation when dependencies are declared as interfaces
                    foreach (var interfaceSymbol in classSymbol.AllInterfaces)
                    {
                        var interfaceDisplayString = interfaceSymbol.ToDisplayString();
                        serviceLifetimes[interfaceDisplayString] = lifetime;
                        allRegisteredServices.Add(interfaceDisplayString);
                    }
                }
            }
        }
    }

    /// <summary>
    ///     Report diagnostics using existing validators with full integration
    /// </summary>
    private static void ReportDiagnosticsWithCompleteValidation(SourceProductionContext context,
        Compilation compilation,
        List<INamedTypeSymbol> servicesWithAttributes,
        HashSet<string> allRegisteredServices,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations,
        Dictionary<string, string> serviceLifetimes)
    {
        try
        {
            // Get diagnostic configuration - simplified approach
            var diagnosticConfig = new DiagnosticConfiguration
            {
                NoImplementationSeverity = DiagnosticSeverity.Warning,
                UnregisteredImplementationSeverity = DiagnosticSeverity.Warning,
                DiagnosticsEnabled = true,
                LifetimeValidationEnabled = true,
                LifetimeValidationSeverity = DiagnosticSeverity.Error
            };

            // COMPLETE VALIDATION: Use adapted diagnostic validation logic
            foreach (var serviceSymbol in servicesWithAttributes)
            foreach (var syntaxRef in serviceSymbol.DeclaringSyntaxReferences)
            {
                var syntax = syntaxRef.GetSyntax();
                if (syntax is TypeDeclarationSyntax classDeclaration)
                {
                    var semanticModel = compilation.GetSemanticModel(syntax.SyntaxTree);
                    var hierarchyDependencies =
                        DependencyAnalyzer.GetInheritanceHierarchyDependenciesForDiagnostics(serviceSymbol,
                            semanticModel, allRegisteredServices, allImplementations);

                    // Call adapted diagnostic validation logic (without GeneratorExecutionContext)
                    ValidateDependenciesComplete(context, classDeclaration, hierarchyDependencies,
                        allRegisteredServices, allImplementations, serviceLifetimes, diagnosticConfig, semanticModel,
                        serviceSymbol);

                    // Only process first syntax reference to avoid duplicates
                    break;
                }
            }

            // CIRCULAR DEPENDENCY VALIDATION: Call once for all services
            ValidateCircularDependenciesComplete(context, servicesWithAttributes, allRegisteredServices,
                diagnosticConfig);
        }
        catch (Exception ex)
        {
            ReportGeneratorError(context, "IOC996", "Diagnostic validation error", ex.Message);
        }
    }

    /// <summary>
    ///     Complete dependency validation adapted for IIncrementalGenerator (SourceProductionContext)
    ///     This provides the core diagnostic validations that were missing
    /// </summary>
    private static void ValidateDependenciesComplete(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        InheritanceHierarchyDependencies hierarchyDependencies,
        HashSet<string> allRegisteredServices,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations,
        Dictionary<string, string> serviceLifetimes,
        DiagnosticConfiguration diagnosticConfig,
        SemanticModel semanticModel,
        INamedTypeSymbol classSymbol)
    {
        if (!diagnosticConfig.DiagnosticsEnabled) return;

        // CRITICAL FIX: Skip ALL diagnostics if class is marked with [ExternalService]
        var hasExternalServiceAttribute = classSymbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.ToDisplayString() ==
                         "IoCTools.Abstractions.Annotations.ExternalServiceAttribute");
        if (hasExternalServiceAttribute) return;

        // IOC012/IOC013: Validate lifetime compatibility 
        ValidateLifetimeDependencies(context, classDeclaration, hierarchyDependencies, serviceLifetimes,
            allRegisteredServices, allImplementations, diagnosticConfig, classSymbol);

        // IOC007: Validate DependsOn vs Inject conflicts
        ValidateDependsOnConflicts(context, classDeclaration, hierarchyDependencies, classSymbol);

        // IOC006: Validate duplicate dependencies in DependsOn
        ValidateDuplicateDependsOn(context, classDeclaration, classSymbol);

        // IOC008: Validate duplicate types within single DependsOn attributes
        ValidateDuplicatesWithinSingleDependsOn(context, classDeclaration, classSymbol);

        // IOC009: Validate unnecessary SkipRegistration attributes
        ValidateUnnecessarySkipRegistration(context, classDeclaration, classSymbol);

        // IOC016-IOC019: Validate configuration injection
        ValidateConfigurationInjection(context, classDeclaration, classSymbol);

        // IOC010/IOC011: Validate BackgroundService requirements
        ValidateBackgroundServiceRequirements(context, classDeclaration, classSymbol);

        // IOC015: Inheritance chain lifetime validation for Singleton services
        var serviceLifetime = GetServiceLifetimeFromSymbol(classSymbol);
        if (serviceLifetime == "Singleton")
            ValidateInheritanceChainLifetimesForSourceProduction(context, classDeclaration, classSymbol,
                serviceLifetimes);

        // IOC001/IOC002: Validate missing/unregistered dependencies
        ValidateMissingDependencies(context, classDeclaration, hierarchyDependencies, allRegisteredServices,
            allImplementations, serviceLifetimes, diagnosticConfig);
    }

    /// <summary>
    ///     IOC015: Validate inheritance chain lifetime violations for SourceProductionContext
    /// </summary>
    private static void ValidateInheritanceChainLifetimesForSourceProduction(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        Dictionary<string, string> serviceLifetimes)
    {
        var serviceLifetime = GetServiceLifetimeFromSymbol(classSymbol);
        if (serviceLifetime != "Singleton") return;

        // Check direct inheritance chain for lifetime violations
        var currentType = classSymbol.BaseType;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            var baseServiceLifetime = GetDependencyLifetimeForSourceProduction(currentType, serviceLifetimes);

            // Check if base class itself is a Scoped service
            if (baseServiceLifetime == "Scoped")
            {
                // Direct inheritance violation: Singleton inherits from Scoped
                var diagnostic = Diagnostic.Create(DiagnosticDescriptors.InheritanceChainLifetimeValidation,
                    classDeclaration.GetLocation(), classSymbol.Name, serviceLifetime, baseServiceLifetime);
                context.ReportDiagnostic(diagnostic);
                return; // Report only the first violation
            }
            
            // CRITICAL FIX: Also check if base class has Scoped dependencies (DependsOn only for now)
            // Using only DependsOn to be conservative and not interfere with constructor generation
            foreach (var attr in currentType.GetAttributes())
            {
                if (attr.AttributeClass?.Name == "DependsOnAttribute" &&
                    attr.AttributeClass?.TypeArguments != null)
                {
                    foreach (var typeArg in attr.AttributeClass.TypeArguments)
                    {
                        var dependencyLifetime = GetDependencyLifetimeForSourceProduction(typeArg, serviceLifetimes);
                        if (dependencyLifetime == "Scoped")
                        {
                            // Inheritance violation: Singleton inherits from class with Scoped dependencies
                            var diagnostic = Diagnostic.Create(DiagnosticDescriptors.InheritanceChainLifetimeValidation,
                                classDeclaration.GetLocation(), classSymbol.Name, serviceLifetime, dependencyLifetime);
                            context.ReportDiagnostic(diagnostic);
                            return; // Report only the first violation
                        }
                    }
                }
            }

            currentType = currentType.BaseType;
        }
    }

    /// <summary>
    ///     Get dependency lifetime for SourceProductionContext (similar to DependencyValidator.GetDependencyLifetime)
    /// </summary>
    private static string? GetDependencyLifetimeForSourceProduction(ITypeSymbol dependencyType,
        Dictionary<string, string> serviceLifetimes)
    {
        var dependencyTypeName = dependencyType.ToDisplayString();

        // First check for direct mapping
        if (serviceLifetimes.TryGetValue(dependencyTypeName, out var lifetime)) return lifetime;

        // If not found and this is a constructed generic type, try to find the open generic
        if (dependencyType is INamedTypeSymbol namedType && namedType.IsGenericType && !namedType.IsUnboundGenericType)
        {
            // Method 1: Use ConstructedFrom.ToDisplayString()
            var openGenericType = namedType.ConstructedFrom.ToDisplayString();
            if (serviceLifetimes.TryGetValue(openGenericType, out var openGenericLifetime)) return openGenericLifetime;

            // Method 2: Try to find by matching the base name and type parameter count
            var namespaceName = namedType.ContainingNamespace?.ToDisplayString();
            var genericTypeName = namedType.Name;
            var typeParameterCount = namedType.TypeArguments.Length;

            // Look for patterns like "TestNamespace.MiddleProcessor<T>" when searching for "TestNamespace.MiddleProcessor<string>"
            foreach (var kvp in serviceLifetimes)
            {
                var serviceType = kvp.Key;
                var serviceLifetime = kvp.Value;

                // Check if this is a potential match for our generic type
                if (serviceType.Contains(genericTypeName + "<") &&
                    (namespaceName == null || serviceType.StartsWith(namespaceName + ".")))
                {
                    // Verify it has the same number of type parameters
                    var angleStart = serviceType.IndexOf('<');
                    var angleEnd = serviceType.LastIndexOf('>');
                    if (angleStart >= 0 && angleEnd > angleStart)
                    {
                        var typeParamSection = serviceType.Substring(angleStart + 1, angleEnd - angleStart - 1);
                        var paramCount = typeParamSection.Split(',').Length;
                        if (paramCount == typeParameterCount) return serviceLifetime;
                    }
                }
            }

            // Method 3: Additional fallback using simple name matching
            foreach (var kvp in serviceLifetimes)
            {
                var serviceType = kvp.Key;
                var serviceLifetime = kvp.Value;
                if (serviceType.StartsWith(genericTypeName + "<") ||
                    serviceType.Contains("." + genericTypeName + "<"))
                    return serviceLifetime;
            }
        }

        return null; // Unable to determine lifetime
    }

    /// <summary>
    ///     Circular dependency validation adapted for IIncrementalGenerator
    /// </summary>
    private static void ValidateCircularDependenciesComplete(SourceProductionContext context,
        List<INamedTypeSymbol> servicesWithAttributes,
        HashSet<string> allRegisteredServices,
        DiagnosticConfiguration diagnosticConfig)
    {
        // Skip all validation if diagnostics are disabled
        if (!diagnosticConfig.DiagnosticsEnabled) return;

        var detector = new CircularDependencyDetector();
        var serviceNameToSymbolMap = new Dictionary<string, INamedTypeSymbol>();
        var interfaceToImplementationMap = new Dictionary<string, string>();
        var processedServices = new HashSet<string>();

        // Use the same circular dependency logic from DependencyValidator
        // First pass: Build the interface to implementation mapping
        foreach (var serviceSymbol in servicesWithAttributes)
        {
            var serviceName = serviceSymbol.Name;

            if (processedServices.Contains(serviceName))
                continue;

            processedServices.Add(serviceName);
            serviceNameToSymbolMap[serviceName] = serviceSymbol;

            var hasExternalServiceAttribute = serviceSymbol.GetAttributes()
                .Any(attr =>
                    attr.AttributeClass?.ToDisplayString() ==
                    "IoCTools.Abstractions.Annotations.ExternalServiceAttribute");

            if (hasExternalServiceAttribute) continue;

            foreach (var implementedInterface in serviceSymbol.AllInterfaces)
            {
                var interfaceTypeName = implementedInterface.ToDisplayString();
                var interfaceName = ExtractServiceNameFromType(interfaceTypeName);
                if (interfaceName != null) interfaceToImplementationMap[interfaceName] = serviceName;
            }
        }

        // Second pass: Build dependency graph and detect cycles
        foreach (var serviceSymbol in servicesWithAttributes)
        {
            var serviceName = serviceSymbol.Name;

            var hasExternalServiceAttribute = serviceSymbol.GetAttributes()
                .Any(attr =>
                    attr.AttributeClass?.ToDisplayString() ==
                    "IoCTools.Abstractions.Annotations.ExternalServiceAttribute");

            if (hasExternalServiceAttribute) continue;

            var dependencies = GetAllDependenciesForServiceAdapted(serviceSymbol, allRegisteredServices);

            foreach (var dependency in dependencies)
            {
                if (IsCollectionTypeAdapted(dependency))
                    continue;

                if (IsFrameworkTypeAdapted(dependency))
                    continue;

                var dependencyInterfaceName = ExtractServiceNameFromType(dependency);
                if (dependencyInterfaceName != null)
                {
                    if (interfaceToImplementationMap.TryGetValue(dependencyInterfaceName, out var implementationName))
                        detector.AddDependency(serviceName, implementationName);
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
                serviceNameToSymbolMap.TryGetValue(serviceForDiagnostic, out var serviceSymbol))
            {
                var location = serviceSymbol.Locations.FirstOrDefault() ?? Location.None;
                var diagnostic = Diagnostic.Create(DiagnosticDescriptors.CircularDependency, location, cycle);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    /// <summary>
    ///     Report generator errors with consistent format
    /// </summary>
    private static void ReportGeneratorError(SourceProductionContext context,
        string id,
        string title,
        string message)
    {
        var descriptor = new DiagnosticDescriptor(
            id, title, "IoCTools Generator: {0}", "IoCTools",
            DiagnosticSeverity.Warning, true);
        var diagnostic = Diagnostic.Create(descriptor, Location.None, message);
        context.ReportDiagnostic(diagnostic);
    }

    /// <summary>
    ///     IOC012/IOC013: Validate service lifetime dependencies
    /// </summary>
    private static void ValidateLifetimeDependencies(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        InheritanceHierarchyDependencies hierarchyDependencies,
        Dictionary<string, string> serviceLifetimes,
        HashSet<string> allRegisteredServices,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations,
        DiagnosticConfiguration diagnosticConfig,
        INamedTypeSymbol classSymbol)
    {
        var serviceLifetime = GetServiceLifetimeFromSymbol(classSymbol);
        if (serviceLifetime == null) return;

        // IOC014: BackgroundService lifetime validation
        ValidateBackgroundServiceLifetime(context, classDeclaration, classSymbol, serviceLifetime, diagnosticConfig);

        // Check each dependency
        foreach (var dependency in hierarchyDependencies.AllDependenciesWithExternalFlag)
        {
            if (dependency.IsExternal) continue;

            var dependencyTypeName = dependency.ServiceType.ToDisplayString();

            // Enhanced IEnumerable detection: Handle both direct and wrapped IEnumerable dependencies
            // Examples: IEnumerable<T>, Lazy<IEnumerable<T>>, Func<IEnumerable<T>>, Task<IEnumerable<T>>, etc.
            var enumerableTypeInfo = ExtractIEnumerableFromWrappedType(dependencyTypeName);
            if (enumerableTypeInfo != null)
            {
                // Find all implementations of the inner type and validate their lifetimes
                ValidateIEnumerableLifetimes(context, classDeclaration, classSymbol, serviceLifetime,
                    enumerableTypeInfo.InnerType, enumerableTypeInfo.FullEnumerableType, serviceLifetimes,
                    allRegisteredServices, allImplementations);
                continue; // Skip regular validation for IEnumerable
            }

            // Try to find the lifetime for this dependency (handles generic matching)
            var (dependencyLifetime, implementationName) =
                GetDependencyLifetimeWithGenericSupportAndImplementationName(dependencyTypeName, serviceLifetimes,
                    allRegisteredServices, allImplementations);

            if (dependencyLifetime == null) continue;

            // IOC012: Singleton → Scoped (Error)
            if (serviceLifetime == "Singleton" && dependencyLifetime == "Scoped")
            {
                var diagnostic = Diagnostic.Create(DiagnosticDescriptors.SingletonDependsOnScoped,
                    classDeclaration.GetLocation(), classSymbol.Name, implementationName ?? dependencyTypeName);
                context.ReportDiagnostic(diagnostic);
            }
            // IOC013: Singleton → Transient (Warning)
            else if (serviceLifetime == "Singleton" && dependencyLifetime == "Transient")
            {
                // If no implementation name is found, try to find it by scanning all registered services
                var displayName = implementationName ??
                                  FindImplementationNameForInterface(dependencyTypeName, allRegisteredServices) ??
                                  ExtractSimpleTypeNameFromFullName(dependencyTypeName);
                var diagnostic = Diagnostic.Create(DiagnosticDescriptors.SingletonDependsOnTransient,
                    classDeclaration.GetLocation(), classSymbol.Name, displayName);
                context.ReportDiagnostic(diagnostic);
            }
        }

        // IOC015: Inheritance chain lifetime validation
        // NOTE: This validation is now handled by ValidateInheritanceChainLifetimesForSourceProduction earlier 
        // in the pipeline to avoid duplicate diagnostics and leverage better generic type resolution.
        // ValidateInheritanceChainLifetimes(context, classDeclaration, classSymbol, serviceLifetime, serviceLifetimes);
    }

    /// <summary>
    ///     IOC015: Validate inheritance chain lifetime compatibility
    /// </summary>
    private static void ValidateInheritanceChainLifetimes(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        string serviceLifetime,
        Dictionary<string, string> serviceLifetimes)
    {
        // Only validate if this service is Singleton (most restrictive lifetime)
        if (serviceLifetime != "Singleton") return;

        var incompatibleLifetimes = new List<(string serviceName, string lifetime)>();
        var currentType = classSymbol.BaseType;

        // Traverse the inheritance chain upward
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            // Check if the base class is a registered service
            var baseTypeName = currentType.ToDisplayString();
            if (serviceLifetimes.TryGetValue(baseTypeName, out var baseLifetime))
                // Check for lifetime incompatibility
                if (baseLifetime == "Scoped" || baseLifetime == "Transient")
                    incompatibleLifetimes.Add((currentType.Name, baseLifetime));

            currentType = currentType.BaseType;
        }

        // Report IOC015 diagnostic if we found any incompatible lifetimes
        if (incompatibleLifetimes.Any())
        {
            // Find the most problematic lifetime (Scoped is worse than Transient for Singleton)
            var mostProblematicLifetime =
                incompatibleLifetimes.Any(x => x.lifetime == "Scoped") ? "Scoped" : "Transient";

            var diagnostic = Diagnostic.Create(DiagnosticDescriptors.InheritanceChainLifetimeValidation,
                classDeclaration.GetLocation(),
                classSymbol.Name,
                "Singleton",
                mostProblematicLifetime);
            context.ReportDiagnostic(diagnostic);
        }
    }

    /// <summary>
    ///     IOC007: Validate DependsOn vs Inject conflicts
    /// </summary>
    private static void ValidateDependsOnConflicts(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        InheritanceHierarchyDependencies hierarchyDependencies,
        INamedTypeSymbol classSymbol)
    {
        // INHERITANCE FIX: Use RawAllDependencies to avoid deduplication that removes conflicting dependencies
        var dependsOnTypes = new HashSet<string>(hierarchyDependencies.RawAllDependencies
            .Where(d => d.Source == DependencySource.DependsOn)
            .Select(d => d.ServiceType.ToDisplayString()));

        var injectTypes = new HashSet<string>(hierarchyDependencies.RawAllDependencies
            .Where(d => d.Source == DependencySource.Inject)
            .Select(d => d.ServiceType.ToDisplayString()));

        foreach (var dependsOnType in dependsOnTypes)
            if (injectTypes.Contains(dependsOnType))
            {
                var diagnostic = Diagnostic.Create(DiagnosticDescriptors.DependsOnConflictsWithInject,
                    classDeclaration.GetLocation(), dependsOnType, classSymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
    }

    /// <summary>
    ///     IOC006: Validate duplicate dependencies in DependsOn attributes (including inheritance hierarchy)
    ///     CRITICAL FIX: Use proper symbol comparison and include inheritance chain to detect inherited duplicates
    /// </summary>
    private static void ValidateDuplicateDependsOn(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        var dependsOnTypeSymbols = GetDependsOnTypeSymbolsFromInheritanceChain(classSymbol);
        var seenTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var duplicates = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var typeSymbol in dependsOnTypeSymbols)
            if (!seenTypes.Add(typeSymbol))
                duplicates.Add(typeSymbol);

        foreach (var duplicate in duplicates)
        {
            // Use FormatTypeNameForDiagnostic only for display purposes, not for comparison
            var displayName = FormatTypeNameForDiagnostic(duplicate);
            var diagnostic = Diagnostic.Create(DiagnosticDescriptors.DuplicateDependsOnType,
                classDeclaration.GetLocation(), displayName, classSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    /// <summary>
    ///     IOC008: Validate duplicate types within single DependsOn attributes
    ///     CRITICAL FIX: Use proper symbol comparison instead of string names to handle cross-namespace types
    /// </summary>
    private static void ValidateDuplicatesWithinSingleDependsOn(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        foreach (var attr in classSymbol.GetAttributes())
            if (attr.AttributeClass?.Name == "DependsOnAttribute" &&
                attr.AttributeClass?.TypeArguments != null)
            {
                var typeArguments = attr.AttributeClass.TypeArguments.ToList();

                // Use symbol comparison to detect true duplicates
                var duplicates = typeArguments.GroupBy(t => t, SymbolEqualityComparer.Default)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                foreach (var duplicate in duplicates)
                {
                    // Use FormatTypeNameForDiagnostic only for display purposes
                    var displayName = FormatTypeNameForDiagnostic((ITypeSymbol)duplicate);
                    var diagnostic = Diagnostic.Create(
                        DiagnosticDescriptors.DuplicateTypeInSingleDependsOn,
                        classDeclaration.GetLocation(),
                        displayName,
                        classSymbol.Name);
                    context.ReportDiagnostic(diagnostic);
                }
            }
    }

    /// <summary>
    ///     IOC016-IOC019: Validate configuration injection
    /// </summary>
    private static void ValidateConfigurationInjection(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        // Get all [InjectConfiguration] fields from the inheritance hierarchy
        var configurationFields = GetConfigurationFieldsFromHierarchy(classSymbol);

        if (!configurationFields.Any()) return;

        // IOC018: InjectConfiguration requires partial class
        if (!classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
        {
            var diagnostic = Diagnostic.Create(DiagnosticDescriptors.ConfigurationOnNonPartialClass,
                classDeclaration.GetLocation(), classSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }

        // Validate each configuration field
        foreach (var fieldSymbol in configurationFields)
        {
            // IOC019: InjectConfiguration on static field not supported
            if (fieldSymbol.IsStatic)
            {
                var diagnostic = Diagnostic.Create(DiagnosticDescriptors.ConfigurationOnStaticField,
                    classDeclaration.GetLocation(), fieldSymbol.Name, classSymbol.Name);
                context.ReportDiagnostic(diagnostic);
                continue; // Skip further validation for static fields
            }

            // Get the InjectConfiguration attribute
            var configAttribute = fieldSymbol.GetAttributes()
                .FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() ==
                                        "IoCTools.Abstractions.Annotations.InjectConfigurationAttribute");

            if (configAttribute == null) continue;

            // IOC016: Invalid configuration key
            if (configAttribute.ConstructorArguments.Length > 0)
            {
                var key = configAttribute.ConstructorArguments[0].Value?.ToString();
                var validationResult = ValidateConfigurationKey(key);
                if (!validationResult.IsValid)
                {
                    var diagnostic = Diagnostic.Create(DiagnosticDescriptors.InvalidConfigurationKey,
                        classDeclaration.GetLocation(), key ?? "", validationResult.ErrorMessage);
                    context.ReportDiagnostic(diagnostic);
                }
            }

            // IOC017: Unsupported configuration type
            var fieldType = fieldSymbol.Type;
            if (!IsSupportedConfigurationType(fieldType))
            {
                var reasonMessage = GetUnsupportedTypeReason(fieldType);
                var diagnostic = Diagnostic.Create(DiagnosticDescriptors.UnsupportedConfigurationType,
                    classDeclaration.GetLocation(), fieldType.ToDisplayString(), reasonMessage);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    /// <summary>
    ///     Gets all fields with [InjectConfiguration] attribute from the class hierarchy
    /// </summary>
    private static List<IFieldSymbol> GetConfigurationFieldsFromHierarchy(INamedTypeSymbol classSymbol)
    {
        var result = new List<IFieldSymbol>();
        var currentType = classSymbol;

        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            // Get [InjectConfiguration] fields for current type
            foreach (var member in currentType.GetMembers().OfType<IFieldSymbol>())
            {
                var hasInjectConfigurationAttribute = member.GetAttributes()
                    .Any(attr =>
                        attr.AttributeClass?.ToDisplayString() ==
                        "IoCTools.Abstractions.Annotations.InjectConfigurationAttribute");

                if (hasInjectConfigurationAttribute) result.Add(member);
            }

            currentType = currentType.BaseType;
        }

        return result;
    }

    /// <summary>
    ///     IOC011: Validate BackgroundService requirements (partial class requirement)
    ///     Note: IOC014 (background service lifetime validation) is now handled in DependencyValidator
    /// </summary>
    private static void ValidateBackgroundServiceRequirements(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        // Check if this is a BackgroundService
        var isBackgroundService = false;
        var currentServiceType = classSymbol;
        while (currentServiceType != null && !isBackgroundService)
        {
            if (currentServiceType.ToDisplayString() == "Microsoft.Extensions.Hosting.BackgroundService")
            {
                isBackgroundService = true;
                break;
            }

            currentServiceType = currentServiceType.BaseType;
        }

        if (!isBackgroundService) return;

        // Check if class is partial
        var isPartial = classDeclaration.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PartialKeyword));

        // Check if there are [Inject] fields that would require constructor generation
        var hasInjectFields = classSymbol.GetMembers().OfType<IFieldSymbol>()
            .Any(field => field.GetAttributes().Any(attr => attr.AttributeClass?.Name == "InjectAttribute"));

        // Check for DependsOn attributes that would require constructor generation
        var hasDependsOnAttributes = classSymbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.Name?.StartsWith("DependsOn") == true);

        // IOC011: BackgroundService with inject/depends fields must be partial
        if ((hasInjectFields || hasDependsOnAttributes) && !isPartial)
        {
            var diagnostic = Diagnostic.Create(DiagnosticDescriptors.BackgroundServiceNotPartial,
                classDeclaration.GetLocation(), classSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }

        // NOTE: IOC014 background service lifetime validation is now handled by ValidateLifetimeDependencies
        // This ensures IOC014 is properly integrated into the new IIncrementalGenerator validation pipeline
    }

    /// <summary>
    ///     IOC014: BackgroundService lifetime validation
    /// </summary>
    private static void ValidateBackgroundServiceLifetime(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        string serviceLifetime,
        DiagnosticConfiguration diagnosticConfig)
    {
        // Check if lifetime validation is enabled
        if (!diagnosticConfig.LifetimeValidationEnabled) return;

        // Check if this is a BackgroundService (inheritance check)
        var isBackgroundService = false;
        var currentType = classSymbol.BaseType;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            if (currentType.ToDisplayString() == "Microsoft.Extensions.Hosting.BackgroundService")
            {
                isBackgroundService = true;
                break;
            }
            currentType = currentType.BaseType;
        }

        // Check for [BackgroundService] attribute as well
        if (!isBackgroundService)
        {
            isBackgroundService = classSymbol.GetAttributes()
                .Any(attr => attr.AttributeClass?.ToDisplayString() ==
                             "IoCTools.Abstractions.Annotations.BackgroundServiceAttribute");
        }

        if (!isBackgroundService) return;

        // IOC014: Background service lifetime validation
        if (serviceLifetime != "Singleton")
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
                if (suppressArg.Key != null && suppressArg.Value.Value is bool suppress)
                    suppressWarnings = suppress;
            }

            // Only report diagnostic if not suppressed
            if (!suppressWarnings)
            {
                var descriptor = DiagnosticUtilities.CreateDynamicDescriptor(DiagnosticDescriptors.BackgroundServiceLifetimeValidation,
                    diagnosticConfig.LifetimeValidationSeverity);
                var location = GetServiceAttributeLocationForBackgroundService(classDeclaration, classSymbol);
                var diagnostic = Diagnostic.Create(descriptor, location,
                    classSymbol.Name, serviceLifetime);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    /// <summary>
    ///     Gets the most precise location for a service attribute diagnostic, pointing to the attribute or lifetime parameter
    /// </summary>
    private static Location GetServiceAttributeLocationForBackgroundService(TypeDeclarationSyntax classDeclaration,
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
    ///     IOC001/IOC002: Validate missing/unregistered dependencies
    /// </summary>
    private static void ValidateMissingDependencies(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        InheritanceHierarchyDependencies hierarchyDependencies,
        HashSet<string> allRegisteredServices,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations,
        Dictionary<string, string> serviceLifetimes,
        DiagnosticConfiguration diagnosticConfig)
    {
        foreach (var dependency in hierarchyDependencies.AllDependenciesWithExternalFlag)
        {
            // Skip external dependencies - they're expected to be managed outside IoCTools
            if (dependency.IsExternal)
                continue;

            // CRITICAL FIX: Skip configuration injection dependencies - they're not regular DI dependencies
            // Configuration fields (except IConfiguration itself) shouldn't generate IOC001 errors
            if (dependency.Source == DependencySource.ConfigurationInjection && dependency.FieldName != "_configuration")
                continue;

            var dependencyType = dependency.ServiceType.ToDisplayString();

            // Skip framework types and types that are already registered as services
            if (IsFrameworkTypeAdapted(dependencyType) || allRegisteredServices.Contains(dependencyType))
                continue;

            // CRITICAL FIX: Use generic-aware lookup to check if dependency exists
            var (dependencyLifetime, implementationName) = GetDependencyLifetimeWithGenericSupportAndImplementationName(
                dependencyType, serviceLifetimes, allRegisteredServices, allImplementations);

            // Check if implementations exist for this dependency using generic-aware lookup
            var implementationExists = dependencyLifetime != null || allImplementations.ContainsKey(dependencyType);

            if (implementationExists)
            {
                // If we found a lifetime, it means we found a registered implementation
                if (dependencyLifetime != null)
                    // Skip - implementation is properly registered
                    continue;

                // Check if we have direct implementations (non-generic case)
                if (allImplementations.ContainsKey(dependencyType))
                {
                    var implementations = allImplementations[dependencyType];

                    // Check if any implementation has the [Service] attribute
                    var hasRegisteredImplementation = implementations.Any(impl =>
                        allRegisteredServices.Contains(impl.ToDisplayString()));

                    if (hasRegisteredImplementation)
                    {
                        // Skip - at least one implementation is properly registered
                    }
                    else
                    {
                        // IOC002: Implementation exists but not registered - use configured severity
                        var descriptor = DiagnosticUtilities.CreateDynamicDescriptor(
                            DiagnosticDescriptors.ImplementationNotRegistered,
                            diagnosticConfig.UnregisteredImplementationSeverity);
                        var diagnostic = Diagnostic.Create(descriptor,
                            classDeclaration.GetLocation(), classDeclaration.Identifier.ValueText, dependencyType);
                        context.ReportDiagnostic(diagnostic);
                    }
                }
            }
            else
            {
                // IOC001: No implementation found - use configured severity
                var descriptor = DiagnosticUtilities.CreateDynamicDescriptor(
                    DiagnosticDescriptors.NoImplementationFound, diagnosticConfig.NoImplementationSeverity);
                var diagnostic = Diagnostic.Create(descriptor,
                    classDeclaration.GetLocation(), classDeclaration.Identifier.ValueText, dependencyType);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    /// <summary>
    ///     Helper: Get service lifetime from symbol
    /// </summary>
    private static string? GetServiceLifetimeFromSymbol(INamedTypeSymbol classSymbol)
    {
        var serviceAttribute = classSymbol.GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass?.Name == "ServiceAttribute");

        if (serviceAttribute?.ConstructorArguments.Length > 0)
        {
            var lifetimeValue = serviceAttribute.ConstructorArguments[0].Value;
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

        return "Scoped"; // Default
    }

    /// <summary>
    ///     Helper: Get DependsOn types from symbol
    /// </summary>
    private static List<string> GetDependsOnTypes(INamedTypeSymbol classSymbol)
    {
        var types = new List<string>();

        foreach (var attr in classSymbol.GetAttributes())
            if (attr.AttributeClass?.Name == "DependsOnAttribute" &&
                attr.AttributeClass?.TypeArguments != null)
                foreach (var typeArg in attr.AttributeClass.TypeArguments)
                    types.Add(typeArg.ToDisplayString());

        return types;
    }

    /// <summary>
    ///     Helper: Get DependsOn type symbols from symbol for proper symbol-based diagnostics
    ///     CRITICAL FIX: Return actual ITypeSymbol instances for proper cross-namespace handling
    /// </summary>
    private static List<ITypeSymbol> GetDependsOnTypeSymbolsForDiagnostics(INamedTypeSymbol classSymbol)
    {
        var types = new List<ITypeSymbol>();

        foreach (var attr in classSymbol.GetAttributes())
            if (attr.AttributeClass?.Name == "DependsOnAttribute" &&
                attr.AttributeClass?.TypeArguments != null)
                foreach (var typeArg in attr.AttributeClass.TypeArguments)
                    types.Add(typeArg);

        return types;
    }

    /// <summary>
    ///     Get DependsOn type symbols from entire inheritance chain for IOC006 duplicate detection
    /// </summary>
    private static List<ITypeSymbol> GetDependsOnTypeSymbolsFromInheritanceChain(INamedTypeSymbol classSymbol)
    {
        var types = new List<ITypeSymbol>();
        var currentType = classSymbol;

        // Traverse up the inheritance hierarchy
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


    /// <summary>
    ///     Helper: Get DependsOn types from symbol with formatted names for diagnostics
    ///     DEPRECATED: Use GetDependsOnTypeSymbolsForDiagnostics for proper symbol comparison
    /// </summary>
    private static List<string> GetDependsOnTypesForDiagnostics(INamedTypeSymbol classSymbol)
    {
        var types = new List<string>();

        foreach (var attr in classSymbol.GetAttributes())
            if (attr.AttributeClass?.Name == "DependsOnAttribute" &&
                attr.AttributeClass?.TypeArguments != null)
                foreach (var typeArg in attr.AttributeClass.TypeArguments)
                    types.Add(FormatTypeNameForDiagnostic(typeArg));

        return types;
    }

    /// <summary>
    ///     Helper: Check if type supports configuration binding
    /// </summary>
    private static bool IsSupportedConfigurationType(ITypeSymbol type)
    {
        // Primitive types are supported
        if (type.SpecialType != SpecialType.None && type.SpecialType != SpecialType.System_Object) return true;

        // String is supported
        if (type.SpecialType == SpecialType.System_String) return true;

        // Nullable types - check the underlying type
        if (type.OriginalDefinition?.SpecialType == SpecialType.System_Nullable_T &&
            type is INamedTypeSymbol nullableType)
            return IsSupportedConfigurationType(nullableType.TypeArguments.First());

        // Check for arrays - element type must be supported
        if (type is IArrayTypeSymbol arrayType) return IsSupportedConfigurationType(arrayType.ElementType);

        // Named types (classes, interfaces, structs, enums)
        if (type is INamedTypeSymbol namedType)
        {
            var typeName = namedType.ToDisplayString();

            // Enum types are supported
            if (namedType.TypeKind == TypeKind.Enum) return true;

            // Common built-in types that are supported
            var supportedBuiltinTypes = new[]
            {
                "System.DateTime",
                "System.DateTimeOffset",
                "System.TimeSpan",
                "System.Guid",
                "System.Uri",
                "System.Decimal"
            };

            if (supportedBuiltinTypes.Contains(typeName)) return true;

            // Options pattern interfaces are supported
            if (typeName.StartsWith("Microsoft.Extensions.Options.IOptions") ||
                typeName.StartsWith("Microsoft.Extensions.Options.IOptionsSnapshot") ||
                typeName.StartsWith("Microsoft.Extensions.Options.IOptionsMonitor"))
                return true;

            // Collection types with supported element types
            if (typeName.StartsWith("System.Collections.Generic.List<") ||
                typeName.StartsWith("System.Collections.Generic.IList<") ||
                typeName.StartsWith("System.Collections.Generic.ICollection<") ||
                typeName.StartsWith("System.Collections.Generic.IEnumerable<") ||
                typeName.StartsWith("System.Collections.Generic.Dictionary<") ||
                typeName.StartsWith("System.Collections.Generic.IDictionary<"))
                // For generic collections, check if element types are supported
                if (namedType.TypeArguments.Length > 0)
                    return namedType.TypeArguments.All(arg => IsSupportedConfigurationType(arg));

            // Interface types are generally not supported (except those handled above)
            if (namedType.TypeKind == TypeKind.Interface) return false;

            // Abstract classes are not supported
            if (namedType.IsAbstract) return false;

            // For complex types, check if they have parameterless constructor
            return namedType.Constructors.Any(c => c.Parameters.Length == 0);
        }

        return false;
    }

    /// <summary>
    ///     Helper: Get reason why a configuration type is not supported
    /// </summary>
    private static string GetUnsupportedTypeReason(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Interface)
        {
            if (type is INamedTypeSymbol interfaceType)
            {
                var typeName = interfaceType.ToDisplayString();
                if (typeName.StartsWith("Microsoft.Extensions.Options."))
                    // Options pattern should be supported, this shouldn't reach here
                    return "requires parameterless constructor";
            }

            return "Interfaces cannot be bound from configuration";
        }

        if (type.IsAbstract) return "Abstract types cannot be bound from configuration";

        if (type is IArrayTypeSymbol arrayType) return "Array element type is not supported for configuration binding";

        if (type is INamedTypeSymbol namedType)
        {
            var typeName = namedType.ToDisplayString();
            if (typeName.StartsWith("System.Collections.Generic."))
                return "Collection element type is not supported for configuration binding";
        }

        return "requires parameterless constructor";
    }

    /// <summary>
    ///     Helper: Validate configuration key for IOC016
    /// </summary>
    private static ConfigurationKeyValidationResult ValidateConfigurationKey(string? key)
    {
        // Empty or null key
        if (string.IsNullOrEmpty(key))
            return new ConfigurationKeyValidationResult
            {
                IsValid = false,
                ErrorMessage = "empty or whitespace-only"
            };

        // Whitespace-only key
        if (string.IsNullOrWhiteSpace(key))
            return new ConfigurationKeyValidationResult
            {
                IsValid = false,
                ErrorMessage = "empty or whitespace-only"
            };

        // Contains double colons
        if (key.Contains("::"))
            return new ConfigurationKeyValidationResult
            {
                IsValid = false,
                ErrorMessage = "contains double colons (::)"
            };

        // Starts or ends with colon
        if (key.StartsWith(":") || key.EndsWith(":"))
            return new ConfigurationKeyValidationResult
            {
                IsValid = false,
                ErrorMessage = "cannot start or end with a colon (:)"
            };

        // Contains invalid characters
        if (key.Any(c => c == '\0' || c == '\r' || c == '\n' || c == '\t'))
            return new ConfigurationKeyValidationResult
            {
                IsValid = false,
                ErrorMessage = "contains invalid characters (null, carriage return, newline, or tab)"
            };

        return new ConfigurationKeyValidationResult
        {
            IsValid = true,
            ErrorMessage = ""
        };
    }

    /// <summary>
    ///     Validates attribute combinations and reports diagnostics for invalid combinations (adapted for
    ///     SourceProductionContext)
    /// </summary>
    private static void ValidateAttributeCombinations(SourceProductionContext context,
        IEnumerable<INamedTypeSymbol> servicesWithAttributes)
    {
        // Group by class name to avoid processing the same class multiple times (for partial classes)
        var uniqueClasses = servicesWithAttributes
            .GroupBy(symbol => symbol.ToDisplayString())
            .Select(g => g.First())
            .ToList();

        foreach (var classSymbol in uniqueClasses)
        {
            var syntaxReferences = classSymbol.DeclaringSyntaxReferences;
            if (!syntaxReferences.Any()) continue;

            var syntaxRef = syntaxReferences.First();
            var classDeclaration = syntaxRef.GetSyntax() as TypeDeclarationSyntax;
            if (classDeclaration == null) continue;

            // Check for SkipRegistration without RegisterAsAll (IOC005)
            var hasSkipRegistration = classSymbol.GetAttributes()
                .Any(attr => attr.AttributeClass?.Name?.StartsWith("SkipRegistrationAttribute") == true);

            var hasRegisterAsAll = classSymbol.GetAttributes()
                .Any(attr => attr.AttributeClass?.Name == "RegisterAsAllAttribute");

            if (hasSkipRegistration && !hasRegisterAsAll)
            {
                var diagnostic = Diagnostic.Create(
                    DiagnosticDescriptors.SkipRegistrationWithoutRegisterAsAll,
                    classDeclaration.GetLocation(),
                    classSymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }

            // Check for RegisterAsAll without Service (IOC004) - This is the target fix
            if (hasRegisterAsAll)
            {
                var hasServiceAttribute = classSymbol.GetAttributes()
                    .Any(attr =>
                        attr.AttributeClass?.ToDisplayString() == "IoCTools.Abstractions.Annotations.ServiceAttribute");

                if (!hasServiceAttribute)
                {
                    var diagnostic = Diagnostic.Create(
                        DiagnosticDescriptors.RegisterAsAllRequiresService,
                        classDeclaration.GetLocation(),
                        classSymbol.Name);
                    context.ReportDiagnostic(diagnostic);
                }
            }

            // Validate conditional services
            ValidateConditionalServices(context, classSymbol, classDeclaration);
        }
    }

    /// <summary>
    ///     Validates conditional service attributes and reports diagnostics for invalid configurations (adapted for
    ///     SourceProductionContext)
    /// </summary>
    private static void ValidateConditionalServices(SourceProductionContext context,
        INamedTypeSymbol classSymbol,
        TypeDeclarationSyntax classDeclaration)
    {
        var conditionalServiceAttributes = classSymbol.GetAttributes()
            .Where(attr => attr.AttributeClass?.ToDisplayString() ==
                           "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute")
            .ToList();

        if (!conditionalServiceAttributes.Any()) return;

        var hasServiceAttribute = classSymbol.GetAttributes()
            .Any(attr =>
                attr.AttributeClass?.ToDisplayString() == "IoCTools.Abstractions.Annotations.ServiceAttribute");

        // IOC021: ConditionalService requires Service attribute
        if (!hasServiceAttribute)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.ConditionalServiceMissingServiceAttribute,
                classDeclaration.GetLocation(),
                classSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }

        // IOC026: Multiple ConditionalService attributes
        if (conditionalServiceAttributes.Count > 1)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.ConditionalServiceMultipleAttributes,
                classDeclaration.GetLocation(),
                classSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }

        // Validate each conditional service attribute
        foreach (var conditionalAttribute in conditionalServiceAttributes)
        {
            var validationResult = ConditionalServiceEvaluator.ValidateConditionsDetailed(conditionalAttribute);

            if (!validationResult.IsValid)
                foreach (var error in validationResult.Errors)
                    if (error.Contains("No conditions specified"))
                    {
                        // IOC022: Empty conditions
                        var diagnostic = Diagnostic.Create(
                            DiagnosticDescriptors.ConditionalServiceEmptyConditions,
                            classDeclaration.GetLocation(),
                            classSymbol.Name);
                        context.ReportDiagnostic(diagnostic);
                    }
                    else if (error.Contains("conflict"))
                    {
                        // IOC020: Conflicting conditions
                        var diagnostic = Diagnostic.Create(
                            DiagnosticDescriptors.ConditionalServiceConflictingConditions,
                            classDeclaration.GetLocation(),
                            classSymbol.Name,
                            error);
                        context.ReportDiagnostic(diagnostic);
                    }
                    else if (error.Contains("ConfigValue") && error.Contains("without Equals or NotEquals"))
                    {
                        // IOC023: ConfigValue without comparison
                        var configValue = validationResult.ConfigValue ?? "unknown";
                        var diagnostic = Diagnostic.Create(
                            DiagnosticDescriptors.ConditionalServiceConfigValueWithoutComparison,
                            classDeclaration.GetLocation(),
                            classSymbol.Name,
                            configValue);
                        context.ReportDiagnostic(diagnostic);
                    }
                    else if (error.Contains("Equals or NotEquals") && error.Contains("without ConfigValue"))
                    {
                        // IOC024: Comparison without ConfigValue
                        var diagnostic = Diagnostic.Create(
                            DiagnosticDescriptors.ConditionalServiceComparisonWithoutConfigValue,
                            classDeclaration.GetLocation(),
                            classSymbol.Name);
                        context.ReportDiagnostic(diagnostic);
                    }
                    else if (error.Contains("ConfigValue is empty"))
                    {
                        // IOC025: Empty ConfigValue
                        var diagnostic = Diagnostic.Create(
                            DiagnosticDescriptors.ConditionalServiceEmptyConfigKey,
                            classDeclaration.GetLocation(),
                            classSymbol.Name);
                        context.ReportDiagnostic(diagnostic);
                    }
        }
    }

    /// <summary>
    ///     Helper: Check if type is framework type (adapted)
    /// </summary>
    private static bool IsFrameworkTypeAdapted(string typeName)
    {
        var frameworkTypes = new[]
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

        return frameworkTypes.Contains(typeName) ||
               frameworkTypes.Any(ft => ft.EndsWith("<>") && typeName.StartsWith(ft.Substring(0, ft.Length - 2)));
    }

    /// <summary>
    ///     Helper: Check if type is collection type (adapted)
    ///     CRITICAL: Collection types should be ignored in circular dependency detection
    ///     because they represent "has many" relationships, not direct dependencies
    /// </summary>
    private static bool IsCollectionTypeAdapted(string typeName) => typeName.EndsWith("[]") ||
                                                                    typeName.StartsWith(
                                                                        "System.Collections.Generic.IEnumerable<") ||
                                                                    typeName.StartsWith(
                                                                        "System.Collections.Generic.IList<") ||
                                                                    typeName.StartsWith(
                                                                        "System.Collections.Generic.List<") ||
                                                                    typeName.StartsWith(
                                                                        "System.Collections.Generic.ICollection<") ||
                                                                    typeName.StartsWith(
                                                                        "System.Collections.Generic.IReadOnlyList<") ||
                                                                    typeName.StartsWith(
                                                                        "System.Collections.Generic.IReadOnlyCollection<") ||
                                                                    typeName.Contains("IEnumerable<") ||
                                                                    typeName.Contains("IList<") ||
                                                                    typeName.Contains("ICollection<") ||
                                                                    typeName.Contains("List<") ||
                                                                    // Handle cases where namespace might be omitted in testing
                                                                    typeName.StartsWith("IEnumerable<") ||
                                                                    typeName.StartsWith("IList<") ||
                                                                    typeName.StartsWith("ICollection<") ||
                                                                    typeName.StartsWith("List<");

    /// <summary>
    ///     Helper: Get all dependencies for service (adapted)
    /// </summary>
    private static List<string> GetAllDependenciesForServiceAdapted(INamedTypeSymbol serviceSymbol,
        HashSet<string> allRegisteredServices)
    {
        var dependencies = new List<string>();

        // Get DependsOn types
        dependencies.AddRange(GetDependsOnTypes(serviceSymbol));

        // Get Inject field types
        foreach (var member in serviceSymbol.GetMembers().OfType<IFieldSymbol>())
        {
            var hasInjectAttribute = member.GetAttributes()
                .Any(attr => attr.AttributeClass?.Name == "InjectAttribute");

            if (hasInjectAttribute) dependencies.Add(member.Type.ToDisplayString());
        }

        return dependencies;
    }

    /// <summary>
    ///     Helper: Get dependency lifetime with generic type support
    /// </summary>
    private static (string? lifetime, string? implementationName)
        GetDependencyLifetimeWithGenericSupportAndImplementationName(
            string dependencyTypeName,
            Dictionary<string, string> serviceLifetimes,
            HashSet<string> allRegisteredServices,
            Dictionary<string, List<INamedTypeSymbol>>? allImplementations = null)
    {
        // First try direct match in service lifetimes (interface or class)
        if (serviceLifetimes.TryGetValue(dependencyTypeName,
                out var lifetime)) return (lifetime, null); // No implementation name needed for direct matches

        // If it's a constructed generic type (e.g., "IService<string, int, DateTime>"), 
        // try to find open generic interface (e.g., "IService<T1, T2, T3>")
        if (IsConstructedGenericTypeSimple(dependencyTypeName))
        {
            var baseName = ExtractBaseTypeNameFromConstructed(dependencyTypeName);
            var typeParamCount = CountTypeParameters(dependencyTypeName);

            // Look for matching open generic interface registrations first
            foreach (var registeredService in allRegisteredServices)
                if (IsMatchingOpenGeneric(baseName, typeParamCount, registeredService))
                    if (serviceLifetimes.TryGetValue(registeredService, out var openGenericLifetime))
                        return (openGenericLifetime, null); // No implementation name needed for direct matches

            // CRITICAL FIX: If no direct interface match, look for implementing classes
            if (allImplementations != null)
            {
                // Look through all implementations to find classes that implement this interface
                foreach (var kvp in allImplementations)
                {
                    var interfaceName = kvp.Key;
                    var implementations = kvp.Value;

                    // Check if this interface could match our dependency (generic matching)
                    if (IsMatchingOpenGeneric(baseName, typeParamCount, interfaceName))
                        // Found matching open generic interface, check its implementations
                        foreach (var impl in implementations)
                        {
                            var implTypeName = impl.ToDisplayString();
                            if (serviceLifetimes.TryGetValue(implTypeName, out var implLifetime))
                            {
                                // Return both the lifetime and the implementation name (formatted for display)
                                var formattedImplName = FormatTypeNameForDiagnostic(impl);
                                return (implLifetime, formattedImplName);
                            }
                        }
                }

                // ENHANCED FIX: Also try direct implementation scanning by checking what interfaces each implementation implements
                foreach (var kvp in allImplementations)
                {
                    var interfaceKey = kvp.Key;
                    var implementations = kvp.Value;

                    foreach (var impl in implementations)
                        // Check if this implementation directly implements the interface we're looking for
                    foreach (var implementedInterface in impl.AllInterfaces)
                    {
                        var implementedInterfaceName = implementedInterface.ToDisplayString();

                        // If the implemented interface matches our dependency type pattern
                        if (IsMatchingGenericInterface(dependencyTypeName, implementedInterfaceName))
                        {
                            var implTypeName = impl.ToDisplayString();
                            if (serviceLifetimes.TryGetValue(implTypeName, out var implLifetime))
                            {
                                var formattedImplName = FormatTypeNameForDiagnostic(impl);
                                return (implLifetime, formattedImplName);
                            }
                        }
                    }
                }
            }
        }

        return (null, null);
    }

    private static string? GetDependencyLifetimeWithGenericSupport(string dependencyTypeName,
        Dictionary<string, string> serviceLifetimes,
        HashSet<string> allRegisteredServices,
        Dictionary<string, List<INamedTypeSymbol>>? allImplementations = null) =>
        GetDependencyLifetimeWithGenericSupportAndImplementationName(dependencyTypeName, serviceLifetimes,
            allRegisteredServices, allImplementations).lifetime;

    /// <summary>
    ///     Extract inner type from IEnumerable<T> type name
    /// </summary>
    private static string? ExtractInnerTypeFromIEnumerable(string enumerableTypeName)
    {
        const string prefix = "System.Collections.Generic.IEnumerable<";
        if (enumerableTypeName.StartsWith(prefix) && enumerableTypeName.EndsWith(">"))
        {
            var innerType = enumerableTypeName.Substring(prefix.Length, enumerableTypeName.Length - prefix.Length - 1);
            return innerType.Trim();
        }

        return null;
    }

    /// <summary>
    ///     Extract IEnumerable
    ///     <T>
    ///         from potentially wrapped types like Lazy<IEnumerable
    ///         <T>
    ///             >, Func<IEnumerable
    ///             <T>
    ///                 >, etc.
    ///                 Returns the inner type T and the full IEnumerable<T> type string for validation purposes.
    /// </summary>
    private static EnumerableTypeInfo? ExtractIEnumerableFromWrappedType(string typeName)
    {
        // Direct IEnumerable<T> case
        if (typeName.StartsWith("System.Collections.Generic.IEnumerable<") && typeName.EndsWith(">"))
        {
            var innerType = ExtractInnerTypeFromIEnumerable(typeName);
            if (innerType != null) return new EnumerableTypeInfo(innerType, typeName);
        }

        // Wrapped IEnumerable cases: recursively search through generic type arguments
        return ExtractIEnumerableFromGenericArguments(typeName);
    }

    /// <summary>
    ///     Recursively search through generic type arguments to find nested IEnumerable
    ///     <T>
    ///         dependencies
    ///         Handles cases like Lazy<IEnumerable<T>>, Func<IEnumerable<T>>, Task<IEnumerable<T>>, etc.
    /// </summary>
    private static EnumerableTypeInfo? ExtractIEnumerableFromGenericArguments(string typeName)
    {
        // Find generic arguments by looking for < and matching >
        var genericStart = typeName.IndexOf('<');
        if (genericStart == -1) return null;

        var depth = 0;
        var start = genericStart + 1;
        var argumentStart = start;

        for (var i = start; i < typeName.Length; i++)
            if (typeName[i] == '<')
            {
                depth++;
            }
            else if (typeName[i] == '>')
            {
                if (depth == 0)
                {
                    // Extract the final argument
                    var argument = typeName.Substring(argumentStart, i - argumentStart).Trim();

                    // Check if this argument is IEnumerable<T>
                    if (argument.StartsWith("System.Collections.Generic.IEnumerable<") && argument.EndsWith(">"))
                    {
                        var innerType = ExtractInnerTypeFromIEnumerable(argument);
                        if (innerType != null) return new EnumerableTypeInfo(innerType, argument);
                    }

                    // Recursively check this argument for nested IEnumerable
                    var nestedResult = ExtractIEnumerableFromGenericArguments(argument);
                    if (nestedResult != null) return nestedResult;

                    break;
                }

                depth--;
            }
            else if (typeName[i] == ',' && depth == 0)
            {
                // Extract this argument
                var argument = typeName.Substring(argumentStart, i - argumentStart).Trim();

                // Check if this argument is IEnumerable<T>
                if (argument.StartsWith("System.Collections.Generic.IEnumerable<") && argument.EndsWith(">"))
                {
                    var innerType = ExtractInnerTypeFromIEnumerable(argument);
                    if (innerType != null) return new EnumerableTypeInfo(innerType, argument);
                }

                // Recursively check this argument for nested IEnumerable
                var nestedResult = ExtractIEnumerableFromGenericArguments(argument);
                if (nestedResult != null) return nestedResult;

                // Move to next argument
                argumentStart = i + 1;
            }

        return null;
    }

    /// <summary>
    ///     Validate IEnumerable<T> dependency lifetimes by checking all implementations of T
    /// </summary>
    private static void ValidateIEnumerableLifetimes(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        string serviceLifetime,
        string innerType,
        string dependencyTypeName,
        Dictionary<string, string> serviceLifetimes,
        HashSet<string> allRegisteredServices,
        Dictionary<string, List<INamedTypeSymbol>> allImplementations)
    {
        // CRITICAL FIX: Use proper interface-to-implementation mapping with deduplication

        // Strategy 1: Direct lookup in allImplementations dictionary with deduplication
        var foundImplementations = false;
        var processedImplementations = new HashSet<string>(); // Deduplicate by type name

        if (allImplementations.TryGetValue(innerType, out var directImplementations))
        {
            foundImplementations = true;
            foreach (var implementation in directImplementations)
            {
                // CRITICAL FIX: Deduplicate by implementation type name to avoid duplicate diagnostics
                if (!processedImplementations.Add(implementation.ToDisplayString()))
                    continue; // Skip if already processed

                var implementationLifetime = GetServiceLifetimeFromSymbol(implementation);
                if (implementationLifetime == null) continue;

                // IOC012: Singleton → Scoped (Error)
                if (serviceLifetime == "Singleton" && implementationLifetime == "Scoped")
                {
                    var diagnostic = Diagnostic.Create(DiagnosticDescriptors.SingletonDependsOnScoped,
                        classDeclaration.GetLocation(), classSymbol.Name,
                        $"{dependencyTypeName} -> {implementation.Name}");
                    context.ReportDiagnostic(diagnostic);
                }
                // IOC013: Singleton → Transient (Warning)  
                else if (serviceLifetime == "Singleton" && implementationLifetime == "Transient")
                {
                    var diagnostic = Diagnostic.Create(DiagnosticDescriptors.SingletonDependsOnTransient,
                        classDeclaration.GetLocation(), classSymbol.Name,
                        $"{dependencyTypeName} -> {implementation.Name}");
                    context.ReportDiagnostic(diagnostic);
                }
            }
        }

        // Strategy 2: ENHANCED GENERIC TYPE MATCHING - Match constructed generics with open generics
        // This handles cases like IRepository<string> matching with Repository<T>
        if (innerType.Contains('<') && innerType.Contains('>'))
        {
            // Extract base generic type from constructed generic (e.g., "IRepository<string>" -> "IRepository<T>")
            var baseGenericType = ExtractBaseGenericInterface(innerType);
            if (baseGenericType != null &&
                allImplementations.TryGetValue(baseGenericType, out var genericImplementations))
            {
                foundImplementations = true;
                foreach (var implementation in genericImplementations)
                {
                    // CRITICAL FIX: Apply same deduplication logic here
                    if (!processedImplementations.Add(implementation.ToDisplayString()))
                        continue; // Skip if already processed

                    var implementationLifetime = GetServiceLifetimeFromSymbol(implementation);
                    if (implementationLifetime == null) continue;

                    // IOC012: Singleton → Scoped (Error)
                    if (serviceLifetime == "Singleton" && implementationLifetime == "Scoped")
                    {
                        var diagnostic = Diagnostic.Create(DiagnosticDescriptors.SingletonDependsOnScoped,
                            classDeclaration.GetLocation(), classSymbol.Name,
                            $"{dependencyTypeName} -> {implementation.Name}");
                        context.ReportDiagnostic(diagnostic);
                    }
                    // IOC013: Singleton → Transient (Warning)  
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

        // Strategy 3: Fallback - comprehensive interface scanning (only if no implementations found)
        if (!foundImplementations)
            foreach (var kvp in allImplementations)
            {
                var interfaceType = kvp.Key;
                var implementations = kvp.Value;

                // Check if any implementations implement our target interface  
                foreach (var implementation in implementations)
                {
                    // CRITICAL FIX: Apply same deduplication logic here
                    if (!processedImplementations.Add(implementation.ToDisplayString()))
                        continue; // Skip if already processed

                    var implementedInterfaces = implementation.AllInterfaces.Select(i => i.ToDisplayString());
                    if (!implementedInterfaces.Contains(innerType)) continue;

                    var implementationLifetime = GetServiceLifetimeFromSymbol(implementation);
                    if (implementationLifetime == null) continue;

                    // IOC012: Singleton → Scoped (Error)
                    if (serviceLifetime == "Singleton" && implementationLifetime == "Scoped")
                    {
                        var diagnostic = Diagnostic.Create(DiagnosticDescriptors.SingletonDependsOnScoped,
                            classDeclaration.GetLocation(), classSymbol.Name,
                            $"{dependencyTypeName} -> {implementation.Name}");
                        context.ReportDiagnostic(diagnostic);
                    }
                    // IOC013: Singleton → Transient (Warning)  
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

    // REMOVED: Legacy name-based heuristic methods replaced with proper symbol resolution

    /// <summary>
    ///     Helper: Check if type is constructed generic (simple version)
    /// </summary>
    private static bool IsConstructedGenericTypeSimple(string typeName) =>
        typeName.Contains('<') && typeName.Contains('>') && !typeName.EndsWith("<>");

    /// <summary>
    ///     Helper: Extract base type name from constructed generic
    /// </summary>
    private static string ExtractBaseTypeNameFromConstructed(string constructedType)
    {
        var angleIndex = constructedType.IndexOf('<');
        return angleIndex >= 0 ? constructedType.Substring(0, angleIndex) : constructedType;
    }

    /// <summary>
    ///     Extract base generic interface from constructed generic type.
    ///     E.g., "IRepository
    ///     <string>
    ///         " -> "IRepository
    ///         <T>
    ///             "
    ///             E.g., "IHandler<ICommand<string>>" -> "IHandler<ICommand<T>>"
    /// </summary>
    private static string? ExtractBaseGenericInterface(string constructedType)
    {
        if (!constructedType.Contains('<') || !constructedType.Contains('>'))
            return null;

        // CRITICAL FIX: Handle nested generic types properly
        // Instead of replacing all type arguments with T, T1, T2, etc.,
        // we need to recursively process nested generics
        return ConvertToOpenGenericForm(constructedType);
    }

    /// <summary>
    ///     Convert a constructed generic type to its open generic form, preserving nested generic structure
    ///     Examples:
    ///     "IHandler
    ///     <string>
    ///         " -> "IHandler
    ///         <T>
    ///             "
    ///             "IHandler<ICommand
    ///             <string>
    ///                 >" -> "IHandler<ICommand
    ///                 <T>
    ///                     >"
    ///                     "IRepository<Dictionary<string, int>>" -> "IRepository<Dictionary<T1, T2>>"
    /// </summary>
    private static string ConvertToOpenGenericForm(string constructedType)
    {
        if (!constructedType.Contains('<') || !constructedType.Contains('>'))
            return constructedType; // Not a generic type, return as-is

        var angleStart = constructedType.IndexOf('<');
        var angleEnd = constructedType.LastIndexOf('>');

        if (angleStart >= 0 && angleEnd > angleStart)
        {
            var baseName = constructedType.Substring(0, angleStart);
            var typeArgsString = constructedType.Substring(angleStart + 1, angleEnd - angleStart - 1);

            // Parse and convert each type argument
            var convertedTypeArgs = ConvertTypeArgumentsToOpenForm(typeArgsString);

            return $"{baseName}<{string.Join(", ", convertedTypeArgs)}>";
        }

        return constructedType;
    }

    /// <summary>
    ///     Convert type arguments string to open generic form
    ///     Examples:
    ///     "string, int" -> ["T1", "T2"]
    ///     "ICommand
    ///     <string>
    ///         " -> ["ICommand
    ///         <T>
    ///             "]
    ///             "string, ICommand<int>" -> ["T1", "ICommand<T>"]
    /// </summary>
    private static List<string> ConvertTypeArgumentsToOpenForm(string typeArgsString)
    {
        var result = new List<string>();
        var typeArgs = SplitTopLevelTypeArguments(typeArgsString);

        var simpleTypeCounter = 1;

        foreach (var typeArg in typeArgs)
        {
            var trimmedArg = typeArg.Trim();

            if (trimmedArg.Contains('<') && trimmedArg.Contains('>'))
            {
                // This is a nested generic type, recursively convert it
                result.Add(ConvertToOpenGenericForm(trimmedArg));
            }
            else
            {
                // This is a simple type, replace with T parameter
                var paramName = simpleTypeCounter == 1 ? "T" : $"T{simpleTypeCounter}";
                result.Add(paramName);
                simpleTypeCounter++;
            }
        }

        return result;
    }

    /// <summary>
    ///     Split type arguments at the top level, respecting nested generics
    ///     Example: "string, List<int>, Dictionary<string, int>" -> ["string", "List<int>", "Dictionary<string, int>"]
    /// </summary>
    private static List<string> SplitTopLevelTypeArguments(string typeArgsString)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(typeArgsString))
            return result;

        var depth = 0;
        var lastStart = 0;

        for (var i = 0; i < typeArgsString.Length; i++)
        {
            var c = typeArgsString[i];

            if (c == '<')
            {
                depth++;
            }
            else if (c == '>')
            {
                depth--;
            }
            else if (c == ',' && depth == 0)
            {
                // Found a top-level comma
                result.Add(typeArgsString.Substring(lastStart, i - lastStart));
                lastStart = i + 1;
            }
        }

        // Add the last type argument
        if (lastStart < typeArgsString.Length) result.Add(typeArgsString.Substring(lastStart));

        return result;
    }

    /// <summary>
    ///     Helper: Count type parameters in constructed generic
    /// </summary>
    private static int CountTypeParameters(string constructedType)
    {
        var start = constructedType.IndexOf('<');
        var end = constructedType.LastIndexOf('>');
        if (start >= 0 && end > start)
        {
            var typeParams = constructedType.Substring(start + 1, end - start - 1);
            return CountTopLevelTypeParameters(typeParams);
        }

        return 0;
    }

    /// <summary>
    ///     Count top-level type parameters, handling nested generics correctly.
    ///     Example: "string, int, List<string>, Dictionary<string, int>" should return 4, not 6.
    /// </summary>
    private static int CountTopLevelTypeParameters(string typeParamsString)
    {
        if (string.IsNullOrWhiteSpace(typeParamsString))
            return 0;

        var count = 0;
        var depth = 0;
        var lastStart = 0;

        for (var i = 0; i < typeParamsString.Length; i++)
        {
            var c = typeParamsString[i];

            if (c == '<')
            {
                depth++;
            }
            else if (c == '>')
            {
                depth--;
            }
            else if (c == ',' && depth == 0)
            {
                // This is a top-level comma - it separates type parameters
                count++;
                lastStart = i + 1;
            }
        }

        // Count the last parameter (after the final comma or the only parameter)
        if (lastStart < typeParamsString.Length && typeParamsString.Substring(lastStart).Trim().Length > 0) count++;

        return count;
    }

    /// <summary>
    ///     Helper: Check if registered service matches the open generic pattern
    /// </summary>
    private static bool IsMatchingOpenGeneric(string baseName,
        int typeParamCount,
        string registeredService)
    {
        if (!registeredService.StartsWith(baseName + "<")) return false;

        var registeredTypeParamCount = CountTypeParameters(registeredService);
        return registeredTypeParamCount == typeParamCount;
    }

    /// <summary>
    ///     Formats type names for diagnostic messages in a user-friendly way.
    ///     Removes namespace prefixes to make messages more readable.
    /// </summary>
    private static string FormatTypeNameForDiagnostic(ITypeSymbol typeSymbol)
    {
        // For generic types like "Test.IRepository<Test.User>", simplify to "IRepository<User>"
        if (typeSymbol is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var typeName = namedType.Name;
            var typeArgs = namedType.TypeArguments
                .Select(arg => FormatTypeNameForDiagnostic(arg)) // Recursive to handle nested generics
                .ToArray();

            return typeArgs.Length > 0 ? $"{typeName}<{string.Join(", ", typeArgs)}>" : typeName;
        }

        // For non-generic types, just use the name
        return typeSymbol.Name;
    }

    /// <summary>
    ///     Attempts to find the implementation name for a given interface by scanning registered services.
    ///     This is a fallback method for when the complex type resolution fails.
    ///     For example:
    ///     - Interface: "TestNamespace.IService
    ///     <string, int, DateTime>
    ///         "
    ///         - Registered services contain: "TestNamespace.ProcessorService
    ///         <T1, T2, T3>
    ///             "
    ///             - Should return: "ProcessorService"
    /// </summary>
    private static string? FindImplementationNameForInterface(string interfaceTypeName,
        HashSet<string> allRegisteredServices)
    {
        // Extract the base interface name (e.g., "IService" from "TestNamespace.IService<string, int, DateTime>")
        var interfaceBaseName = ExtractSimpleTypeNameFromFullName(interfaceTypeName);

        // Try to find a service that could implement this interface
        // Look for services with similar names (e.g., IService -> ProcessorService, MyService, etc.)
        foreach (var registeredService in allRegisteredServices)
        {
            var serviceBaseName = ExtractSimpleTypeNameFromFullName(registeredService);

            // Check if this could be an implementation of the interface
            // Pattern 1: IService -> ProcessorService, UserService, etc. (contains "Service")
            if (interfaceBaseName.StartsWith("I") && interfaceBaseName.Length > 1 &&
                serviceBaseName.EndsWith("Service") && serviceBaseName.Contains(interfaceBaseName.Substring(1)))
                return serviceBaseName;

            // Pattern 2: IService -> SomethingService (ends with Service)
            if (interfaceBaseName.StartsWith("I") && serviceBaseName.EndsWith("Service"))
            {
                // Check if the service name contains the interface name without the 'I'
                var interfaceRoot = interfaceBaseName.Substring(1);
                if (serviceBaseName.Contains(interfaceRoot)) return serviceBaseName;
            }
        }

        return null;
    }

    /// <summary>
    ///     Checks if two generic interface types match at the generic type definition level.
    ///     Examples:
    ///     - "IService
    ///     <string, int, DateTime>
    ///         " matches "IService
    ///         <T1, T2, T3>
    ///             " -> true
    ///             - "IService
    ///             <string, int>
    ///                 " matches "IService
    ///                 <T1, T2, T3>
    ///                     " -> false (different arity)
    ///                     - "IRepository<User>" matches "IService<T>" -> false (different interface)
    /// </summary>
    private static bool IsMatchingGenericInterface(string constructedType,
        string implementedInterfaceType)
    {
        // Both must be generic types
        if (!constructedType.Contains('<') || !implementedInterfaceType.Contains('<'))
            return false;

        // Extract base names and parameter counts
        var constructedBaseName = ExtractBaseTypeNameFromConstructed(constructedType);
        var constructedParamCount = CountTypeParameters(constructedType);

        var implementedBaseName = ExtractBaseTypeNameFromConstructed(implementedInterfaceType);
        var implementedParamCount = CountTypeParameters(implementedInterfaceType);

        // Must have same base name (interface name) and same number of type parameters
        return constructedBaseName == implementedBaseName && constructedParamCount == implementedParamCount;
    }

    /// <summary>
    ///     Extracts a simple, readable type name from a full type name for diagnostic display
    ///     Examples:
    ///     - "TestNamespace.IService
    ///     <string, int, DateTime>
    ///         " -> "IService"
    ///         - "MyApp.ProcessorService<T1, T2, T3>" -> "ProcessorService"
    /// </summary>
    private static string ExtractSimpleTypeNameFromFullName(string fullTypeName)
    {
        // Remove namespace by finding the last dot before any generic parameters
        var genericStart = fullTypeName.IndexOf('<');
        var searchEnd = genericStart >= 0 ? genericStart : fullTypeName.Length;

        var lastDotIndex = fullTypeName.LastIndexOf('.', searchEnd - 1);
        var typeName = lastDotIndex >= 0 ? fullTypeName.Substring(lastDotIndex + 1) : fullTypeName;

        // Remove generic parameters for clean display
        var angleIndex = typeName.IndexOf('<');
        if (angleIndex >= 0) typeName = typeName.Substring(0, angleIndex);

        return typeName;
    }

    /// <summary>
    ///     Helper: Extract service name from type for circular dependency detection
    ///     CRITICAL FIX: Preserve generic type parameters to prevent AnotherService<T> -> AnotherService confusion
    /// </summary>
    private static string? ExtractServiceNameFromType(string dependencyType)
    {
        var typeName = dependencyType;
        var lastDotIndex = typeName.LastIndexOf('.');
        if (lastDotIndex >= 0) typeName = typeName.Substring(lastDotIndex + 1);

        // CRITICAL FIX: DO NOT strip generic type parameters - they are essential for correct symbol resolution
        // The bug was here: Generic types like "AnotherService<T>" were being reduced to "AnotherService"
        // This caused dependencies on "AnotherService<string>" to be incorrectly attributed to "AnotherService"

        // Only strip interface 'I' prefix if it exists, but preserve full generic signature
        if (typeName.StartsWith("I") && typeName.Length > 1 && char.IsUpper(typeName[1]))
            // For interfaces like IRepository<T>, return Repository<T> (preserve generics)
            return typeName.Substring(1);

        // Return full type name with generic parameters intact
        return typeName;
    }

    /// <summary>
    ///     Generate service registrations for a single class (adapted for proper IIncrementalGenerator)
    /// </summary>
    private static IEnumerable<ServiceRegistration> GetServicesToRegisterForSingleClass(
        SemanticModel semanticModel,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        SourceProductionContext context)
    {
        var serviceRegistrations = new List<ServiceRegistration>();

        try
        {
            // Skip static classes
            if (classSymbol.IsStatic) return serviceRegistrations;

            // Skip abstract classes 
            if (classSymbol.IsAbstract) return serviceRegistrations;

            // Skip unregistered services
            if (classSymbol.GetAttributes().Any(attr =>
                    attr.AttributeClass?.ToDisplayString() ==
                    "IoCTools.Abstractions.Annotations.UnregisteredServiceAttribute"))
                return serviceRegistrations;

            var serviceAttribute = classSymbol.GetAttributes().FirstOrDefault(attr =>
                attr.AttributeClass?.ToDisplayString() == "IoCTools.Abstractions.Annotations.ServiceAttribute");

            // Check for BackgroundService inheritance
            var isBackgroundService = false;
            var currentServiceType = classSymbol;
            while (currentServiceType != null && !isBackgroundService)
            {
                if (currentServiceType.ToDisplayString() == "Microsoft.Extensions.Hosting.BackgroundService")
                {
                    isBackgroundService = true;
                    break;
                }

                currentServiceType = currentServiceType.BaseType;
            }

            var isHostedService = classSymbol.Interfaces.Any(i =>
                i.ToDisplayString() == "Microsoft.Extensions.Hosting.IHostedService");

            // Check for ConditionalService attributes
            var conditionalServiceAttrs = classSymbol.GetAttributes()
                .Where(attr =>
                    attr.AttributeClass?.ToDisplayString() ==
                    "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute")
                .ToList();

            // Enhanced processing to prevent duplicate service registration issues
            
            // CRITICAL FIX: Conditional services processing
            if (conditionalServiceAttrs.Any())
            {
                // ConditionalService classes with [InjectConfiguration] should be processed even without explicit [Service] attribute
                var hasInjectConfigurationFields = HasInjectConfigurationFieldsAcrossPartialClasses(classSymbol);
                
                // ESSENTIAL CHECK: ConditionalService requires [Service] attribute, BackgroundService inheritance, OR configuration injection
                if (serviceAttribute == null && !isBackgroundService && !isHostedService && !hasInjectConfigurationFields)
                {
                    // Report diagnostic for missing [Service] attribute - IOC021 Error severity
                    var diagnostic = Diagnostic.Create(
                        DiagnosticDescriptors.ConditionalServiceMissingServiceAttribute,
                        classDeclaration.GetLocation(),
                        classSymbol.Name);
                    context.ReportDiagnostic(diagnostic);
                    return serviceRegistrations;
                }
                
                // Check for SkipRegistration attribute - skip all registrations if present
                var hasNonGenericSkipRegistration = classSymbol.GetAttributes()
                    .Any(attr =>
                        attr.AttributeClass?.ToDisplayString() ==
                        "IoCTools.Abstractions.Annotations.SkipRegistrationAttribute");

                if (hasNonGenericSkipRegistration)
                    // Skip all registrations for this conditional service
                    return serviceRegistrations;

                // Validate conditional service attributes
                foreach (var conditionalAttr in conditionalServiceAttrs)
                {
                    var validationResult = ConditionalServiceEvaluator.ValidateConditionsDetailed(conditionalAttr);
                    if (!validationResult.IsValid)
                        foreach (var error in validationResult.Errors)
                            ReportGeneratorError(context, "IOC020", "ConditionalService validation error",
                                $"Class '{classSymbol.Name}': {error}");
                    // CRITICAL FIX: Continue processing even if validation fails - just report errors
                    // Don't exit early as this prevents all conditional services from being registered
                }

                // Extract lifetime from Service attribute if present, or use default
                var lifetime = "Scoped";
                if (serviceAttribute?.ConstructorArguments.Length > 0)
                {
                    var lifetimeValue = serviceAttribute.ConstructorArguments[0].Value;
                    if (lifetimeValue != null)
                        lifetime = (int)lifetimeValue switch
                        {
                            0 => "Scoped",
                            1 => "Transient",
                            2 => "Singleton",
                            _ => "Scoped"
                        };
                }

                // For BackgroundService/HostedService, use special lifetime and check AutoRegister setting
                if (isBackgroundService || isHostedService)
                {
                    lifetime = "BackgroundService";

                    // Check BackgroundService AutoRegister setting for conditional services
                    if (isBackgroundService)
                    {
                        var backgroundServiceAttr = classSymbol.GetAttributes().FirstOrDefault(attr =>
                            attr.AttributeClass?.ToDisplayString() ==
                            "IoCTools.Abstractions.Annotations.BackgroundServiceAttribute");

                        var autoRegister = true; // Default for BackgroundService classes
                        if (backgroundServiceAttr != null)
                        {
                            // Check constructor arguments first (for BackgroundService(false) syntax)
                            if (backgroundServiceAttr.ConstructorArguments.Length > 0)
                            {
                                var constructorArgValue = backgroundServiceAttr.ConstructorArguments[0].Value;
                                if (constructorArgValue is bool constructorAutoRegister)
                                    autoRegister = constructorAutoRegister;
                            }

                            // Also check named arguments (for BackgroundService(AutoRegister = false) syntax)
                            var autoRegisterArg = backgroundServiceAttr.NamedArguments
                                .FirstOrDefault(kvp => kvp.Key == "AutoRegister");
                            if (autoRegisterArg.Key != null && autoRegisterArg.Value.Value is bool namedAutoRegister)
                                autoRegister = namedAutoRegister;
                        }

                        // If AutoRegister is false, don't register conditional BackgroundService  
                        if (!autoRegister) return serviceRegistrations;
                    }
                }

                // Get RegisterAsAll attribute for conditional services
                var serviceRegisterAsAllAttr = classSymbol.GetAttributes().FirstOrDefault(attr =>
                    attr.AttributeClass?.ToDisplayString() ==
                    "IoCTools.Abstractions.Annotations.RegisterAsAllAttribute");

                // Create conditional registrations for each conditional attribute
                foreach (var conditionalAttr in conditionalServiceAttrs)
                {
                    var condition = ConditionalServiceEvaluator.ExtractCondition(conditionalAttr);
                    
                    // CRITICAL SAFETY: Validate that condition extraction succeeded
                    if (condition == null) continue; // Skip invalid conditional services to prevent total failure

                    if (serviceRegisterAsAllAttr != null)
                    {
                        // Handle conditional service with RegisterAsAll (using conditional service defaults)
                        var multiInterfaceRegistrations =
                            ServiceRegistrationGenerator.GetMultiInterfaceRegistrationsForConditionalServices(
                                classSymbol, serviceRegisterAsAllAttr, lifetime);
                        foreach (var registration in multiInterfaceRegistrations)
                            // Convert regular registrations to conditional registrations
                            serviceRegistrations.Add(new ConditionalServiceRegistration(
                                registration.ClassSymbol,
                                registration.InterfaceSymbol,
                                registration.Lifetime,
                                condition,
                                registration.UseSharedInstance,
                                registration.HasConfigurationInjection));
                    }
                    else
                    {
                        // Register concrete class conditionally
                        var hasConfigInjection = HasInjectConfigurationFieldsAcrossPartialClasses(classSymbol);
                        // CRITICAL FIX: Services with configuration injection REQUIRE factory pattern (useSharedInstance = true)
                        // to properly inject configuration parameters through the constructor
                        var useSharedInstance = hasConfigInjection;
                        serviceRegistrations.Add(new ConditionalServiceRegistration(classSymbol, classSymbol, lifetime,
                            condition, useSharedInstance, hasConfigInjection));

                        // CRITICAL FIX: BackgroundService/HostedService should ONLY have concrete registration
                        // Interface registrations for BackgroundService would create duplicate AddHostedService calls
                        if (lifetime != "BackgroundService")
                        {
                            // Get all interfaces for conditional registration (only for non-BackgroundService)
                            var allInterfaces = GetAllInterfacesForService(classSymbol);
                            foreach (var interfaceSymbol in allInterfaces)
                            {
                                serviceRegistrations.Add(new ConditionalServiceRegistration(classSymbol, interfaceSymbol,
                                    lifetime, condition, useSharedInstance, hasConfigInjection));
                            }
                        }
                    }
                }

                return serviceRegistrations;
            }

            // Handle regular services (non-conditional)
            
            // CRITICAL FIX: Also check for RegisterAsAll attribute - services with RegisterAsAll should proceed even without explicit Service attribute
            var registerAsAllAttribute = classSymbol.GetAttributes().FirstOrDefault(attr =>
                attr.AttributeClass?.ToDisplayString() == "IoCTools.Abstractions.Annotations.RegisterAsAllAttribute");
                
            // CRITICAL FIX: Also check for InjectConfiguration fields - services with configuration injection should be registered
            var hasInjectConfigurationOnly = HasInjectConfigurationFieldsAcrossPartialClasses(classSymbol);

            // CRITICAL FIX: If class has ConditionalService attributes, it should ONLY be processed as conditional
            // This prevents duplicate processing of services with both [BackgroundService] and [ConditionalService]
            var hasConditionalAttributes = classSymbol.GetAttributes()
                .Any(attr => attr.AttributeClass?.ToDisplayString() == "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute");
                
            if (hasConditionalAttributes)
            {
                return serviceRegistrations; // Already processed as conditional service above
            }
                
            if (serviceAttribute == null && !isBackgroundService && !isHostedService && registerAsAllAttribute == null && !hasInjectConfigurationOnly)
                return serviceRegistrations;

            // Extract service registration information
            var regularLifetime = "Scoped";
            if (serviceAttribute?.ConstructorArguments.Length > 0)
            {
                var lifetimeValue = serviceAttribute.ConstructorArguments[0].Value;
                if (lifetimeValue != null)
                    regularLifetime = (int)lifetimeValue switch
                    {
                        0 => "Scoped",
                        1 => "Transient",
                        2 => "Singleton",
                        _ => "Scoped"
                    };
            }
            // CRITICAL FIX: For services with ONLY InjectConfiguration (no explicit Service attribute), use Scoped as default

            // For BackgroundService, use special lifetime and check AutoRegister setting
            if (isBackgroundService || isHostedService)
            {
                regularLifetime = "BackgroundService";

                // Check BackgroundService AutoRegister setting
                if (isBackgroundService)
                {
                    var backgroundServiceAttr = classSymbol.GetAttributes().FirstOrDefault(attr =>
                        attr.AttributeClass?.ToDisplayString() ==
                        "IoCTools.Abstractions.Annotations.BackgroundServiceAttribute");

                    var autoRegister = true; // Default for BackgroundService classes
                    if (backgroundServiceAttr != null)
                    {
                        // Check constructor arguments first (for BackgroundService(false) syntax)
                        if (backgroundServiceAttr.ConstructorArguments.Length > 0)
                        {
                            var constructorArgValue = backgroundServiceAttr.ConstructorArguments[0].Value;
                            if (constructorArgValue is bool constructorAutoRegister)
                                autoRegister = constructorAutoRegister;
                        }

                        // Also check named arguments (for BackgroundService(AutoRegister = false) syntax)
                        var autoRegisterArg = backgroundServiceAttr.NamedArguments
                            .FirstOrDefault(kvp => kvp.Key == "AutoRegister");
                        if (autoRegisterArg.Key != null && autoRegisterArg.Value.Value is bool namedAutoRegister)
                            autoRegister = namedAutoRegister;
                    }

                    // If AutoRegister is false, don't register the BackgroundService
                    if (!autoRegister) return serviceRegistrations;
                }
            }

            // CRITICAL FIX: Check for RegisterAsAll attribute and use proper registration logic
            // (registerAsAllAttribute already retrieved above)

            if (registerAsAllAttribute != null)
            {
                // Use ServiceRegistrationGenerator logic that handles InstanceSharing correctly
                serviceRegistrations.AddRange(
                    GetMultiInterfaceRegistrationsFromServiceGenerator(classSymbol, registerAsAllAttribute,
                        regularLifetime));
            }
            else
            {
                // Check for non-generic SkipRegistration attribute - completely skip registration if present
                var hasNonGenericSkipRegistration = classSymbol.GetAttributes()
                    .Any(attr =>
                        attr.AttributeClass?.ToDisplayString() ==
                        "IoCTools.Abstractions.Annotations.SkipRegistrationAttribute");

                if (hasNonGenericSkipRegistration)
                    // Skip all registrations for this service
                    return serviceRegistrations;

                // Standard registration for services without RegisterAsAll
                // CRITICAL FIX: Check for configuration injection to determine if factory lambda pattern is needed
                var hasConfigInjection = HasInjectConfigurationFieldsAcrossPartialClasses(classSymbol);

                serviceRegistrations.Add(new ServiceRegistration(classSymbol, classSymbol, regularLifetime, false,
                    hasConfigInjection));

                // Add interface registrations for each interface this class implements
                var interfaces = GetAllInterfacesForService(classSymbol);
                foreach (var @interface in interfaces)
                    serviceRegistrations.Add(new ServiceRegistration(classSymbol, @interface, regularLifetime, false,
                        hasConfigInjection));
            }

            return serviceRegistrations;
        }
        catch (Exception ex)
        {
            // CRITICAL FIX: Provide detailed error information to diagnose service registration failures
            var className = classSymbol?.Name ?? "Unknown";
            var fullClassName = classSymbol?.ToDisplayString() ?? "Unknown";
            var errorDetails = $"Class: {fullClassName}, Error: {ex.Message}, StackTrace: {ex.StackTrace}";
            ReportGeneratorError(context, "IOC998", "Service registration processing error", errorDetails);
            return serviceRegistrations;
        }
    }

    /// <summary>
    ///     Adapted service registration logic from ServiceRegistrationGenerator for IIncrementalGenerator compatibility
    ///     This includes full ConditionalService support
    /// </summary>
    private static IEnumerable<ServiceRegistration> GetServicesToRegisterAdapted(
        SemanticModel semanticModel,
        SyntaxNode root,
        SourceProductionContext context,
        HashSet<string> processedClasses)
    {
        var serviceRegistrations = new List<ServiceRegistration>();
        var typeDeclarations = root.DescendantNodes().OfType<TypeDeclarationSyntax>().ToList();

        foreach (var classDeclaration in typeDeclarations)
            try
            {
                var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);
                if (classSymbol == null) continue;

                // Skip if we've already processed this class (handles multiple partial declarations)
                if (processedClasses.Contains(classSymbol.ToDisplayString()))
                    continue;

                // Skip static classes - they cannot be registered for dependency injection
                if (classSymbol.IsStatic)
                    continue;

                // Skip abstract classes - they cannot be instantiated for dependency injection
                if (classSymbol.IsAbstract)
                    continue;

                // Skip unregistered services for DI registration
                if (classSymbol.GetAttributes().Any(attr =>
                        attr.AttributeClass?.ToDisplayString() ==
                        "IoCTools.Abstractions.Annotations.UnregisteredServiceAttribute"))
                    continue;

                var serviceAttribute = classSymbol.GetAttributes().FirstOrDefault(attr =>
                    attr.AttributeClass?.ToDisplayString() == "IoCTools.Abstractions.Annotations.ServiceAttribute");

                // Check for BackgroundService inheritance
                var isBackgroundService = false;
                var currentServiceType = classSymbol;
                while (currentServiceType != null && !isBackgroundService)
                {
                    if (currentServiceType.ToDisplayString() == "Microsoft.Extensions.Hosting.BackgroundService")
                    {
                        isBackgroundService = true;
                        break;
                    }

                    currentServiceType = currentServiceType.BaseType;
                }

                var isHostedService = classSymbol.Interfaces.Any(i =>
                    i.ToDisplayString() == "Microsoft.Extensions.Hosting.IHostedService");

                // Check for ConditionalService attributes first
                var conditionalServiceAttrs = classSymbol.GetAttributes()
                    .Where(attr =>
                        attr.AttributeClass?.ToDisplayString() ==
                        "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute")
                    .ToList();

                // CRITICAL FIX: Handle ConditionalService registrations
                if (conditionalServiceAttrs.Any())
                {
                    // Check for SkipRegistration attribute - skip all registrations if present
                    var hasNonGenericSkipRegistration = classSymbol.GetAttributes()
                        .Any(attr =>
                            attr.AttributeClass?.ToDisplayString() ==
                            "IoCTools.Abstractions.Annotations.SkipRegistrationAttribute");

                    if (hasNonGenericSkipRegistration)
                        // Skip all registrations for this conditional service
                        return serviceRegistrations;

                    // Validate conditional service attributes
                    foreach (var conditionalAttr in conditionalServiceAttrs)
                    {
                        var validationResult = ConditionalServiceEvaluator.ValidateConditionsDetailed(conditionalAttr);
                        if (!validationResult.IsValid)
                            // Report validation errors as diagnostics
                            foreach (var error in validationResult.Errors)
                                ReportGeneratorError(context, "IOC020", "ConditionalService validation error",
                                    $"Class '{classSymbol.Name}': {error}");
                    }

                    // Extract lifetime from Service attribute if present, or use default
                    var lifetime = "Scoped";
                    if (serviceAttribute?.ConstructorArguments.Length > 0)
                    {
                        var lifetimeValue = serviceAttribute.ConstructorArguments[0].Value;
                        if (lifetimeValue != null)
                            lifetime = (int)lifetimeValue switch
                            {
                                0 => "Scoped",
                                1 => "Transient",
                                2 => "Singleton",
                                _ => "Scoped"
                            };
                    }

                    // For BackgroundService/HostedService, use special lifetime
                    if (isBackgroundService || isHostedService)
                        lifetime = "BackgroundService";

                    // Create conditional registrations for each conditional attribute
                    foreach (var conditionalAttr in conditionalServiceAttrs)
                    {
                        var condition = ConditionalServiceEvaluator.ExtractCondition(conditionalAttr);

                        // CRITICAL FIX: Check if class has configuration injection for conditional services
                        var hasConfigInjection = HasInjectConfigurationFieldsAcrossPartialClasses(classSymbol);
                        // CRITICAL FIX: Conditional services should NEVER use factory patterns, always direct registration
                        // Configuration injection is handled through constructor generation, not factory patterns
                        var useSharedInstance = false;

                        // Register concrete class conditionally
                        serviceRegistrations.Add(new ConditionalServiceRegistration(classSymbol, classSymbol, lifetime,
                            condition, useSharedInstance, hasConfigInjection));

                        // Get all interfaces for conditional registration
                        var allInterfaces = GetAllInterfacesForService(classSymbol);
                        foreach (var interfaceSymbol in allInterfaces)
                            serviceRegistrations.Add(new ConditionalServiceRegistration(classSymbol, interfaceSymbol,
                                lifetime, condition, useSharedInstance, hasConfigInjection));
                    }

                    processedClasses.Add(classSymbol.ToDisplayString());
                    continue;
                }

                // Handle regular services (non-conditional)
                if (serviceAttribute == null && !isBackgroundService && !isHostedService)
                    continue;

                processedClasses.Add(classSymbol.ToDisplayString());

                // Extract service registration information
                var regularLifetime = "Scoped";
                if (serviceAttribute?.ConstructorArguments.Length > 0)
                {
                    var lifetimeValue = serviceAttribute.ConstructorArguments[0].Value;
                    if (lifetimeValue != null)
                        regularLifetime = (int)lifetimeValue switch
                        {
                            0 => "Scoped",
                            1 => "Transient",
                            2 => "Singleton",
                            _ => "Scoped"
                        };
                }

                // For BackgroundService, use special lifetime
                if (isBackgroundService || isHostedService)
                    regularLifetime = "BackgroundService";

                // CRITICAL FIX: Check for RegisterAsAll attribute and use proper registration logic
                var registerAsAllAttribute = classSymbol.GetAttributes().FirstOrDefault(attr =>
                    attr.AttributeClass?.ToDisplayString() ==
                    "IoCTools.Abstractions.Annotations.RegisterAsAllAttribute");

                if (registerAsAllAttribute != null)
                {
                    // Use ServiceRegistrationGenerator logic that handles InstanceSharing correctly
                    serviceRegistrations.AddRange(
                        GetMultiInterfaceRegistrationsFromServiceGenerator(classSymbol, registerAsAllAttribute,
                            regularLifetime));
                }
                else
                {
                    // Standard registration for services without RegisterAsAll
                    // CRITICAL FIX: Check for configuration injection to determine if factory lambda pattern is needed
                    var hasConfigInjection = HasInjectConfigurationFieldsAcrossPartialClasses(classSymbol);

                    serviceRegistrations.Add(new ServiceRegistration(classSymbol, classSymbol, regularLifetime, false,
                        hasConfigInjection));

                    // Add interface registrations for each interface this class implements
                    var interfaces = GetAllInterfacesForService(classSymbol);
                    foreach (var @interface in interfaces)
                        serviceRegistrations.Add(new ServiceRegistration(classSymbol, @interface, regularLifetime,
                            false, hasConfigInjection));
                }
            }
            catch (Exception ex)
            {
                ReportGeneratorError(context, "IOC998", "Service registration processing error", ex.Message);
            }

        return serviceRegistrations;
    }

    /// <summary>
    ///     Get all interfaces that should be registered for a service
    /// </summary>
    private static List<INamedTypeSymbol> GetAllInterfacesForService(INamedTypeSymbol classSymbol)
    {
        try
        {
            var allInterfaces = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

            // Collect all interfaces recursively
            CollectAllInterfacesRecursive(classSymbol, allInterfaces);

            return allInterfaces.Where(i =>
                    // Skip system interfaces - with additional null safety
                    i?.ContainingNamespace?.ToDisplayString()?.StartsWith("System") != true)
                .ToList();
        }
        catch
        {
            // CRITICAL FALLBACK: If interface collection fails, return empty list to prevent total failure
            // This allows service registration to continue with concrete class registration only
            return new List<INamedTypeSymbol>();
        }
    }

    /// <summary>
    ///     Recursively collect all interfaces from a type and its inheritance hierarchy
    /// </summary>
    private static void CollectAllInterfacesRecursive(INamedTypeSymbol typeSymbol,
        HashSet<INamedTypeSymbol> allInterfaces)
    {
        try
        {
            if (typeSymbol == null) return;
            
            // Add direct interfaces with null safety
            foreach (var interfaceSymbol in typeSymbol.Interfaces)
            {
                if (interfaceSymbol != null && allInterfaces.Add(interfaceSymbol))
                    // Recursively collect interfaces from this interface (interface inheritance)
                    CollectAllInterfacesRecursive(interfaceSymbol, allInterfaces);
            }

            // Collect from base classes with null safety
            var baseType = typeSymbol.BaseType;
            if (baseType != null && baseType.SpecialType != SpecialType.System_Object)
                CollectAllInterfacesRecursive(baseType, allInterfaces);

            // Use Roslyn's AllInterfaces as fallback with null safety
            foreach (var interfaceSymbol in typeSymbol.AllInterfaces)
            {
                if (interfaceSymbol != null)
                    allInterfaces.Add(interfaceSymbol);
            }
        }
        catch
        {
            // CRITICAL SAFETY: Ignore interface collection errors to prevent total generator failure
            // The worst case is that we miss some interface registrations but still register concrete classes
        }
    }


    /// <summary>
    ///     Converts an interface symbol to enhanced open generic form for better matching
    ///     Examples:
    ///     "IProcessor
    ///     <T>
    ///         " -> "IProcessor
    ///         <T>
    ///             " (unchanged)
    ///             "IProcessor<List
    ///             <string>
    ///                 >" -> "IProcessor
    ///                 <T>
    ///                     " (converted)
    ///                     "IRequestHandler<GetUserQuery, string>" -> "IRequestHandler<T1, T2>" (converted)
    /// </summary>
    private static string? ConvertToEnhancedOpenGenericFormForInterface(INamedTypeSymbol interfaceSymbol)
    {
        if (!interfaceSymbol.IsGenericType) return null;

        var interfaceName = interfaceSymbol.ToDisplayString();

        // If it's already an open generic (uses T, T1, T2, etc.), don't convert
        if (IsOpenGenericForm(interfaceName)) return interfaceName;

        // Convert constructed generic to open form
        return ConvertToEnhancedOpenGenericFormFromString(interfaceName);
    }

    /// <summary>
    ///     Checks if a type name is already in open generic form (uses T, T1, T2, etc.)
    /// </summary>
    private static bool IsOpenGenericForm(string typeName)
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
    ///     Converts a constructed generic type string to enhanced open generic form
    /// </summary>
    private static string? ConvertToEnhancedOpenGenericFormFromString(string constructedType)
    {
        if (!constructedType.Contains('<') || !constructedType.Contains('>'))
            return null;

        var angleStart = constructedType.IndexOf('<');
        var angleEnd = constructedType.LastIndexOf('>');

        if (angleStart >= 0 && angleEnd > angleStart)
        {
            var baseName = constructedType.Substring(0, angleStart);
            var typeArgsSection = constructedType.Substring(angleStart + 1, angleEnd - angleStart - 1);

            // Parse type arguments with proper bracket matching
            var typeArgs = ParseGenericTypeArgumentsFromString(typeArgsSection);

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
    ///     Parses generic type arguments from a string with proper bracket matching
    /// </summary>
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
    ///     Remove namespace prefix from type name for cleaner generated code
    /// </summary>
    private static string RemoveNamespaceFromTypeName(string typeName,
        string namespaceToRemove)
    {
        if (typeName.StartsWith($"{namespaceToRemove}."))
            return typeName.Substring(namespaceToRemove.Length + 1);
        return typeName;
    }

    /// <summary>
    ///     Clean all known namespaces from a type name for cleaner parameter generation
    ///     Handles nested generics and removes all namespaces that will be included in using statements
    /// </summary>
    private static string CleanTypeNameForParameter(string typeName,
        HashSet<string> namespacesToClean)
    {
        // Clean common system namespaces that are always included
        var cleanedType = typeName;

        // Remove current namespace and common system namespaces
        var standardNamespacesToRemove = new[]
        {
            "System.Collections.Generic",
            "System.Collections",
            "System.Threading.Tasks",
            "System.Linq",
            "System",
            "Microsoft.Extensions.Configuration",
            "Microsoft.Extensions.DependencyInjection"
        };

        foreach (var ns in standardNamespacesToRemove.Concat(namespacesToClean))
            if (!string.IsNullOrEmpty(ns))
                // Replace "Namespace.Type" with "Type" but be careful about partial matches
                cleanedType = cleanedType.Replace($"{ns}.", "");

        return cleanedType;
    }

    /// <summary>
    ///     Convert field name to parameter name (e.g., "_service2" -> "service2")
    ///     Based on ConstructorGenerator.GetParameterNameFromFieldName
    /// </summary>
    private static string GetParameterNameFromFieldName(string fieldName,
        HashSet<string> existingNames)
    {
        var baseName = fieldName;

        // Remove leading underscore if present
        if (baseName.StartsWith("_"))
            baseName = baseName.Substring(1);

        // Convert first character to lowercase for camelCase
        if (baseName.Length > 0)
            baseName = char.ToLowerInvariant(baseName[0]) + baseName.Substring(1);

        // Handle conflicts with existing parameter names
        var paramName = baseName;
        var counter = 1;
        while (existingNames.Contains(paramName))
        {
            paramName = $"{baseName}{counter}";
            counter++;
        }

        existingNames.Add(paramName);
        return paramName;
    }

    /// <summary>
    ///     Adapter to use the sophisticated ConstructorGenerator with SourceProductionContext
    ///     CRITICAL FIX: Create a proper adapter that uses the existing ConstructorGenerator logic
    /// </summary>
    private static string GenerateInheritanceAwareConstructorCodeWithContext(
        TypeDeclarationSyntax classDeclaration,
        InheritanceHierarchyDependencies hierarchyDependencies,
        SemanticModel semanticModel,
        SourceProductionContext context)
    {
        try
        {
            // CRITICAL FIX: Use the working ConstructorGenerator with a context adapter
            return ConstructorGenerator.GenerateInheritanceAwareConstructorCodeWithContext(
                classDeclaration, hierarchyDependencies, semanticModel, context);
        }
        catch (Exception ex)
        {
            ReportGeneratorError(context, "IOC992", "Constructor generation adapter error", ex.Message);
            return string.Empty;
        }
    }

    /// <summary>
    ///     Helper to call ServiceRegistrationGenerator.GetMultiInterfaceRegistrations
    /// </summary>
    private static IEnumerable<ServiceRegistration> GetMultiInterfaceRegistrationsFromServiceGenerator(
        INamedTypeSymbol classSymbol,
        AttributeData registerAsAllAttribute,
        string lifetime) =>
        // Delegate to the ServiceRegistrationGenerator which has the correct InstanceSharing logic
        ServiceRegistrationGenerator.GetMultiInterfaceRegistrations(classSymbol, registerAsAllAttribute, lifetime);

    /// <summary>
    ///     IOC009: Validates unnecessary SkipRegistration attributes for interfaces not registered by RegisterAsAll
    /// </summary>
    private static void ValidateUnnecessarySkipRegistration(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        // Get RegisterAsAll attribute
        var registerAsAllAttribute = classSymbol.GetAttributes().FirstOrDefault(attr =>
            attr.AttributeClass?.ToDisplayString() == "IoCTools.Abstractions.Annotations.RegisterAsAllAttribute");

        if (registerAsAllAttribute == null) return; // IOC005 handles this case

        // Get SkipRegistration attributes
        var skipRegistrationAttributes = classSymbol.GetAttributes()
            .Where(attr =>
                attr.AttributeClass?.ToDisplayString()
                    .StartsWith("IoCTools.Abstractions.Annotations.SkipRegistrationAttribute") == true)
            .ToList();

        if (!skipRegistrationAttributes.Any()) return;

        // Get all interfaces that would be registered by RegisterAsAll
        var allInterfaces = classSymbol.AllInterfaces.ToList();

        foreach (var attr in skipRegistrationAttributes)
            if (attr.AttributeClass?.TypeArguments != null)
                foreach (var typeArg in attr.AttributeClass.TypeArguments)
                    // Check if this type is actually an interface that would be registered
                    if (!allInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, typeArg)))
                    {
                        var diagnostic = Diagnostic.Create(
                            DiagnosticDescriptors.SkipRegistrationForNonRegisteredInterface,
                            attr.ApplicationSyntaxReference?.GetSyntax()?.GetLocation() ??
                            classDeclaration.GetLocation(),
                            FormatTypeNameForDiagnostic(typeArg),
                            classSymbol.Name);
                        context.ReportDiagnostic(diagnostic);
                    }
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
    ///     Configuration key validation result
    /// </summary>
    private struct ConfigurationKeyValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    ///     Information about an IEnumerable found within a potentially wrapped type
    /// </summary>
    private class EnumerableTypeInfo
    {
        public EnumerableTypeInfo(string innerType,
            string fullEnumerableType)
        {
            InnerType = innerType;
            FullEnumerableType = fullEnumerableType;
        }

        public string InnerType { get; }
        public string FullEnumerableType { get; }
    }
}

/// <summary>
///     Service class information for IIncrementalGenerator pipeline
/// </summary>
internal readonly struct ServiceClassInfo
{
    public ServiceClassInfo(INamedTypeSymbol classSymbol,
        TypeDeclarationSyntax classDeclaration,
        SemanticModel semanticModel)
    {
        ClassSymbol = classSymbol;
        ClassDeclaration = classDeclaration;
        SemanticModel = semanticModel;
    }

    public INamedTypeSymbol ClassSymbol { get; }
    public TypeDeclarationSyntax ClassDeclaration { get; }
    public SemanticModel SemanticModel { get; }
}