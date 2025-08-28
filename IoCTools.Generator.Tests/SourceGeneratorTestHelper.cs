namespace IoCTools.Generator.Tests;

using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Reflection;
using System.Runtime;
using System.Text;

using Abstractions.Annotations;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
///     ULTIMATE SOURCE GENERATOR TEST HELPER
///     This class provides rock-solid infrastructure for testing every possible scenario
/// </summary>
public static class SourceGeneratorTestHelper
{
    /// <summary>
    ///     Compiles source code with the IoCTools generator and returns results
    /// </summary>
    public static GeneratorTestResult CompileWithGenerator(string sourceCode,
        bool includeSystemReferences = true,
        Dictionary<string, string>? analyzerBuildProperties = null)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode,
            new CSharpParseOptions(LanguageVersion.Preview));

        // Start with essential system references to ensure fundamental types are available
        var references = new HashSet<string>(); // Use HashSet to avoid duplicates
        var metadataRefs = new List<MetadataReference>();

        // Add System.Private.CoreLib first (contains object, TimeSpan, DateTime, etc.)
        var corelibLocation = typeof(object).Assembly.Location;
        if (!references.Contains(corelibLocation))
        {
            references.Add(corelibLocation);
            metadataRefs.Add(MetadataReference.CreateFromFile(corelibLocation));
        }

        // Add essential system assemblies
        var essentialTypes = new[]
        {
            typeof(TimeSpan), typeof(DateTime), typeof(decimal), typeof(Attribute), typeof(IEnumerable<>),
            typeof(GCSettings)
        };

        foreach (var type in essentialTypes)
        {
            var location = type.Assembly.Location;
            if (!references.Contains(location))
            {
                references.Add(location);
                metadataRefs.Add(MetadataReference.CreateFromFile(location));
            }
        }

        // Add IoCTools references - CRITICAL: Include ALL essential attributes
        var iocToolsTypes = new[]
        {
            typeof(ScopedAttribute), typeof(SingletonAttribute), typeof(TransientAttribute), typeof(InjectAttribute),
            typeof(InjectConfigurationAttribute), typeof(ConditionalServiceAttribute), typeof(ExternalServiceAttribute),
            typeof(RegisterAsAllAttribute),
            typeof(RegisterAsAttribute<object>), // CRITICAL FIX: Add RegisterAs generic attribute variants
            typeof(RegisterAsAttribute<object, object>), typeof(RegisterAsAttribute<object, object, object>),
            typeof(SkipRegistrationAttribute), typeof(IServiceCollection), typeof(ServiceCollectionServiceExtensions),
            typeof(ServiceCollectionContainerBuilderExtensions)
        };

        foreach (var type in iocToolsTypes)
        {
            var location = type.Assembly.Location;
            if (!references.Contains(location))
            {
                references.Add(location);
                metadataRefs.Add(MetadataReference.CreateFromFile(location));
            }
        }

        // Add additional optional references
        var optionalTypes = new[] { typeof(BackgroundService), typeof(Task), typeof(CancellationToken) };

        foreach (var type in optionalTypes)
            try
            {
                var location = type.Assembly.Location;
                if (!references.Contains(location))
                {
                    references.Add(location);
                    metadataRefs.Add(MetadataReference.CreateFromFile(location));
                }
            }
            catch (Exception)
            {
                // Skip if not available
            }

        // Add configuration and additional assembly references
        var configTypes = new[] { typeof(IConfiguration), typeof(IOptions<>), typeof(ILogger<>) };

        // Add data annotations types
        var dataAnnotationTypes = new[]
        {
            typeof(RequiredAttribute), typeof(RangeAttribute), typeof(StringLengthAttribute)
        };

        foreach (var type in configTypes)
            try
            {
                var location = type.Assembly.Location;
                if (!references.Contains(location))
                {
                    references.Add(location);
                    metadataRefs.Add(MetadataReference.CreateFromFile(location));
                }
            }
            catch (Exception)
            {
                // Skip if not available
            }

        foreach (var type in dataAnnotationTypes)
            try
            {
                var location = type.Assembly.Location;
                if (!references.Contains(location))
                {
                    references.Add(location);
                    metadataRefs.Add(MetadataReference.CreateFromFile(location));
                }
            }
            catch (Exception)
            {
                // Skip if not available
            }

        // Add additional assemblies by name
        var additionalAssemblies = new[]
        {
            "netstandard", "System.Runtime", "System.Collections.Concurrent", "System.Collections.Specialized",
            "System.Collections.Immutable", "Microsoft.Extensions.Configuration.Binder",
            "System.ComponentModel.Primitives", "System.ComponentModel", "System.ComponentModel.DataAnnotations"
        };

        foreach (var assemblyName in additionalAssemblies)
            try
            {
                var assembly = Assembly.Load(assemblyName);
                var location = assembly.Location;
                if (!references.Contains(location))
                {
                    references.Add(location);
                    metadataRefs.Add(MetadataReference.CreateFromFile(location));
                }
            }
            catch (Exception)
            {
                // Skip if not available
            }

        // Add references for common external assembly types used in tests
        var externalTypes = new[]
        {
            typeof(Uri), typeof(TextWriter), typeof(Console), typeof(Path), typeof(Directory), typeof(File),
            typeof(Activator), typeof(Environment)
        };

        foreach (var type in externalTypes)
            try
            {
                var location = type.Assembly.Location;
                if (!references.Contains(location))
                {
                    references.Add(location);
                    metadataRefs.Add(MetadataReference.CreateFromFile(location));
                }
            }
            catch (Exception)
            {
                // Skip if not available
            }

        if (includeSystemReferences)
        {
            // Add comprehensive system references
            var systemAssemblies = new[]
            {
                "System.Runtime", "System.Collections", "System.Private.CoreLib", "System.Linq", "netstandard"
            };

            foreach (var assemblyName in systemAssemblies)
                try
                {
                    var assembly = Assembly.Load(assemblyName);
                    var location = assembly.Location;
                    if (!references.Contains(location))
                    {
                        references.Add(location);
                        metadataRefs.Add(MetadataReference.CreateFromFile(location));
                    }
                }
                catch
                {
                    // Skip if assembly not found
                }
        }

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            metadataRefs,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: false,
                nullableContextOptions: NullableContextOptions.Enable));

        // Run the source generator
        var generator = new DependencyInjectionGenerator();
        var additionalTexts = new List<AdditionalText>();
        if (analyzerBuildProperties is not null && analyzerBuildProperties.Count > 0)
            additionalTexts.Add(CreateEditorConfig(analyzerBuildProperties));

        var driver = CSharpGeneratorDriver.Create(new[] { generator.AsSourceGenerator() },
            additionalTexts.ToArray(),
            new CSharpParseOptions(LanguageVersion.Preview));

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Extract results
        var generatedSources = new List<GeneratedSource>();
        var originalSyntaxTreeCount = 1; // Original source

        foreach (var tree in outputCompilation.SyntaxTrees.Skip(originalSyntaxTreeCount))
        {
            var hint = tree.FilePath; // Generated file hint
            var content = tree.ToString();
            generatedSources.Add(new GeneratedSource(hint, content));
        }

        return new GeneratorTestResult(
            outputCompilation,
            generatedSources,
            diagnostics.ToList(),
            outputCompilation.GetDiagnostics().ToList()
        );
    }

    /// <summary>
    ///     Gets standard references for compilation
    /// </summary>
    public static List<MetadataReference> GetStandardReferences()
    {
        var references = new List<MetadataReference>
        {
            // Core .NET references
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IEnumerable<>).Assembly.Location),

            // IoCTools references
            MetadataReference.CreateFromFile(typeof(ScopedAttribute).Assembly.Location),

            // DI references
            MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ServiceCollectionServiceExtensions).Assembly.Location)
        };

        // Add BuildServiceProvider extension method reference
        try
        {
            references.Add(
                MetadataReference.CreateFromFile(typeof(ServiceCollectionContainerBuilderExtensions).Assembly
                    .Location));
        }
        catch
        {
            // Skip if not available
        }

        // Add Configuration references for conditional services
        try
        {
            references.Add(MetadataReference.CreateFromFile(typeof(IConfiguration).Assembly.Location));
        }
        catch
        {
            // Skip if not available
        }

        try
        {
            references.Add(MetadataReference.CreateFromFile(typeof(ConfigurationBinder).Assembly.Location));
        }
        catch
        {
            // Skip if not available - try alternative approach
            try
            {
                var binderAssembly = Assembly.Load("Microsoft.Extensions.Configuration.Binder");
                references.Add(MetadataReference.CreateFromFile(binderAssembly.Location));
            }
            catch
            {
                // Skip if still not available
            }
        }

        return references;
    }

    /// <summary>
    ///     Creates a basic service class source code template
    /// </summary>
    public static string CreateServiceTemplate(string className,
        string[]? dependencies = null,
        string[]? attributes = null,
        string? baseClass = null)
    {
        var usings = new List<string>
        {
            "using IoCTools.Abstractions.Annotations;", "using System.Collections.Generic;"
        };

        var classAttributes = attributes != null ? string.Join("\n", attributes.Select(a => $"[{a}]")) : "[Scoped]";
        var baseClause = baseClass != null ? $" : {baseClass}" : "";

        var dependencyFields = dependencies != null
            ? string.Join("\n    ", dependencies.Select((dep,
                    i) => $"[Inject] private readonly {dep} _dep{i};"))
            : "";

        return $@"
{string.Join("\n", usings)}

namespace TestNamespace;

{classAttributes}
public partial class {className}{baseClause}
{{
    {dependencyFields}
}}";
    }

    #region Helper Methods

    /// <summary>
    ///     Gets the default value for a given type
    /// </summary>
    private static object? GetDefaultValue(Type type)
    {
        if (type.IsValueType) return Activator.CreateInstance(type);
        return null;
    }

    #endregion

    #region Runtime Testing Infrastructure

    /// <summary>
    ///     Compiles generated code and creates actual service instances for runtime testing
    /// </summary>
    /// <param name="testResult">The generator test result containing compiled code</param>
    /// <returns>A runtime test context with compiled assembly and service provider</returns>
    public static RuntimeTestContext CreateRuntimeContext(GeneratorTestResult testResult)
    {
        if (testResult.HasErrors)
            throw new InvalidOperationException(
                $"Cannot create runtime context from compilation with errors: {string.Join(", ", testResult.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.GetMessage()))}");

        using var stream = new MemoryStream();
        var emitResult = testResult.Compilation.Emit(stream);

        if (!emitResult.Success)
            throw new InvalidOperationException(
                $"Failed to emit assembly: {string.Join(", ", emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.GetMessage()))}");

        stream.Seek(0, SeekOrigin.Begin);
        var assembly = Assembly.Load(stream.ToArray());

        return new RuntimeTestContext(assembly, testResult);
    }

    /// <summary>
    ///     Builds a ServiceProvider using generated registration methods from the compiled assembly
    /// </summary>
    /// <param name="context">Runtime test context</param>
    /// <param name="configureServices">Optional additional service configuration</param>
    /// <param name="configuration">Optional configuration instance</param>
    /// <returns>Configured service provider</returns>
    public static IServiceProvider BuildServiceProvider(RuntimeTestContext context,
        Action<IServiceCollection>? configureServices = null,
        IConfiguration? configuration = null)
    {
        var services = new ServiceCollection();

        // Add configuration if provided
        if (configuration != null) services.AddSingleton(configuration);

        // Add logging for testing
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));

        // Find and invoke generated registration methods
        var extensionTypes = context.Assembly.GetTypes()
            .Where(t => t.Name.Contains("ServiceCollectionExtensions") || t.Name.Contains("Extensions"))
            .ToList();

        foreach (var extensionType in extensionTypes)
        {
            var registrationMethods = extensionType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name.StartsWith("Add") &&
                            m.GetParameters().Length > 0 &&
                            m.GetParameters()[0].ParameterType == typeof(IServiceCollection))
                .ToList();

            foreach (var method in registrationMethods)
                try
                {
                    var parameters = method.GetParameters();
                    var args = new object[parameters.Length];
                    args[0] = services;

                    // Fill in default values for optional parameters
                    for (var i = 1; i < parameters.Length; i++)
                    {
                        var param = parameters[i];
                        if (param.HasDefaultValue)
                            args[i] = param.DefaultValue;
                        else if (param.ParameterType == typeof(IConfiguration))
                            args[i] = configuration ?? CreateEmptyConfiguration();
                        else
                            args[i] = GetDefaultValue(param.ParameterType);
                    }

                    method.Invoke(null, args);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to invoke registration method {method.Name}: {ex.Message}", ex);
                }
        }

        // Apply additional configuration
        configureServices?.Invoke(services);

        return services.BuildServiceProvider();
    }

    /// <summary>
    ///     Resolves a service from the provider and validates its properties were injected correctly
    /// </summary>
    /// <typeparam name="T">Service type to resolve</typeparam>
    /// <param name="serviceProvider">Service provider</param>
    /// <param name="validateProperties">Optional property validation function</param>
    /// <returns>Resolved service instance</returns>
    public static T ResolveAndValidateService<T>(IServiceProvider serviceProvider,
        Action<T>? validateProperties = null)
    {
        var service = serviceProvider.GetRequiredService<T>();
        validateProperties?.Invoke(service);
        return service;
    }

    /// <summary>
    ///     Tests service lifetime behavior by resolving services multiple times
    /// </summary>
    /// <typeparam name="T">Service type to test</typeparam>
    /// <param name="serviceProvider">Service provider</param>
    /// <param name="expectedLifetime">Expected service lifetime</param>
    public static void ValidateServiceLifetime<T>(IServiceProvider serviceProvider,
        ServiceLifetime expectedLifetime)
    {
        var service1 = serviceProvider.GetRequiredService<T>();
        var service2 = serviceProvider.GetRequiredService<T>();

        switch (expectedLifetime)
        {
            case ServiceLifetime.Singleton:
                if (!ReferenceEquals(service1, service2))
                    throw new InvalidOperationException(
                        $"Service {typeof(T).Name} should be singleton but got different instances");
                break;
            case ServiceLifetime.Transient:
                if (ReferenceEquals(service1, service2))
                    throw new InvalidOperationException(
                        $"Service {typeof(T).Name} should be transient but got same instance");
                break;
            case ServiceLifetime.Scoped:
                // For scoped, we need to test within a scope
                using (var scope1 = serviceProvider.CreateScope())
                using (var scope2 = serviceProvider.CreateScope())
                {
                    var scoped1a = scope1.ServiceProvider.GetRequiredService<T>();
                    var scoped1b = scope1.ServiceProvider.GetRequiredService<T>();
                    var scoped2 = scope2.ServiceProvider.GetRequiredService<T>();

                    if (!ReferenceEquals(scoped1a, scoped1b))
                        throw new InvalidOperationException($"Service {typeof(T).Name} should be same within scope");
                    if (ReferenceEquals(scoped1a, scoped2))
                        throw new InvalidOperationException(
                            $"Service {typeof(T).Name} should be different across scopes");
                }

                break;
        }
    }

    #endregion

    #region Configuration Testing Helpers

    /// <summary>
    ///     Creates an IConfiguration instance from a dictionary of key-value pairs
    /// </summary>
    /// <param name="configurationData">Configuration data</param>
    /// <returns>Configuration instance</returns>
    public static IConfiguration CreateConfiguration(Dictionary<string, string> configurationData) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

    /// <summary>
    ///     Creates a complex configuration hierarchy with nested sections
    /// </summary>
    /// <param name="sections">Dictionary of section names to configuration data</param>
    /// <returns>Configuration instance with nested sections</returns>
    public static IConfiguration CreateHierarchicalConfiguration(
        Dictionary<string, Dictionary<string, string>> sections)
    {
        var flatConfig = new Dictionary<string, string>();

        foreach (var section in sections)
        foreach (var kvp in section.Value)
            flatConfig[$"{section.Key}:{kvp.Key}"] = kvp.Value;

        return CreateConfiguration(flatConfig);
    }

    /// <summary>
    ///     Creates configuration from a JSON string
    /// </summary>
    /// <param name="jsonContent">JSON configuration content</param>
    /// <returns>Configuration instance</returns>
    public static IConfiguration CreateConfigurationFromJson(string jsonContent)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonContent));
        return new ConfigurationBuilder()
            .AddJsonStream(stream)
            .Build();
    }

    /// <summary>
    ///     Creates configuration with intentionally missing values for error testing
    /// </summary>
    /// <param name="existingKeys">Keys that should exist</param>
    /// <returns>Configuration instance with limited data</returns>
    public static IConfiguration CreatePartialConfiguration(params string[] existingKeys)
    {
        var config = new Dictionary<string, string>();
        for (var i = 0; i < existingKeys.Length; i++) config[existingKeys[i]] = $"Value{i}";
        return CreateConfiguration(config);
    }

    /// <summary>
    ///     Creates an empty configuration instance for testing
    /// </summary>
    /// <returns>Empty configuration instance</returns>
    public static IConfiguration CreateEmptyConfiguration() => new ConfigurationBuilder().Build();

    /// <summary>
    ///     Creates a configuration instance that binds to a specific type
    /// </summary>
    /// <typeparam name="T">Type to bind configuration to</typeparam>
    /// <param name="instance">Instance to extract configuration from</param>
    /// <param name="sectionName">Optional section name</param>
    /// <returns>Configuration instance</returns>
    public static IConfiguration CreateConfigurationForType<T>(T instance,
        string? sectionName = null)
    {
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetMethod != null);

        var config = new Dictionary<string, string>();
        var prefix = sectionName != null ? $"{sectionName}:" : "";

        foreach (var property in properties)
        {
            var value = property.GetValue(instance);
            if (value != null) config[$"{prefix}{property.Name}"] = value.ToString() ?? "";
        }

        return CreateConfiguration(config);
    }

    #endregion

    #region Environment Testing Helpers

    private static readonly ConcurrentDictionary<string, string?> _originalEnvironmentValues = new();

    /// <summary>
    ///     Safely sets an environment variable and remembers the original value for restoration
    /// </summary>
    /// <param name="name">Environment variable name</param>
    /// <param name="value">New value (null to remove)</param>
    public static void SetEnvironmentVariable(string name,
        string? value)
    {
        if (!_originalEnvironmentValues.ContainsKey(name))
            _originalEnvironmentValues[name] = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    /// <summary>
    ///     Restores all environment variables to their original values
    /// </summary>
    public static void RestoreEnvironmentVariables()
    {
        foreach (var kvp in _originalEnvironmentValues) Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
        _originalEnvironmentValues.Clear();
    }

    /// <summary>
    ///     Creates a test environment context with specific environment variables
    /// </summary>
    /// <param name="environmentVariables">Environment variables to set</param>
    /// <returns>Disposable context that restores environment on disposal</returns>
    public static TestEnvironmentContext CreateTestEnvironment(Dictionary<string, string?> environmentVariables) =>
        new(environmentVariables);

    /// <summary>
    ///     Sets the ASPNETCORE_ENVIRONMENT variable for testing
    /// </summary>
    /// <param name="environment">Environment name (Development, Staging, Production, etc.)</param>
    public static void SetAspNetCoreEnvironment(string environment) =>
        SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", environment);

    /// <summary>
    ///     Tests conditional service registration based on environment
    /// </summary>
    /// <param name="testCode">Test code with conditional services</param>
    /// <param name="environments">Environments to test</param>
    /// <param name="expectedServiceCounts">Expected service count for each environment</param>
    public static void TestConditionalRegistration(string testCode,
        string[] environments,
        Dictionary<string, int> expectedServiceCounts)
    {
        foreach (var env in environments)
        {
            using var envContext = CreateTestEnvironment(new Dictionary<string, string?>
            {
                ["ASPNETCORE_ENVIRONMENT"] = env
            });

            var result = CompileWithGenerator(testCode);
            if (result.HasErrors)
                throw new InvalidOperationException(
                    $"Compilation failed for environment {env}: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.GetMessage()))}");

            var runtimeContext = CreateRuntimeContext(result);
            var serviceProvider = BuildServiceProvider(runtimeContext);

            // Validate expected service count
            if (expectedServiceCounts.TryGetValue(env, out var expectedCount))
            {
                var actualCount = serviceProvider.GetServices<object>().Count();
                if (actualCount != expectedCount)
                    throw new InvalidOperationException(
                        $"Environment {env}: expected {expectedCount} services, got {actualCount}");
            }
        }
    }

    #endregion

    #region MSBuild Configuration Simulation

    /// <summary>
    ///     Simulates MSBuild properties for diagnostic configuration testing
    /// </summary>
    /// <param name="sourceCode">Source code to compile</param>
    /// <param name="msBuildProperties">MSBuild properties to simulate</param>
    /// <returns>Generator test result with simulated MSBuild context</returns>
    public static GeneratorTestResult CompileWithMSBuildProperties(string sourceCode,
        Dictionary<string, string> msBuildProperties) =>
        // Reuse the rich setup that includes logging/config/etc and pass properties via .editorconfig
        CompileWithGenerator(sourceCode, true, msBuildProperties);

    /// <summary>
    ///     Compiles multiple source files with optional additional texts (e.g., custom .editorconfig content)
    /// </summary>
    public static GeneratorTestResult CompileWithAdditionalTexts(
        Dictionary<string, string> sources,
        List<AdditionalText>? additionalTexts = null,
        bool includeSystemReferences = true)
    {
        var trees = new List<SyntaxTree>();
        foreach (var kvp in sources)
            trees.Add(CSharpSyntaxTree.ParseText(kvp.Value, new CSharpParseOptions(LanguageVersion.Preview), kvp.Key));

        var refs = GetStandardReferences();
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            trees,
            refs,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: false,
                nullableContextOptions: NullableContextOptions.Enable));

        var generator = new DependencyInjectionGenerator();
        var driver = CSharpGeneratorDriver.Create(new[] { generator.AsSourceGenerator() },
            additionalTexts?.ToArray() ?? Array.Empty<AdditionalText>(),
            new CSharpParseOptions(LanguageVersion.Preview));

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var generatedSources = new List<GeneratedSource>();
        foreach (var tree in outputCompilation.SyntaxTrees.Skip(trees.Count))
            generatedSources.Add(new GeneratedSource(tree.FilePath, tree.ToString()));

        return new GeneratorTestResult(outputCompilation, generatedSources,
            diagnostics.ToList(), outputCompilation.GetDiagnostics().ToList());
    }

    public static AdditionalText CreateEditorConfigRaw(string content) =>
        new TestAdditionalFile("/AnalyzerConfig/.editorconfig", content);

    /// <summary>
    ///     Tests different diagnostic severity configurations
    /// </summary>
    /// <param name="sourceCode">Source code with potential diagnostic issues</param>
    /// <param name="severityConfigurations">Different severity configurations to test</param>
    /// <returns>Dictionary of configuration name to diagnostic results</returns>
    public static Dictionary<string, List<Diagnostic>> TestDiagnosticSeverities(string sourceCode,
        Dictionary<string, Dictionary<string, string>> severityConfigurations)
    {
        var results = new Dictionary<string, List<Diagnostic>>();

        foreach (var config in severityConfigurations)
        {
            var result = CompileWithMSBuildProperties(sourceCode, config.Value);
            results[config.Key] = result.Diagnostics.ToList();
        }

        return results;
    }

    #endregion

    #region Enhanced Diagnostic Validation

    /// <summary>
    ///     Validates that specific diagnostics are present with expected severity
    /// </summary>
    /// <param name="diagnostics">Diagnostics to check</param>
    /// <param name="expectedDiagnostics">Expected diagnostic codes and severities</param>
    public static void ValidateDiagnostics(IEnumerable<Diagnostic> diagnostics,
        Dictionary<string, DiagnosticSeverity> expectedDiagnostics)
    {
        var diagnosticList = diagnostics.ToList();

        foreach (var expected in expectedDiagnostics)
        {
            var actualDiagnostics = diagnosticList.Where(d => d.Id == expected.Key).ToList();

            if (!actualDiagnostics.Any())
                throw new InvalidOperationException($"Expected diagnostic {expected.Key} not found");

            var severityMismatch = actualDiagnostics.Where(d => d.Severity != expected.Value).ToList();
            if (severityMismatch.Any())
                throw new InvalidOperationException(
                    $"Diagnostic {expected.Key} has wrong severity. Expected: {expected.Value}, Actual: {string.Join(", ", severityMismatch.Select(d => d.Severity))}");
        }
    }

    /// <summary>
    ///     Validates diagnostic location information
    /// </summary>
    /// <param name="diagnostic">Diagnostic to validate</param>
    /// <param name="expectedLine">Expected line number (1-based)</param>
    /// <param name="expectedColumn">Expected column number (1-based)</param>
    public static void ValidateDiagnosticLocation(Diagnostic diagnostic,
        int expectedLine,
        int expectedColumn)
    {
        var location = diagnostic.Location;
        if (location == Location.None)
            throw new InvalidOperationException($"Diagnostic {diagnostic.Id} has no location information");

        var linePosition = location.GetLineSpan().StartLinePosition;
        var actualLine = linePosition.Line + 1; // Convert to 1-based
        var actualColumn = linePosition.Character + 1; // Convert to 1-based

        if (actualLine != expectedLine)
            throw new InvalidOperationException(
                $"Diagnostic {diagnostic.Id} at wrong line. Expected: {expectedLine}, Actual: {actualLine}");

        if (actualColumn != expectedColumn)
            throw new InvalidOperationException(
                $"Diagnostic {diagnostic.Id} at wrong column. Expected: {expectedColumn}, Actual: {actualColumn}");
    }

    /// <summary>
    ///     Groups diagnostics by severity for analysis
    /// </summary>
    /// <param name="diagnostics">Diagnostics to group</param>
    /// <returns>Dictionary of severity to diagnostic list</returns>
    public static Dictionary<DiagnosticSeverity, List<Diagnostic>> GroupDiagnosticsBySeverity(
        IEnumerable<Diagnostic> diagnostics)
    {
        return diagnostics.GroupBy(d => d.Severity)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    #endregion

    #region Performance Testing Infrastructure

    private static AdditionalText CreateEditorConfig(Dictionary<string, string> buildProperties)
    {
        var sb = new StringBuilder();
        sb.AppendLine("root = true");
        sb.AppendLine("is_global = true");
        foreach (var kvp in buildProperties)
        {
            var key = kvp.Key.StartsWith("build_property.") ? kvp.Key : $"build_property.{kvp.Key}";
            sb.AppendLine($"{key} = {kvp.Value}");
        }

        sb.AppendLine();
        sb.AppendLine("[*.cs]");
        foreach (var kvp in buildProperties)
        {
            var key = kvp.Key.StartsWith("build_property.") ? kvp.Key : $"build_property.{kvp.Key}";
            sb.AppendLine($"{key} = {kvp.Value}");
        }

        return new TestAdditionalFile("/AnalyzerConfig/.editorconfig", sb.ToString());
    }

    /// <summary>
    ///     Measures source generation performance
    /// </summary>
    /// <param name="sourceCode">Source code to test</param>
    /// <param name="iterations">Number of iterations to run</param>
    /// <returns>Performance measurements</returns>
    public static PerformanceTestResult MeasureGenerationPerformance(string sourceCode,
        int iterations = 10)
    {
        var times = new List<TimeSpan>();
        var memoryUsages = new List<long>();

        for (var i = 0; i < iterations; i++)
        {
            var initialMemory = GC.GetTotalMemory(true);
            var stopwatch = Stopwatch.StartNew();

            var result = CompileWithGenerator(sourceCode);

            stopwatch.Stop();
            var finalMemory = GC.GetTotalMemory(false);

            times.Add(stopwatch.Elapsed);
            memoryUsages.Add(finalMemory - initialMemory);
        }

        return new PerformanceTestResult(times, memoryUsages);
    }

    /// <summary>
    ///     Tests service resolution performance
    /// </summary>
    /// <param name="serviceProvider">Service provider to test</param>
    /// <param name="serviceType">Type of service to resolve</param>
    /// <param name="iterations">Number of resolution attempts</param>
    /// <returns>Performance measurements for service resolution</returns>
    public static PerformanceTestResult MeasureResolutionPerformance(IServiceProvider serviceProvider,
        Type serviceType,
        int iterations = 1000)
    {
        var times = new List<TimeSpan>();

        for (var i = 0; i < iterations; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            var service = serviceProvider.GetRequiredService(serviceType);
            stopwatch.Stop();
            times.Add(stopwatch.Elapsed);
        }

        return new PerformanceTestResult(times, new List<long>());
    }

    /// <summary>
    ///     Creates a large-scale test scenario with many services
    /// </summary>
    /// <param name="serviceCount">Number of services to generate</param>
    /// <param name="dependencyDepth">Maximum dependency depth</param>
    /// <returns>Source code for large-scale testing</returns>
    public static string CreateLargeScaleTestCode(int serviceCount,
        int dependencyDepth = 3)
    {
        var services = new List<string>();

        for (var i = 0; i < serviceCount; i++)
        {
            var dependencies = new List<string>();
            var dependencyCount = Math.Min(dependencyDepth, i);

            for (var j = 0; j < dependencyCount; j++) dependencies.Add($"IService{j}");

            var serviceCode = CreateServiceTemplate($"Service{i}",
                dependencies.ToArray(),
                new[] { "Service" });

            services.Add(serviceCode);

            // Also create interface
            services.Add($"public interface IService{i} {{ }}");
        }

        return string.Join("\n\n", services);
    }

    #endregion

    #region Error Injection and Testing

    /// <summary>
    ///     Injects compilation errors for resilience testing
    /// </summary>
    /// <param name="validSourceCode">Valid source code</param>
    /// <param name="errorType">Type of error to inject</param>
    /// <returns>Source code with injected error</returns>
    public static string InjectCompilationError(string validSourceCode,
        CompilationErrorType errorType)
    {
        return errorType switch
        {
            CompilationErrorType.SyntaxError => validSourceCode.Replace("{", "{ invalid syntax here"),
            CompilationErrorType.MissingUsing =>
                validSourceCode.Replace("using IoCTools.Abstractions.Annotations;", ""),
            CompilationErrorType.InvalidAttribute => validSourceCode.Replace("[Scoped]", "[NonExistentAttribute]"),
            CompilationErrorType.MissingPartial => validSourceCode.Replace("partial class", "class"),
            _ => validSourceCode
        };
    }

    /// <summary>
    ///     Tests error recovery scenarios
    /// </summary>
    /// <param name="sourceCode">Source code to test</param>
    /// <param name="expectedRecovery">Whether the generator should recover</param>
    public static void TestErrorRecovery(string sourceCode,
        bool expectedRecovery)
    {
        var result = CompileWithGenerator(sourceCode);

        if (expectedRecovery && result.HasErrors)
            throw new InvalidOperationException("Generator should have recovered from errors but compilation failed");

        if (!expectedRecovery && !result.HasErrors)
            throw new InvalidOperationException("Generator should have failed but compilation succeeded");
    }

    /// <summary>
    ///     Validates exception handling in generated code
    /// </summary>
    /// <param name="context">Runtime test context</param>
    /// <param name="exceptionTestCases">Test cases that should throw exceptions</param>
    public static void ValidateExceptionHandling(RuntimeTestContext context,
        Dictionary<Type, Action<IServiceProvider>> exceptionTestCases)
    {
        var serviceProvider = BuildServiceProvider(context);

        foreach (var testCase in exceptionTestCases)
        {
            var exceptionThrown = false;
            try
            {
                testCase.Value(serviceProvider);
            }
            catch (Exception ex) when (ex.GetType() == testCase.Key || ex.InnerException?.GetType() == testCase.Key)
            {
                exceptionThrown = true;
            }

            if (!exceptionThrown)
                throw new InvalidOperationException($"Expected exception of type {testCase.Key.Name} was not thrown");
        }
    }

    #endregion
}

/// <summary>
///     Result of running source generator on test code
/// </summary>
public record GeneratorTestResult(
    Compilation Compilation,
    List<GeneratedSource> GeneratedSources,
    List<Diagnostic> GeneratorDiagnostics,
    List<Diagnostic> CompilationDiagnostics
)
{
    public bool HasErrors => CompilationDiagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
    public bool HasWarnings => CompilationDiagnostics.Any(d => d.Severity == DiagnosticSeverity.Warning);
    public int ErrorCount => CompilationDiagnostics.Count(d => d.Severity == DiagnosticSeverity.Error);
    public int WarningCount => CompilationDiagnostics.Count(d => d.Severity == DiagnosticSeverity.Warning);
    public IEnumerable<Diagnostic> Diagnostics => CompilationDiagnostics.Concat(GeneratorDiagnostics);

    public GeneratedSource? GetConstructorSource(string className)
    {
        return GeneratedSources.FirstOrDefault(s =>
        {
            // Handle both non-generic and generic class names
            // For "ValueTypeService", match both "ValueTypeService" and "ValueTypeService<T>"
            var hasPartialClass = s.Content.Contains($"partial class {className}") ||
                                  s.Content.Contains($"partial class {className}<") ||
                                  s.Content.Contains($"partial record {className}") ||
                                  s.Content.Contains($"partial record {className}<");

            // For constructor matching, look for the class name followed by either "(" or "<" then later "("
            var hasConstructor = s.Content.Contains($"{className}(") ||
                                 (s.Content.Contains($"{className}<") && s.Content.Contains("("));

            return hasPartialClass && hasConstructor;
        });
    }

    public GeneratedSource? GetServiceRegistrationSource()
    {
        return GeneratedSources.FirstOrDefault(s =>
            s.Content.Contains("ServiceCollectionExtensions") ||
            s.Content.Contains("GeneratedServiceCollectionExtensions") ||
            s.Content.Contains("AddIoCTools") ||
            s.Content.Contains("RegisteredServices") ||
            s.Content.Contains("IServiceCollection services"));
    }

    public List<Diagnostic> GetDiagnosticsByCode(string code)
    {
        // Check both compilation diagnostics AND generator diagnostics
        return CompilationDiagnostics.Concat(GeneratorDiagnostics).Where(d => d.Id == code).ToList();
    }
}

/// <summary>
///     A single generated source file
/// </summary>
public record GeneratedSource(string Hint, string Content);

/// <summary>
///     Runtime test context containing compiled assembly and test result
/// </summary>
public class RuntimeTestContext
{
    public RuntimeTestContext(Assembly assembly,
        GeneratorTestResult testResult)
    {
        Assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
        TestResult = testResult ?? throw new ArgumentNullException(nameof(testResult));
    }

    public Assembly Assembly { get; }
    public GeneratorTestResult TestResult { get; }
}

/// <summary>
///     Performance test result containing timing and memory measurements
/// </summary>
public class PerformanceTestResult
{
    public PerformanceTestResult(List<TimeSpan> executionTimes,
        List<long> memoryUsages)
    {
        ExecutionTimes = executionTimes ?? throw new ArgumentNullException(nameof(executionTimes));
        MemoryUsages = memoryUsages ?? throw new ArgumentNullException(nameof(memoryUsages));
    }

    public List<TimeSpan> ExecutionTimes { get; }
    public List<long> MemoryUsages { get; }

    public TimeSpan AverageTime => TimeSpan.FromTicks((long)ExecutionTimes.Select(t => t.Ticks).Average());
    public TimeSpan MinTime => ExecutionTimes.Min();
    public TimeSpan MaxTime => ExecutionTimes.Max();
    public long AverageMemory => MemoryUsages.Any() ? (long)MemoryUsages.Average() : 0;
    public long MinMemory => MemoryUsages.Any() ? MemoryUsages.Min() : 0;
    public long MaxMemory => MemoryUsages.Any() ? MemoryUsages.Max() : 0;
}

/// <summary>
///     Test environment context that safely manages environment variables
/// </summary>
public class TestEnvironmentContext : IDisposable
{
    private readonly Dictionary<string, string?> _originalValues = new();
    private bool _disposed;

    public TestEnvironmentContext(Dictionary<string, string?> environmentVariables)
    {
        foreach (var kvp in environmentVariables)
        {
            _originalValues[kvp.Key] = Environment.GetEnvironmentVariable(kvp.Key);
            Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            foreach (var kvp in _originalValues) Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
            _disposed = true;
        }
    }
}

/// <summary>
///     Types of compilation errors that can be injected for testing
/// </summary>
public enum CompilationErrorType
{
    SyntaxError,
    MissingUsing,
    InvalidAttribute,
    MissingPartial,
    TypeNotFound,
    InvalidGeneric
}

/// <summary>
///     Test implementation of AdditionalText for MSBuild property simulation
/// </summary>
public class TestAdditionalFile : AdditionalText
{
    private readonly string _content;

    public TestAdditionalFile(string path,
        string content)
    {
        Path = path;
        _content = content;
    }

    public override string Path { get; }

    public override SourceText? GetText(CancellationToken cancellationToken = default) => SourceText.From(_content);
}
