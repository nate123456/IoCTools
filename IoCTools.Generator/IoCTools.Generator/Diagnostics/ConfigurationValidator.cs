namespace IoCTools.Generator.Diagnostics;

using System.Collections.Generic;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Models;

internal static class ConfigurationValidator
{
    // Types that can be bound from configuration without issues
    private static readonly HashSet<string> SupportedPrimitiveTypes = new()
    {
        "System.String",
        "string",
        "System.Int32",
        "int",
        "System.Int64",
        "long",
        "System.Int16",
        "short",
        "System.Byte",
        "byte",
        "System.Boolean",
        "bool",
        "System.Double",
        "double",
        "System.Single",
        "float",
        "System.Decimal",
        "decimal",
        "System.DateTime",
        "System.DateTimeOffset",
        "System.TimeSpan",
        "System.Guid",
        "System.Uri"
    };

    // Collection types that support configuration binding
    private static readonly HashSet<string> SupportedCollectionTypes = new()
    {
        "System.Collections.Generic.List<>",
        "System.Collections.Generic.IList<>",
        "System.Collections.Generic.ICollection<>",
        "System.Collections.Generic.IEnumerable<>",
        "System.Collections.Generic.IReadOnlyList<>",
        "System.Collections.Generic.IReadOnlyCollection<>",
        "System.Collections.Generic.Dictionary<,>",
        "System.Collections.Generic.IDictionary<,>"
    };

    // Options pattern types
    private static readonly HashSet<string> OptionsPatternTypes = new()
    {
        "Microsoft.Extensions.Options.IOptions<>",
        "Microsoft.Extensions.Options.IOptionsSnapshot<>",
        "Microsoft.Extensions.Options.IOptionsMonitor<>"
    };

    /// <summary>
    ///     Validates configuration injection usage on a class
    /// </summary>
    public static void ValidateConfigurationInjection(
        GeneratorExecutionContext context,
        TypeDeclarationSyntax classDeclaration,
        DiagnosticConfiguration diagnosticConfig,
        SemanticModel semanticModel)
    {
        var symbol = ModelExtensions.GetDeclaredSymbol(semanticModel, classDeclaration);
        if (symbol is not INamedTypeSymbol classSymbol) return;

        // Skip all validation if diagnostics are disabled
        if (!diagnosticConfig.DiagnosticsEnabled) return;

        // Check if class has [ExternalService] attribute (skip validation)
        var classHasExternalServiceAttribute = classSymbol.GetAttributes()
            .Any(attr => attr.AttributeClass?.ToDisplayString() ==
                         "IoCTools.Abstractions.Annotations.ExternalServiceAttribute");

        if (classHasExternalServiceAttribute) return;

        // Get all fields with [InjectConfiguration] attribute
        var configurationFields = GetConfigurationFields(classSymbol, semanticModel);

        if (!configurationFields.Any()) return;

        // Validate that class is partial if it has configuration fields
        ValidateClassIsPartial(context, classDeclaration, classSymbol, diagnosticConfig);

        // Validate each configuration field
        foreach (var (fieldSymbol, fieldDeclaration) in configurationFields)
            ValidateConfigurationField(context, fieldSymbol, fieldDeclaration, classSymbol, diagnosticConfig);
    }

    /// <summary>
    ///     Gets all fields with [InjectConfiguration] attribute from the class hierarchy
    /// </summary>
    private static List<(IFieldSymbol fieldSymbol, FieldDeclarationSyntax fieldDeclaration)> GetConfigurationFields(
        INamedTypeSymbol classSymbol,
        SemanticModel semanticModel)
    {
        var result = new List<(IFieldSymbol, FieldDeclarationSyntax)>();
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

                if (hasInjectConfigurationAttribute)
                {
                    // Find the corresponding syntax node
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

    /// <summary>
    ///     Validates that a class using [InjectConfiguration] is marked as partial
    /// </summary>
    private static void ValidateClassIsPartial(
        GeneratorExecutionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol,
        DiagnosticConfiguration diagnosticConfig)
    {
        var isPartial = classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));

        if (!isPartial)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.ConfigurationOnNonPartialClass,
                classDeclaration.Identifier.GetLocation(),
                classSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    /// <summary>
    ///     Validates a specific configuration field
    /// </summary>
    private static void ValidateConfigurationField(
        GeneratorExecutionContext context,
        IFieldSymbol fieldSymbol,
        FieldDeclarationSyntax fieldDeclaration,
        INamedTypeSymbol classSymbol,
        DiagnosticConfiguration diagnosticConfig)
    {
        // IOC019: Check if field is static
        if (fieldSymbol.IsStatic)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.ConfigurationOnStaticField,
                fieldDeclaration.GetLocation(),
                fieldSymbol.Name,
                classSymbol.Name);
            context.ReportDiagnostic(diagnostic);
            return; // Skip further validation for static fields
        }

        // Get the InjectConfiguration attribute
        var injectConfigAttribute = fieldSymbol.GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() ==
                                    "IoCTools.Abstractions.Annotations.InjectConfigurationAttribute");

        if (injectConfigAttribute == null) return;

        // IOC016: Validate configuration key
        ValidateConfigurationKey(context, fieldSymbol, fieldDeclaration, injectConfigAttribute);

        // IOC017: Validate field type is supported for configuration binding
        ValidateConfigurationType(context, fieldSymbol, fieldDeclaration);
    }

    /// <summary>
    ///     Validates the configuration key provided in the attribute
    /// </summary>
    private static void ValidateConfigurationKey(
        GeneratorExecutionContext context,
        IFieldSymbol fieldSymbol,
        FieldDeclarationSyntax fieldDeclaration,
        AttributeData injectConfigAttribute)
    {
        // Get the configuration key from the attribute
        string? configurationKey = null;
        if (injectConfigAttribute.ConstructorArguments.Length > 0)
        {
            var keyArgument = injectConfigAttribute.ConstructorArguments[0];
            if (keyArgument.Value is string key) configurationKey = key;
        }

        // If no explicit key provided, it will be inferred from type name - that's valid
        if (configurationKey == null) return;

        // Validate the configuration key
        var validationResult = ValidateConfigurationKeyFormat(configurationKey);
        if (!validationResult.IsValid)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.InvalidConfigurationKey,
                fieldDeclaration.GetLocation(),
                configurationKey,
                validationResult.ErrorMessage);
            context.ReportDiagnostic(diagnostic);
        }
    }

    /// <summary>
    ///     Validates that the configuration key format is correct
    /// </summary>
    private static (bool IsValid, string ErrorMessage) ValidateConfigurationKeyFormat(string configurationKey)
    {
        // Check for null, empty, or whitespace-only keys
        if (string.IsNullOrWhiteSpace(configurationKey))
            return (false, "Configuration key cannot be empty or whitespace-only");

        // Check for double colons (invalid in configuration keys)
        if (configurationKey.Contains("::")) return (false, "Configuration key cannot contain double colons ('::')");

        // Check for leading/trailing colons
        if (configurationKey.StartsWith(":") || configurationKey.EndsWith(":"))
            return (false, "Configuration key cannot start or end with a colon");

        // Check for invalid characters that would cause issues
        var invalidChars = new[] { '\0', '\r', '\n', '\t' };
        if (configurationKey.Any(c => invalidChars.Contains(c)))
            return (false, "Configuration key contains invalid characters");

        return (true, string.Empty);
    }

    /// <summary>
    ///     Validates that the field type can be bound from configuration
    /// </summary>
    private static void ValidateConfigurationType(
        GeneratorExecutionContext context,
        IFieldSymbol fieldSymbol,
        FieldDeclarationSyntax fieldDeclaration)
    {
        var fieldType = fieldSymbol.Type;
        var fieldTypeName = fieldType.ToDisplayString();

        var validationResult = ValidateTypeForConfiguration(fieldType);
        if (!validationResult.IsValid)
        {
            var diagnostic = Diagnostic.Create(
                DiagnosticDescriptors.UnsupportedConfigurationType,
                fieldDeclaration.Declaration.Type.GetLocation(),
                fieldTypeName,
                validationResult.ErrorMessage);
            context.ReportDiagnostic(diagnostic);
        }
    }

    /// <summary>
    ///     Validates that a type can be bound from configuration
    /// </summary>
    private static (bool IsValid, string ErrorMessage) ValidateTypeForConfiguration(ITypeSymbol type)
    {
        var typeName = type.ToDisplayString();
        var originalTypeName = type.OriginalDefinition.ToDisplayString();

        // Handle all named types that could be generic (both constructed and unbound generics)
        if (type is INamedTypeSymbol namedType)
        {
            var originalDef = namedType.OriginalDefinition.ToDisplayString();

            // Handle Nullable<T> - check both constructed and generic definition
            if (originalDef == "System.Nullable<>" || (namedType.IsGenericType &&
                                                       namedType.OriginalDefinition.SpecialType ==
                                                       SpecialType.System_Nullable_T))
                if (namedType.TypeArguments.Length > 0)
                {
                    var underlyingType = namedType.TypeArguments.First();
                    return ValidateTypeForConfiguration(underlyingType);
                }

            // Handle Options pattern types - these are always valid
            if (OptionsPatternTypes.Contains(originalDef)) return (true, string.Empty);

            // Handle collection types (including collection interfaces like IList<T>)
            if (SupportedCollectionTypes.Contains(originalDef))
            {
                // For collections, validate the element type(s)
                if (namedType.TypeArguments.Length > 0)
                    foreach (var typeArg in namedType.TypeArguments)
                    {
                        var elementValidation = ValidateTypeForConfiguration(typeArg);
                        if (!elementValidation.IsValid)
                            return (false,
                                $"Collection element type is not supported: {elementValidation.ErrorMessage}");
                    }

                return (true, string.Empty);
            }

            // Fallback checks for types that should be supported but aren't caught by OriginalDefinition matching
            var namedTypeName = namedType.ToDisplayString();

            // Support all common collection interfaces
            if (namedTypeName.StartsWith("System.Collections.Generic.IList<") ||
                namedTypeName.StartsWith("System.Collections.Generic.ICollection<") ||
                namedTypeName.StartsWith("System.Collections.Generic.IEnumerable<") ||
                namedTypeName.StartsWith("System.Collections.Generic.IReadOnlyList<") ||
                namedTypeName.StartsWith("System.Collections.Generic.IReadOnlyCollection<"))
            {
                // These are collection interface types, should be supported
                if (namedType.TypeArguments.Length > 0)
                    foreach (var typeArg in namedType.TypeArguments)
                    {
                        var elementValidation = ValidateTypeForConfiguration(typeArg);
                        if (!elementValidation.IsValid)
                            return (false,
                                $"Collection element type is not supported: {elementValidation.ErrorMessage}");
                    }

                return (true, string.Empty);
            }

            // Support dictionary interfaces  
            if (namedTypeName.StartsWith("System.Collections.Generic.IDictionary<"))
            {
                // Dictionary types are supported
                if (namedType.TypeArguments.Length >= 2)
                    foreach (var typeArg in namedType.TypeArguments)
                    {
                        var elementValidation = ValidateTypeForConfiguration(typeArg);
                        if (!elementValidation.IsValid)
                            return (false,
                                $"Dictionary type argument is not supported: {elementValidation.ErrorMessage}");
                    }

                return (true, string.Empty);
            }

            // Support Options pattern types
            if (namedTypeName.StartsWith("Microsoft.Extensions.Options.IOptions<") ||
                namedTypeName.StartsWith("Microsoft.Extensions.Options.IOptionsSnapshot<") ||
                namedTypeName.StartsWith("Microsoft.Extensions.Options.IOptionsMonitor<"))
                // These are Options pattern types, should be supported
                return (true, string.Empty);
        }

        // Handle array types
        if (type is IArrayTypeSymbol arrayType)
        {
            var elementValidation = ValidateTypeForConfiguration(arrayType.ElementType);
            if (!elementValidation.IsValid)
                return (false, $"Array element type is not supported: {elementValidation.ErrorMessage}");
            return (true, string.Empty);
        }

        // Check for supported primitive types
        if (SupportedPrimitiveTypes.Contains(typeName) || SupportedPrimitiveTypes.Contains(originalTypeName))
            return (true, string.Empty);

        // Check if it's an enum (supported)
        if (type.TypeKind == TypeKind.Enum) return (true, string.Empty);

        // For value types (structs), check if they have accessible properties/fields
        if (type.IsValueType) return (true, string.Empty); // Most structs can be bound if they have public properties

        // Check if it's a reference type that could potentially be bound (POCOs)
        if (type.IsReferenceType && type.TypeKind == TypeKind.Class)
            // For classes, we need to check if they have a parameterless constructor
            if (type is INamedTypeSymbol classType)
            {
                var hasParameterlessConstructor = classType.Constructors
                    .Any(ctor => ctor.Parameters.Length == 0 && ctor.DeclaredAccessibility == Accessibility.Public);

                if (!hasParameterlessConstructor)
                    return (false, "requires a parameterless constructor for configuration binding");

                return (true, string.Empty);
            }

        // Check if it's an interface (not supported for direct configuration binding)
        // Note: Collection interfaces are handled above in the generic type section
        // Do this check BEFORE abstract check since interfaces are also considered abstract
        if (type.TypeKind == TypeKind.Interface) return (false, "Interfaces cannot be bound");

        // Check if it's an abstract class
        if (type.IsAbstract) return (false, "Abstract types cannot be instantiated");

        // If we get here, it's probably not a good candidate for configuration binding
        return (false, "not supported for configuration binding");
    }
}
