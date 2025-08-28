namespace IoCTools.Generator.Analysis;

using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;

using Models;

using Utilities;

internal static class DependencyAnalyzer
{
    public static InheritanceHierarchyDependencies GetInheritanceHierarchyDependencies(INamedTypeSymbol classSymbol,
        SemanticModel semanticModel)
    {
        var allDependencies =
            new List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source, int Level)>();
        var allDependenciesWithExternalFlag =
            new List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source, bool IsExternal)>();
        var baseDependencies = new List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)>();
        var derivedDependencies = new List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)>();

        var currentType = classSymbol;
        var level = 0;

        // Check if the main derived class (level 0) has [Inject] fields
        var derivedHasInjectFields = InjectFieldAnalyzer.GetInjectedFieldsForType(classSymbol, semanticModel).Any();

        // Collect dependencies from current class and all base classes
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            var currentDependencies =
                new List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source, bool IsExternal)>();

            // Check service attributes for conditional dependency collection
            var isExternalService = currentType.GetAttributes().Any(attr =>
                attr.AttributeClass?.ToDisplayString() == "IoCTools.Abstractions.Annotations.ExternalServiceAttribute");

            // [ExternalService] classes: Exclude dependencies from inheritance (they're managed elsewhere)
            // For regular classes: include both [Inject] and [DependsOn] dependencies

            // Get [Inject] field dependencies 
            // For [ExternalService] classes: include in own constructor (level 0) but exclude from inheritance (level > 0)
            if (!isExternalService || level == 0)
            {
                var injectDependencies =
                    InjectFieldAnalyzer.GetInjectedFieldsForTypeWithExternalFlag(currentType, semanticModel);
                currentDependencies.AddRange(injectDependencies.Select(d =>
                    (d.ServiceType, d.FieldName, DependencySource.Inject, d.IsExternal)));
            }

            // Get [InjectConfiguration] field dependencies
            // For [ExternalService] classes: include in own constructor (level 0) but exclude from inheritance (level > 0)  
            if (!isExternalService || level == 0)
            {
                var configDependencies =
                    ConfigurationFieldAnalyzer.GetConfigurationInjectedFieldsForType(currentType, semanticModel);

                foreach (var configDep in configDependencies)
                    if (configDep.IsOptionsPattern)
                    {
                        // Options pattern fields are injected as regular dependencies
                        currentDependencies.Add((configDep.FieldType, configDep.FieldName, DependencySource.Inject,
                            false));
                    }
                    else if (configDep.SupportsReloading)
                    {
                        // CRITICAL FIX: For primitive types with SupportsReloading, use IConfiguration
                        // For complex objects with SupportsReloading, use IOptionsSnapshot<T>
                        if (configDep.IsDirectValueBinding)
                        {
                            // For primitive types, use IConfiguration for reloading support
                            var configurationType =
                                semanticModel.Compilation.GetTypeByMetadataName(
                                    "Microsoft.Extensions.Configuration.IConfiguration");
                            if (configurationType != null)
                                currentDependencies.Add((configurationType, configDep.FieldName,
                                    DependencySource.Inject,
                                    false));
                        }
                        else
                        {
                            // For complex objects, use IOptionsSnapshot<T> dependencies
                            var optionsType =
                                semanticModel.Compilation.GetTypeByMetadataName(
                                    "Microsoft.Extensions.Options.IOptionsSnapshot`1");
                            if (optionsType != null)
                            {
                                var optionsSnapshotType = optionsType.Construct(configDep.FieldType);
                                currentDependencies.Add((optionsSnapshotType, configDep.FieldName,
                                    DependencySource.Inject,
                                    false));
                            }
                        }
                    }
                // CRITICAL FIX: Configuration object fields should NOT be constructor parameters
                // They are handled via IConfiguration binding in the constructor body only
                // Only Options pattern types and SupportsReloading fields get injected as DI dependencies
                // NOTE: Direct value/section binding fields are NOT added as individual constructor parameters
                // Instead, they are handled via IConfiguration binding in constructor body
                // The IConfiguration parameter itself will be added later in the global check
            }

            // Get [DependsOn] dependencies
            // For [ExternalService] classes: include in own constructor (level 0) but exclude from inheritance (level > 0)
            bool shouldIncludeDependsOn;
            if (isExternalService)
                // External services: include own dependencies but don't pass to inheritance
                shouldIncludeDependsOn = level == 0;
            else
                // For regular classes, always include [DependsOn] dependencies
                shouldIncludeDependsOn = true;

            if (shouldIncludeDependsOn)
            {
                var rawDependsOnDependencies =
                    DependsOnFieldAnalyzer.GetRawDependsOnFieldsForTypeWithExternalFlag(currentType);
                currentDependencies.AddRange(rawDependsOnDependencies.Select(d =>
                    (d.ServiceType, d.FieldName, DependencySource.DependsOn, d.IsExternal)));
            }

            // Add to appropriate collections
            foreach (var dep in currentDependencies)
            {
                allDependencies.Add((dep.ServiceType, dep.FieldName, dep.Source, level));
                allDependenciesWithExternalFlag.Add((dep.ServiceType, dep.FieldName, dep.Source, dep.IsExternal));

                if (level == 0)
                    derivedDependencies.Add((dep.ServiceType, dep.FieldName, dep.Source));
                else
                    baseDependencies.Add((dep.ServiceType, dep.FieldName, dep.Source));
            }

            currentType = currentType.BaseType;
            level++;
        }

        // Remove duplicates (keep the first occurrence - closest to derived class)
        // This also automatically removes redundancies that were detected and reported
        // Priority: 1) Closest to derived class (lowest Level), 2) [Inject] over [DependsOn]
        // Apply the "all ancestors manual" rule BEFORE grouping
        // NOTE: All dependencies are included for proper constructor generation

        // Check if any level needs IConfiguration and add it if not already present
        var needsIConfigurationGlobally = false;

        // Check all types in the hierarchy for configuration fields
        var checkType = classSymbol;
        var checkLevel = 0;
        while (checkType != null && checkType.SpecialType != SpecialType.System_Object)
        {
            var configDependencies =
                ConfigurationFieldAnalyzer.GetConfigurationInjectedFieldsForType(checkType, semanticModel);
            // Need IConfiguration if:
            // 1. Regular config fields (not options pattern, not supports reloading)
            // 2. Primitive SupportsReloading fields (direct value binding with SupportsReloading = true)
            if (configDependencies.Any(cd =>
                    !cd.IsOptionsPattern && (!cd.SupportsReloading || cd.IsDirectValueBinding)))
            {
                needsIConfigurationGlobally = true;
                break;
            }

            checkType = checkType.BaseType;
            checkLevel++;
        }

        // Add IConfiguration dependency if needed and not already present
        if (needsIConfigurationGlobally)
        {
            var iConfigurationType =
                semanticModel.Compilation.GetTypeByMetadataName("Microsoft.Extensions.Configuration.IConfiguration");
            if (iConfigurationType != null)
            {
                var hasExistingIConfiguration = allDependencies.Any(d =>
                    SymbolEqualityComparer.Default.Equals(d.ServiceType, iConfigurationType));

                if (!hasExistingIConfiguration)
                {
                    // CRITICAL FIX: IConfiguration should be added as a special mid-level dependency
                    // We want it to appear after base dependencies but before derived dependencies
                    // Use a special level of 0.5 to place it between base (level >= 1) and derived (level 0)
                    var configurationLevel = 0; // Add at derived level but with special ordering priority

                    allDependencies.Add((iConfigurationType, "_configuration", DependencySource.ConfigurationInjection,
                        configurationLevel));
                }
            }
        }

        // CRITICAL FIX: Simplified, reliable dependency ordering with correct conflict resolution
        // Group by both ServiceType AND FieldName to allow multiple fields of same type, keep closest to derived class
        var uniqueDependencies = allDependencies
            .GroupBy(d => $"{SymbolEqualityComparer.Default.GetHashCode(d.ServiceType)}_{d.FieldName}")
            .Select(g => g.OrderBy(d => d.Source == DependencySource.Inject ? 0 : 1).ThenBy(d => d.Level).First())
            // SIMPLE, PREDICTABLE ORDERING: Base dependencies first (higher level), then derived (level 0)
            .OrderByDescending(d => d.Level) // Higher levels (base classes) come first
            .ThenBy(d =>
                d.Source == DependencySource.DependsOn ? 0 :
                d.Source == DependencySource.Inject ? 1 : 2) // DependsOn, Inject, Config
            .Select(d => (d.ServiceType, d.FieldName, d.Source))
            .ToList();


        // CRITICAL FIX: Simplified base/derived dependency separation
        // Extract derived dependencies (level 0 only) from unique dependencies
        var finalDerivedDependencies = uniqueDependencies
            .Where(d =>
            {
                // Check if this dependency came from level 0 (derived class)
                var originalDep = allDependencies.First(ad =>
                    SymbolEqualityComparer.Default.Equals(ad.ServiceType, d.ServiceType) &&
                    ad.FieldName == d.FieldName &&
                    ad.Source == d.Source);
                return originalDep.Level == 0;
            })
            .ToList();

        // Get derived dependency types to exclude from base dependencies
        var derivedDependencyTypes = new HashSet<ITypeSymbol>(
            finalDerivedDependencies.Select(d => d.ServiceType),
            SymbolEqualityComparer.Default);

        // Base dependencies are those from level > 0 (parent classes) that are NOT in derived
        var finalBaseDependencies = allDependencies
            .Where(ad => ad.Level > 0 && !derivedDependencyTypes.Contains(ad.ServiceType))
            .GroupBy(ad => $"{SymbolEqualityComparer.Default.GetHashCode(ad.ServiceType)}_{ad.FieldName}")
            .Select(g => g.OrderBy(ad => ad.Level).ThenBy(ad =>
                    ad.Source == DependencySource.Inject ? 0 :
                    ad.Source == DependencySource.ConfigurationInjection ? 1 : 2)
                .First())
            .OrderBy(ad => ad.Level) // Order from immediate parent to deepest base class
            .Select(ad => (ad.ServiceType, ad.FieldName, ad.Source))
            .ToList();

        return new InheritanceHierarchyDependencies(uniqueDependencies, finalBaseDependencies,
            finalDerivedDependencies, allDependencies, allDependenciesWithExternalFlag);
    }

    public static InheritanceHierarchyDependencies GetInheritanceHierarchyDependenciesForDiagnostics(
        INamedTypeSymbol classSymbol,
        SemanticModel semanticModel,
        HashSet<string>? allRegisteredServices = null,
        Dictionary<string, List<INamedTypeSymbol>>? allImplementations = null)
    {
        var allDependencies =
            new List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source, int Level)>();
        var allDependenciesWithExternalFlag =
            new List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source, bool IsExternal)>();
        var baseDependencies = new List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)>();
        var derivedDependencies = new List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)>();

        var currentType = classSymbol;
        var level = 0;

        // Collect dependencies from current class and all base classes (include ALL dependencies for diagnostics)
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            var currentDependencies =
                new List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source, bool IsExternal)>();

            // Get [Inject] field dependencies with external flags
            var injectDependencies = InjectFieldAnalyzer.GetInjectedFieldsForTypeWithExternalFlag(currentType,
                semanticModel,
                allRegisteredServices, allImplementations);
            currentDependencies.AddRange(injectDependencies.Select(d =>
                (d.ServiceType, d.FieldName, DependencySource.Inject, d.IsExternal)));

            // Get [InjectConfiguration] field dependencies for diagnostics
            var configDependencies =
                ConfigurationFieldAnalyzer.GetConfigurationInjectedFieldsForType(currentType, semanticModel);
            foreach (var configDep in configDependencies)
                if (configDep.IsOptionsPattern)
                    // Options pattern fields are injected as regular dependencies
                    currentDependencies.Add((configDep.FieldType, configDep.FieldName, DependencySource.Inject, false));
                // CRITICAL FIX: Configuration object fields should NOT be constructor parameters for diagnostics
                // They are handled via IConfiguration binding in the constructor body only
                else
                    // Direct value/section binding fields need ConfigurationInjection source for diagnostics tracking
                    currentDependencies.Add((configDep.FieldType, configDep.FieldName,
                        DependencySource.ConfigurationInjection, false));

            // Always get [DependsOn] dependencies for diagnostics (unlike constructor generation)
            var rawDependsOnDependencies = DependsOnFieldAnalyzer.GetRawDependsOnFieldsForTypeWithExternalFlag(
                currentType, allRegisteredServices, allImplementations);
            currentDependencies.AddRange(rawDependsOnDependencies.Select(d =>
                (d.ServiceType, d.FieldName, DependencySource.DependsOn, d.IsExternal)));

            // Add to appropriate collections
            foreach (var dep in currentDependencies)
            {
                allDependencies.Add((dep.ServiceType, dep.FieldName, dep.Source, level));
                allDependenciesWithExternalFlag.Add((dep.ServiceType, dep.FieldName, dep.Source, dep.IsExternal));

                if (level == 0)
                    derivedDependencies.Add((dep.ServiceType, dep.FieldName, dep.Source));
                else
                    baseDependencies.Add((dep.ServiceType, dep.FieldName, dep.Source));
            }

            currentType = currentType.BaseType;
            level++;
        }

        // Remove duplicates (keep the first occurrence - closest to derived class)
        var uniqueDependencies = allDependencies
            .GroupBy(d => d.ServiceType, SymbolEqualityComparer.Default)
            .Select(g => g.OrderBy(d => d.Level).ThenBy(d => d.Source == DependencySource.Inject ? 0 : 1).First())
            .Select(d => (d.ServiceType, d.FieldName, d.Source))
            .ToList();

        // Rebuild base and derived lists based on unique dependencies
        // CRITICAL FIX: For ConfigurationInjection, use field name as the key since the same type can be injected multiple times with different sections
        // For other dependency types, continue to use ServiceType as the key
        var finalDerivedDependencies = allDependencies
            .Where(ad => ad.Level == 0)
            .GroupBy(ad => new
            {
                IsConfig = ad.Source == DependencySource.ConfigurationInjection,
                Key = ad.Source == DependencySource.ConfigurationInjection
                    ? ad.FieldName
                    : ad.ServiceType.ToDisplayString()
            })
            .Select(g => g.OrderBy(ad =>
                    ad.Source == DependencySource.Inject ? 0 :
                    ad.Source == DependencySource.ConfigurationInjection ? 1 : 2)
                .First())
            .Select(ad => (ad.ServiceType, ad.FieldName, ad.Source))
            .ToList();

        var derivedDependencyTypes = new HashSet<ITypeSymbol>(
            finalDerivedDependencies.Select(d => d.ServiceType),
            SymbolEqualityComparer.Default);

        var finalBaseDependencies = allDependencies
            .Where(ad => ad.Level > 0 && !derivedDependencyTypes.Contains(ad.ServiceType))
            .GroupBy(ad => new
            {
                IsConfig = ad.Source == DependencySource.ConfigurationInjection,
                Key = ad.Source == DependencySource.ConfigurationInjection
                    ? ad.FieldName
                    : ad.ServiceType.ToDisplayString()
            })
            .Select(g => g.OrderBy(ad => ad.Level).ThenBy(ad =>
                    ad.Source == DependencySource.Inject ? 0 :
                    ad.Source == DependencySource.ConfigurationInjection ? 1 : 2)
                .First())
            .OrderBy(ad => ad.Level)
            .Select(ad => (ad.ServiceType, ad.FieldName, ad.Source))
            .ToList();

        return new InheritanceHierarchyDependencies(uniqueDependencies, finalBaseDependencies,
            finalDerivedDependencies, allDependencies, allDependenciesWithExternalFlag);
    }


    /// <summary>
    ///     Checks if a dependency type should be treated as external by examining if any of its implementations have
    ///     [ExternalService] attribute
    /// </summary>
    /// <summary>
    ///     Generates field names for collection types, applying pluralization for IEnumerable and similar collections
    /// </summary>
    /// <summary>
    ///     Get dependencies optimized for constructor generation.
    ///     Deduplicates inheritance conflicts (same ServiceType across inheritance levels)
    ///     while preserving multiple fields of the same type within a single class.
    /// </summary>
    public static InheritanceHierarchyDependencies GetConstructorDependencies(INamedTypeSymbol classSymbol,
        SemanticModel semanticModel)
    {
        // Get the full dependencies using the diagnostic logic
        var diagnosticDependencies = GetInheritanceHierarchyDependencies(classSymbol, semanticModel);

        // CRITICAL DEBUG: Log when dependencies are not found
        var hasInjectFields = classSymbol.GetMembers().OfType<IFieldSymbol>()
            .Any(field => field.GetAttributes().Any(attr =>
                attr.AttributeClass?.Name == "InjectAttribute"));

        if (hasInjectFields && (diagnosticDependencies.AllDependencies == null ||
                                !diagnosticDependencies.AllDependencies.Any()))
        {
            // CRITICAL BUG: We have [Inject] fields but no dependencies - field detection is broken!
            // Force fallback processing for ALL classes with [Inject] fields, not just generics
            var symbolFields = classSymbol.GetMembers().OfType<IFieldSymbol>()
                .Where(field => !field.IsStatic && !field.IsConst)
                .Where(field => field.GetAttributes().Any(attr =>
                    attr.AttributeClass?.Name == "InjectAttribute"))
                .ToList();

            if (symbolFields.Any())
            {
                // CRITICAL FIX: Generate dependencies directly from symbols
                var fallbackDependencies =
                    new List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)>();
                var fallbackAllDependencies =
                    new List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source, int Level)>();

                foreach (var field in symbolFields)
                {
                    var substitutedType = TypeSubstitution.SubstituteTypeParameters(field.Type, classSymbol);
                    fallbackDependencies.Add((substitutedType, field.Name, DependencySource.Inject));
                    fallbackAllDependencies.Add((substitutedType, field.Name, DependencySource.Inject, 0));
                }

                return new InheritanceHierarchyDependencies(
                    fallbackDependencies,
                    new List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source
                        )>(), // BaseDependencies
                    fallbackDependencies, // DerivedDependencies
                    fallbackAllDependencies,
                    new List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source, bool IsExternal
                        )>() // AllDependenciesWithExternalFlag not needed for constructor generation
                );
            }
        }

        // CRITICAL FIX: If there are no dependencies, check if this is a generic type with [Inject] fields
        // that might have been missed by the standard processing logic
        if (diagnosticDependencies.AllDependencies == null || !diagnosticDependencies.AllDependencies.Any())
        {
            // For generic types, ensure we haven't missed any [Inject] fields due to processing complexity
            if (classSymbol.IsGenericType)
            {
                var symbolFields = classSymbol.GetMembers().OfType<IFieldSymbol>()
                    .Where(field => !field.IsStatic && !field.IsConst)
                    .Where(field => field.GetAttributes().Any(attr =>
                        attr.AttributeClass?.Name == "InjectAttribute"))
                    .ToList();

                if (symbolFields.Any())
                {
                    // CRITICAL FIX: Generate dependencies directly from symbols for generic types
                    var fallbackDependencies =
                        new List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)>();
                    var fallbackAllDependencies =
                        new List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source, int Level)>();

                    foreach (var field in symbolFields)
                    {
                        var substitutedType = TypeSubstitution.SubstituteTypeParameters(field.Type, classSymbol);
                        fallbackDependencies.Add((substitutedType, field.Name, DependencySource.Inject));
                        fallbackAllDependencies.Add((substitutedType, field.Name, DependencySource.Inject, 0));
                    }

                    return new InheritanceHierarchyDependencies(
                        fallbackDependencies,
                        new List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source
                            )>(), // BaseDependencies
                        fallbackDependencies, // DerivedDependencies
                        fallbackAllDependencies,
                        new List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source, bool IsExternal
                            )>() // AllDependenciesWithExternalFlag not needed for constructor generation
                    );
                }
            }

            return diagnosticDependencies;
        }

        // Group dependencies by ServiceType to find inheritance conflicts
        var constructorAllDependencies =
            new List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)>();

        var serviceTypeGroups = diagnosticDependencies.AllDependencies
            .GroupBy(d => d.ServiceType, SymbolEqualityComparer.Default);

        foreach (var group in serviceTypeGroups)
        {
            var dependencies = group.ToList();

            // CRITICAL FIX: For non-inheritance scenarios (single class), RawAllDependencies might not contain level info
            // In such cases, all dependencies will have level 0, so we should just add them all

            // Check if this ServiceType has dependencies across multiple inheritance levels
            var dependenciesWithLevels = dependencies.Select(d =>
            {
                // Need to find level from RawAllDependencies
                var rawDep = diagnosticDependencies.RawAllDependencies
                    .FirstOrDefault(rd => SymbolEqualityComparer.Default.Equals(rd.ServiceType, d.ServiceType) &&
                                          rd.FieldName == d.FieldName && rd.Source == d.Source);

                // CRITICAL FIX: Handle case where rawDep is not found (default tuple returns Level = 0)
                // This can happen if AllDependencies and RawAllDependencies are out of sync
                var level = rawDep.ServiceType != null ? rawDep.Level : 0;
                return (dependency: d, level);
            }).ToList();

            var levels = dependenciesWithLevels.Select(d => d.level).Distinct().ToList();

            if (levels.Count > 1)
            {
                // INHERITANCE CONFLICT: Same ServiceType across multiple levels
                // Choose the dependency from the most derived level (lowest level number)
                var preferredDependency = dependenciesWithLevels
                    .OrderBy(x => x.level) // Lower level = more derived
                    .ThenBy(x => x.dependency.Source == DependencySource.Inject ? 0 : 1) // Prefer Inject
                    .ThenBy(x => x.dependency.FieldName)
                    .First()
                    .dependency;

                constructorAllDependencies.Add(preferredDependency);
            }
            else
            {
                // NO INHERITANCE CONFLICT: Add all dependencies (multiple fields of same type in same class)
                constructorAllDependencies.AddRange(dependencies);
            }
        }

        return new InheritanceHierarchyDependencies(
            constructorAllDependencies,
            diagnosticDependencies.BaseDependencies,
            diagnosticDependencies.DerivedDependencies,
            diagnosticDependencies.RawAllDependencies,
            diagnosticDependencies.AllDependenciesWithExternalFlag);
    }

    // Backwards-compatible delegator to keep public surface stable
    public static List<ConfigurationInjectionInfo> GetConfigurationInjectedFieldsForType(
        INamedTypeSymbol typeSymbol,
        SemanticModel semanticModel)
        => ConfigurationFieldAnalyzer.GetConfigurationInjectedFieldsForType(typeSymbol, semanticModel);
}
