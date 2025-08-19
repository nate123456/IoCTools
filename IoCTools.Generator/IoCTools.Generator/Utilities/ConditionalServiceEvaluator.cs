using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace IoCTools.Generator.Utilities;

/// <summary>
///     Provides utilities for evaluating conditional service registration conditions.
///     Handles environment-based and configuration-based condition evaluation.
/// </summary>
internal static class ConditionalServiceEvaluator
{
    /// <summary>
    ///     Evaluates environment-based conditions for service registration.
    /// </summary>
    /// <param name="conditionalAttribute">The ConditionalService attribute data</param>
    /// <param name="currentEnvironment">The current environment (can be null/empty)</param>
    /// <returns>True if the environment condition is satisfied</returns>
    public static bool EvaluateEnvironmentCondition(AttributeData conditionalAttribute,
        string? currentEnvironment)
    {
        // Handle null/empty environment (default to empty string for comparison)
        var environment = currentEnvironment ?? string.Empty;

        var environmentArg = GetNamedArgumentValue(conditionalAttribute, "Environment");
        var notEnvironmentArg = GetNamedArgumentValue(conditionalAttribute, "NotEnvironment");

        // If no environment conditions are specified, condition is satisfied
        if (string.IsNullOrEmpty(environmentArg) && string.IsNullOrEmpty(notEnvironmentArg)) return true;

        var environmentMatches = true;
        var notEnvironmentMatches = true;

        // Check positive environment condition - use case-sensitive comparison for consistency
        if (!string.IsNullOrEmpty(environmentArg))
        {
            var environments = ParseEnvironmentList(environmentArg ?? "");
            environmentMatches = environments.Any(env =>
                string.Equals(env.Trim(), environment.Trim(), StringComparison.Ordinal));
        }

        // Check negative environment condition - use case-sensitive comparison
        if (!string.IsNullOrEmpty(notEnvironmentArg))
        {
            var notEnvironments = ParseEnvironmentList(notEnvironmentArg ?? "");
            notEnvironmentMatches = !notEnvironments.Any(env =>
                string.Equals(env.Trim(), environment.Trim(), StringComparison.Ordinal));
        }

        // Both conditions must be satisfied
        return environmentMatches && notEnvironmentMatches;
    }

    /// <summary>
    ///     Evaluates configuration-based conditions for service registration.
    /// </summary>
    /// <param name="conditionalAttribute">The ConditionalService attribute data</param>
    /// <param name="configValue">The configuration value to check</param>
    /// <returns>True if the configuration condition is satisfied</returns>
    public static bool EvaluateConfigurationCondition(AttributeData conditionalAttribute,
        string? configValue)
    {
        var configKeyArg = GetNamedArgumentValue(conditionalAttribute, "ConfigValue");
        var equalsArg = GetNamedArgumentValue(conditionalAttribute, "Equals");
        var notEqualsArg = GetNamedArgumentValue(conditionalAttribute, "NotEquals");

        // If no configuration conditions are specified, condition is satisfied
        if (string.IsNullOrEmpty(configKeyArg)) return true;

        // Handle null configuration value (default to empty string for comparison)
        var actualValue = configValue ?? string.Empty;

        var equalsMatches = true;
        var notEqualsMatches = true;

        // Check equals condition - use case-sensitive comparison as per test expectations
        if (!string.IsNullOrEmpty(equalsArg))
            equalsMatches = string.Equals(actualValue, equalsArg, StringComparison.Ordinal);

        // Check not equals condition - use case-sensitive comparison
        if (!string.IsNullOrEmpty(notEqualsArg))
        {
            var notEqualsValues = ParseCommaSeperatedList(notEqualsArg ?? "");
            notEqualsMatches = !notEqualsValues.Any(val =>
                string.Equals(actualValue, val.Trim(), StringComparison.Ordinal));
        }

        // Both conditions must be satisfied
        return equalsMatches && notEqualsMatches;
    }

    /// <summary>
    ///     Extracts condition information from a ConditionalService attribute.
    /// </summary>
    /// <param name="conditionalAttribute">The ConditionalService attribute data</param>
    /// <returns>Condition information for code generation</returns>
    public static ConditionalServiceCondition ExtractCondition(AttributeData conditionalAttribute) => new()
    {
        Environment = GetNamedArgumentValue(conditionalAttribute, "Environment"),
        NotEnvironment = GetNamedArgumentValue(conditionalAttribute, "NotEnvironment"),
        ConfigValue = GetNamedArgumentValue(conditionalAttribute, "ConfigValue"),
        EqualsValue = GetNamedArgumentValue(conditionalAttribute, "Equals"),
        NotEquals = GetNamedArgumentValue(conditionalAttribute, "NotEquals")
    };

    /// <summary>
    ///     Validates that conditional service conditions are not conflicting.
    /// </summary>
    /// <param name="conditionalAttribute">The ConditionalService attribute data</param>
    /// <returns>True if conditions are valid, false if conflicting</returns>
    public static bool ValidateConditions(AttributeData conditionalAttribute)
    {
        var environmentArg = GetNamedArgumentValue(conditionalAttribute, "Environment");
        var notEnvironmentArg = GetNamedArgumentValue(conditionalAttribute, "NotEnvironment");

        // Check for conflicting environment conditions
        if (!string.IsNullOrEmpty(environmentArg) && !string.IsNullOrEmpty(notEnvironmentArg))
        {
            var environments = ParseEnvironmentList(environmentArg ?? "");
            var notEnvironments = ParseEnvironmentList(notEnvironmentArg ?? "");

            // Check if any environment appears in both lists - use case-sensitive comparison
            var conflicting = environments.Intersect(notEnvironments, StringComparer.Ordinal).Any();
            if (conflicting) return false;
        }

        var equalsArg = GetNamedArgumentValue(conditionalAttribute, "Equals");
        var notEqualsArg = GetNamedArgumentValue(conditionalAttribute, "NotEquals");

        // Check for conflicting configuration conditions
        if (!string.IsNullOrEmpty(equalsArg) && !string.IsNullOrEmpty(notEqualsArg))
        {
            var equalsValues = new[] { equalsArg };
            var notEqualsValues = ParseCommaSeperatedList(notEqualsArg ?? "");

            // Check if equals value appears in not equals list - use case-sensitive comparison
            var conflicting = equalsValues.Intersect(notEqualsValues, StringComparer.Ordinal).Any();
            if (conflicting) return false;
        }

        return true;
    }

    /// <summary>
    ///     Checks if conditional service has empty conditions.
    /// </summary>
    /// <param name="conditionalAttribute">The ConditionalService attribute data</param>
    /// <returns>True if conditions are empty</returns>
    public static bool HasEmptyConditions(AttributeData conditionalAttribute)
    {
        var environmentArg = GetNamedArgumentValue(conditionalAttribute, "Environment");
        var notEnvironmentArg = GetNamedArgumentValue(conditionalAttribute, "NotEnvironment");
        // Environment conditions are valid even when empty string - only null means not specified
        var hasEnvironmentCondition = environmentArg != null || notEnvironmentArg != null;

        var configKeyArg = GetNamedArgumentValue(conditionalAttribute, "ConfigValue");
        var hasConfigCondition = !string.IsNullOrEmpty(configKeyArg);

        return !hasEnvironmentCondition && !hasConfigCondition;
    }

    /// <summary>
    ///     Validates conditional service conditions with detailed error reporting.
    /// </summary>
    /// <param name="conditionalAttribute">The ConditionalService attribute data</param>
    /// <returns>ValidationResult with success and specific error details</returns>
    public static ConditionalServiceValidationResult ValidateConditionsDetailed(AttributeData conditionalAttribute)
    {
        var errors = new List<string>();

        var environmentArg = GetNamedArgumentValue(conditionalAttribute, "Environment");
        var notEnvironmentArg = GetNamedArgumentValue(conditionalAttribute, "NotEnvironment");
        var configValueArg = GetNamedArgumentValue(conditionalAttribute, "ConfigValue");
        var equalsArg = GetNamedArgumentValue(conditionalAttribute, "Equals");
        var notEqualsArg = GetNamedArgumentValue(conditionalAttribute, "NotEquals");

        // Check for conflicting environment conditions
        var conflictingEnvironments = new List<string>();
        if (!string.IsNullOrEmpty(environmentArg) && !string.IsNullOrEmpty(notEnvironmentArg))
        {
            var environments = ParseEnvironmentList(environmentArg ?? "");
            var notEnvironments = ParseEnvironmentList(notEnvironmentArg ?? "");

            // Check if any environment appears in both lists - use case-sensitive comparison
            conflictingEnvironments = environments.Intersect(notEnvironments, StringComparer.Ordinal).ToList();
            if (conflictingEnvironments.Any())
                errors.Add(
                    $"Environment conflict: '{string.Join(", ", conflictingEnvironments)}' appears in both Environment and NotEnvironment");
        }

        // Check for conflicting configuration conditions
        var conflictingConfigValues = new List<string>();
        if (!string.IsNullOrEmpty(equalsArg) && !string.IsNullOrEmpty(notEqualsArg))
        {
            var equalsValues = new[] { equalsArg };
            var notEqualsValues = ParseCommaSeperatedList(notEqualsArg ?? "");

            // Check if equals value appears in not equals list - use case-sensitive comparison
            conflictingConfigValues = equalsValues.Intersect(notEqualsValues.Where(x => x != null), StringComparer.Ordinal).ToList();
            if (conflictingConfigValues.Any())
                errors.Add(
                    $"Configuration conflict for '{configValueArg}': value '{string.Join(", ", conflictingConfigValues)}' appears in both Equals and NotEquals");
        }

        // Check if any conditions are specified at all
        // Environment conditions are valid even when empty string - only null means not specified  
        var hasEnvironmentCondition = environmentArg != null || notEnvironmentArg != null;
        // Configuration conditions are valid even when empty string - only null means not specified
        var hasConfigCondition = configValueArg != null;

        if (!hasEnvironmentCondition && !hasConfigCondition)
            errors.Add(
                "No conditions specified - at least one Environment, NotEnvironment, or ConfigValue condition is required");

        // Check for ConfigValue without comparison - ConfigValue can be empty string, but not null
        if (configValueArg != null && string.IsNullOrEmpty(equalsArg) && string.IsNullOrEmpty(notEqualsArg))
            errors.Add($"ConfigValue '{configValueArg}' specified without Equals or NotEquals");

        // Check for comparison without ConfigValue - only null ConfigValue is invalid, empty string is valid
        if ((!string.IsNullOrEmpty(equalsArg) || !string.IsNullOrEmpty(notEqualsArg)) && configValueArg == null)
            errors.Add("Equals or NotEquals specified without ConfigValue");

        // Check for empty or whitespace-only ConfigValue
        if (!string.IsNullOrEmpty(configValueArg) && string.IsNullOrWhiteSpace(configValueArg))
            errors.Add("ConfigValue is empty or contains only whitespace");

        return new ConditionalServiceValidationResult
        {
            IsValid = !errors.Any(),
            Errors = errors,
            ConflictingEnvironments = conflictingEnvironments,
            ConflictingConfigValues = conflictingConfigValues,
            ConfigValue = configValueArg
        };
    }

    /// <summary>
    ///     Gets the runtime environment detection code for code generation.
    /// </summary>
    /// <returns>C# code to detect current environment</returns>
    public static string GetEnvironmentDetectionCode() =>
        "var environment = Environment.GetEnvironmentVariable(\"ASPNETCORE_ENVIRONMENT\") ?? \"\";";

    /// <summary>
    ///     Gets the configuration access code for code generation.
    /// </summary>
    /// <param name="hasConfigurationParameter">Whether the method already has an IConfiguration parameter</param>
    /// <returns>C# code to access configuration</returns>
    public static string GetConfigurationAccessCode(bool hasConfigurationParameter = false)
    {
        if (hasConfigurationParameter)
            // If the method already has an IConfiguration parameter, don't create a local variable
            return "";

        // CRITICAL FIX: Configuration should be passed as parameter, not retrieved via BuildServiceProvider
        // If configuration is needed but not provided as parameter, this is a generator architecture issue
        // The registration method signature should include IConfiguration when conditional services need it
        throw new InvalidOperationException(
            "Configuration parameter is required for conditional service registration but was not provided. " +
            "This indicates a bug in the generator's parameter detection logic.");
    }

    /// <summary>
    ///     Generates code to safely access a configuration value with proper null handling.
    /// </summary>
    /// <param name="configKey">The configuration key path</param>
    /// <param name="hasConfigurationParameter">Whether the method has an IConfiguration parameter</param>
    /// <returns>C# code to safely access the configuration value</returns>
    public static string GenerateConfigValueAccessCode(string configKey,
        bool hasConfigurationParameter = true)
    {
        // CRITICAL FIX: Ensure the configuration key is properly escaped
        var escapedKey = EscapeStringLiteral(configKey ?? "");

        // Generate configuration access code for conditional services
        // Tests expect: (configuration.GetValue<string>("Key") ?? "") == "Value"
        return $"(configuration.GetValue<string>(\"{escapedKey}\") ?? \"\")";
    }

    /// <summary>
    ///     Evaluates a specific configuration condition for testing/validation.
    /// </summary>
    /// <param name="configValue">The actual configuration value</param>
    /// <param name="equalsValue">Expected value for equals condition</param>
    /// <param name="notEqualsValue">Expected value for not equals condition</param>
    /// <returns>True if the condition is satisfied</returns>
    public static bool EvaluateConfigValue(string? configValue,
        string? equalsValue,
        string? notEqualsValue)
    {
        // Handle null configuration value (default to empty string for comparison)
        var actualValue = configValue ?? string.Empty;

        var equalsMatches = true;
        var notEqualsMatches = true;

        // Check equals condition - use case-sensitive comparison
        if (!string.IsNullOrEmpty(equalsValue))
            equalsMatches = string.Equals(actualValue, equalsValue, StringComparison.Ordinal);

        // Check not equals condition - use case-sensitive comparison
        if (!string.IsNullOrEmpty(notEqualsValue))
        {
            var notEqualsValues = ParseCommaSeperatedList(notEqualsValue ?? "");
            notEqualsMatches = !notEqualsValues.Any(val =>
                string.Equals(actualValue, val.Trim(), StringComparison.Ordinal));
        }

        // Both conditions must be satisfied
        return equalsMatches && notEqualsMatches;
    }

    private static string? GetNamedArgumentValue(AttributeData attribute,
        string argumentName)
    {
        // CRITICAL FIX: Handle the ConditionalServiceAttribute.Equals property name collision
        // The Equals property uses 'new' keyword to hide System.Object.Equals, which can cause
        // issues with Roslyn's attribute reflection in some cases

        var argument = attribute.NamedArguments.FirstOrDefault(arg => arg.Key == argumentName);
        if (argument.Key != null) return argument.Value.Value?.ToString();

        // If the argument wasn't found by exact name, and we're looking for 'Equals',
        // try alternative approaches to handle the name collision
        if (argumentName == "Equals")
        {
            // Try case-insensitive search as a fallback
            foreach (var namedArg in attribute.NamedArguments)
                if (string.Equals(namedArg.Key, "Equals", StringComparison.OrdinalIgnoreCase))
                    return namedArg.Value.Value?.ToString();

            // Final fallback: Sometimes reflection systems handle shadowed members differently
            // Look for any argument that semantically represents the Equals value
            // CRITICAL FIX: Only match exact "Equals", not "NotEquals" or other properties ending with "Equals"
            foreach (var namedArg in attribute.NamedArguments)
                // Check if this could be the Equals property by examining the attribute class structure
                // Only match exactly "Equals", not "NotEquals" which also ends with "Equals"
                if (string.Equals(namedArg.Key, "Equals", StringComparison.OrdinalIgnoreCase))
                    return namedArg.Value.Value?.ToString();
        }

        return null;
    }

    private static string[] ParseEnvironmentList(string environmentString) =>
        ParseCommaSeperatedList(environmentString);

    private static string[] ParseCommaSeperatedList(string input)
    {
        if (string.IsNullOrEmpty(input)) return new string[0];

        // Allow empty strings in the parsed result - they are valid environment/config values
        return input.Split(',')
            .Select(s => s.Trim())
            .ToArray();
    }

    /// <summary>
    ///     Escapes special characters in a string literal for safe code generation.
    /// </summary>
    /// <param name="value">The string value to escape</param>
    /// <returns>The escaped string literal content (without outer quotes)</returns>
    public static string EscapeStringLiteral(string? value)
    {
        if (value == null) return "";

        // CRITICAL FIX: Proper string literal escaping for C# code generation
        // Order matters - backslashes must be escaped first
        return value
            .Replace("\\", "\\\\") // Escape backslashes first
            .Replace("\"", "\\\"") // Escape double quotes
            .Replace("\n", "\\n") // Escape newlines
            .Replace("\r", "\\r") // Escape carriage returns
            .Replace("\t", "\\t") // Escape tabs
            .Replace("\b", "\\b") // Escape backspaces
            .Replace("\f", "\\f") // Escape form feeds
            .Replace("\0", "\\0") // Escape null characters
            .Replace("\a", "\\a") // Escape alert/bell
            .Replace("\v", "\\v"); // Escape vertical tabs
    }
}

/// <summary>
///     Represents the condition information extracted from a ConditionalService attribute.
/// </summary>
internal class ConditionalServiceCondition : IEquatable<ConditionalServiceCondition>
{
    public string? Environment { get; set; }
    public string? NotEnvironment { get; set; }
    public string? ConfigValue { get; set; }
    public string? EqualsValue { get; set; }
    public string? NotEquals { get; set; }

    /// <summary>
    ///     Determines if this condition requires environment checking.
    /// </summary>
    public bool RequiresEnvironment => Environment != null || NotEnvironment != null;

    /// <summary>
    ///     Determines if this condition requires configuration checking.
    /// </summary>
    public bool RequiresConfiguration => ConfigValue != null;

    /// <summary>
    ///     CRITICAL FIX: Provides unique string representation for proper deduplication
    /// </summary>
    public override string ToString()
    {
        var parts = new List<string>();

        if (Environment != null) parts.Add($"Env={Environment}");
        if (NotEnvironment != null) parts.Add($"NotEnv={NotEnvironment}");
        if (ConfigValue != null) parts.Add($"Config={ConfigValue}");
        if (EqualsValue != null) parts.Add($"Equals={EqualsValue}");
        if (NotEquals != null) parts.Add($"NotEquals={NotEquals}");

        return parts.Any() ? string.Join(";", parts) : "NoConditions";
    }

    /// <summary>
    ///     CRITICAL FIX: Implements proper equality checking for deduplication
    /// </summary>
    public bool Equals(ConditionalServiceCondition? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        
        return string.Equals(Environment, other.Environment, StringComparison.Ordinal) &&
               string.Equals(NotEnvironment, other.NotEnvironment, StringComparison.Ordinal) &&
               string.Equals(ConfigValue, other.ConfigValue, StringComparison.Ordinal) &&
               string.Equals(EqualsValue, other.EqualsValue, StringComparison.Ordinal) &&
               string.Equals(NotEquals, other.NotEquals, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as ConditionalServiceCondition);
    }

    public override int GetHashCode()
    {
        // CRITICAL FIX: Use .NET Standard 2.0 compatible hash code calculation
        unchecked
        {
            int hash = 17;
            hash = hash * 23 + (Environment?.GetHashCode() ?? 0);
            hash = hash * 23 + (NotEnvironment?.GetHashCode() ?? 0);
            hash = hash * 23 + (ConfigValue?.GetHashCode() ?? 0);
            hash = hash * 23 + (EqualsValue?.GetHashCode() ?? 0);
            hash = hash * 23 + (NotEquals?.GetHashCode() ?? 0);
            return hash;
        }
    }
}

/// <summary>
///     Represents the result of validating a ConditionalService attribute.
/// </summary>
internal class ConditionalServiceValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> ConflictingEnvironments { get; set; } = new();
    public List<string> ConflictingConfigValues { get; set; } = new();
    public string? ConfigValue { get; set; }
}