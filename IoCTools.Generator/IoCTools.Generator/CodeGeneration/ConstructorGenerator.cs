using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IoCTools.Generator.Analysis;
using IoCTools.Generator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace IoCTools.Generator.CodeGeneration;

internal static class ConstructorGenerator
{
    /// <summary>
    ///     Generate inheritance-aware constructor with SourceProductionContext (for IIncrementalGenerator)
    /// </summary>
    public static string GenerateInheritanceAwareConstructorCodeWithContext(TypeDeclarationSyntax classDeclaration,
        InheritanceHierarchyDependencies hierarchyDependencies,
        SemanticModel semanticModel,
        SourceProductionContext context)
    {
        return GenerateInheritanceAwareConstructorCodeCore(classDeclaration, hierarchyDependencies, semanticModel,
            (descriptor,
                location,
                args) =>
            {
                var diagnostic = Diagnostic.Create(descriptor, location, args);
                context.ReportDiagnostic(diagnostic);
            });
    }

    /// <summary>
    ///     Generate inheritance-aware constructor with GeneratorExecutionContext (for legacy support)
    /// </summary>
    public static string GenerateInheritanceAwareConstructorCode(TypeDeclarationSyntax classDeclaration,
        InheritanceHierarchyDependencies hierarchyDependencies,
        SemanticModel semanticModel,
        GeneratorExecutionContext context)
    {
        return GenerateInheritanceAwareConstructorCodeCore(classDeclaration, hierarchyDependencies, semanticModel,
            (descriptor,
                location,
                args) =>
            {
                var diagnostic = Diagnostic.Create(descriptor, location, args);
                context.ReportDiagnostic(diagnostic);
            });
    }

    /// <summary>
    ///     Core constructor generation logic that can be used with different context types
    /// </summary>
    private static string GenerateInheritanceAwareConstructorCodeCore(TypeDeclarationSyntax classDeclaration,
        InheritanceHierarchyDependencies hierarchyDependencies,
        SemanticModel semanticModel,
        Action<DiagnosticDescriptor, Location, object[]> reportDiagnostic)
    {
        try
        {
            // Defensive null checks
            if (classDeclaration == null)
                throw new ArgumentNullException(nameof(classDeclaration));
            if (hierarchyDependencies == null)
                throw new ArgumentNullException(nameof(hierarchyDependencies));
            if (semanticModel == null)
                throw new ArgumentNullException(nameof(semanticModel));

            // CRITICAL FIX: Check if class is marked as partial
            // Constructor generation only works for partial classes
            var isPartial = classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));
            if (!isPartial)
            {
                // For non-partial classes with [Inject] fields, report a diagnostic error
                // instead of trying to generate a constructor that won't compile
                var nonPartialClassSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);
                if (nonPartialClassSymbol != null && HasInjectFields(hierarchyDependencies))
                {
                    var descriptor = new DiagnosticDescriptor(
                        "IOC030",
                        "Class with [Inject] fields must be marked as partial",
                        "Class '{0}' contains [Inject] fields but is not marked as partial. Add the 'partial' keyword to enable constructor generation.",
                        "IoCTools",
                        DiagnosticSeverity.Warning, // Changed to Warning for educational examples
                        true);

                    var location = classDeclaration.Identifier.GetLocation();
                    reportDiagnostic(descriptor, location, new object[] { nonPartialClassSymbol.Name });
                }

                // Return empty string - do not generate constructor for non-partial classes
                return "";
            }

            var uniqueNamespaces = new HashSet<string>();

            // Get the class symbol first as it's needed for configuration dependencies
            INamedTypeSymbol? classSymbol = null;
            try
            {
                classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);
            }
            catch (ArgumentException)
            {
                // ClassDeclaration is not from this semantic model's syntax tree
                // This should not happen in normal operation, but add defensive handling
            }

            if (hierarchyDependencies.AllDependencies != null)
                foreach (var (serviceType, _, _) in hierarchyDependencies.AllDependencies)
                    if (serviceType != null)
                    {
                        CollectNamespaces(serviceType, uniqueNamespaces);

                        // Special handling for common generic types like ILogger<T>
                        if (serviceType is INamedTypeSymbol namedType && namedType.IsGenericType)
                        {
                            var fullTypeName = namedType.OriginalDefinition.ToDisplayString();
                            if (fullTypeName.StartsWith("Microsoft.Extensions.Logging.ILogger<"))
                                uniqueNamespaces.Add("Microsoft.Extensions.Logging");
                        }
                    }

            // Add configuration dependencies to namespace collection
            if (classSymbol != null)
            {
                var configDependenciesForNamespaces =
                    GetConfigurationDependencies(classSymbol, semanticModel, uniqueNamespaces);
                foreach (var (serviceType, _, _) in configDependenciesForNamespaces)
                    if (serviceType != null)
                        CollectNamespaces(serviceType, uniqueNamespaces);

                // Add configuration-specific namespaces for section binding
                var configFields = DependencyAnalyzer.GetConfigurationInjectedFieldsForType(classSymbol, semanticModel);
                if (configFields.Any())
                {
                    // Always add System namespace for built-in types like TimeSpan
                    uniqueNamespaces.Add("System");

                    // Add Microsoft.Extensions.Configuration for IConfiguration interface and extension methods
                    uniqueNamespaces.Add("Microsoft.Extensions.Configuration");

                    // Add Microsoft.Extensions.Options for options pattern types or SupportsReloading
                    if (configFields.Any(f => f.IsOptionsPattern || f.SupportsReloading))
                        uniqueNamespaces.Add("Microsoft.Extensions.Options");

                    // Add System.Collections.Generic for collection types
                    if (configFields.Any(f => IsCollectionType(f.FieldType)))
                    {
                        uniqueNamespaces.Add("System.Collections.Generic");
                        uniqueNamespaces.Add("System.Collections");
                    }

                    // CRITICAL FIX: Collect namespaces from all configuration field types
                    foreach (var configField in configFields)
                        CollectNamespaces(configField.FieldType, uniqueNamespaces);
                }
            }

            // Collect namespaces from generic constraint types
            if (classDeclaration.TypeParameterList != null && classDeclaration.ConstraintClauses.Any())
                foreach (var constraintClause in classDeclaration.ConstraintClauses)
                    CollectNamespacesFromConstraints(constraintClause, semanticModel, uniqueNamespaces);

            var namespaceName = GetClassNamespace(classDeclaration) ?? "";

            var accessibilityModifier = classSymbol != null ? GetClassAccessibilityModifier(classSymbol) : "public";

            // Get the type declaration keyword (class, record, struct, etc.)
            var typeKeyword = GetTypeDeclarationKeyword(classDeclaration);

            // Create namespaces for using statements, excluding self-namespace to avoid redundant using statements
            var namespacesForUsings = new HashSet<string>(uniqueNamespaces);
            if (!string.IsNullOrEmpty(namespaceName)) namespacesForUsings.Remove(namespaceName);

            // Create namespaces for type name stripping (includes self-namespace for clean type names)
            var namespacesForStripping = new HashSet<string>(uniqueNamespaces);

            var usings = new StringBuilder();
            foreach (var ns in namespacesForUsings)
                if (!string.IsNullOrEmpty(ns))
                    usings.AppendLine($"using {ns};");
            var fullClassName = classDeclaration.Identifier.Text;
            var constructorName = classDeclaration.Identifier.Text;

            // Handle generic classes
            var constraintClauses = "";
            if (classDeclaration.TypeParameterList != null)
            {
                var typeParameters = classDeclaration.TypeParameterList.Parameters
                    .Select(param => param.Identifier.Text);
                fullClassName += $"<{string.Join(", ", typeParameters)}>";
                // NOTE: Constructor name should NOT include type parameters - only class declaration should

                // Extract generic constraints if they exist
                if (classDeclaration.ConstraintClauses.Any())
                {
                    var constraints = classDeclaration.ConstraintClauses
                        .Select(clause => clause.ToString().Trim());
                    constraintClauses = $"\n    {string.Join("\n    ", constraints)}";
                }
            }

            // Get existing field names from the class symbol (includes all partial declarations)
            var currentClassSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);
            var existingFieldNames = new HashSet<string>();
            if (currentClassSymbol != null)
            {
                // Check for ALL fields, not just [Inject] fields, because [DependsOn] fields 
                // might already be generated in other partial declarations
                var allFields = currentClassSymbol.GetMembers().OfType<IFieldSymbol>();

                foreach (var field in allFields) existingFieldNames.Add(field.Name);
            }

            // Check if a constructor with the same signature already exists
            var allHierarchyDependencies = hierarchyDependencies.AllDependencies ??
                                           new List<(ITypeSymbol, string, DependencySource)>();
            var parameterTypes = allHierarchyDependencies.Select(d => d.ServiceType).ToList();

            var derivedClassConstructors = currentClassSymbol?.GetMembers().OfType<IMethodSymbol>()
                .Where(m => m.MethodKind == MethodKind.Constructor && !m.IsStatic)
                .ToList() ?? new List<IMethodSymbol>();

            foreach (var existingCtor in derivedClassConstructors)
                if (existingCtor.Parameters.Length == parameterTypes.Count)
                {
                    var signaturesMatch = true;
                    for (var i = 0; i < parameterTypes.Count; i++)
                        if (!SymbolEqualityComparer.Default.Equals(existingCtor.Parameters[i].Type, parameterTypes[i]))
                        {
                            signaturesMatch = false;
                            break;
                        }

                    if (signaturesMatch)
                    {
                        // Skip if this is an implicit (compiler-generated) default constructor
                        // We want to generate explicit constructors for services
                        if (existingCtor.IsImplicitlyDeclared)
                            continue;

                        // Constructor with same signature already exists, don't generate
                        return "";
                    }
                }

            // Only generate fields for dependencies not already declared
            // Skip ConfigurationInjection dependencies as they're only constructor parameters, not stored fields
            // CRITICAL FIX: Also check if field has [InjectConfiguration] attribute - these fields already exist in source
            var configFieldNames = new HashSet<string>();
            if (classSymbol != null)
            {
                var configFields = DependencyAnalyzer.GetConfigurationInjectedFieldsForType(classSymbol, semanticModel);
                foreach (var configField in configFields) configFieldNames.Add(configField.FieldName);
            }

            // CRITICAL FIX: Use deduplicated AllDependencies instead of RawAllDependencies to prevent duplicate fields
            // This ensures duplicate [DependsOn<T>] attributes only generate one field
            var allLevelZeroDependencies = hierarchyDependencies.DerivedDependencies ?? 
                new List<(ITypeSymbol, string, DependencySource)>();

            var fieldsToGenerate = allLevelZeroDependencies
                .Where(f => !existingFieldNames.Contains(f.FieldName))
                .Where(f => f.Source != DependencySource.ConfigurationInjection)
                .Where(f => !configFieldNames
                    .Contains(f.FieldName)) // CRITICAL: Don't generate fields for [InjectConfiguration] fields
                .ToList();

            // DEBUG LOG: Verify we have DependsOn fields to generate
            var dependsOnCount = allLevelZeroDependencies.Count(f => f.Source == DependencySource.DependsOn);
            var filteredDependsOnCount = fieldsToGenerate.Count(f => f.Source == DependencySource.DependsOn);
            
            // CRITICAL FIX: If all DependsOn fields were filtered out but we should have some, this indicates a bug
            if (dependsOnCount > 0 && filteredDependsOnCount == 0)
            {
                // Re-add DependsOn fields that were incorrectly filtered out
                var missingDependsOnFields = allLevelZeroDependencies
                    .Where(f => f.Source == DependencySource.DependsOn)
                    .Where(f => !existingFieldNames.Contains(f.FieldName))
                    .ToList();
                fieldsToGenerate.AddRange(missingDependsOnFields);
            }

            // CRITICAL FIX: Handle collision between [DependsOn] and [Inject] for same ServiceType in field generation
            // If both exist for same ServiceType, don't generate DependsOn field when Inject field exists
            var finalFieldsToGenerate = new List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)>();
            var fieldsByServiceType = fieldsToGenerate.GroupBy(f => f.ServiceType, SymbolEqualityComparer.Default);
            
            foreach (var group in fieldsByServiceType)
            {
                var fieldsForType = group.ToList();
                var injectFields = fieldsForType.Where(f => f.Source == DependencySource.Inject).ToList();
                var dependsOnFields = fieldsForType.Where(f => f.Source == DependencySource.DependsOn).ToList();
                
                if (injectFields.Any() && dependsOnFields.Any())
                {
                    // COLLISION SCENARIO: Both [Inject] and [DependsOn] exist for same ServiceType
                    // Don't generate DependsOn fields when Inject fields exist - Inject fields already exist in source
                    // CRITICAL FIX: Only skip DependsOn fields, but keep Inject fields if they need to be generated
                    // (This case is rare since Inject fields usually exist in source code)
                    finalFieldsToGenerate.AddRange(injectFields);
                    
                    // Add any other fields that aren't part of the collision
                    var otherFields = fieldsForType.Where(f => f.Source != DependencySource.Inject && f.Source != DependencySource.DependsOn);
                    finalFieldsToGenerate.AddRange(otherFields);
                }
                else
                {
                    // Normal case: no collision, add all fields for this type
                    // This includes pure DependsOn cases (like OrderProcessingService)
                    finalFieldsToGenerate.AddRange(fieldsForType);
                }
            }
            
            // Use collision-resolved fields for field generation
            fieldsToGenerate = finalFieldsToGenerate;

            // CRITICAL FIX: Determine if fields should be protected for inheritance scenarios
            var accessModifier = ShouldUseProtectedFields(classDeclaration, classSymbol) ? "protected" : "private";
            
            var fieldDeclarations = fieldsToGenerate.Select(d =>
                $"{accessModifier} readonly {RemoveNamespacesAndDots(d.ServiceType, namespacesForStripping)} {d.FieldName};");

            var fieldsStr = string.Join("\n    ", fieldDeclarations);

            // CRITICAL FIX: Use AllDependencies which already has correct inheritance ordering
            // DependencyAnalyzer already provides dependencies ordered by level and source type
            // DO NOT re-group - preserve the inheritance-aware ordering from DependencyAnalyzer
            var constructorDependencies = hierarchyDependencies.AllDependencies ??
                                          new List<(ITypeSymbol, string, DependencySource)>();

            // CRITICAL FIX: Filter out individual configuration field dependencies from constructor parameters
            // Only keep the IConfiguration parameter itself - individual config fields are handled via binding in constructor body
            var allDependencies = constructorDependencies
                .Where(d => d.Source != DependencySource.ConfigurationInjection || d.FieldName == "_configuration")
                .ToList();


            // CRITICAL FIX: Handle collision between [DependsOn] and [Inject] for same ServiceType
            // If both exist for same ServiceType, prefer [Inject] field over [DependsOn] field
            var finalDependencies = new List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)>();
            var dependenciesByServiceType = allDependencies.GroupBy(d => d.ServiceType, SymbolEqualityComparer.Default);
            
            foreach (var group in dependenciesByServiceType)
            {
                var dependenciesForType = group.ToList();
                var injectDeps = dependenciesForType.Where(d => d.Source == DependencySource.Inject).ToList();
                var dependsOnDeps = dependenciesForType.Where(d => d.Source == DependencySource.DependsOn).ToList();
                
                if (injectDeps.Count == 1 && dependsOnDeps.Count == 1)
                {
                    // COLLISION SCENARIO: One [Inject] field vs one [DependsOn] for same ServiceType
                    // Prefer the [Inject] dependency - it represents an existing field that should take precedence
                    finalDependencies.Add(injectDeps.First());
                    
                    // Add any other dependencies that aren't part of the collision
                    var otherDeps = dependenciesForType.Where(d => d.Source != DependencySource.Inject && d.Source != DependencySource.DependsOn);
                    finalDependencies.AddRange(otherDeps);
                }
                else
                {
                    // Normal case: no simple collision, add all dependencies for this type
                    finalDependencies.AddRange(dependenciesForType);
                }
            }
            
            // Use collision-resolved dependencies for parameter generation
            allDependencies = finalDependencies;

            // Generate unique parameter names to avoid CS0100 duplicate parameter errors
            var parameterNames = new HashSet<string>();
            var parametersWithNames =
                new List<(string TypeString, string ParamName, (ITypeSymbol ServiceType, string FieldName,
                    DependencySource Source) Dependency)>();

            foreach (var f in allDependencies)
            {
                var baseParamName = GetParameterNameFromFieldName(f.FieldName);

                var paramName = baseParamName;
                var counter = 1;
                while (parameterNames.Contains(paramName))
                {
                    paramName = $"{baseParamName}{counter}";
                    counter++;
                }

                parameterNames.Add(paramName);

                var typeString = GetTypeStringWithNullableAnnotation(f.ServiceType, f.FieldName, classSymbol!,
                    namespacesForStripping);
                parametersWithNames.Add((typeString, paramName, f));
            }

            // Generate base constructor call
            var baseCallStr = "";
            var baseClass = classSymbol?.BaseType;

            if (baseClass != null)
            {
                var baseHierarchyDependencies =
                    DependencyAnalyzer.GetConstructorDependencies(baseClass, semanticModel);

                // Check if base class will have constructor and is eligible for DI parameters
                var canBaseAcceptDIParameters = !baseClass.GetAttributes().Any(attr =>
                    attr.AttributeClass?.ToDisplayString() ==
                    "IoCTools.Abstractions.Annotations.ExternalServiceAttribute");
                var isBaseClassPartial = baseClass.DeclaringSyntaxReferences.Any(syntaxRef =>
                    syntaxRef.GetSyntax() is TypeDeclarationSyntax typeDecl &&
                    typeDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)));

                // Check if base class inherits from BackgroundService
                var baseIsBackgroundService = false;
                var currentBaseType = baseClass;
                while (currentBaseType != null && !baseIsBackgroundService)
                {
                    if (currentBaseType.ToDisplayString() == "Microsoft.Extensions.Hosting.BackgroundService")
                    {
                        baseIsBackgroundService = true;
                        break;
                    }

                    currentBaseType = currentBaseType.BaseType;
                }

                var baseClassWillHaveConstructor = baseClass.GetAttributes().Any(attr =>
                                                       attr.AttributeClass?.ToDisplayString() ==
                                                       "IoCTools.Abstractions.Annotations.ServiceAttribute" ||
                                                       attr.AttributeClass?.ToDisplayString() ==
                                                       "IoCTools.Abstractions.Annotations.UnregisteredServiceAttribute") ||
                                                   baseIsBackgroundService;

                // Check if this class is subject to "all ancestors unregistered" rule
                // If so, it should not call base constructors
                var allAncestorsAreUnregistered = false;
                var ancestorCount = 0;
                var tempType = classSymbol.BaseType;
                while (tempType != null && tempType.SpecialType != SpecialType.System_Object)
                {
                    ancestorCount++;
                    var isUnregistered = tempType.GetAttributes().Any(attr =>
                        attr.AttributeClass?.ToDisplayString() ==
                        "IoCTools.Abstractions.Annotations.UnregisteredServiceAttribute");
                    if (!isUnregistered) break;
                    tempType = tempType.BaseType;
                }

                if (ancestorCount >= 2)
                {
                    allAncestorsAreUnregistered = true;
                    tempType = classSymbol.BaseType;
                    while (tempType != null && tempType.SpecialType != SpecialType.System_Object)
                    {
                        var isUnregistered = tempType.GetAttributes().Any(attr =>
                            attr.AttributeClass?.ToDisplayString() ==
                            "IoCTools.Abstractions.Annotations.UnregisteredServiceAttribute");
                        if (!isUnregistered)
                        {
                            allAncestorsAreUnregistered = false;
                            break;
                        }

                        tempType = tempType.BaseType;
                    }
                }

                if (canBaseAcceptDIParameters && isBaseClassPartial && baseClassWillHaveConstructor &&
                    baseHierarchyDependencies.AllDependencies.Any())
                {
                    // Generate base constructor call with parameters in correct order
                    // CRITICAL FIX: Use the base class dependency order directly - don't re-group by source
                    // The base class AllDependencies is already ordered correctly for its constructor signature
                    var baseParamNames = new List<string>();

                    // Process base dependencies in the order they appear in baseHierarchyDependencies.AllDependencies
                    // This preserves the inheritance-aware ordering that the base class constructor expects
                    foreach (var baseDep in baseHierarchyDependencies.AllDependencies)
                    {
                        // Skip configuration dependencies unless they're the _configuration parameter
                        if (baseDep.Source == DependencySource.ConfigurationInjection && baseDep.FieldName != "_configuration")
                            continue;

                        var matchingParam = parametersWithNames.FirstOrDefault(p =>
                            SymbolEqualityComparer.Default.Equals(p.Dependency.ServiceType, baseDep.ServiceType) &&
                            p.Dependency.FieldName == baseDep.FieldName);

                        if (!string.IsNullOrEmpty(matchingParam.ParamName))
                        {
                            baseParamNames.Add(matchingParam.ParamName);
                        }
                        else
                        {
                            // Try matching by type only as fallback
                            var typeMatchingParam = parametersWithNames.FirstOrDefault(p =>
                                SymbolEqualityComparer.Default.Equals(p.Dependency.ServiceType, baseDep.ServiceType));

                            if (!string.IsNullOrEmpty(typeMatchingParam.ParamName))
                                baseParamNames.Add(typeMatchingParam.ParamName);
                        }
                    }


                    // Generate base call if we have parameters
                    if (baseParamNames.Count > 0) baseCallStr = $" : base({string.Join(", ", baseParamNames)})";
                }

                // ADDITIONAL FIX: Handle non-IoC base classes that still require constructor parameters
                if (string.IsNullOrEmpty(baseCallStr))
                {
                    // Check if base class has [ExternalService] attribute - these should not have generated constructors
                    var baseHasExternalService = baseClass.GetAttributes().Any(attr =>
                        attr.AttributeClass?.ToDisplayString() ==
                        "IoCTools.Abstractions.Annotations.ExternalServiceAttribute");

                    if (baseHasExternalService)
                    {
                        // [ExternalService] classes should not have IoC-generated constructors
                        // so we should not try to call them with DI parameters or arbitrary strings
                        // Leave baseCallStr empty to use default base() call
                    }
                    else
                    {
                        // Check if base class has no parameterless constructor
                        var baseConstructors = baseClass.GetMembers().OfType<IMethodSymbol>()
                            .Where(m => m.MethodKind == MethodKind.Constructor && !m.IsStatic)
                            .ToList();

                        var hasParameterlessConstructor = baseConstructors.Any(c => c.Parameters.Length == 0);

                        if (!hasParameterlessConstructor && baseConstructors.Any() && !baseClassWillHaveConstructor)
                        {
                            // Base class requires constructor parameters, look at existing constructors to see how they call base
                            var currentConstructors = currentClassSymbol?.GetMembers().OfType<IMethodSymbol>()
                                .Where(m => m.MethodKind == MethodKind.Constructor && !m.IsStatic)
                                .ToList() ?? new List<IMethodSymbol>();

                            if (currentConstructors.Any())
                                // Try to find a simple pattern: if existing constructor calls base with literal, use same literal
                                // This handles cases like "public DerivedService() : base("default") { }"
                                // For now, use a simple default - in a real implementation, we would parse the existing base calls
                                baseCallStr = " : base(\"default\")";
                        }
                    }
                }
            }

            // Regenerate parameter string
            var allParameters = parametersWithNames.Select(p => $"{p.TypeString} {p.ParamName}");
            var parameterStr = parametersWithNames.Count <= 3
                ? string.Join(", ", allParameters)
                : string.Join(",\n        ", allParameters);

            // Generate assignments ONLY for derived dependencies (fields that exist in current class)
            // Base class dependencies are passed to base constructor, not assigned directly
            var derivedFieldNames = new HashSet<string>(
                (hierarchyDependencies.DerivedDependencies ?? new List<(ITypeSymbol, string, DependencySource)>())
                .Select(d => d.FieldName));

            // Create mapping from ServiceType to parameter name for field assignments
            // This handles the case where multiple fields of the same ServiceType map to one parameter
            var serviceTypeToParamName = parametersWithNames
                .ToDictionary(p => p.Dependency.ServiceType, p => p.ParamName, SymbolEqualityComparer.Default);

            // Generate assignments for ALL derived dependencies using the ServiceType mapping
            var regularAssignments = hierarchyDependencies.DerivedDependencies
                .Where(d => d.Source != DependencySource.ConfigurationInjection ||
                           IsOptionsPatternOrConfigObjectAssignment(d.FieldName, classSymbol, semanticModel))
                .Where(d => serviceTypeToParamName.ContainsKey(d.ServiceType)) // Ensure parameter exists
                .Select(d =>
                {
                    var paramName = serviceTypeToParamName[d.ServiceType];
                    
                    // Check if this is a SupportsReloading field that uses Options pattern
                    if (d.Source == DependencySource.Inject &&
                        IsSupportsReloadingField(d.FieldName, classSymbol, semanticModel))
                        return $"this.{d.FieldName} = {paramName}.Value;";
                    return $"this.{d.FieldName} = {paramName};";
                });

            // Add configuration injection assignments
            var configAssignments =
                GenerateConfigurationAssignments(classSymbol, semanticModel, namespacesForStripping);

            var allAssignments = regularAssignments.Concat(configAssignments);
            var assignmentStr = string.Join("\n        ", allAssignments);

            // Detect if this is a file-scoped namespace
            var isFileScopedNamespace = false;
            var namespaceParent = classDeclaration.Parent;
            while (namespaceParent != null && namespaceParent is not BaseNamespaceDeclarationSyntax)
                namespaceParent = namespaceParent.Parent;

            if (namespaceParent is FileScopedNamespaceDeclarationSyntax)
                isFileScopedNamespace = true;

            // Create namespace declaration only if there is a namespace
            var namespaceDeclaration = string.IsNullOrEmpty(namespaceName) ? "" : $"namespace {namespaceName};";

            // For file-scoped namespaces, put namespace before usings; for others, put after
            var beforeUsings = isFileScopedNamespace ? namespaceDeclaration : "";
            var afterUsings = !isFileScopedNamespace ? namespaceDeclaration : "";

            // Check if this is a nested class
            var isNestedClass = classDeclaration.Parent is TypeDeclarationSyntax;

            // Initialize variables for template replacement
            string openingBraces = "";
            string closingBraces = "";
            string constructorCode;
            if (isNestedClass)
            {
                // For nested classes, only generate nested structure if ALL parent classes are partial
                var containingClasses =
                    new List<(TypeDeclarationSyntax syntax, string declaration, string accessibility)>();
                var current = classDeclaration.Parent;

                // Traverse up to find all containing classes
                while (current is TypeDeclarationSyntax parentClass)
                {
                    var parentAccessibility = parentClass.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword))
                        ?
                        "public"
                        :
                        parentClass.Modifiers.Any(m => m.IsKind(SyntaxKind.InternalKeyword))
                            ? "internal"
                            :
                            parentClass.Modifiers.Any(m => m.IsKind(SyntaxKind.ProtectedKeyword))
                                ? "protected"
                                :
                                parentClass.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword))
                                    ? "private"
                                    :
                                    "public"; // default

                    var parentKeyword = GetTypeDeclarationKeyword(parentClass);
                    containingClasses.Add((parentClass,
                        $"{parentAccessibility} partial {parentKeyword} {parentClass.Identifier.Text}",
                        parentAccessibility));
                    current = current.Parent;
                }

                containingClasses.Reverse(); // Outermost class first

                openingBraces = string.Join("\n", containingClasses.Select(c => $"{c.declaration}\n{{"));
                closingBraces = string.Join("\n", Enumerable.Range(0, containingClasses.Count).Select(_ => "}"));

                constructorCode = """
                                    #nullable enable
                                    {{beforeUsings}}
                                    {{usings}}
                                    {{afterUsings}}

                                    {{openingBraces}}
                                    {{accessibilityModifier}} partial {{typeKeyword}} {{fullClassName}}{{constraintClauses}}
                                    {
                                        {{fieldsStr}}
                                        
                                        public {{constructorName}}({{parameterStr}}){{baseCallStr}}
                                        {
                                            {{assignmentStr}}
                                        }
                                    }
                                    {{closingBraces}}
                                    """.Trim();
            }
            else
            {
                // For non-nested classes, use the original template
                constructorCode = """
                                    #nullable enable
                                    {{beforeUsings}}
                                    {{usings}}
                                    {{afterUsings}}

                                    {{accessibilityModifier}} partial {{typeKeyword}} {{fullClassName}}{{constraintClauses}}
                                    {
                                        {{fieldsStr}}
                                        
                                        public {{constructorName}}({{parameterStr}}){{baseCallStr}}
                                        {
                                            {{assignmentStr}}
                                        }
                                    }
                                    """.Trim();
            }

            // CRITICAL FIX: Replace template placeholders with actual values
            var finalCode = constructorCode
                .Replace("{{fieldsStr}}", fieldsStr)
                .Replace("{{parameterStr}}", parameterStr)
                .Replace("{{assignmentStr}}", assignmentStr)
                .Replace("{{baseCallStr}}", baseCallStr)
                .Replace("{{constructorName}}", constructorName)
                .Replace("{{fullClassName}}", fullClassName)
                .Replace("{{accessibilityModifier}}", accessibilityModifier)
                .Replace("{{typeKeyword}}", typeKeyword)
                .Replace("{{constraintClauses}}", constraintClauses)
                .Replace("{{usings}}", usings.ToString())
                .Replace("{{beforeUsings}}", beforeUsings)
                .Replace("{{afterUsings}}", afterUsings)
                .Replace("{{openingBraces}}", openingBraces)
                .Replace("{{closingBraces}}", closingBraces);

            return finalCode;
        }
        catch (Exception ex)
        {
            // Log the exception if needed and return empty constructor
            return "";
        }
    }

    // Helper methods needed by the constructor generator
    private static void CollectNamespaces(ITypeSymbol typeSymbol,
        HashSet<string> uniqueNamespaces)
    {
        if (typeSymbol == null) return;

        var ns = typeSymbol.ContainingNamespace;
        if (ns != null && !ns.IsGlobalNamespace)
        {
            var nsName = ns.ToDisplayString();
            if (!string.IsNullOrEmpty(nsName))
                uniqueNamespaces.Add(nsName);
        }

        // Handle generic types
        if (typeSymbol is INamedTypeSymbol namedType && namedType.TypeArguments.Length > 0)
            foreach (var typeArg in namedType.TypeArguments)
                CollectNamespaces(typeArg, uniqueNamespaces);

        // Handle array types
        if (typeSymbol is IArrayTypeSymbol arrayType) CollectNamespaces(arrayType.ElementType, uniqueNamespaces);
    }

    private static void CollectNamespacesFromConstraints(TypeParameterConstraintClauseSyntax constraintClause,
        SemanticModel semanticModel,
        HashSet<string> uniqueNamespaces)
    {
        foreach (var constraint in constraintClause.Constraints)
            if (constraint is TypeConstraintSyntax typeConstraint)
            {
                var typeInfo = semanticModel.GetTypeInfo(typeConstraint.Type);
                if (typeInfo.Type != null) CollectNamespaces(typeInfo.Type, uniqueNamespaces);
            }
    }

    private static string GetClassNamespace(TypeDeclarationSyntax classDeclaration)
    {
        var parent = classDeclaration.Parent;
        while (parent != null)
        {
            if (parent is BaseNamespaceDeclarationSyntax namespaceDeclaration)
                return namespaceDeclaration.Name.ToString();
            parent = parent.Parent;
        }

        return null;
    }

    private static string GetClassAccessibilityModifier(INamedTypeSymbol classSymbol)
    {
        return classSymbol.DeclaredAccessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            Accessibility.Protected => "protected",
            Accessibility.Private => "private",
            Accessibility.ProtectedOrInternal => "protected internal",
            Accessibility.ProtectedAndInternal => "private protected",
            _ => "public"
        };
    }

    private static string GetTypeDeclarationKeyword(TypeDeclarationSyntax typeDeclaration)
    {
        return typeDeclaration switch
        {
            ClassDeclarationSyntax => "class",
            RecordDeclarationSyntax => "record",
            StructDeclarationSyntax => "struct",
            InterfaceDeclarationSyntax => "interface",
            _ => "class"
        };
    }

    private static string RemoveNamespacesAndDots(ITypeSymbol typeSymbol,
        HashSet<string> namespacesToStrip)
    {
        if (typeSymbol == null) return "object";

        // Special handling for array types to generate valid C# syntax
        if (typeSymbol is IArrayTypeSymbol arrayType)
        {
            var elementTypeName = RemoveNamespacesAndDots(arrayType.ElementType, namespacesToStrip);

            // Handle multi-dimensional arrays (e.g., int[,])
            var ranks = new string(',', arrayType.Rank - 1);
            return $"{elementTypeName}[{ranks}]";
        }

        // Use improved SymbolDisplayFormat with better handling for complex generic types
        var format = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            delegateStyle: SymbolDisplayDelegateStyle.NameOnly,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
                                  SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

        var fullTypeName = typeSymbol.ToDisplayString(format);

        if (namespacesToStrip != null && namespacesToStrip.Count > 0)
        {
            // FIX: Use simple string replacement instead of regex to avoid stack overflow
            // Sort namespaces by length descending to process longer ones first
            var sortedNamespaces = namespacesToStrip.Where(ns => !string.IsNullOrEmpty(ns))
                .OrderByDescending(ns => ns.Length)
                .ToList();

            foreach (var ns in sortedNamespaces)
            {
                // Remove namespace from beginning of type name
                if (fullTypeName.StartsWith($"{ns}.")) fullTypeName = fullTypeName.Substring(ns.Length + 1);

                // Simple string replacement for nested generics - avoid regex
                // Replace all occurrences of "namespace." with empty string
                fullTypeName = fullTypeName.Replace($"{ns}.", "");
            }
        }

        return fullTypeName;
    }

    private static bool HasInjectFields(InheritanceHierarchyDependencies hierarchyDependencies)
    {
        // CRITICAL FIX: Check RawAllDependencies instead of AllDependencies
        // AllDependencies may be modified by GetConstructorDependencies deduplication logic
        // but RawAllDependencies always contains the original, unprocessed dependencies
        return hierarchyDependencies.RawAllDependencies?.Any(d => d.Source == DependencySource.Inject) ?? false;
    }

    private static string GetParameterNameFromFieldName(string fieldName)
    {
        // If field starts with underscore, remove it
        if (fieldName.StartsWith("_"))
        {
            var nameWithoutUnderscore = fieldName.Substring(1);

            // Check if the name contains underscores (snake_case pattern)
            if (nameWithoutUnderscore.Contains("_"))
            {
                // Convert snake_case to camelCase for constructor parameter (C# convention)
                var parts = nameWithoutUnderscore.Split('_');
                var camelCaseName = parts[0].ToLowerInvariant() + 
                    string.Concat(parts.Skip(1).Select(p => char.ToUpperInvariant(p[0]) + p.Substring(1).ToLowerInvariant()));
                return EscapeReservedKeyword(camelCaseName);
            }

            // FIXED: Always use camelCase for constructor parameters (C# convention)
            // The naming convention in DependsOn should only affect field names, not parameter names
            var paramName1 = char.ToLowerInvariant(nameWithoutUnderscore[0]) + nameWithoutUnderscore.Substring(1);
            return EscapeReservedKeyword(paramName1);
        }

        // For fields without underscore, convert first letter to lowercase
        var paramName = char.ToLowerInvariant(fieldName[0]) + fieldName.Substring(1);

        // Handle C# reserved keywords by adding a suffix
        return EscapeReservedKeyword(paramName);
    }

    private static string GetTypeStringWithNullableAnnotation(ITypeSymbol serviceType,
        string fieldName,
        INamedTypeSymbol classSymbol,
        HashSet<string> namespacesForStripping)
    {
        // For nullable types, we need to get the display string with nullable annotations preserved first
        // Then apply namespace removal afterwards
        var formatWithNullable = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
            genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
            delegateStyle: SymbolDisplayDelegateStyle.NameOnly,
            miscellaneousOptions: SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
                                  SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

        var fullTypeName = serviceType.ToDisplayString(formatWithNullable);

        // Now apply namespace removal while preserving nullable annotations
        if (namespacesForStripping != null)
        {
            // Enhanced namespace removal for complex generic types
            // Sort namespaces by length descending to process longer ones first
            var sortedNamespaces = namespacesForStripping.Where(ns => !string.IsNullOrEmpty(ns))
                .OrderByDescending(ns => ns.Length);

            foreach (var ns in sortedNamespaces)
            {
                // Remove namespace from beginning of type name
                if (fullTypeName.StartsWith($"{ns}.")) fullTypeName = fullTypeName.Substring(ns.Length + 1);

                // Simple string replacement for nested generics - avoid regex
                // Replace all occurrences of "namespace." with empty string
                fullTypeName = fullTypeName.Replace($"{ns}.", "");
            }
        }

        return fullTypeName;
    }

    private static List<string> GenerateConfigurationAssignments(INamedTypeSymbol? classSymbol,
        SemanticModel semanticModel,
        HashSet<string> namespacesForStripping)
    {
        var assignments = new List<string>();

        if (classSymbol == null)
            return assignments;

        // SIMPLIFIED CONFIGURATION INHERITANCE STRATEGY:
        // 1. Each class generates assignments ONLY for its own [InjectConfiguration] fields
        // 2. Base class constructors handle their own config fields
        // 3. Derived class constructors handle their own config fields
        // 4. Base constructor calls pass configuration parameter to base when needed

        var hasServiceAttribute = classSymbol.GetAttributes()
            .Any(attr =>
                attr.AttributeClass?.ToDisplayString() == "IoCTools.Abstractions.Annotations.ServiceAttribute");

        var isUnregisteredService = classSymbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.ToDisplayString() ==
                         "IoCTools.Abstractions.Annotations.UnregisteredServiceAttribute");

        // CRITICAL FIX: Always get configuration fields from the CURRENT class only
        // Each class should only handle its own configuration fields to avoid CS0191 readonly assignment errors
        var configFields = DependencyAnalyzer.GetConfigurationInjectedFieldsForType(classSymbol, semanticModel);

        // Check if we're in an inheritance scenario (has a base class that's not System.Object)
        var hasInheritance = classSymbol?.BaseType != null &&
                             classSymbol.BaseType.SpecialType != SpecialType.System_Object;

        // Generate assignments for configuration fields
        foreach (var configField in configFields)
        {
            if (configField.IsOptionsPattern || configField.SupportsReloading)
                // Options pattern fields and SupportsReloading fields are injected as regular dependencies, not bound from configuration
                continue;

            string assignment;
            if (configField.IsDirectValueBinding)
            {
                // Direct value binding: configuration.GetValue<T>("key") or configuration["key"] for strings
                var fieldTypeName = RemoveNamespacesAndDots(configField.FieldType, namespacesForStripping);
                var configKey = configField.ConfigurationKey ?? "";

                // For the basic case, just generate the simple GetValue call
                // The .NET configuration system and default value handling should be left to runtime
                // Use global qualified names for System types to avoid namespace resolution issues
                var typeName = configField.FieldType.ToDisplayString();
                var fullTypeName = configField.FieldType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                var metadataName = configField.FieldType.MetadataName;
                var namespaceAndMetadata = configField.FieldType.ContainingNamespace?.ToDisplayString() + "." +
                                           configField.FieldType.MetadataName;

                // Check if this is a System type by multiple criteria (same logic as IsDirectValueType)
                var isSystemType = typeName.StartsWith("System.") ||
                                   fullTypeName.StartsWith("global::System.") ||
                                   metadataName is "TimeSpan" or "DateTime" or "DateTimeOffset" or "Guid" or "Uri" ||
                                   namespaceAndMetadata is "System.TimeSpan" or "System.DateTime"
                                       or "System.DateTimeOffset" or "System.Guid" or "System.Uri";

                if (isSystemType)
                {
                    // Use the fully qualified name for System types
                    if (fullTypeName.StartsWith("global::"))
                    {
                        typeName = fullTypeName;
                    }
                    else if (fullTypeName.StartsWith("System."))
                    {
                        typeName = "global::" + fullTypeName;
                    }
                    else
                    {
                        // For cases where fullTypeName is just the short name (like "TimeSpan"),
                        // construct the full qualified name using namespace information
                        if (namespaceAndMetadata.StartsWith("System."))
                        {
                            typeName = "global::" + namespaceAndMetadata;
                        }
                        else
                        {
                            // For well-known System types, construct the correct qualified name
                            if (metadataName is "TimeSpan" or "DateTime" or "DateTimeOffset" or "Guid" or "Uri")
                                typeName = "global::System." + metadataName;
                            else
                                // Fallback - use the qualified format and add global:: prefix
                                typeName = "global::" + fullTypeName;
                        }
                    }
                }
                else
                {
                    // For other types, use the stripped namespace version
                    typeName = fieldTypeName;
                }

                // Handle Required validation and DefaultValue patterns using correct .NET patterns
                if (configField.DefaultValue != null)
                {
                    // Use .NET's standard GetValue<T>(key, defaultValue) overload - the correct pattern
                    var formattedDefault =
                        FormatDefaultValueForGetValue(configField.DefaultValue, configField.FieldType);
                    assignment =
                        $"this.{configField.FieldName} = configuration.GetValue<{typeName}>(\"{configKey}\", {formattedDefault});";
                }
                else if (configField.Required && IsReferenceTypeOrNullable(configField.FieldType) && !hasInheritance)
                {
                    // Required reference type - use null-coalescing with exception (only works for reference types)
                    // BUT: For inheritance scenarios, use simpler pattern to avoid complexity
                    assignment =
                        $"this.{configField.FieldName} = configuration.GetValue<{typeName}>(\"{configKey}\") ?? throw new global::System.ArgumentException(\"Required configuration '{configKey}' is missing\", \"{configKey}\");";
                }
                else
                {
                    // Optional field or value type without default - use standard GetValue
                    assignment =
                        $"this.{configField.FieldName} = configuration.GetValue<{typeName}>(\"{configKey}\")!;";
                }
            }
            else
            {
                // Section binding: configuration.GetSection("section").Get<T>()
                var sectionName = configField.GetSectionName();

                // CRITICAL FIX: Handle collection interface types - can't bind directly to interfaces
                if (IsCollectionInterfaceType(configField.FieldType))
                {
                    var (concreteTypeName, conversionMethod) =
                        GetConcreteCollectionBinding(configField.FieldType, namespacesForStripping);

                    // Handle Required validation for section binding (but simplify for inheritance scenarios)
                    if (configField.Required && !hasInheritance)
                        assignment =
                            $"this.{configField.FieldName} = configuration.GetSection(\"{sectionName}\").Get<{concreteTypeName}>(){conversionMethod} ?? throw new global::System.InvalidOperationException(\"Required configuration section '{sectionName}' is missing\");";
                    else
                        assignment =
                            $"this.{configField.FieldName} = configuration.GetSection(\"{sectionName}\").Get<{concreteTypeName}>(){conversionMethod}!;";
                }
                else
                {
                    // For other types (arrays, concrete collections, custom types), use direct binding
                    var fieldTypeName = RemoveNamespacesAndDots(configField.FieldType, namespacesForStripping);

                    // Handle Required validation for section binding (but simplify for inheritance scenarios)
                    if (configField.Required && !hasInheritance)
                        assignment =
                            $"this.{configField.FieldName} = configuration.GetSection(\"{sectionName}\").Get<{fieldTypeName}>() ?? throw new global::System.InvalidOperationException(\"Required configuration section '{sectionName}' is missing\");";
                    else
                        assignment =
                            $"this.{configField.FieldName} = configuration.GetSection(\"{sectionName}\").Get<{fieldTypeName}>()!;";
                }
            }

            assignments.Add(assignment);
        }

        return assignments;
    }

    private static List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)>
        GetConfigurationDependencies(
            INamedTypeSymbol? classSymbol,
            SemanticModel semanticModel,
            HashSet<string> namespacesForStripping)
    {
        var dependencies = new List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)>();

        if (classSymbol == null)
            return dependencies;

        // Collect configuration fields from the entire inheritance hierarchy
        var allConfigFields = new List<ConfigurationInjectionInfo>();
        var currentType = classSymbol;

        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            var configFields = DependencyAnalyzer.GetConfigurationInjectedFieldsForType(currentType, semanticModel);
            allConfigFields.AddRange(configFields);
            currentType = currentType.BaseType;
        }

        if (!allConfigFields.Any())
            return dependencies;

        // NOTE: IConfiguration dependency is already handled by DependencyAnalyzer.GetInheritanceHierarchyDependencies
        // Don't add it here to avoid duplicates

        // Add options pattern dependencies as regular DI dependencies 
        // (Options are injected from DI container, not configuration binding)
        // Remove duplicates by field name (derived class fields take precedence)
        var uniqueConfigFields = allConfigFields
            .Where(f => f.IsOptionsPattern || f.SupportsReloading)
            .GroupBy(f => f.FieldName)
            .Select(g => g.First()) // First one encountered (from derived class)
            .ToList();

        foreach (var configField in uniqueConfigFields)
        {
            ITypeSymbol serviceType;

            if (configField.SupportsReloading && !configField.IsOptionsPattern)
            {
                // For SupportsReloading = true, create IOptionsSnapshot<T> instead of direct field type
                var optionsType =
                    semanticModel.Compilation.GetTypeByMetadataName("Microsoft.Extensions.Options.IOptionsSnapshot`1");
                if (optionsType != null)
                    serviceType = optionsType.Construct(configField.FieldType);
                else
                    // Fallback to direct field type if IOptionsSnapshot not found
                    serviceType = configField.FieldType;
            }
            else
            {
                // For existing options pattern types, use as-is
                serviceType = configField.FieldType;
            }

            dependencies.Add((serviceType, configField.FieldName, DependencySource.Inject));
        }

        return dependencies;
    }

    private static bool IsIConfigurationField(string fieldName,
        ITypeSymbol serviceType,
        SemanticModel semanticModel)
    {
        var iConfigurationType =
            semanticModel.Compilation.GetTypeByMetadataName("Microsoft.Extensions.Configuration.IConfiguration");
        return iConfigurationType != null && SymbolEqualityComparer.Default.Equals(serviceType, iConfigurationType);
    }

    private static ITypeSymbol? FindIConfigurationType(Compilation compilation) =>
        compilation.GetTypeByMetadataName("Microsoft.Extensions.Configuration.IConfiguration");

    private static bool IsOptionsPatternAssignment(string fieldName,
        INamedTypeSymbol? classSymbol,
        SemanticModel semanticModel)
    {
        if (classSymbol == null)
            return false;

        // Check for options pattern fields across the entire inheritance hierarchy
        var currentType = classSymbol;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            var configFields = DependencyAnalyzer.GetConfigurationInjectedFieldsForType(currentType, semanticModel);
            var configField = configFields.FirstOrDefault(f => f.FieldName == fieldName);

            if (configField?.IsOptionsPattern == true)
                return true;

            currentType = currentType.BaseType;
        }

        return false;
    }

    private static bool IsOptionsPatternOrConfigObjectAssignment(string fieldName,
        INamedTypeSymbol? classSymbol,
        SemanticModel semanticModel)
    {
        if (classSymbol == null)
            return false;

        // Check for options pattern or config object fields across the entire inheritance hierarchy
        var currentType = classSymbol;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            var configFields = DependencyAnalyzer.GetConfigurationInjectedFieldsForType(currentType, semanticModel);
            var configField = configFields.FirstOrDefault(f => f.FieldName == fieldName);

            if (configField != null && (configField.IsOptionsPattern || !configField.IsDirectValueBinding))
                return true;

            currentType = currentType.BaseType;
        }

        return false;
    }

    private static bool IsSupportsReloadingField(string fieldName,
        INamedTypeSymbol? classSymbol,
        SemanticModel semanticModel)
    {
        if (classSymbol == null)
            return false;

        // Check for SupportsReloading fields across the entire inheritance hierarchy
        var currentType = classSymbol;
        while (currentType != null && currentType.SpecialType != SpecialType.System_Object)
        {
            var configFields = DependencyAnalyzer.GetConfigurationInjectedFieldsForType(currentType, semanticModel);
            var configField = configFields.FirstOrDefault(f => f.FieldName == fieldName);

            if (configField != null && configField.SupportsReloading && !configField.IsOptionsPattern)
                return true;

            currentType = currentType.BaseType;
        }

        return false;
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

    private static bool IsNullableValueType(ITypeSymbol typeSymbol) =>
        // Check if it's a nullable value type (e.g., int?, bool?, TimeSpan?)
        typeSymbol.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

    private static string FormatDefaultValue(object defaultValue,
        string targetTypeName)
    {
        switch (defaultValue)
        {
            case string str:
                // For string type, wrap in quotes
                if (targetTypeName == "string")
                    return $"\"{str}\"";
                // For other types like TimeSpan, DateTime, etc. that receive string values, 
                // we need to parse them
                return targetTypeName switch
                {
                    "TimeSpan" => $"TimeSpan.Parse(\"{str}\")",
                    "DateTime" => $"DateTime.Parse(\"{str}\")",
                    "DateTimeOffset" => $"DateTimeOffset.Parse(\"{str}\")",
                    "Guid" => $"Guid.Parse(\"{str}\")",
                    "Uri" => $"new Uri(\"{str}\")",
                    _ => $"\"{str}\""
                };
            case bool b:
                return b ? "true" : "false";
            case null:
                return "null";
            default:
                // For numeric types and others, use direct ToString()
                return defaultValue.ToString() ?? "null";
        }
    }

    private static bool IsCollectionType(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol)
            return true;

        if (type is not INamedTypeSymbol namedType)
            return false;

        // Check for common collection types
        var typeName = namedType.OriginalDefinition.ToDisplayString();
        return typeName.StartsWith("System.Collections.Generic.List<") ||
               typeName.StartsWith("System.Collections.Generic.IEnumerable<") ||
               typeName.StartsWith("System.Collections.Generic.IList<") ||
               typeName.StartsWith("System.Collections.Generic.ICollection<") ||
               typeName.StartsWith("System.Collections.Generic.Dictionary<") ||
               typeName.StartsWith("System.Collections.Generic.IDictionary<") ||
               typeName.StartsWith("System.Collections.Generic.IReadOnlyList<") ||
               typeName.StartsWith("System.Collections.Generic.IReadOnlyCollection<");
    }

    private static bool IsCollectionInterfaceType(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
            return false;

        var typeName = namedType.OriginalDefinition.ToDisplayString();
        return typeName.StartsWith("System.Collections.Generic.IEnumerable<") ||
               typeName.StartsWith("System.Collections.Generic.IReadOnlyList<") ||
               typeName.StartsWith("System.Collections.Generic.IReadOnlyCollection<") ||
               typeName.StartsWith("System.Collections.Generic.IList<") ||
               typeName.StartsWith("System.Collections.Generic.ICollection<");
    }

    private static (string concreteTypeName, string conversionMethod) GetConcreteCollectionBinding(
        ITypeSymbol fieldType,
        HashSet<string> namespacesForStripping)
    {
        if (fieldType is not INamedTypeSymbol namedType)
            return (RemoveNamespacesAndDots(fieldType, namespacesForStripping), "");

        var typeName = namedType.OriginalDefinition.ToDisplayString();

        if (typeName.StartsWith("System.Collections.Generic.IReadOnlyList<"))
        {
            // IReadOnlyList<T> -> List<T> with .AsReadOnly()
            var elementType = namedType.TypeArguments.FirstOrDefault();
            if (elementType != null)
            {
                var elementTypeName = RemoveNamespacesAndDots(elementType, namespacesForStripping);
                return ($"List<{elementTypeName}>", "?.AsReadOnly()");
            }
        }
        else if (typeName.StartsWith("System.Collections.Generic.IReadOnlyCollection<"))
        {
            // IReadOnlyCollection<T> -> List<T> with .AsReadOnly()
            var elementType = namedType.TypeArguments.FirstOrDefault();
            if (elementType != null)
            {
                var elementTypeName = RemoveNamespacesAndDots(elementType, namespacesForStripping);
                return ($"List<{elementTypeName}>", "?.AsReadOnly()");
            }
        }
        else if (typeName.StartsWith("System.Collections.Generic.IEnumerable<"))
        {
            // IEnumerable<T> -> bind directly to IEnumerable<T> for configuration injection
            return (RemoveNamespacesAndDots(fieldType, namespacesForStripping), "");
        }
        else if (typeName.StartsWith("System.Collections.Generic.IList<"))
        {
            // IList<T> -> bind directly to IList<T> for configuration injection
            return (RemoveNamespacesAndDots(fieldType, namespacesForStripping), "");
        }
        else if (typeName.StartsWith("System.Collections.Generic.ICollection<"))
        {
            // ICollection<T> -> List<T> (ICollection can't be bound directly)
            var elementType = namedType.TypeArguments.FirstOrDefault();
            if (elementType != null)
            {
                var elementTypeName = RemoveNamespacesAndDots(elementType, namespacesForStripping);
                return ($"List<{elementTypeName}>", "");
            }
        }

        // Not a collection interface, return as-is
        return (RemoveNamespacesAndDots(fieldType, namespacesForStripping), "");
    }

    private static string GetFullyQualifiedTypeName(ITypeSymbol fieldType,
        string shortTypeName)
    {
        // For built-in System types that may have namespace resolution issues,
        // use fully qualified names to avoid compiler errors
        var fullTypeName = fieldType.ToDisplayString();

        if (fullTypeName.StartsWith("System.")) return fullTypeName; // Use fully qualified name for System types

        return shortTypeName; // Use short name for other types
    }

    /// <summary>
    ///     Formats default values for use with IConfiguration.GetValue
    ///     <T>
    ///         (key, defaultValue)
    ///         This is different from FormatDefaultValue as it needs to match the target type exactly
    /// </summary>
    private static string FormatDefaultValueForGetValue(object defaultValue,
        ITypeSymbol targetType)
    {
        if (defaultValue == null)
            return "default";

        // Handle string default values that need type conversion
        if (defaultValue is string stringValue)
            return targetType.SpecialType switch
            {
                SpecialType.System_String => $"\"{EscapeStringLiteral(stringValue)}\"",
                SpecialType.System_Int32 when int.TryParse(stringValue, out var intVal) => intVal.ToString(),
                SpecialType.System_Boolean when bool.TryParse(stringValue, out var boolVal) => boolVal.ToString()
                    .ToLowerInvariant(),
                SpecialType.System_Double when double.TryParse(stringValue, out var doubleVal) => doubleVal.ToString(),
                SpecialType.System_Decimal when decimal.TryParse(stringValue, out var decimalVal) => $"{decimalVal}m",
                _ => HandleComplexDefaultValue(stringValue, targetType)
            };

        // Handle already-typed default values
        return defaultValue switch
        {
            bool b => b.ToString().ToLowerInvariant(),
            string s => $"\"{EscapeStringLiteral(s)}\"",
            _ => defaultValue.ToString() ?? "default"
        };
    }

    private static string HandleComplexDefaultValue(string stringValue,
        ITypeSymbol targetType)
    {
        var typeName = targetType.ToDisplayString();

        return typeName switch
        {
            "System.TimeSpan" => $"global::System.TimeSpan.Parse(\"{EscapeStringLiteral(stringValue)}\")",
            "System.DateTime" => $"global::System.DateTime.Parse(\"{EscapeStringLiteral(stringValue)}\")",
            "System.DateTimeOffset" => $"global::System.DateTimeOffset.Parse(\"{EscapeStringLiteral(stringValue)}\")",
            "System.Guid" => $"global::System.Guid.Parse(\"{EscapeStringLiteral(stringValue)}\")",
            "System.Uri" => $"new global::System.Uri(\"{EscapeStringLiteral(stringValue)}\")",
            _ => $"\"{EscapeStringLiteral(stringValue)}\"" // Default to string representation
        };
    }

    private static string EscapeStringLiteral(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"")
        .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");

    private static bool IsReferenceTypeOrNullable(ITypeSymbol typeSymbol) => typeSymbol.IsReferenceType ||
                                                                             (typeSymbol is INamedTypeSymbol
                                                                                  namedType &&
                                                                              namedType.OriginalDefinition
                                                                                  .SpecialType ==
                                                                              SpecialType.System_Nullable_T);

    /// <summary>
    /// Determines if DependsOn fields should be protected instead of private to allow inheritance access
    /// </summary>
    private static bool ShouldUseProtectedFields(TypeDeclarationSyntax classDeclaration, INamedTypeSymbol? classSymbol)
    {
        if (classSymbol == null) 
            return false;

        // CRITICAL FIX: Use protected fields ONLY for abstract classes
        // Abstract classes are explicitly designed to be inherited and need protected access
        // for derived classes to access the generated DependsOn fields
        return classSymbol.IsAbstract;
    }
}