using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using IoCTools.Generator.Models;
using IoCTools.Generator.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace IoCTools.Generator.Analysis;

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
        // This will be used to determine the global rule for [UnregisteredService] [DependsOn] handling
        var derivedHasInjectFields = GetInjectedFieldsForType(classSymbol, semanticModel).Any();

        // Collect dependencies from current class and all base classes
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            var currentDependencies =
                new List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source, bool IsExternal)>();

            // Check service attributes for conditional dependency collection
            var isUnregisteredService = currentType.GetAttributes().Any(attr =>
                attr.AttributeClass?.ToDisplayString() ==
                "IoCTools.Abstractions.Annotations.UnregisteredServiceAttribute");
            var isExternalService = currentType.GetAttributes().Any(attr =>
                attr.AttributeClass?.ToDisplayString() == "IoCTools.Abstractions.Annotations.ExternalServiceAttribute");

            // [ExternalService] classes: Exclude dependencies from inheritance (they're managed elsewhere)
            // [UnregisteredService] classes: Include [Inject] fields but exclude [DependsOn] dependencies from inheritance
            // For regular classes: include both [Inject] and [DependsOn] dependencies

            // Get [Inject] field dependencies 
            // For [ExternalService] classes: include in own constructor (level 0) but exclude from inheritance (level > 0)
            if (!isExternalService || level == 0)
            {
                var injectDependencies = GetInjectedFieldsForTypeWithExternalFlag(currentType, semanticModel);
                currentDependencies.AddRange(injectDependencies.Select(d =>
                    (d.ServiceType, d.FieldName, DependencySource.Inject, d.IsExternal)));
            }

            // Get [InjectConfiguration] field dependencies
            // For [ExternalService] classes: include in own constructor (level 0) but exclude from inheritance (level > 0)  
            if (!isExternalService || level == 0)
            {
                var configDependencies = GetConfigurationInjectedFieldsForType(currentType, semanticModel);

                foreach (var configDep in configDependencies)
                    if (configDep.IsOptionsPattern)
                    {
                        // Options pattern fields are injected as regular dependencies
                        currentDependencies.Add((configDep.FieldType, configDep.FieldName, DependencySource.Inject,
                            false));
                    }
                    else if (configDep.SupportsReloading)
                    {
                        // SupportsReloading fields get converted to IOptionsSnapshot<T> dependencies
                        var optionsType =
                            semanticModel.Compilation.GetTypeByMetadataName(
                                "Microsoft.Extensions.Options.IOptionsSnapshot`1");
                        if (optionsType != null)
                        {
                            var optionsSnapshotType = optionsType.Construct(configDep.FieldType);
                            currentDependencies.Add((optionsSnapshotType, configDep.FieldName, DependencySource.Inject,
                                false));
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
            // For [UnregisteredService] classes: special handling based on [Inject] field presence
            bool shouldIncludeDependsOn;
            if (isExternalService)
                // External services: include own dependencies but don't pass to inheritance
                shouldIncludeDependsOn = level == 0;
            else if (isUnregisteredService && level > 0)
                // UnregisteredService base classes: always include [DependsOn] dependencies from inheritance
                // [UnregisteredService] only affects registration, NOT dependency inheritance
                shouldIncludeDependsOn = true;
            else
                // For [Service] classes, always include [DependsOn] dependencies
                shouldIncludeDependsOn = true;

            if (shouldIncludeDependsOn)
            {
                var rawDependsOnDependencies = GetRawDependsOnFieldsForTypeWithExternalFlag(currentType, semanticModel);
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
        // Apply the "all ancestors unregistered" rule BEFORE grouping
        // NOTE: Disabled for constructor generation - we need all dependencies for base constructor calls
        // if (allAncestorsAreUnregistered && ancestorCount >= 2)
        // {
        //     // Special case: if ALL ancestors are [UnregisteredService], exclude all [DependsOn] dependencies from base classes (level > 0)
        //     allDependencies = allDependencies.Where(d => !(d.Level > 0 && d.Source == DependencySource.DependsOn)).ToList();
        // }

        // Check if any level needs IConfiguration and add it if not already present
        var needsIConfigurationGlobally = false;

        // Check all types in the hierarchy for configuration fields
        var checkType = classSymbol;
        var checkLevel = 0;
        while (checkType != null && checkType.SpecialType != SpecialType.System_Object)
        {
            var configDependencies = GetConfigurationInjectedFieldsForType(checkType, semanticModel);
            if (configDependencies.Any(cd => !cd.IsOptionsPattern && !cd.SupportsReloading))
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
            var injectDependencies = GetInjectedFieldsForTypeWithExternalFlag(currentType, semanticModel,
                allRegisteredServices, allImplementations);
            currentDependencies.AddRange(injectDependencies.Select(d =>
                (d.ServiceType, d.FieldName, DependencySource.Inject, d.IsExternal)));

            // Get [InjectConfiguration] field dependencies for diagnostics
            var configDependencies = GetConfigurationInjectedFieldsForType(currentType, semanticModel);
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
            var rawDependsOnDependencies = GetRawDependsOnFieldsForTypeWithExternalFlag(currentType, semanticModel,
                allRegisteredServices, allImplementations);
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

    private static List<(ITypeSymbol ServiceType, string FieldName)> GetInjectedFieldsForType(
        INamedTypeSymbol typeSymbol,
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

                        // Check for [Inject] attribute
                        var hasInjectAttribute = false;
                        foreach (var attributeList in fieldDeclaration.AttributeLists)
                        foreach (var attribute in attributeList.Attributes)
                        {
                            var attributeText = attribute.Name.ToString();
                            if (attributeText == "Inject" || attributeText == "InjectAttribute" ||
                                (attributeText.EndsWith("Inject") && !attributeText.Contains("Configuration")) ||
                                (attributeText.EndsWith("InjectAttribute") && !attributeText.Contains("Configuration")))
                            {
                                hasInjectAttribute = true;
                                break;
                            }
                        }

                        if (hasInjectAttribute)
                            foreach (var variable in fieldDeclaration.Declaration.Variables)
                            {
                                var fieldName = variable.Identifier.Text;

                                // Only add if not already found by symbol approach
                                if (!fields.Any(f => f.FieldName == fieldName))
                                {
                                    // Get the field symbol to preserve nullable annotations
                                    var fieldSymbol = semanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;
                                    if (fieldSymbol?.Type != null)
                                    {
                                        var substitutedType =
                                            TypeSubstitution.SubstituteTypeParameters(fieldSymbol.Type, typeSymbol);
                                        fields.Add((substitutedType, fieldName));
                                    }
                                    else
                                    {
                                        // Fallback to GetTypeInfo if GetDeclaredSymbol fails
                                        var fieldType = semanticModel.GetTypeInfo(fieldDeclaration.Declaration.Type)
                                            .Type;
                                        if (fieldType != null)
                                        {
                                            var substitutedType =
                                                TypeSubstitution.SubstituteTypeParameters(fieldType, typeSymbol);
                                            fields.Add((substitutedType, fieldName));
                                        }
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

    private static List<(ITypeSymbol ServiceType, string FieldName, bool IsExternal)>
        GetInjectedFieldsForTypeWithExternalFlag(
            INamedTypeSymbol typeSymbol,
            SemanticModel semanticModel,
            HashSet<string>? allRegisteredServices = null,
            Dictionary<string, List<INamedTypeSymbol>>? allImplementations = null)
    {
        var fields = new List<(ITypeSymbol ServiceType, string FieldName, bool IsExternal)>();

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

                        // Check for [Inject] attribute and [ExternalService] attribute
                        var hasInjectAttribute = false;
                        var hasExternalServiceAttribute = false;

                        foreach (var attributeList in fieldDeclaration.AttributeLists)
                        foreach (var attribute in attributeList.Attributes)
                        {
                            var attributeText = attribute.Name.ToString();

                            // CRITICAL FIX: Use exact matching to avoid treating [InjectConfiguration] as [Inject]
                            // InjectConfiguration ends with "Inject" but should NOT be treated as [Inject]
                            if (attributeText == "Inject" || attributeText == "InjectAttribute" || 
                                (attributeText.EndsWith("Inject") && !attributeText.Contains("Configuration")))
                                hasInjectAttribute = true;

                            if (attributeText == "ExternalService" || attributeText == "ExternalServiceAttribute" ||
                                attributeText.EndsWith("ExternalService") ||
                                attributeText.EndsWith("ExternalServiceAttribute"))
                                hasExternalServiceAttribute = true;
                        }

                        if (hasInjectAttribute)
                            foreach (var variable in fieldDeclaration.Declaration.Variables)
                            {
                                var fieldName = variable.Identifier.Text;

                                // Only add if not already found by symbol approach
                                if (!fields.Any(f => f.FieldName == fieldName))
                                {
                                    // Get the field symbol to preserve nullable annotations
                                    var fieldSymbol = semanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;
                                    if (fieldSymbol?.Type != null)
                                    {
                                        var substitutedType =
                                            TypeSubstitution.SubstituteTypeParameters(fieldSymbol.Type, typeSymbol);
                                        var isExternal = hasExternalServiceAttribute || IsTypeExternal(substitutedType,
                                            allRegisteredServices, allImplementations);

                                        fields.Add((substitutedType, fieldName, isExternal));
                                    }
                                    else
                                    {
                                        // Fallback to GetTypeInfo if GetDeclaredSymbol fails
                                        var fieldType = semanticModel.GetTypeInfo(fieldDeclaration.Declaration.Type)
                                            .Type;
                                        if (fieldType != null)
                                        {
                                            var substitutedType =
                                                TypeSubstitution.SubstituteTypeParameters(fieldType, typeSymbol);
                                            var isExternal = hasExternalServiceAttribute ||
                                                             IsTypeExternal(substitutedType, allRegisteredServices,
                                                                 allImplementations);
                                            fields.Add((substitutedType, fieldName, isExternal));
                                        }
                                    }
                                }
                            }
                    }
            }
            catch (ArgumentException)
            {
                // CRITICAL FIX: For generic types, ArgumentException might occur during syntax processing
                // Fall back to symbol-based detection for generic type handling
                
                // Try symbol-based detection as fallback for generic types
                if (typeSymbol.IsGenericType)
                {
                    var symbolFields = typeSymbol.GetMembers().OfType<IFieldSymbol>()
                        .Where(field => !field.IsStatic && !field.IsConst)
                        .Where(field => field.GetAttributes().Any(attr => 
                            attr.AttributeClass?.Name == "InjectAttribute"))
                        .ToList();

                    foreach (var symbolField in symbolFields)
                    {
                        var fieldName = symbolField.Name;
                        if (!fields.Any(f => f.FieldName == fieldName))
                        {
                            var substitutedType = TypeSubstitution.SubstituteTypeParameters(symbolField.Type, typeSymbol);
                            var isExternal = IsTypeExternal(substitutedType, allRegisteredServices, allImplementations);
                            fields.Add((substitutedType, fieldName, isExternal));
                        }
                    }
                }
                
                // Continue with what we have from symbol detection
            }

        // CRITICAL FIX: For generic types, always ensure symbol-based detection as additional safety measure
        // This handles cases where syntax-based detection might miss fields due to partial classes or complex generics
        if (typeSymbol.IsGenericType && fields.Count == 0)
        {
            var symbolFields = typeSymbol.GetMembers().OfType<IFieldSymbol>()
                .Where(field => !field.IsStatic && !field.IsConst)
                .Where(field => field.GetAttributes().Any(attr => 
                    attr.AttributeClass?.Name == "InjectAttribute"))
                .ToList();

            foreach (var symbolField in symbolFields)
            {
                var fieldName = symbolField.Name;
                if (!fields.Any(f => f.FieldName == fieldName))
                {
                    var substitutedType = TypeSubstitution.SubstituteTypeParameters(symbolField.Type, typeSymbol);
                    var isExternal = IsTypeExternal(substitutedType, allRegisteredServices, allImplementations);
                    fields.Add((substitutedType, fieldName, isExternal));
                }
            }
        }

        return fields;
    }

    private static List<(ITypeSymbol ServiceType, string FieldName)> GetRawDependsOnFieldsForType(
        INamedTypeSymbol typeSymbol,
        SemanticModel semanticModel)
    {
        var fields = new List<(ITypeSymbol ServiceType, string FieldName)>();

        // Get the original generic type definition if this is a constructed generic type
        var originalTypeDefinition = typeSymbol.OriginalDefinition;

        var dependsOnAttributes = originalTypeDefinition.GetAttributes()
            .Where(attr => attr.AttributeClass?.Name?.StartsWith("DependsOn") == true)
            .ToList();

        foreach (var attribute in dependsOnAttributes)
        {
            var genericTypeArguments = attribute.AttributeClass?.TypeArguments.ToList();

            if (genericTypeArguments == null) continue;

            var (namingConvention, stripI, prefix) = AttributeParser.GetNamingConventionOptionsFromAttribute(attribute);

            foreach (var genericTypeArgument in genericTypeArguments)
            {
                // Substitute type parameters with actual type arguments if this is a constructed generic type
                var substitutedType = TypeSubstitution.SubstituteTypeParameters(genericTypeArgument, typeSymbol);

                var fieldName = AttributeParser.GenerateFieldName(TypeUtilities.GetMeaningfulTypeName(substitutedType),
                    namingConvention, stripI, prefix);
                fields.Add((substitutedType, fieldName));
            }
        }

        return fields; // Return raw fields without deduplication
    }

    private static List<(ITypeSymbol ServiceType, string FieldName)> GetRawDependsOnFieldsForTypeWithSubstitution(
        INamedTypeSymbol typeSymbol,
        INamedTypeSymbol targetTypeForSubstitution,
        SemanticModel semanticModel)
    {
        var fields = new List<(ITypeSymbol ServiceType, string FieldName)>();

        var dependsOnAttributes = typeSymbol.GetAttributes()
            .Where(attr =>
                attr.AttributeClass?.ToDisplayString()
                    .StartsWith("IoCTools.Abstractions.Annotations.DependsOnAttribute") == true)
            .ToList();

        foreach (var attribute in dependsOnAttributes)
            if (attribute.AttributeClass?.TypeArguments != null)
            {
                var (namingConvention, stripI, prefix) =
                    AttributeParser.GetNamingConventionOptionsFromAttribute(attribute);

                foreach (var genericTypeArgument in attribute.AttributeClass.TypeArguments)
                {
                    // CRITICAL FIX: Apply inheritance chain type substitution
                    var substitutedType = TypeSubstitution.ApplyInheritanceChainSubstitution(genericTypeArgument,
                        typeSymbol, targetTypeForSubstitution);

                    var fieldName = AttributeParser.GenerateFieldName(
                        TypeUtilities.GetMeaningfulTypeName(substitutedType), namingConvention, stripI, prefix);
                    fields.Add((substitutedType, fieldName));
                }
            }

        return fields; // Return raw fields without deduplication
    }

    private static List<(ITypeSymbol ServiceType, string FieldName, bool IsExternal)>
        GetRawDependsOnFieldsForTypeWithExternalFlag(
            INamedTypeSymbol typeSymbol,
            SemanticModel semanticModel,
            HashSet<string>? allRegisteredServices = null,
            Dictionary<string, List<INamedTypeSymbol>>? allImplementations = null)
    {
        var fields = new List<(ITypeSymbol ServiceType, string FieldName, bool IsExternal)>();

        // Get the original generic type definition if this is a constructed generic type
        var originalTypeDefinition = typeSymbol.OriginalDefinition;

        var dependsOnAttributes = originalTypeDefinition.GetAttributes()
            .Where(attr => attr.AttributeClass?.Name?.StartsWith("DependsOn") == true)
            .ToList();

        foreach (var attribute in dependsOnAttributes)
        {
            var genericTypeArguments = attribute.AttributeClass?.TypeArguments.ToList();

            if (genericTypeArguments == null) continue;

            var (namingConvention, stripI, prefix, external) =
                AttributeParser.GetDependsOnOptionsFromAttribute(attribute);

            foreach (var genericTypeArgument in genericTypeArguments)
            {
                // Substitute type parameters with actual type arguments if this is a constructed generic type
                var substitutedType = TypeSubstitution.SubstituteTypeParameters(genericTypeArgument, typeSymbol);

                var fieldName = AttributeParser.GenerateFieldName(
                    TypeUtilities.GetMeaningfulTypeName(substitutedType), namingConvention, stripI, prefix);
                var isExternal = external || IsTypeExternal(substitutedType, allRegisteredServices, allImplementations);
                fields.Add((substitutedType, fieldName, isExternal));
            }
        }

        return fields; // Return raw fields without deduplication
    }

    /// <summary>
    ///     Checks if a dependency type should be treated as external by examining if any of its implementations have
    ///     [ExternalService] attribute
    /// </summary>
    private static bool IsTypeExternal(ITypeSymbol dependencyType,
        HashSet<string>? allRegisteredServices,
        Dictionary<string, List<INamedTypeSymbol>>? allImplementations)
    {
        if (allImplementations == null || allRegisteredServices == null)
            return false;

        var dependencyTypeName = dependencyType.ToDisplayString();

        // CRITICAL FIX: Advanced DI patterns (Func<T>, Lazy<T>, etc.) are NOT external services
        // They are built-in framework types that should always be treated as standard dependencies
        if (IsAdvancedDIPattern(dependencyType)) return false; // Never mark advanced patterns as external

        // Check if any implementation of this type has [ExternalService] attribute
        if (allImplementations.TryGetValue(dependencyTypeName, out var implementations))
            return implementations.Any(impl => impl.GetAttributes()
                .Any(attr =>
                    attr.AttributeClass?.ToDisplayString() ==
                    "IoCTools.Abstractions.Annotations.ExternalServiceAttribute"));

        return false;
    }

    /// <summary>
    ///     Determines if a type represents an advanced DI pattern that should never be marked as external
    /// </summary>
    private static bool IsAdvancedDIPattern(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
            return false;

        var typeName = namedType.OriginalDefinition.ToDisplayString();

        // Framework-provided advanced DI patterns
        return typeName == "System.Func<>" ||
               typeName == "System.Lazy<>" ||
               typeName.StartsWith("System.Func<") ||
               typeName.StartsWith("System.Lazy<") ||
               (type.CanBeReferencedByName &&
                type.NullableAnnotation == NullableAnnotation.Annotated); // Nullable types
    }

    /// <summary>
    ///     Generates field names for collection types, applying pluralization for IEnumerable and similar collections
    /// </summary>
    private static string GenerateFieldNameForCollectionType(ITypeSymbol typeSymbol,
        string namingConvention,
        bool stripI,
        string prefix)
    {
        // Check if this is a collection type that should be pluralized
        if (typeSymbol is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            var collectionTypes = new[]
            {
                "IEnumerable", "IList", "ICollection", "List",
                "IReadOnlyList", "IReadOnlyCollection", "Array"
            };

            if (collectionTypes.Contains(namedType.Name) && namedType.TypeArguments.Length > 0)
            {
                // For collection types, get the inner type name and pluralize it
                var innerType = namedType.TypeArguments[0];
                var innerTypeName = TypeUtilities.GetMeaningfulTypeName(innerType);

                // Special handling for numbered interfaces (e.g., IService1, IService2)
                // Extract the base name and number separately for proper pluralization
                var baseTypeName = innerTypeName;
                var numberSuffix = "";

                var match = Regex.Match(innerTypeName, @"^(.+?)(\d+)$");
                if (match.Success)
                {
                    baseTypeName = match.Groups[1].Value; // e.g., "Service" from "Service1"
                    numberSuffix = match.Groups[2].Value; // e.g., "1" from "Service1"
                }

                // Generate base field name from the base type name
                var baseFieldName = AttributeParser.GenerateFieldName(baseTypeName, namingConvention, stripI, prefix);

                // Pluralize by converting the field name (e.g., "_service" -> "_services")
                // Remove the prefix to work with the actual name part
                var prefixToUse = string.IsNullOrEmpty(prefix) ? "_" : prefix;
                string pluralizedFieldName;

                if (baseFieldName.StartsWith(prefixToUse))
                {
                    var nameWithoutPrefix = baseFieldName.Substring(prefixToUse.Length);
                    var pluralizedName = PluralizeName(nameWithoutPrefix);
                    pluralizedFieldName = prefixToUse + pluralizedName;
                }
                else
                {
                    // Fallback: use pluralization helper
                    pluralizedFieldName = PluralizeName(baseFieldName);
                }

                // For numbered types, append the number appropriately
                // IService1 -> _services (no suffix for "1")
                // IService2 -> _services2, IService3 -> _services3, etc.
                if (!string.IsNullOrEmpty(numberSuffix))
                {
                    if (numberSuffix == "1") return pluralizedFieldName; // No suffix for "1"

                    // Append the number to the plural form
                    // "_services" -> "_services2", "_services3", etc.
                    return pluralizedFieldName + numberSuffix;
                }

                return pluralizedFieldName;
            }
        }

        // For non-collection types, use the normal field name generation
        return AttributeParser.GenerateFieldName(TypeUtilities.GetMeaningfulTypeName(typeSymbol), namingConvention,
            stripI, prefix);
    }

    /// <summary>
    ///     Applies proper English pluralization rules to field names
    /// </summary>
    private static string PluralizeName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        // Already plural (ends with 's', but not special cases like 'ss' or 'us')
        if (name.EndsWith("s") && !name.EndsWith("ss") && !name.EndsWith("us") && !name.EndsWith("is"))
            return name;

        // Common English pluralization rules
        var lowerName = name.ToLowerInvariant();

        // Words ending in -y (preceded by consonant) -> -ies
        if (lowerName.EndsWith("y") && lowerName.Length > 1 && !"aeiou".Contains(lowerName[lowerName.Length - 2]))
            return name.Substring(0, name.Length - 1) + "ies";

        // Words ending in -s, -sh, -ch, -x, -z -> add -es
        if (lowerName.EndsWith("s") || lowerName.EndsWith("sh") || lowerName.EndsWith("ch") ||
            lowerName.EndsWith("x") || lowerName.EndsWith("z"))
            return name + "es";

        // Words ending in -f or -fe -> -ves
        if (lowerName.EndsWith("f")) return name.Substring(0, name.Length - 1) + "ves";
        if (lowerName.EndsWith("fe")) return name.Substring(0, name.Length - 2) + "ves";

        // Words ending in -o (preceded by consonant) -> -oes (but many exceptions, so be conservative)
        // Only apply to common words to avoid over-pluralization
        if (lowerName.EndsWith("o") && lowerName.Length > 1 && !"aeiou".Contains(lowerName[lowerName.Length - 2]))
        {
            // Common words that take -oes
            var oesToWords = new[] { "hero", "potato", "tomato", "echo", "embargo", "veto" };
            if (oesToWords.Contains(lowerName)) return name + "es";
        }

        // Default: just add 's'
        return name + "s";
    }

    public static List<ConfigurationInjectionInfo> GetConfigurationInjectedFieldsForType(
        INamedTypeSymbol typeSymbol,
        SemanticModel semanticModel)
    {
        var configFields = new List<ConfigurationInjectionInfo>();

        // Use syntax-based detection as PRIMARY approach for better reliability with compound access modifiers
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

                        // Check for [InjectConfiguration] attribute
                        AttributeSyntax? injectConfigAttribute = null;
                        foreach (var attributeList in fieldDeclaration.AttributeLists)
                        foreach (var attribute in attributeList.Attributes)
                        {
                            var attributeText = attribute.Name.ToString();
                            if (attributeText == "InjectConfiguration" ||
                                attributeText == "InjectConfigurationAttribute" ||
                                attributeText.EndsWith("InjectConfiguration") ||
                                attributeText.EndsWith("InjectConfigurationAttribute"))
                            {
                                injectConfigAttribute = attribute;
                                break;
                            }
                        }

                        if (injectConfigAttribute != null)
                            foreach (var variable in fieldDeclaration.Declaration.Variables)
                            {
                                var fieldName = variable.Identifier.Text;

                                // Get the field symbol to access type information
                                var fieldSymbol = semanticModel.GetDeclaredSymbol(variable) as IFieldSymbol;
                                if (fieldSymbol?.Type != null)
                                {
                                    var substitutedType =
                                        TypeSubstitution.SubstituteTypeParameters(fieldSymbol.Type, typeSymbol);

                                    // Parse attribute parameters
                                    var (configKey, defaultValue, required, supportsReloading) =
                                        ParseInjectConfigurationAttribute(injectConfigAttribute, semanticModel);

                                    configFields.Add(new ConfigurationInjectionInfo(
                                        fieldName,
                                        substitutedType,
                                        configKey,
                                        defaultValue,
                                        required,
                                        supportsReloading));
                                }
                            }
                    }
            }
            catch (ArgumentException)
            {
                // Continue with what we have
            }

        return configFields;
    }

    private static (string? configKey, object? defaultValue, bool required, bool supportsReloading)
        ParseInjectConfigurationAttribute(
            AttributeSyntax attributeSyntax,
            SemanticModel semanticModel)
    {
        string? configKey = null;
        object? defaultValue = null;
        var required = true; // default value
        var supportsReloading = false; // default value

        if (attributeSyntax.ArgumentList != null)
            foreach (var argument in attributeSyntax.ArgumentList.Arguments)
                if (argument.NameEquals != null)
                {
                    // Named parameters like DefaultValue = 30, Required = false
                    var parameterName = argument.NameEquals.Name.Identifier.ValueText;
                    switch (parameterName)
                    {
                        case "DefaultValue":
                            defaultValue = GetConstantValue(argument.Expression, semanticModel);
                            break;
                        case "Required":
                            if (GetConstantValue(argument.Expression, semanticModel) is bool reqValue)
                                required = reqValue;
                            break;
                        case "SupportsReloading":
                            if (GetConstantValue(argument.Expression, semanticModel) is bool reloadValue)
                                supportsReloading = reloadValue;
                            break;
                    }
                }
                else
                {
                    // Positional parameters - first one is configuration key
                    if (configKey == null && GetConstantValue(argument.Expression, semanticModel) is string keyValue)
                        configKey = keyValue;
                }

        return (configKey, defaultValue, required, supportsReloading);
    }

    private static object? GetConstantValue(ExpressionSyntax expression,
        SemanticModel semanticModel)
    {
        try
        {
            var constantValue = semanticModel.GetConstantValue(expression);
            if (constantValue.HasValue)
                return constantValue.Value;

            // CRITICAL FIX: Fallback to literal value extraction for cases where semantic model fails
            // This handles cases like string literals that might not be fully resolved during generation
            if (expression is LiteralExpressionSyntax literal)
            {
                var text = literal.Token.ValueText;
                return text; // Return the string value directly
            }

            // For boolean literals
            if (expression is LiteralExpressionSyntax boolLiteral &&
                (boolLiteral.Token.IsKind(SyntaxKind.TrueKeyword) ||
                 boolLiteral.Token.IsKind(SyntaxKind.FalseKeyword)))
                return boolLiteral.Token.IsKind(SyntaxKind.TrueKeyword);

            // For numeric literals, try to parse them
            if (expression is LiteralExpressionSyntax numLiteral &&
                numLiteral.Token.IsKind(SyntaxKind.NumericLiteralToken))
            {
                var numText = numLiteral.Token.ValueText;
                if (int.TryParse(numText, out var intValue))
                    return intValue;
                if (double.TryParse(numText, out var doubleValue))
                    return doubleValue;
                if (decimal.TryParse(numText, out var decimalValue))
                    return decimalValue;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

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
                
        if (hasInjectFields && (diagnosticDependencies.AllDependencies == null || !diagnosticDependencies.AllDependencies.Any()))
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
                var fallbackDependencies = new List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)>();
                var fallbackAllDependencies = new List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source, int Level)>();
                
                foreach (var field in symbolFields)
                {
                    var substitutedType = TypeSubstitution.SubstituteTypeParameters(field.Type, classSymbol);
                    fallbackDependencies.Add((substitutedType, field.Name, DependencySource.Inject));
                    fallbackAllDependencies.Add((substitutedType, field.Name, DependencySource.Inject, 0));
                }
                
                return new InheritanceHierarchyDependencies(
                    fallbackDependencies,
                    new List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)>(),  // BaseDependencies
                    fallbackDependencies,  // DerivedDependencies
                    fallbackAllDependencies,
                    null  // AllDependenciesWithExternalFlag not needed for constructor generation
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
                    var fallbackDependencies = new List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)>();
                    var fallbackAllDependencies = new List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source, int Level)>();
                    
                    foreach (var field in symbolFields)
                    {
                        var substitutedType = TypeSubstitution.SubstituteTypeParameters(field.Type, classSymbol);
                        fallbackDependencies.Add((substitutedType, field.Name, DependencySource.Inject));
                        fallbackAllDependencies.Add((substitutedType, field.Name, DependencySource.Inject, 0));
                    }
                    
                    return new InheritanceHierarchyDependencies(
                        fallbackDependencies,
                        new List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)>(),  // BaseDependencies
                        fallbackDependencies,  // DerivedDependencies
                        fallbackAllDependencies,
                        null  // AllDependenciesWithExternalFlag not needed for constructor generation
                    );
                }
            }
            
            return diagnosticDependencies;
        }
        
        // Group dependencies by ServiceType to find inheritance conflicts
        var constructorAllDependencies = new List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)>();
        
        var serviceTypeGroups = diagnosticDependencies.AllDependencies
            .GroupBy(d => d.ServiceType, SymbolEqualityComparer.Default);
            
        foreach (var group in serviceTypeGroups)
        {
            var dependencies = group.ToList();
            
            // CRITICAL FIX: For non-inheritance scenarios (single class), RawAllDependencies might not contain level info
            // In such cases, all dependencies will have level 0, so we should just add them all
            
            // Check if this ServiceType has dependencies across multiple inheritance levels
            var dependenciesWithLevels = dependencies.Select(d => {
                // Need to find level from RawAllDependencies
                var rawDep = diagnosticDependencies.RawAllDependencies
                    .FirstOrDefault(rd => SymbolEqualityComparer.Default.Equals(rd.ServiceType, d.ServiceType) && 
                                         rd.FieldName == d.FieldName && rd.Source == d.Source);
                
                // CRITICAL FIX: Handle case where rawDep is not found (default tuple returns Level = 0)
                // This can happen if AllDependencies and RawAllDependencies are out of sync
                var level = rawDep.ServiceType != null ? rawDep.Level : 0;
                return (dependency: d, level: level);
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
}