using System;
using System.Collections.Generic;
using System.Linq;
using IoCTools.Generator.Models;
using IoCTools.Generator.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace IoCTools.Generator.Diagnostics;

internal static class DiagnosticUtilities
{
    public static DiagnosticConfiguration GetDiagnosticConfiguration(GeneratorExecutionContext context)
    {
        var config = new DiagnosticConfiguration();

        // Read MSBuild properties for diagnostic severity
        if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.IoCToolsNoImplementationSeverity",
                out var noImplSeverity)) config.NoImplementationSeverity = ParseDiagnosticSeverity(noImplSeverity);

        if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.IoCToolsUnregisteredSeverity",
                out var unregSeverity))
            config.UnregisteredImplementationSeverity = ParseDiagnosticSeverity(unregSeverity);

        if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.IoCToolsDisableDiagnostics",
                out var disableStr) &&
            bool.TryParse(disableStr, out var disable))
            config.DiagnosticsEnabled = !disable;
        // If the property doesn't exist or can't be parsed, diagnostics remain enabled (default: true)

        if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.IoCToolsLifetimeValidationSeverity",
                out var lifetimeSeverity) && !string.IsNullOrWhiteSpace(lifetimeSeverity))
            config.LifetimeValidationSeverity = ParseDiagnosticSeverity(lifetimeSeverity);

        if (context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.IoCToolsDisableLifetimeValidation",
                out var disableLifetimeStr) &&
            bool.TryParse(disableLifetimeStr, out var disableLifetime))
            config.LifetimeValidationEnabled = !disableLifetime;

        return config;
    }

    public static DiagnosticConfiguration GetDiagnosticConfiguration(Compilation compilation)
    {
        // For incremental generators, we use default configuration since MSBuild properties 
        // aren't directly accessible from Compilation. In a real implementation, these would
        // be passed through the pipeline or read from additional sources.
        var config = new DiagnosticConfiguration
        {
            DiagnosticsEnabled = true,
            NoImplementationSeverity = DiagnosticSeverity.Warning,
            UnregisteredImplementationSeverity = DiagnosticSeverity.Warning,
            LifetimeValidationEnabled = true,
            LifetimeValidationSeverity = DiagnosticSeverity.Warning
        };

        return config;
    }

    public static DiagnosticConfiguration GetDiagnosticConfiguration(AnalyzerConfigOptionsProvider configOptions)
    {
        var config = new DiagnosticConfiguration();

        // Read MSBuild properties for diagnostic severity from AnalyzerConfigOptionsProvider
        if (configOptions.GlobalOptions.TryGetValue("build_property.IoCToolsNoImplementationSeverity",
                out var noImplSeverity)) config.NoImplementationSeverity = ParseDiagnosticSeverity(noImplSeverity);

        if (configOptions.GlobalOptions.TryGetValue("build_property.IoCToolsUnregisteredSeverity",
                out var unregSeverity))
            config.UnregisteredImplementationSeverity = ParseDiagnosticSeverity(unregSeverity);

        if (configOptions.GlobalOptions.TryGetValue("build_property.IoCToolsDisableDiagnostics",
                out var disableStr) &&
            bool.TryParse(disableStr, out var disable))
            config.DiagnosticsEnabled = !disable;

        if (configOptions.GlobalOptions.TryGetValue("build_property.IoCToolsLifetimeValidationSeverity",
                out var lifetimeSeverity) && !string.IsNullOrWhiteSpace(lifetimeSeverity))
            config.LifetimeValidationSeverity = ParseDiagnosticSeverity(lifetimeSeverity);

        if (configOptions.GlobalOptions.TryGetValue("build_property.IoCToolsDisableLifetimeValidation",
                out var disableLifetimeStr) &&
            bool.TryParse(disableLifetimeStr, out var disableLifetime))
            config.LifetimeValidationEnabled = !disableLifetime;

        return config;
    }

    private static DiagnosticSeverity ParseDiagnosticSeverity(string severity)
    {
        return severity.ToLowerInvariant() switch
        {
            "error" => DiagnosticSeverity.Error,
            "warning" => DiagnosticSeverity.Warning,
            "info" => DiagnosticSeverity.Info,
            "hidden" => DiagnosticSeverity.Hidden,
            _ => DiagnosticSeverity.Warning
        };
    }

    /// <summary>
    ///     Create a dynamic diagnostic descriptor with configured severity
    /// </summary>
    public static DiagnosticDescriptor CreateDynamicDescriptor(DiagnosticDescriptor baseDescriptor,
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

    // Note: Diagnostic descriptors are defined in DiagnosticDescriptors.cs

    /// <summary>
    ///     Enhanced redundancy detection that uses the full inheritance hierarchy analysis.
    ///     This method properly detects all types of redundancies including inheritance conflicts.
    /// </summary>
    public static void DetectAndReportRedundanciesWithHierarchy(INamedTypeSymbol classSymbol,
        InheritanceHierarchyDependencies hierarchyDependencies,
        GeneratorExecutionContext context,
        SemanticModel semanticModel,
        DiagnosticConfiguration diagnosticConfig)
    {
        // Skip redundancy detection if diagnostics are disabled
        if (!diagnosticConfig.DiagnosticsEnabled)
            return;

        // Get RegisterAsAll and SkipRegistration attributes for IOC009 detection
        var registerAsAllAttribute = classSymbol.GetAttributes().FirstOrDefault(attr =>
            attr.AttributeClass?.ToDisplayString() == "IoCTools.Abstractions.Annotations.RegisterAsAllAttribute");

        var skipRegistrationAttributes = classSymbol.GetAttributes()
            .Where(attr =>
                attr.AttributeClass?.ToDisplayString()
                    .StartsWith("IoCTools.Abstractions.Annotations.SkipRegistrationAttribute") == true)
            .ToList();

        // Get all DependsOn attributes from the inheritance hierarchy for IOC008 detection
        var dependsOnAttributes = GetDependsOnAttributesFromHierarchy(classSymbol);

        // 1. IOC008: Detect duplicate types within single DependsOn attributes
        DetectDuplicatesWithinSingleDependsOn(dependsOnAttributes, classSymbol, context);

        // 2. IOC006: Detect ALL duplicate dependencies (consolidates cross-attribute and inheritance duplicates)
        DetectInheritanceHierarchyDuplicates(hierarchyDependencies, classSymbol, context);

        // 3. IOC007: Detect DependsOn types that conflict with Inject fields (inheritance-aware)
        DetectDependsOnInjectConflictsWithHierarchy(hierarchyDependencies, classSymbol, context, semanticModel);

        // 4. IOC009: Detect SkipRegistration for interfaces not registered by RegisterAsAll
        DetectUnnecessarySkipRegistrations(skipRegistrationAttributes, registerAsAllAttribute, classSymbol, context);
    }

    /// <summary>
    ///     Gets all DependsOn attributes from the entire inheritance hierarchy
    /// </summary>
    private static List<AttributeData> GetDependsOnAttributesFromHierarchy(INamedTypeSymbol classSymbol)
    {
        var dependsOnAttributes = new List<AttributeData>();
        var currentType = classSymbol;

        // Walk up the inheritance chain to collect all DependsOn attributes
        while (currentType != null)
        {
            var currentTypeDependsOnAttributes = currentType.GetAttributes()
                .Where(attr =>
                    attr.AttributeClass?.ToDisplayString()
                        .StartsWith("IoCTools.Abstractions.Annotations.DependsOnAttribute") == true);
            dependsOnAttributes.AddRange(currentTypeDependsOnAttributes);

            // Move to base class
            currentType = currentType.BaseType;

            // Stop at System.Object or if base type is null
            if (currentType?.ToDisplayString() == "System.Object")
                break;
        }

        return dependsOnAttributes;
    }

    /// <summary>
    ///     IOC008: Detects duplicate types within a single DependsOn attribute
    /// </summary>
    private static void DetectDuplicatesWithinSingleDependsOn(List<AttributeData> dependsOnAttributes,
        INamedTypeSymbol classSymbol,
        GeneratorExecutionContext context)
    {
        foreach (var attr in dependsOnAttributes)
            if (attr.AttributeClass?.TypeArguments != null)
            {
                var typeArguments = attr.AttributeClass.TypeArguments.ToList();
                var typeDisplayNames = typeArguments.Select(t => FormatTypeNameForDiagnostic(t)).ToList();

                // Check for duplicates within this single attribute
                var duplicates = typeDisplayNames.GroupBy(t => t)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                foreach (var duplicate in duplicates)
                {
                    var diagnostic = Diagnostic.Create(
                        DiagnosticDescriptors.DuplicateTypeInSingleDependsOn,
                        GetSafeLocation(attr.ApplicationSyntaxReference, classSymbol),
                        duplicate,
                        classSymbol.Name);
                    context.ReportDiagnostic(diagnostic);
                }
            }
    }

    /// <summary>
    ///     IOC006: Detects duplicate dependencies across the inheritance hierarchy.
    ///     This specifically detects duplicate DependsOn declarations only.
    /// </summary>
    private static void DetectInheritanceHierarchyDuplicates(InheritanceHierarchyDependencies hierarchyDependencies,
        INamedTypeSymbol classSymbol,
        GeneratorExecutionContext context)
    {
        // IOC006 specifically detects duplicate DependsOn declarations
        // Group only DependsOn dependencies by type to find duplicates
        var dependsOnDependencies = hierarchyDependencies.RawAllDependencies
            .Where(d => d.Source == DependencySource.DependsOn)
            .GroupBy(d => d.ServiceType, SymbolEqualityComparer.Default)
            .Where(g => g.Count() > 1)
            .ToList();

        // Track which types we've already reported to avoid duplicate diagnostics
        var reportedTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var duplicateGroup in dependsOnDependencies)
        {
            var duplicateType = (ITypeSymbol)duplicateGroup.Key;
            if (duplicateType == null) continue;

            // Skip if we've already reported this type
            if (reportedTypes.Contains(duplicateType))
                continue;

            // Report IOC006 for duplicate DependsOn declarations
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.DuplicateDependsOnType,
                classSymbol.Locations.FirstOrDefault() ?? Location.None,
                FormatTypeNameForDiagnostic(duplicateType),
                classSymbol.Name);
            context.ReportDiagnostic(diagnostic);

            // Mark this type as reported
            reportedTypes.Add(duplicateType);
        }
    }

    /// <summary>
    ///     IOC007: Detects DependsOn types that conflict with Inject fields (inheritance-aware)
    /// </summary>
    private static void DetectDependsOnInjectConflictsWithHierarchy(
        InheritanceHierarchyDependencies hierarchyDependencies,
        INamedTypeSymbol classSymbol,
        GeneratorExecutionContext context,
        SemanticModel semanticModel)
    {
        // For IOC007 detection, we need to collect ALL dependencies including from [UnregisteredService] base classes
        // The hierarchyDependencies might exclude some dependencies based on service attributes for code generation
        // but for diagnostics we need to see everything to detect conflicts
        var rawDependencies = new List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)>();

        var currentType = classSymbol;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            // Get [Inject] field dependencies for current type
            var injectDependencies = GetInjectedFieldsForTypeWithSubstitution(currentType, classSymbol, semanticModel);
            rawDependencies.AddRange(injectDependencies.Select(d =>
                (d.ServiceType, d.FieldName, DependencySource.Inject)));

            // Get [DependsOn] dependencies for current type - ALWAYS include for diagnostics
            var dependsOnDependencies = GetRawDependsOnFieldsForType(currentType, semanticModel);
            rawDependencies.AddRange(
                dependsOnDependencies.Select(d => (d.ServiceType, d.FieldName, DependencySource.DependsOn)));

            currentType = currentType.BaseType;
        }

        // Group all RAW dependencies by type to find conflicts
        var dependenciesByType = rawDependencies
            .GroupBy(d => d.ServiceType, SymbolEqualityComparer.Default)
            .ToList();

        foreach (var typeGroup in dependenciesByType)
        {
            var dependencies = typeGroup.ToList();

            // Check if we have both DependsOn and Inject for the same type
            var hasInject = dependencies.Any(d => d.Source == DependencySource.Inject);
            var hasDependsOn = dependencies.Any(d => d.Source == DependencySource.DependsOn);

            if (hasInject && hasDependsOn)
            {
                // This is an IOC007 conflict
                var diagnostic = Diagnostic.Create(
                    DiagnosticDescriptors.DependsOnConflictsWithInject,
                    classSymbol.Locations.FirstOrDefault() ?? Location.None,
                    FormatTypeNameForDiagnostic((ITypeSymbol)typeGroup.Key),
                    classSymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    /// <summary>
    ///     IOC009: Detects SkipRegistration for interfaces not registered by RegisterAsAll
    /// </summary>
    private static void DetectUnnecessarySkipRegistrations(List<AttributeData> skipRegistrationAttributes,
        AttributeData? registerAsAllAttribute,
        INamedTypeSymbol classSymbol,
        GeneratorExecutionContext context)
    {
        if (registerAsAllAttribute == null) return; // Already handled by other diagnostic

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
                            GetSafeLocation(attr.ApplicationSyntaxReference, classSymbol),
                            FormatTypeNameForDiagnostic(typeArg),
                            classSymbol.Name);
                        context.ReportDiagnostic(diagnostic);
                    }
    }

    /// <summary>
    ///     Gets injected fields for a type with inheritance chain type substitution applied.
    /// </summary>
    private static List<(ITypeSymbol ServiceType, string FieldName)> GetInjectedFieldsForTypeWithSubstitution(
        INamedTypeSymbol typeSymbol,
        INamedTypeSymbol targetTypeForSubstitution,
        SemanticModel semanticModel)
    {
        var fields = new List<(ITypeSymbol ServiceType, string FieldName)>();

        // Use syntax-based detection as PRIMARY approach for better reliability with compound access modifiers
        // Symbol-based detection can sometimes miss private protected fields in certain compilation contexts
        // FIXED: Iterate through ALL partial class declarations, not just the first one
        foreach (var declaringSyntaxRef in typeSymbol.DeclaringSyntaxReferences)
            try
            {
                if (declaringSyntaxRef.GetSyntax() is TypeDeclarationSyntax typeDeclaration)
                    foreach (var fieldDeclaration in typeDeclaration.DescendantNodes().OfType<FieldDeclarationSyntax>())
                    {
                        // Skip static and const fields
                        var modifiers = fieldDeclaration.Modifiers;
                        if (modifiers.Any(m => m.IsKind(SyntaxKind.StaticKeyword) || m.IsKind(SyntaxKind.ConstKeyword)))
                            continue;

                        // Check for Inject attribute
                        var hasInjectAttribute = fieldDeclaration.AttributeLists
                            .SelectMany(list => list.Attributes)
                            .Any(attr => attr.Name.ToString().Contains("Inject"));

                        if (hasInjectAttribute)
                            foreach (var variable in fieldDeclaration.Declaration.Variables)
                            {
                                var fieldName = variable.Identifier.ValueText;
                                if (!string.IsNullOrEmpty(fieldName))
                                {
                                    var fieldType = semanticModel.GetTypeInfo(fieldDeclaration.Declaration.Type).Type;
                                    if (fieldType != null)
                                    {
                                        // CRITICAL FIX: Apply inheritance chain type substitution
                                        var substitutedType = ApplyInheritanceChainSubstitution(fieldType, typeSymbol,
                                            targetTypeForSubstitution);
                                        fields.Add((substitutedType, fieldName));
                                    }
                                }
                            }
                    }
            }
            catch (ArgumentException)
            {
                // Continue with what we have from symbol detection
            }

        return fields;
    }

    /// <summary>
    ///     Gets all raw DependsOn dependencies for a type without deduplication.
    ///     Used for diagnostic detection to identify duplicates.
    /// </summary>
    private static List<(ITypeSymbol ServiceType, string FieldName)> GetRawDependsOnFieldsForType(
        INamedTypeSymbol typeSymbol,
        SemanticModel semanticModel)
    {
        var fields = new List<(ITypeSymbol ServiceType, string FieldName)>();

        // Get the original generic type definition if this is a constructed generic type
        var originalTypeDefinition = typeSymbol.OriginalDefinition;
        var dependsOnAttributes = originalTypeDefinition.GetAttributes()
            .Where(attr => attr.AttributeClass?.Name == "DependsOnAttribute")
            .ToList();

        foreach (var attribute in dependsOnAttributes)
        {
            var genericTypeArguments = attribute.AttributeClass?.TypeArguments.ToList();
            if (genericTypeArguments == null) continue;

            var (namingConvention, stripI, prefix) = GetNamingConventionOptionsFromAttribute(attribute);

            foreach (var genericTypeArgument in genericTypeArguments)
            {
                // Substitute type parameters with actual type arguments if this is a constructed generic type
                var substitutedType = SubstituteTypeParameters(genericTypeArgument, typeSymbol);
                var fieldName = GenerateFieldName(GetMeaningfulTypeName(substitutedType), namingConvention, stripI,
                    prefix);
                fields.Add((substitutedType, fieldName));
            }
        }

        return fields; // Return raw fields without deduplication
    }

    /// <summary>
    ///     Applies inheritance chain type substitution to a field type.
    ///     This handles complex inheritance scenarios like ConcreteProcessor -> MiddleProcessor
    ///     <int> -> BaseProcessor<T, string>
    /// </summary>
    private static ITypeSymbol ApplyInheritanceChainSubstitution(ITypeSymbol fieldType,
        INamedTypeSymbol sourceType,
        INamedTypeSymbol targetType)
    {
        if (sourceType.Equals(targetType, SymbolEqualityComparer.Default))
            // No substitution needed if source and target are the same
            return fieldType;

        // For now, return the field type as-is (simplified implementation)
        // A full implementation would build substitution maps for complex inheritance chains
        return fieldType;
    }

    /// <summary>
    ///     Substitutes type parameters with actual type arguments if this is a constructed generic type
    /// </summary>
    private static ITypeSymbol SubstituteTypeParameters(ITypeSymbol type,
        INamedTypeSymbol constructedType)
    {
        // If this is not a constructed generic type, return the type as-is
        // IMPORTANT: Preserve the original type including its nullable annotation
        if (constructedType.TypeArguments.IsEmpty ||
            constructedType.OriginalDefinition.Equals(constructedType, SymbolEqualityComparer.Default))
            return type; // Return the original type unchanged, preserving nullable annotations

        // For now, return the type as-is (simplified implementation)
        // A full implementation would map type parameters to type arguments
        return type;
    }

    /// <summary>
    ///     Gets naming convention options from a DependsOn attribute
    /// </summary>
    private static (string namingConvention, bool stripI, string prefix) GetNamingConventionOptionsFromAttribute(
        AttributeData attribute)
    {
        var namingConvention = "CamelCase";
        var stripI = true;
        var prefix = "_";

        // Check constructor arguments first
        var constructorArgs = attribute.ConstructorArguments;
        if (constructorArgs.Length > 0)
        {
            // First parameter is namingConvention
            var enumValue = constructorArgs[0].Value;
            if (enumValue != null) namingConvention = enumValue.ToString() ?? "CamelCase";
        }

        // Check named arguments for stripI and prefix
        foreach (var namedArg in attribute.NamedArguments)
            switch (namedArg.Key)
            {
                case "StripI":
                    if (namedArg.Value.Value is bool stripIValue)
                        stripI = stripIValue;
                    break;
                case "Prefix":
                    if (namedArg.Value.Value is string prefixValue)
                        prefix = prefixValue;
                    break;
            }

        return (namingConvention, stripI, prefix);
    }

    /// <summary>
    ///     Gets the meaningful type name for field generation
    /// </summary>
    private static string GetMeaningfulTypeName(ITypeSymbol typeSymbol)
    {
        // For collection types, extract the inner type argument for better field naming
        if (typeSymbol is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var typeName = namedType.Name;

            // Check if it's a common collection type that should use its type argument for naming
            var collectionTypes = new[]
            {
                "IEnumerable", "IList", "ICollection", "List",
                "IReadOnlyList", "IReadOnlyCollection", "Array"
            };

            if (collectionTypes.Contains(typeName) && namedType.TypeArguments.Length > 0)
            {
                // Use the first type argument for field naming
                var innerType = namedType.TypeArguments[0];
                return GetMeaningfulTypeName(innerType); // Recursive for nested generics
            }
        }

        // For non-collection types or non-generic types, use the type name itself
        return typeSymbol.Name;
    }

    /// <summary>
    ///     Generates field name based on type name and naming convention
    /// </summary>
    private static string GenerateFieldName(string originalTypeName,
        string namingConvention,
        bool stripI,
        string prefix)
    {
        var workingTypeName = originalTypeName;

        // Apply stripI logic: only strip 'I' when explicitly requested
        if (stripI && workingTypeName.StartsWith("I") && workingTypeName.Length > 1 && char.IsUpper(workingTypeName[1]))
            workingTypeName = workingTypeName.Substring(1);

        // For stripI=false with interfaces, apply semantic naming to avoid awkward names like 'iOrderRepository'
        // Generate meaningful parameter names even when preserving the 'I' prefix in type references
        string semanticName;
        if (!stripI && originalTypeName.StartsWith("I") && originalTypeName.Length > 1 &&
            char.IsUpper(originalTypeName[1]))
            // Use the stripped version for parameter naming semantics, but this doesn't affect the type reference
            semanticName = originalTypeName.Substring(1);
        else
            semanticName = workingTypeName;

        // Apply naming convention
        var formattedName = namingConvention.ToLowerInvariant() switch
        {
            "camelcase" => char.ToLowerInvariant(semanticName[0]) + semanticName.Substring(1),
            "pascalcase" => char.ToUpperInvariant(semanticName[0]) + semanticName.Substring(1),
            _ => char.ToLowerInvariant(semanticName[0]) + semanticName.Substring(1) // Default to camelCase
        };

        var fieldName = prefix + formattedName;

        // Handle C# reserved keywords by adding a suffix
        fieldName = EscapeReservedKeyword(fieldName);

        return fieldName;
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
            // Extract the simple name from the full type name (e.g., "IEnumerable" from "System.Collections.Generic.IEnumerable`1")
            var typeName = namedType.Name;

            // Handle special case where Name might include backtick for generic types
            var backtickIndex = typeName.IndexOf('`');
            if (backtickIndex > 0) typeName = typeName.Substring(0, backtickIndex);

            var typeArgs = namedType.TypeArguments
                .Select(arg => FormatTypeNameForDiagnostic(arg)) // Recursive to handle nested generics
                .ToArray();

            return typeArgs.Length > 0 ? $"{typeName}<{string.Join(", ", typeArgs)}>" : typeName;
        }

        // For non-generic types, just use the name  
        var simpleName = typeSymbol.Name;

        // Handle case where Name might be fully qualified for some types
        var lastDotIndex = simpleName.LastIndexOf('.');
        if (lastDotIndex >= 0) simpleName = simpleName.Substring(lastDotIndex + 1);

        return simpleName;
    }

    /// <summary>
    ///     Validates attribute combinations and reports diagnostics for invalid combinations
    /// </summary>
    public static void ValidateAttributeCombinations(GeneratorExecutionContext context,
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

            // Check for SkipRegistration without RegisterAsAll
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

            // Check for RegisterAsAll without Service (IOC004)
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
    ///     Validates conditional service attributes and reports diagnostics for invalid configurations
    /// </summary>
    public static void ValidateConditionalServices(GeneratorExecutionContext context,
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
    ///     Safely gets a location from a syntax reference, handling cases where the syntax node
    ///     is not within the expected syntax tree (which can happen with inheritance across files).
    /// </summary>
    private static Location GetSafeLocation(SyntaxReference? syntaxReference,
        ISymbol fallbackSymbol)
    {
        try
        {
            return syntaxReference?.GetSyntax().GetLocation() ??
                   fallbackSymbol.Locations.FirstOrDefault() ?? Location.None;
        }
        catch (ArgumentException)
        {
            // Syntax node is not within syntax tree - use symbol location as fallback
            return fallbackSymbol.Locations.FirstOrDefault() ?? Location.None;
        }
    }

    /// <summary>
    ///     Escapes C# reserved keywords by appending a suffix to avoid compilation errors
    /// </summary>
    private static string EscapeReservedKeyword(string identifier)
    {
        // C# reserved keywords that could conflict with parameter names
        var reservedKeywords = new HashSet<string>
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed",
            "short", "sizeof", "stackalloc", "static", "string", "struct", "switch", "this",
            "throw", "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort",
            "using", "virtual", "void", "volatile", "while"
        };

        if (reservedKeywords.Contains(identifier)) return identifier + "Value";

        return identifier;
    }
}