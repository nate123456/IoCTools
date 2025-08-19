using Microsoft.CodeAnalysis;

namespace IoCTools.Generator.Models;

/// <summary>
///     Represents configuration injection information for a field
/// </summary>
internal class ConfigurationInjectionInfo
{
    public ConfigurationInjectionInfo(string fieldName,
        ITypeSymbol fieldType,
        string? configurationKey,
        object? defaultValue,
        bool required,
        bool supportsReloading)
    {
        FieldName = fieldName;
        FieldType = fieldType;
        ConfigurationKey = configurationKey;
        DefaultValue = defaultValue;
        Required = required;
        SupportsReloading = supportsReloading;
    }

    /// <summary>
    ///     The field name that will receive the configuration value
    /// </summary>
    public string FieldName { get; }

    /// <summary>
    ///     The type of the field
    /// </summary>
    public ITypeSymbol FieldType { get; }

    /// <summary>
    ///     The configuration key to bind to, or null for section name inference
    /// </summary>
    public string? ConfigurationKey { get; }

    /// <summary>
    ///     The default value to use if configuration is missing
    /// </summary>
    public object? DefaultValue { get; }

    /// <summary>
    ///     Whether the configuration value is required
    /// </summary>
    public bool Required { get; }

    /// <summary>
    ///     Whether the configuration supports reloading
    /// </summary>
    public bool SupportsReloading { get; }

    /// <summary>
    ///     Determines if this is a direct value binding (primitive types)
    /// </summary>
    public bool IsDirectValueBinding => IsDirectValueType(FieldType);

    /// <summary>
    ///     Determines if this is an options pattern injection
    /// </summary>
    public bool IsOptionsPattern => IsOptionsPatternType(FieldType);

    /// <summary>
    ///     Gets the inner type for options pattern types
    /// </summary>
    public ITypeSymbol? GetOptionsInnerType()
    {
        if (!IsOptionsPattern || FieldType is not INamedTypeSymbol namedType || namedType.TypeArguments.Length == 0)
            return null;

        return namedType.TypeArguments[0];
    }

    /// <summary>
    ///     Gets the section name to use for section binding
    /// </summary>
    public string GetSectionName()
    {
        if (!string.IsNullOrEmpty(ConfigurationKey))
            return ConfigurationKey;

        var targetType = IsOptionsPattern ? GetOptionsInnerType() : FieldType;
        if (targetType == null)
            return "Unknown";

        return InferSectionNameFromType(targetType);
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
               typeName.StartsWith("System.Collections.Generic.IReadOnlyCollection<") ||
               typeName.StartsWith("System.Collections.Generic.ISet<") ||
               typeName.StartsWith("System.Collections.Generic.HashSet<");
    }

    private static bool IsDirectValueType(ITypeSymbol type)
    {
        // Check for nullable types first to avoid infinite recursion
        if (type is INamedTypeSymbol namedType &&
            namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
            namedType.TypeArguments.Length == 1)
            return IsDirectValueType(namedType.TypeArguments[0]);

        // CRITICAL FIX: Collection types should NEVER be considered direct value types
        // They must always use GetSection().Get<T>() pattern, regardless of element type
        if (IsCollectionType(type))
            return false;

        // Check for enum types early
        if (type.TypeKind == TypeKind.Enum)
            return true;

        // Check for primitive types that can be bound directly using GetValue<T>
        // Use SpecialType for better performance and reliability
        switch (type.SpecialType)
        {
            case SpecialType.System_String:
            case SpecialType.System_Int32:
            case SpecialType.System_Int64:
            case SpecialType.System_Int16:
            case SpecialType.System_Byte:
            case SpecialType.System_Boolean:
            case SpecialType.System_Decimal:
            case SpecialType.System_Double:
            case SpecialType.System_Single:
                return true;
        }

        // Check by multiple methods for types not covered by SpecialType
        var fullTypeName = type.ToDisplayString();
        var metadataName = type.MetadataName;
        var namespaceAndMetadata = type.ContainingNamespace?.ToDisplayString() + "." + type.MetadataName;

        // Check all possible representations to be robust
        return fullTypeName is "System.TimeSpan" or "System.DateTime" or "System.DateTimeOffset" or "System.Guid"
                   or "System.Uri" ||
               metadataName is "TimeSpan" or "DateTime" or "DateTimeOffset" or "Guid" or "Uri" ||
               namespaceAndMetadata is "System.TimeSpan" or "System.DateTime" or "System.DateTimeOffset"
                   or "System.Guid" or "System.Uri";
    }

    private static bool IsOptionsPatternType(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
            return false;

        // Check by full type name with proper generic handling
        var fullTypeName = namedType.OriginalDefinition.ToDisplayString();

        // Check for Microsoft.Extensions.Options types
        if (fullTypeName.StartsWith("Microsoft.Extensions.Options.IOptions<") ||
            fullTypeName.StartsWith("Microsoft.Extensions.Options.IOptionsSnapshot<") ||
            fullTypeName.StartsWith("Microsoft.Extensions.Options.IOptionsMonitor<"))
            return true;

        // Also check by metadata name for exact matches
        var metadataName = namedType.OriginalDefinition.MetadataName;
        return metadataName switch
        {
            "IOptions`1" => namedType.ContainingNamespace?.ToDisplayString() == "Microsoft.Extensions.Options",
            "IOptionsSnapshot`1" => namedType.ContainingNamespace?.ToDisplayString() == "Microsoft.Extensions.Options",
            "IOptionsMonitor`1" => namedType.ContainingNamespace?.ToDisplayString() == "Microsoft.Extensions.Options",
            _ => false
        };
    }

    private static ITypeSymbol GetNullableUnderlyingType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol namedType &&
            namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
            namedType.TypeArguments.Length == 1)
            return namedType.TypeArguments[0];
        return type;
    }

    private static string InferSectionNameFromType(ITypeSymbol type)
    {
        var typeName = type.Name;

        // Remove common suffixes
        if (typeName.EndsWith("Settings"))
            return typeName.Substring(0, typeName.Length - "Settings".Length);
        if (typeName.EndsWith("Configuration"))
            return typeName.Substring(0, typeName.Length - "Configuration".Length);
        if (typeName.EndsWith("Config"))
            return typeName.Substring(0, typeName.Length - "Config".Length);
        if (typeName.EndsWith("Options"))
            return typeName.Substring(0, typeName.Length - "Options".Length);
        if (typeName.EndsWith("Object"))
            return typeName.Substring(0, typeName.Length - "Object".Length);

        return typeName;
    }
}