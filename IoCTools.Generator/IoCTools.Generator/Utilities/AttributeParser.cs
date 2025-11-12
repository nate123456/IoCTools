namespace IoCTools.Generator.Utilities;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;

internal static class AttributeParser
{
    public static (string namingConvention, bool stripI, string prefix) GetNamingConventionOptionsFromAttribute(
        AttributeData attribute)
    {
        var namingConvention = "CamelCase";
        var stripI = true;
        var prefix = "_";

        // Check constructor arguments first (like ExtractLifetime method)
        var constructorArgs = attribute.ConstructorArguments;
        if (constructorArgs.Length > 0)
        {
            // First parameter is namingConvention
            var enumValue = constructorArgs[0].Value;
            if (enumValue != null)
                namingConvention = enumValue switch
                {
                    0 => "CamelCase",
                    1 => "PascalCase",
                    2 => "SnakeCase",
                    _ => "CamelCase" // Default fallback
                };
        }

        if (constructorArgs.Length > 1)
        {
            // Second parameter is stripI
            var stripIValue = constructorArgs[1].Value;
            if (stripIValue is bool b) stripI = b;
        }

        if (constructorArgs.Length > 2)
        {
            // Third parameter is prefix
            var prefixValue = constructorArgs[2].Value;
            if (prefixValue != null) prefix = prefixValue.ToString() ?? "_";
        }

        // Also check named arguments as fallback (for backwards compatibility)
        foreach (var namedArg in attribute.NamedArguments)
            switch (namedArg.Key)
            {
                case "NamingConvention":
                    var enumValue = namedArg.Value.Value;
                    if (enumValue != null)
                        namingConvention = enumValue switch
                        {
                            0 => "CamelCase",
                            1 => "PascalCase",
                            2 => "SnakeCase",
                            _ => "CamelCase" // Default fallback
                        };
                    break;
                case "StripI":
                    stripI = namedArg.Value.Value as bool? ?? true;
                    break;
                case "Prefix":
                    prefix = namedArg.Value.Value?.ToString() ?? "_";
                    break;
            }

        return (namingConvention, stripI, prefix);
    }

    public static (string namingConvention, bool stripI, string prefix, bool external) GetDependsOnOptionsFromAttribute(
        AttributeData attribute)
    {
        var namingConvention = "CamelCase";
        var stripI = true;
        var prefix = "_";
        var external = false;

        // Check constructor arguments first
        var constructorArgs = attribute.ConstructorArguments;
        if (constructorArgs.Length > 0)
        {
            // First parameter is namingConvention
            var enumValue = constructorArgs[0].Value;
            if (enumValue != null)
                namingConvention = enumValue switch
                {
                    0 => "CamelCase",
                    1 => "PascalCase",
                    2 => "SnakeCase",
                    _ => "CamelCase" // Default fallback
                };
        }

        if (constructorArgs.Length > 1)
        {
            // Second parameter is stripI
            var stripIValue = constructorArgs[1].Value;
            if (stripIValue is bool b) stripI = b;
        }

        if (constructorArgs.Length > 2)
        {
            // Third parameter is prefix
            var prefixValue = constructorArgs[2].Value;
            if (prefixValue != null) prefix = prefixValue.ToString() ?? "_";
        }

        if (constructorArgs.Length > 3)
        {
            // Fourth parameter is external
            var externalValue = constructorArgs[3].Value;
            if (externalValue is bool ext) external = ext;
        }

        // Also check named arguments as fallback (for backwards compatibility)
        foreach (var namedArg in attribute.NamedArguments)
            switch (namedArg.Key)
            {
                case "NamingConvention":
                    var enumValue = namedArg.Value.Value;
                    if (enumValue != null)
                        namingConvention = enumValue switch
                        {
                            0 => "CamelCase",
                            1 => "PascalCase",
                            2 => "SnakeCase",
                            _ => "CamelCase" // Default fallback
                        };
                    break;
                case "StripI":
                    stripI = namedArg.Value.Value as bool? ?? true;
                    break;
                case "Prefix":
                    prefix = namedArg.Value.Value?.ToString() ?? "_";
                    break;
                case "External":
                    external = namedArg.Value.Value as bool? ?? false;
                    break;
            }

        return (namingConvention, stripI, prefix, external);
    }

    public static string GetRegistrationMode(AttributeData attribute)
    {
        const string defaultMode = "All";
        if (attribute == null) return defaultMode;

        if (attribute.ConstructorArguments.Length > 0)
        {
            var ctorValue = attribute.ConstructorArguments[0].Value;
            if (ctorValue is int enumValue)
                return enumValue switch
                {
                    0 => "DirectOnly",
                    1 => "All",
                    2 => "Exclusionary",
                    _ => defaultMode
                };

            if (int.TryParse(ctorValue?.ToString(), out var parsedCtor))
                return parsedCtor switch
                {
                    0 => "DirectOnly",
                    1 => "All",
                    2 => "Exclusionary",
                    _ => defaultMode
                };
        }

        var modeArg = attribute.NamedArguments.FirstOrDefault(arg => arg.Key == "Mode");
        if (modeArg.Key != null)
        {
            if (modeArg.Value.Value is int namedEnum)
                return namedEnum switch
                {
                    0 => "DirectOnly",
                    1 => "All",
                    2 => "Exclusionary",
                    _ => defaultMode
                };

            var namedString = modeArg.Value.Value?.ToString();
            if (namedString is "DirectOnly" or "All" or "Exclusionary") return namedString;
        }

        return defaultMode;
    }

    public static string GenerateFieldName(string originalTypeName,
        string namingConvention,
        bool stripI,
        string prefix)
    {
        var workingTypeName = originalTypeName;

        // Apply stripI logic: only strip 'I' when explicitly requested
        if (stripI && workingTypeName.StartsWith("I") && workingTypeName.Length > 1 && char.IsUpper(workingTypeName[1]))
            workingTypeName = workingTypeName.Substring(1);

        // CRITICAL FIX: Always use semantic naming for field generation
        // Field names should be semantically meaningful regardless of stripI parameter
        // stripI only affects the naming convention application, not the fundamental semantic naming
        string fieldBaseName;
        if (originalTypeName.StartsWith("I") && originalTypeName.Length > 1 && char.IsUpper(originalTypeName[1]))
            // For interface types, always use semantic naming (strip 'I') for field names
            // This ensures consistent field naming: IService -> _service, IDerivedService -> _derivedService  
            fieldBaseName = originalTypeName.Substring(1);
        else
            // For non-interface types, use the original type name
            fieldBaseName = originalTypeName;

        // Generate the final field name based on prefix type
        string fieldName;

        if (prefix == "")
        {
            // Empty prefix: apply naming convention to type name, no prefixes at all
            switch (namingConvention)
            {
                case "CamelCase":
                    fieldName = char.ToLowerInvariant(fieldBaseName[0]) + fieldBaseName.Substring(1);
                    break;
                case "PascalCase":
                    fieldName = char.ToUpperInvariant(fieldBaseName[0]) + fieldBaseName.Substring(1);
                    break;
                case "SnakeCase":
                    fieldName = Regex.Replace(fieldBaseName, @"(?<!^)([A-Z])", "_$1").ToLower();
                    break;
                default:
                    fieldName = fieldBaseName;
                    break;
            }
        }
        else if (prefix == "_")
        {
            // Default prefix: apply naming convention to type name, then add underscore prefix
            switch (namingConvention)
            {
                case "CamelCase":
                    fieldName = "_" + char.ToLowerInvariant(fieldBaseName[0]) + fieldBaseName.Substring(1);
                    break;
                case "PascalCase":
                    fieldName = "_" + char.ToUpperInvariant(fieldBaseName[0]) + fieldBaseName.Substring(1);
                    break;
                case "SnakeCase":
                    fieldName = "_" + Regex.Replace(fieldBaseName, @"(?<!^)([A-Z])", "_$1").ToLower();
                    break;
                default:
                    fieldName = "_" + fieldBaseName;
                    break;
            }
        }
        else if (prefix.EndsWith("_"))
        {
            // Custom prefix ending with underscore: apply naming convention to type name, use prefix as-is
            var formattedTypeName = fieldBaseName;
            switch (namingConvention)
            {
                case "CamelCase":
                    formattedTypeName = char.ToLowerInvariant(fieldBaseName[0]) + fieldBaseName.Substring(1);
                    break;
                case "PascalCase":
                    formattedTypeName = char.ToUpperInvariant(fieldBaseName[0]) + fieldBaseName.Substring(1);
                    break;
                case "SnakeCase":
                    formattedTypeName = Regex.Replace(fieldBaseName, @"(?<!^)([A-Z])", "_$1").ToLower();
                    break;
            }

            fieldName = prefix + formattedTypeName;
        }
        else
        {
            // Custom prefix not ending with underscore: format prefix+type together, then add underscore
            var combinedName = prefix + fieldBaseName;
            switch (namingConvention)
            {
                case "CamelCase":
                    fieldName = "_" + char.ToLowerInvariant(combinedName[0]) + combinedName.Substring(1);
                    break;
                case "PascalCase":
                    fieldName = "_" + char.ToUpperInvariant(combinedName[0]) + combinedName.Substring(1);
                    break;
                case "SnakeCase":
                    fieldName = "_" + Regex.Replace(combinedName, @"(?<!^)([A-Z])", "_$1").ToLower();
                    break;
                default:
                    fieldName = "_" + combinedName;
                    break;
            }
        }

        // Handle C# reserved keywords by adding a suffix
        fieldName = EscapeReservedKeyword(fieldName);

        return fieldName;
    }

    public static string ExtractLifetime(AttributeData serviceAttribute)
    {
        // Check constructor arguments first
        var constructorArgLifetime = serviceAttribute.ConstructorArguments.FirstOrDefault().Value;
        if (constructorArgLifetime != null)
        {
            // Assuming constructorArgLifetime is an enum represented as an int, map it to the string.
            // This example uses a switch expression for mapping.
            var lifetimeStr = constructorArgLifetime switch
            {
                0 => "Scoped",
                1 => "Transient",
                2 => "Singleton",
                _ => throw new Exception(
                    $"Couldn't parse lifetime value from constructor arguments: {constructorArgLifetime}")
            };
            return lifetimeStr;
        }

        // Then check named arguments if constructor argument wasn't used or didn't provide a valid value
        var lifetimeArg = serviceAttribute.NamedArguments.FirstOrDefault(arg => arg.Key == "Lifetime");

        if (lifetimeArg.Key == null) return "Scoped";

        var lifetimeValue = lifetimeArg.Value.Value?.ToString();
        if (lifetimeValue is "Scoped" or "Transient" or "Singleton")
            return lifetimeValue;
        if (lifetimeValue != null)
            throw new Exception("Couldn't parse lifetime value from named arguments: " + lifetimeValue);

        // Default to "Scoped" if neither constructor nor named arguments specified a lifetime
        return "Scoped";
    }

    /// <summary>
    ///     Escapes C# reserved keywords by appending a suffix to avoid compilation errors
    /// </summary>
    private static string EscapeReservedKeyword(string identifier)
    {
        // C# reserved keywords that could conflict with parameter names
        var reservedKeywords = new HashSet<string>
        {
            "abstract",
            "as",
            "base",
            "bool",
            "break",
            "byte",
            "case",
            "catch",
            "char",
            "checked",
            "class",
            "const",
            "continue",
            "decimal",
            "default",
            "delegate",
            "do",
            "double",
            "else",
            "enum",
            "event",
            "explicit",
            "extern",
            "false",
            "finally",
            "fixed",
            "float",
            "for",
            "foreach",
            "goto",
            "if",
            "implicit",
            "in",
            "int",
            "interface",
            "internal",
            "is",
            "lock",
            "long",
            "namespace",
            "new",
            "null",
            "object",
            "operator",
            "out",
            "override",
            "params",
            "private",
            "protected",
            "public",
            "readonly",
            "ref",
            "return",
            "sbyte",
            "sealed",
            "short",
            "sizeof",
            "stackalloc",
            "static",
            "string",
            "struct",
            "switch",
            "this",
            "throw",
            "true",
            "try",
            "typeof",
            "uint",
            "ulong",
            "unchecked",
            "unsafe",
            "ushort",
            "using",
            "virtual",
            "void",
            "volatile",
            "while"
        };

        if (reservedKeywords.Contains(identifier)) return identifier + "Value";

        return identifier;
    }
}
