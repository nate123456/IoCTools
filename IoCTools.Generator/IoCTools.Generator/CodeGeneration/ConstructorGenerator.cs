namespace IoCTools.Generator.CodeGeneration;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Analysis;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Models;

using Utilities;

internal static partial class ConstructorGenerator
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
                classSymbol = null;
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
                    if (configFields.Any(f => CollectionUtilities.IsCollectionType(f.FieldType)))
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

                foreach (var field in allFields)
                    existingFieldNames.Add(field.Name);
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

            // DEBUG LOG: Verify we have DependsOn fields to generate (removed unused variables to fix CS0219)
            // var dependsOnCount = allLevelZeroDependencies.Count(f => f.Source == DependencySource.DependsOn);
            // var filteredDependsOnCount = fieldsToGenerate.Count(f => f.Source == DependencySource.DependsOn);

            // CRITICAL FIX: If all DependsOn fields were filtered out but we should have some, this indicates a bug
            var dependsOnCount = allLevelZeroDependencies.Count(f => f.Source == DependencySource.DependsOn);
            var filteredDependsOnCount = fieldsToGenerate.Count(f => f.Source == DependencySource.DependsOn);
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
            var finalFieldsToGenerate =
                new List<(ITypeSymbol ServiceType, string FieldName, DependencySource Source)>();
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
                    var otherFields = fieldsForType.Where(f =>
                        f.Source != DependencySource.Inject && f.Source != DependencySource.DependsOn);
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
                    var otherDeps = dependenciesForType.Where(d =>
                        d.Source != DependencySource.Inject && d.Source != DependencySource.DependsOn);
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

                var typeString = GetTypeStringWithNullableAnnotation(f.ServiceType, f.FieldName, classSymbol,
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
                var baseIsHostedService = TypeAnalyzer.IsAssignableFromIHostedService(baseClass);

                // Check if base class will have constructor using intelligent inference
                var baseHasInjectFields = baseClass.GetMembers().OfType<IFieldSymbol>()
                    .Any(field => field.GetAttributes().Any(attr => attr.AttributeClass?.Name == "InjectAttribute"));
                var baseHasInjectConfigurationFields = baseClass.GetMembers().OfType<IFieldSymbol>()
                    .Any(field =>
                        field.GetAttributes().Any(attr => attr.AttributeClass?.Name == "InjectConfigurationAttribute"));
                var baseHasDependsOnAttribute = baseClass.GetAttributes()
                    .Any(attr => attr.AttributeClass?.Name?.StartsWith("DependsOn") == true);
                var baseHasConditionalServiceAttribute = baseClass.GetAttributes().Any(attr =>
                    attr.AttributeClass?.ToDisplayString() ==
                    "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute");
                var baseHasRegisterAsAllAttribute = baseClass.GetAttributes()
                    .Any(attr => attr.AttributeClass?.Name == "RegisterAsAllAttribute");
                var baseHasRegisterAsAttribute = baseClass.GetAttributes().Any(attr =>
                    attr.AttributeClass?.Name?.StartsWith("RegisterAsAttribute") == true &&
                    attr.AttributeClass?.IsGenericType == true);

                var baseClassWillHaveConstructor =
                    baseHasInjectFields || baseHasInjectConfigurationFields || baseHasDependsOnAttribute ||
                    baseHasConditionalServiceAttribute || baseHasRegisterAsAllAttribute || baseHasRegisterAsAttribute ||
                    baseIsHostedService;

                // REMOVED: ManualService ancestor logic - using intelligent inference now

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
                        if (baseDep.Source == DependencySource.ConfigurationInjection &&
                            baseDep.FieldName != "_configuration")
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
            // Skip primitive SupportsReloading fields - they're handled in config assignments section
            var regularAssignments = hierarchyDependencies.DerivedDependencies
                .Where(d => d.Source != DependencySource.ConfigurationInjection ||
                            IsOptionsPatternOrConfigObjectAssignment(d.FieldName, classSymbol, semanticModel))
                .Where(d => !IsPrimitiveSupportsReloadingField(d.FieldName, classSymbol, semanticModel))
                .Where(d => serviceTypeToParamName.ContainsKey(d.ServiceType)) // Ensure parameter exists
                .Select(d =>
                {
                    var paramName = serviceTypeToParamName[d.ServiceType];

                    // Check if this is a SupportsReloading field that uses Options pattern (complex objects only)
                    if (d.Source == DependencySource.Inject &&
                        IsSupportsReloadingFieldWithOptionsPattern(d.FieldName, classSymbol, semanticModel))
                        return $"this.{d.FieldName} = {paramName}.Value;";
                    return $"this.{d.FieldName} = {paramName};";
                });

            // Add configuration injection assignments
            // Find IConfiguration parameter name
            var iConfigurationType =
                semanticModel.Compilation.GetTypeByMetadataName("Microsoft.Extensions.Configuration.IConfiguration");
            var configurationParameterName = "configuration"; // default fallback
            if (iConfigurationType != null)
            {
                var configParam = parametersWithNames.FirstOrDefault(p =>
                    SymbolEqualityComparer.Default.Equals(p.Dependency.ServiceType, iConfigurationType));
                if (!string.IsNullOrEmpty(configParam.ParamName))
                    configurationParameterName = configParam.ParamName;
            }

            var configAssignments =
                GenerateConfigurationAssignments(classSymbol, semanticModel, namespacesForStripping,
                    configurationParameterName);

            var allAssignments = regularAssignments.Concat(configAssignments);
            var assignmentStr = string.Join("\n        ", allAssignments);

            // Detect if this is a file-scoped namespace
            var isFileScopedNamespace = false;
            var namespaceParent = classDeclaration.Parent;
            while (namespaceParent != null && namespaceParent is not BaseNamespaceDeclarationSyntax)
                namespaceParent = namespaceParent.Parent;

            if (namespaceParent is FileScopedNamespaceDeclarationSyntax)
                isFileScopedNamespace = true;

            var namespaceDeclaration = string.IsNullOrEmpty(namespaceName) ? "" : $"namespace {namespaceName};";
            var beforeUsings = isFileScopedNamespace ? namespaceDeclaration : "";
            var afterUsings = !isFileScopedNamespace ? namespaceDeclaration : "";

            var isNestedClass = classDeclaration.Parent is TypeDeclarationSyntax;

            var openingBraces = "";
            var closingBraces = "";
            if (isNestedClass)
            {
                var containingClasses =
                    new List<(TypeDeclarationSyntax syntax, string declaration, string accessibility)>();
                var current = classDeclaration.Parent;
                while (current is TypeDeclarationSyntax parentClass)
                {
                    var parentAccessibility = parentClass.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword))
                        ? "public"
                        : parentClass.Modifiers.Any(m => m.IsKind(SyntaxKind.InternalKeyword))
                            ? "internal"
                            : parentClass.Modifiers.Any(m => m.IsKind(SyntaxKind.ProtectedKeyword))
                                ? "protected"
                                : parentClass.Modifiers.Any(m => m.IsKind(SyntaxKind.PrivateKeyword))
                                    ? "private"
                                    : "public";

                    var parentKeyword = GetTypeDeclarationKeyword(parentClass);
                    containingClasses.Add((parentClass,
                        $"{parentAccessibility} partial {parentKeyword} {parentClass.Identifier.Text}",
                        parentAccessibility));
                    current = current.Parent;
                }

                containingClasses.Reverse();
                openingBraces = string.Join("\n", containingClasses.Select(c => $"{c.declaration}\n{{"));
                closingBraces = string.Join("\n", Enumerable.Range(0, containingClasses.Count).Select(_ => "}"));
            }

            return RenderConstructorSource(
                beforeUsings,
                usings.ToString(),
                afterUsings,
                isNestedClass,
                openingBraces,
                closingBraces,
                accessibilityModifier,
                typeKeyword,
                fullClassName,
                constraintClauses,
                fieldsStr,
                parameterStr,
                baseCallStr,
                constructorName,
                assignmentStr);
        }
        catch (Exception)
        {
            // Log the exception if needed and return empty constructor
            return "";
        }
    }

    // Helper methods needed by the constructor generator


    private static bool HasInjectFields(InheritanceHierarchyDependencies hierarchyDependencies)
    {
        // CRITICAL FIX: Check RawAllDependencies instead of AllDependencies
        // AllDependencies may be modified by GetConstructorDependencies deduplication logic
        // but RawAllDependencies always contains the original, unprocessed dependencies
        return hierarchyDependencies.RawAllDependencies?.Any(d => d.Source == DependencySource.Inject) ?? false;
    }


    /// <summary>
    ///     Determines if DependsOn fields should be protected instead of private to allow inheritance access
    /// </summary>
    private static bool ShouldUseProtectedFields(TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol? classSymbol)
    {
        if (classSymbol == null)
            return false;

        // CRITICAL FIX: Use protected fields ONLY for abstract classes
        // Abstract classes are explicitly designed to be inherited and need protected access
        // for derived classes to access the generated DependsOn fields
        return classSymbol.IsAbstract;
    }
}
