namespace IoCTools.Generator.Tests;

using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
///     ENHANCED TEST UTILITIES
///     Additional test utilities that make testing the implementation gaps easier:
///     - Helper methods to parse generated source code
///     - Utilities to verify specific patterns in generated code
///     - Mock configuration objects for testing config injection
///     - Fluent test builders for complex scenarios
///     - Pattern matching utilities for generated code verification
///     - Configuration test data builders
///     - Service resolution validators
/// </summary>
public static class EnhancedTestUtilities
{
    #region Mock Objects

    /// <summary>
    ///     Creates mock configuration objects for specific scenarios
    /// </summary>
    public static class MockConfigurations
    {
        public static IConfiguration DatabaseConfig() => CreateConfiguration()
            .AddValue("Database:ConnectionString", "Server=localhost;Database=TestDb")
            .AddValue("Database:Timeout", 30)
            .AddValue("Database:EnableRetry", true)
            .Build();

        public static IConfiguration CacheConfig() => CreateConfiguration()
            .AddValue("Cache:Provider", "Redis")
            .AddValue("Cache:TTL", "00:05:00")
            .AddValue("Cache:Servers:0", "localhost:6379")
            .AddValue("Cache:Servers:1", "localhost:6380")
            .Build();

        public static IConfiguration MultiEnvironmentConfig(string environment)
        {
            var builder = CreateConfiguration()
                .AddEnvironment(environment)
                .AddValue("Features:Email", "enabled")
                .AddValue("Features:SMS", environment == "Production" ? "enabled" : "disabled")
                .AddValue("Features:Push", "enabled");

            if (environment == "Development")
                builder.AddValue("Logging:Level", "Debug");
            else if (environment == "Production") builder.AddValue("Logging:Level", "Information");

            return builder.Build();
        }
    }

    #endregion

    #region Generated Code Parsing Utilities

    /// <summary>
    ///     Extracts constructor parameters from generated constructor source
    /// </summary>
    public static List<ConstructorParameter> ExtractConstructorParameters(string constructorSource)
    {
        var parameters = new List<ConstructorParameter>();

        // Find constructor declaration
        var constructorMatch = Regex.Match(constructorSource,
            @"public\s+\w+\s*\(\s*([^)]+)\s*\)",
            RegexOptions.Multiline);

        if (!constructorMatch.Success)
            return parameters;

        var parameterString = constructorMatch.Groups[1].Value.Trim();
        if (string.IsNullOrEmpty(parameterString))
            return parameters;

        // Split parameters by comma, handling generic types
        var paramParts = SplitParameters(parameterString);

        foreach (var part in paramParts)
        {
            var trimmed = part.Trim();
            var lastSpace = trimmed.LastIndexOf(' ');
            if (lastSpace > 0)
            {
                var type = trimmed.Substring(0, lastSpace).Trim();
                var name = trimmed.Substring(lastSpace + 1).Trim();
                parameters.Add(new ConstructorParameter(type, name));
            }
        }

        return parameters;
    }

    /// <summary>
    ///     Extracts field declarations from generated source
    /// </summary>
    public static List<FieldDeclaration> ExtractFieldDeclarations(string source)
    {
        var fields = new List<FieldDeclaration>();

        var fieldMatches = Regex.Matches(source,
            @"(private|protected|public|internal)?\s*(readonly)?\s*([^;]+?)\s+(\w+)\s*;",
            RegexOptions.Multiline);

        foreach (Match match in fieldMatches)
        {
            var visibility = match.Groups[1].Value.Trim();
            var isReadonly = !string.IsNullOrEmpty(match.Groups[2].Value);
            var type = match.Groups[3].Value.Trim();
            var name = match.Groups[4].Value.Trim();

            if (visibility == "" || visibility == "private" || visibility == "protected" || visibility == "public" ||
                visibility == "internal")
                fields.Add(new FieldDeclaration(
                    visibility.IsNullOrEmpty() ? "private" : visibility,
                    type,
                    name,
                    isReadonly
                ));
        }

        return fields;
    }

    /// <summary>
    ///     Extracts service registration calls from generated registration source
    /// </summary>
    public static List<ServiceRegistration> ExtractServiceRegistrations(string registrationSource)
    {
        var registrations = new List<ServiceRegistration>();

        // Match service registration patterns
        var patterns = new[]
        {
            @"services\.Add(Transient|Scoped|Singleton)<([^,>]+),?\s*([^>]*)>\s*\(\)",
            @"services\.AddHostedService<([^>]+)>\s*\(\)", @"services\.Configure<([^>]+)>\s*\("
        };

        foreach (var pattern in patterns)
        {
            var matches = Regex.Matches(registrationSource, pattern);
            foreach (Match match in matches)
                if (pattern.Contains("AddHostedService"))
                {
                    registrations.Add(new ServiceRegistration(
                        "IHostedService",
                        match.Groups[1].Value,
                        "HostedService"));
                }
                else if (pattern.Contains("Configure"))
                {
                    registrations.Add(new ServiceRegistration(
                        "IOptions<" + match.Groups[1].Value + ">",
                        match.Groups[1].Value,
                        "Configure"));
                }
                else
                {
                    var lifetime = match.Groups[1].Value;
                    var serviceType = match.Groups[2].Value.Trim();
                    var implementationType = match.Groups[3].Value.Trim();

                    if (string.IsNullOrEmpty(implementationType))
                        implementationType = serviceType;

                    registrations.Add(new ServiceRegistration(
                        serviceType,
                        implementationType,
                        lifetime));
                }
        }

        return registrations;
    }

    /// <summary>
    ///     Checks if generated code contains specific configuration injection patterns
    /// </summary>
    public static bool ContainsConfigurationPattern(string source,
        string key,
        string type,
        string? defaultValue = null)
    {
        if (IsPrimitiveType(type))
        {
            if (defaultValue != null)
                return source.Contains(
                    $"configuration.GetValue<{type}>(\"{key}\", {FormatDefaultValue(type, defaultValue)})");

            return source.Contains($"configuration.GetValue<{type}>(\"{key}\")");
        }

        return source.Contains($"configuration.GetSection(\"{key}\").Get<{type}>()");
    }

    /// <summary>
    ///     Checks if generated code contains conditional service patterns
    /// </summary>
    public static bool ContainsConditionalPattern(string source,
        string? environment = null,
        string? configKey = null,
        string? configValue = null)
    {
        if (environment != null && configKey != null)
        {
            var combinedPattern =
                $"if (string.Equals(environment, \"{environment}\", StringComparison.OrdinalIgnoreCase) && string.Equals(configuration[\"{configKey}\"], \"{configValue}\", StringComparison.OrdinalIgnoreCase))";
            return source.Contains(combinedPattern);
        }

        if (environment != null)
        {
            var envPattern = $"if (string.Equals(environment, \"{environment}\", StringComparison.OrdinalIgnoreCase))";
            return source.Contains(envPattern);
        }

        if (configKey != null)
        {
            var configPattern =
                $"if (string.Equals(configuration[\"{configKey}\"], \"{configValue}\", StringComparison.OrdinalIgnoreCase))";
            return source.Contains(configPattern);
        }

        return false;
    }

    /// <summary>
    ///     Verifies no duplicate service registrations exist
    /// </summary>
    public static bool HasDuplicateRegistrations(string registrationSource)
    {
        var registrations = ExtractServiceRegistrations(registrationSource);
        var registrationKeys = registrations
            .Select(r => $"{r.ServiceType}-{r.ImplementationType}")
            .ToList();

        return registrationKeys.Count != registrationKeys.Distinct().Count();
    }

    #endregion

    #region Configuration Test Data Builders

    /// <summary>
    ///     Fluent builder for creating test configurations
    /// </summary>
    public class ConfigurationBuilder
    {
        private readonly Dictionary<string, string> _data = new();

        public ConfigurationBuilder AddValue(string key,
            string value)
        {
            _data[key] = value;
            return this;
        }

        public ConfigurationBuilder AddValue(string key,
            int value)
        {
            _data[key] = value.ToString();
            return this;
        }

        public ConfigurationBuilder AddValue(string key,
            bool value)
        {
            _data[key] = value.ToString().ToLowerInvariant();
            return this;
        }

        public ConfigurationBuilder AddSection(string section,
            object sectionData)
        {
            var json = JsonSerializer.Serialize(sectionData,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            if (dict != null) FlattenObject(dict, section, _data);

            return this;
        }

        public ConfigurationBuilder AddArray(string key,
            string[] values)
        {
            for (var i = 0; i < values.Length; i++) _data[$"{key}:{i}"] = values[i];
            return this;
        }

        public ConfigurationBuilder AddEnvironment(string environment)
        {
            _data["ASPNETCORE_ENVIRONMENT"] = environment;
            return this;
        }

        public IConfiguration Build() => SourceGeneratorTestHelper.CreateConfiguration(_data);
    }

    /// <summary>
    ///     Creates a new configuration builder
    /// </summary>
    public static ConfigurationBuilder CreateConfiguration() => new();

    #endregion

    #region Service Test Builders

    /// <summary>
    ///     Fluent builder for creating test service source code
    /// </summary>
    public class ServiceBuilder
    {
        private readonly List<string> _attributes = new();
        private readonly List<string> _fields = new();
        private readonly List<string> _interfaces = new();
        private readonly List<string> _methods = new();
        private string? _baseClass;
        private string _className = "TestService";
        private string _namespace = "Test";

        public ServiceBuilder WithName(string className)
        {
            _className = className;
            return this;
        }

        public ServiceBuilder InNamespace(string namespaceName)
        {
            _namespace = namespaceName;
            return this;
        }

        public ServiceBuilder Implements(params string[] interfaces)
        {
            _interfaces.AddRange(interfaces);
            return this;
        }

        public ServiceBuilder Inherits(string baseClass)
        {
            _baseClass = baseClass;
            return this;
        }

        public ServiceBuilder WithAttribute(string attribute)
        {
            _attributes.Add(attribute);
            return this;
        }

        public ServiceBuilder WithService(string? lifetime = null)
        {
            if (lifetime != null)
                _attributes.Add($"[{lifetime}]");
            else
                _attributes.Add("[Scoped]");
            return this;
        }

        public ServiceBuilder WithConditionalService(string? environment = null,
            string? configKey = null,
            string? configValue = null)
        {
            var parts = new List<string>();
            if (environment != null) parts.Add($"Environment = \"{environment}\"");
            if (configKey != null) parts.Add($"ConfigValue = \"{configKey}\", Equals = \"{configValue}\"");

            _attributes.Add($"[ConditionalService({string.Join(", ", parts)})]");
            return this;
        }

        public ServiceBuilder WithDependsOn<T>(string? prefix = null,
            bool? stripI = null,
            string? namingConvention = null)
        {
            var parts = new List<string>();
            if (prefix != null) parts.Add($"Prefix = \"{prefix}\"");
            if (stripI.HasValue) parts.Add($"StripI = {stripI.Value.ToString().ToLowerInvariant()}");
            if (namingConvention != null) parts.Add($"NamingConvention = NamingConvention.{namingConvention}");

            var attribute = parts.Any()
                ? $"[DependsOn<{typeof(T).Name}>({string.Join(", ", parts)})]"
                : $"[DependsOn<{typeof(T).Name}>]";

            _attributes.Add(attribute);
            return this;
        }

        public ServiceBuilder WithInjectField(string type,
            string name)
        {
            _fields.Add($"[Inject] private readonly {type} {name};");
            return this;
        }

        public ServiceBuilder WithConfigurationField(string type,
            string name,
            string key,
            string? defaultValue = null)
        {
            var attribute = defaultValue != null
                ? $"[InjectConfiguration(\"{key}\", DefaultValue = {FormatDefaultValue(type, defaultValue)})]"
                : $"[InjectConfiguration(\"{key}\")]";

            _fields.Add($"{attribute} private readonly {type} {name};");
            return this;
        }

        public ServiceBuilder WithMethod(string method)
        {
            _methods.Add(method);
            return this;
        }

        public string Build()
        {
            var source = new StringBuilder();
            source.AppendLine("using IoCTools.Abstractions.Annotations;");
            source.AppendLine("using IoCTools.Abstractions.Enumerations;");
            source.AppendLine("using Microsoft.Extensions.Configuration;");
            source.AppendLine("using Microsoft.Extensions.Hosting;");
            source.AppendLine("using System.ComponentModel.DataAnnotations;");
            source.AppendLine();
            source.AppendLine($"namespace {_namespace};");
            source.AppendLine();

            foreach (var attr in _attributes) source.AppendLine(attr);

            var inheritance = new List<string>();
            if (_baseClass != null) inheritance.Add(_baseClass);
            inheritance.AddRange(_interfaces);

            var inheritanceClause = inheritance.Any() ? $" : {string.Join(", ", inheritance)}" : "";

            source.AppendLine($"public partial class {_className}{inheritanceClause}");
            source.AppendLine("{");

            foreach (var field in _fields) source.AppendLine($"    {field}");

            if (_fields.Any() && _methods.Any())
                source.AppendLine();

            foreach (var method in _methods) source.AppendLine($"    {method}");

            source.AppendLine("}");
            return source.ToString();
        }
    }

    /// <summary>
    ///     Creates a new service builder
    /// </summary>
    public static ServiceBuilder CreateService() => new();

    #endregion

    #region Test Result Validators

    /// <summary>
    ///     Validates that a test result has the expected characteristics
    /// </summary>
    public class TestResultValidator
    {
        private readonly GeneratorTestResult _result;

        public TestResultValidator(GeneratorTestResult result)
        {
            _result = result;
        }

        public TestResultValidator ShouldCompile()
        {
            _result.HasErrors.Should()
                .BeFalse(
                    $"Compilation errors: {string.Join(", ", _result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.GetMessage()))}");
            return this;
        }

        public TestResultValidator ShouldHaveConstructorFor(string className)
        {
            var constructor = _result.GetRequiredConstructorSource(className);
            return this;
        }

        public TestResultValidator ShouldHaveServiceRegistration()
        {
            _ = _result.GetRequiredServiceRegistrationSource();
            return this;
        }

        public TestResultValidator ShouldHaveDiagnostic(string code)
        {
            var diagnostics = _result.GetDiagnosticsByCode(code);
            diagnostics.Should().NotBeEmpty();
            return this;
        }

        public TestResultValidator ShouldNotHaveDiagnostic(string code)
        {
            var diagnostics = _result.GetDiagnosticsByCode(code);
            diagnostics.Should().BeEmpty();
            return this;
        }

        public TestResultValidator ShouldContainInConstructor(string className,
            string pattern)
        {
            var constructor = _result.GetRequiredConstructorSource(className);
            constructor.Content.Should().Contain(pattern);
            return this;
        }

        public TestResultValidator ShouldContainInRegistration(string pattern)
        {
            var registration = _result.GetRequiredServiceRegistrationSource();
            registration.Content.Should().Contain(pattern);
            return this;
        }

        public TestResultValidator ShouldHaveUniqueRegistrations()
        {
            var registration = _result.GetRequiredServiceRegistrationSource();
            HasDuplicateRegistrations(registration.Content).Should().BeFalse();
            return this;
        }
    }

    /// <summary>
    ///     Creates a validator for a test result
    /// </summary>
    public static TestResultValidator Validate(GeneratorTestResult result) => new(result);

    #endregion

    #region Helper Methods

    private static List<string> SplitParameters(string parameters)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var depth = 0;

        foreach (var c in parameters)
        {
            if (c == '<' || c == '(')
            {
                depth++;
            }
            else if (c == '>' || c == ')')
            {
                depth--;
            }
            else if (c == ',' && depth == 0)
            {
                result.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0)
            result.Add(current.ToString());

        return result;
    }

    private static void FlattenObject(Dictionary<string, object> source,
        string prefix,
        Dictionary<string, string> target)
    {
        foreach (var kvp in source)
        {
            var key = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}:{kvp.Key}";

            if (kvp.Value is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.Object)
                {
                    var nested = JsonSerializer.Deserialize<Dictionary<string, object>>(element.GetRawText());
                    if (nested != null)
                        FlattenObject(nested, key, target);
                }
                else
                {
                    target[key] = element.ToString();
                }
            }
            else
            {
                target[key] = kvp.Value?.ToString() ?? "";
            }
        }
    }

    private static bool IsPrimitiveType(string type)
    {
        var primitiveTypes = new[]
        {
            "string", "int", "bool", "double", "decimal", "long", "DateTime", "TimeSpan", "Guid"
        };
        return primitiveTypes.Any(p => type.Contains(p));
    }

    private static string FormatDefaultValue(string type,
        string defaultValue)
    {
        if (type.Contains("string"))
            return $"\"{defaultValue}\"";
        if (type.Contains("bool"))
            return defaultValue.ToLowerInvariant();
        if (type.Contains("TimeSpan"))
            return $"global::System.TimeSpan.Parse(\"{defaultValue}\")";
        if (type.Contains("DateTime"))
            return $"global::System.DateTime.Parse(\"{defaultValue}\")";
        return defaultValue;
    }

    #endregion

    #region Data Classes

    /// <summary>
    ///     Represents a constructor parameter
    /// </summary>
    public record ConstructorParameter(string Type, string Name);

    /// <summary>
    ///     Represents a field declaration
    /// </summary>
    public record FieldDeclaration(string Visibility, string Type, string Name, bool IsReadonly);

    /// <summary>
    ///     Represents a service registration
    /// </summary>
    public record ServiceRegistration(string ServiceType, string ImplementationType, string Lifetime);

    #endregion
}

/// <summary>
///     Extension methods for enhanced testing
/// </summary>
public static class TestExtensions
{
    /// <summary>
    ///     Extension method to check if string is null or empty
    /// </summary>
    public static bool IsNullOrEmpty(this string? value) => string.IsNullOrEmpty(value);

    /// <summary>
    ///     Extension method to get services of a specific type safely
    /// </summary>
    public static IEnumerable<T> GetServicesOfType<T>(this IServiceProvider serviceProvider) where T : class
    {
        try
        {
            return serviceProvider.GetServices<T>();
        }
        catch
        {
            return Enumerable.Empty<T>();
        }
    }

    /// <summary>
    ///     Extension method to check if service is registered
    /// </summary>
    public static bool IsRegistered<T>(this IServiceProvider serviceProvider) where T : notnull
    {
        try
        {
            serviceProvider.GetRequiredService<T>();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     Extension method to check if service is registered by type
    /// </summary>
    public static bool IsRegistered(this IServiceProvider serviceProvider,
        Type serviceType)
    {
        try
        {
            serviceProvider.GetRequiredService(serviceType);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
