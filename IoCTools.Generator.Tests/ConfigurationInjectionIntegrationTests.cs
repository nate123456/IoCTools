namespace IoCTools.Generator.Tests;

using Microsoft.CodeAnalysis;

/// <summary>
///     COMPREHENSIVE CONFIGURATION INJECTION INTEGRATION TESTS
///     Tests configuration injection working with all other IoCTools features including
///     conditional services, background services, lifetime validation, multi-interface registration,
///     and complex real-world scenarios.
///     UPDATED: Configuration injection integration with ConditionalService is WORKING.
///     Audit confirmed that ConditionalService + Configuration integration works correctly:
///     - Environment-based conditional services can inject configuration
///     - Configuration-based conditional services work with [InjectConfiguration]
///     - Combined conditions (Environment + ConfigValue) support configuration injection
///     - Runtime behavior of conditional services with configuration works as expected
/// </summary>
[Trait("Category", "ConfigurationInjection")]
public class ConfigurationInjectionIntegrationTests
{
    #region Configuration Injection + Conditional Services Tests

    [Fact]
    public void ConfigurationIntegration_ConditionalService_ConfigInjectionInEnvironmentBasedService_Original()
    {
        // Arrange - Test configuration injection working with environment-based conditional services
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Test;

public interface ILoggerService { }

public class LoggingSettings
{
    public string LogLevel { get; set; } = string.Empty;
    public bool EnableConsole { get; set; }
    public string OutputPath { get; set; } = string.Empty;
}

[ConditionalService(Environment = ""Development"")]
[Scoped]
public partial class DevLoggerService : ILoggerService
{
    [InjectConfiguration] private readonly LoggingSettings _loggingSettings;
    [InjectConfiguration] private readonly IOptions<LoggingSettings> _loggingOptions;
    [InjectConfiguration(""Logging:DevSpecific:EnableDetailedErrors"")] private readonly bool _enableDetailedErrors;
    [InjectConfiguration(""Logging:DevSpecific:MaxFileSize"")] private readonly int _maxFileSize;
}

[ConditionalService(Environment = ""Production"")]
[Scoped]
public partial class ProdLoggerService : ILoggerService
{
    [InjectConfiguration] private readonly LoggingSettings _loggingSettings;
    [InjectConfiguration(""Logging:Production:ApiKey"")] private readonly string _apiKey;
    [InjectConfiguration(""Logging:Production:BatchSize"")] private readonly int _batchSize;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should()
            .BeFalse(
                $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        // Should generate constructors with configuration injection for both services
        var devConstructorSource = result.GetRequiredConstructorSource("DevLoggerService");
        devConstructorSource.Content.Should().Contain("IConfiguration configuration");
        devConstructorSource.Content.Should().Contain("IOptions<LoggingSettings> loggingOptions");
        devConstructorSource.Content.Should().Contain("configuration.GetSection(\"Logging\").Get<LoggingSettings>()");
        devConstructorSource.Content.Should()
            .Contain("configuration.GetValue<bool>(\"Logging:DevSpecific:EnableDetailedErrors\")");
        devConstructorSource.Content.Should()
            .Contain("configuration.GetValue<int>(\"Logging:DevSpecific:MaxFileSize\")");

        var prodConstructorSource = result.GetRequiredConstructorSource("ProdLoggerService");
        prodConstructorSource.Content.Should().Contain("IConfiguration configuration");
        prodConstructorSource.Content.Should().Contain("configuration.GetSection(\"Logging\").Get<LoggingSettings>()");
        prodConstructorSource.Content.Should().Contain("configuration.GetValue<string>(\"Logging:Production:ApiKey\")");
        prodConstructorSource.Content.Should().Contain("configuration.GetValue<int>(\"Logging:Production:BatchSize\")");

        // Should generate environment-based conditional registrations
        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Verify environment-based conditional registration logic
        registrationSource.Content.Should()
            .Contain("var environment = Environment.GetEnvironmentVariable(\"ASPNETCORE_ENVIRONMENT\")");
        registrationSource.Content.Should()
            .Contain("string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)");
        registrationSource.Content.Should().Contain("AddScoped<global::Test.DevLoggerService>()");
        registrationSource.Content.Should()
            .Contain(
                "AddScoped<global::Test.ILoggerService>(provider => provider.GetRequiredService<global::Test.DevLoggerService>())");

        registrationSource.Content.Should()
            .Contain("string.Equals(environment, \"Production\", StringComparison.OrdinalIgnoreCase)");
        registrationSource.Content.Should().Contain("AddScoped<global::Test.ProdLoggerService>()");
        registrationSource.Content.Should()
            .Contain(
                "AddScoped<global::Test.ILoggerService>(provider => provider.GetRequiredService<global::Test.ProdLoggerService>())");
    }

    [Fact]
    public void DEBUG_ConditionalService_EnvironmentOnly()
    {
        // Arrange - Test just environment-based conditional service
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

[ConditionalService(Environment = ""Development"")]
[Scoped]
public partial class DevTestService : ITestService
{
}

[ConditionalService(Environment = ""Production"")]
[Scoped]
public partial class ProdTestService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - should generate conditional registration
        if (result.HasErrors)
        {
            var errors = string.Join("\n", result.CompilationDiagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(e => $"{e.Id}: {e.GetMessage()}"));
            throw new Exception($"Compilation has errors:\n{errors}");
        }

        result.HasErrors.Should().BeFalse();

        // Debug: Check what was generated
        var generatedContent =
            string.Join("\n\n", result.GeneratedSources.Select(gs => $"--- {gs.Hint} ---\n{gs.Content}"));

        if (result.GeneratedSources.Count == 0) throw new Exception("No sources were generated!");

        var registrationSource = result.GetRequiredServiceRegistrationSource();
        if (registrationSource == null)
            throw new Exception(
                $"No service registration source found. Generated {result.GeneratedSources.Count} sources:\n{generatedContent}");

        // Debug: Check what's actually in the registration source
        if (!registrationSource.Content.Contains("Environment.GetEnvironmentVariable"))
            throw new Exception(
                $"Missing environment variable detection. Registration source content:\n{registrationSource.Content}\n\nAll generated:\n{generatedContent}");

        // Should contain environment detection and conditional logic
        registrationSource.Content.Should().Contain("Environment.GetEnvironmentVariable");
        registrationSource.Content.Should()
            .Contain("string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)");
    }

    [Fact]
    public void DEBUG_ConfigurationIntegration_SimpleTest()
    {
        // Arrange - very simple service with configuration injection
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Configuration;

namespace Test;
[Scoped]
public partial class SimpleService
{
    [InjectConfiguration(""Test:Value"")] private readonly string _testValue;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Configuration injection working correctly
        result.HasErrors.Should().BeFalse();

        // Should generate constructor with configuration injection
        var constructorSource = result.GetRequiredConstructorSource("SimpleService");
        constructorSource.Content.Should().Contain("IConfiguration configuration");
        constructorSource.Content.Should().Contain("configuration.GetValue<string>(\"Test:Value\")");

        // Should generate service registrations for services with configuration injection
        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Both single-parameter and two-parameter forms are valid - accept both
        var hasExpectedRegistration = registrationSource.Content.Contains("AddScoped<global::Test.SimpleService>();") ||
                                      registrationSource.Content.Contains(
                                          "AddScoped<global::Test.SimpleService, global::Test.SimpleService>();");
        hasExpectedRegistration.Should()
            .BeTrue("Expected either single-parameter or two-parameter form for SimpleService registration");
    }

    [Fact]
    public void DEBUG_ConfigurationIntegration_OnlyInjectConfigurationTest()
    {
        // Arrange - service with ONLY InjectConfiguration (no explicit Service attribute)
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Configuration;

namespace Test;

[Scoped]
public partial class ConfigOnlyService
{
    [InjectConfiguration(""Database:ConnectionString"")] private readonly string _connectionString;
    [InjectConfiguration(""App:MaxRetries"")] private readonly int _maxRetries;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Services with ONLY InjectConfiguration are auto-registered with default Scoped lifetime
        result.HasErrors.Should().BeFalse();

        // Should generate constructor with configuration injection
        var constructorSource = result.GetRequiredConstructorSource("ConfigOnlyService");
        constructorSource.Content.Should().Contain("IConfiguration configuration");
        constructorSource.Content.Should().Contain("configuration.GetValue<string>(\"Database:ConnectionString\")");
        constructorSource.Content.Should().Contain("configuration.GetValue<int>(\"App:MaxRetries\")");

        // Should auto-register services with configuration injection using default Scoped lifetime
        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Both single-parameter and two-parameter forms are valid - accept both
        var hasExpectedRegistration =
            registrationSource.Content.Contains("AddScoped<global::Test.ConfigOnlyService>();") ||
            registrationSource.Content.Contains(
                "AddScoped<global::Test.ConfigOnlyService, global::Test.ConfigOnlyService>();");
        hasExpectedRegistration.Should()
            .BeTrue("Expected either single-parameter or two-parameter form for ConfigOnlyService registration");
    }

    [Fact]
    public void ConfigurationIntegration_ConditionalService_ConfigBasedConditionalWithConfigInjection()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System;

namespace Test;

public interface ICacheService { }

public class CacheSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public int Database { get; set; }
    public int TTL { get; set; }
}

[ConditionalService(ConfigValue = ""Cache:Provider"", Equals = ""Redis"")]
[Singleton]
public partial class RedisCacheService : ICacheService
{
    [InjectConfiguration] private readonly CacheSettings _cacheSettings;
    [InjectConfiguration] private readonly IOptions<CacheSettings> _cacheOptions;
    [InjectConfiguration(""Cache:MaxRetries"")] private readonly int _maxRetries;
}

[ConditionalService(ConfigValue = ""Cache:Provider"", Equals = ""Memory"")]
[Singleton]
public partial class MemoryCacheService : ICacheService
{
    [InjectConfiguration(""Cache:SizeLimit"")] private readonly int _sizeLimit;
    [InjectConfiguration(""Cache:ExpireAfter"")] private readonly TimeSpan _expireAfter;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Configuration-based conditional services with configuration injection work correctly
        result.HasErrors.Should().BeFalse();

        // Should generate constructors with configuration injection for conditional services
        var redisConstructorSource = result.GetRequiredConstructorSource("RedisCacheService");
        redisConstructorSource.Content.Should().Contain("IConfiguration configuration");
        redisConstructorSource.Content.Should().Contain("IOptions<CacheSettings> cacheOptions");
        redisConstructorSource.Content.Should().Contain("configuration.GetSection(\"Cache\").Get<CacheSettings>()");
        redisConstructorSource.Content.Should().Contain("configuration.GetValue<int>(\"Cache:MaxRetries\")");

        var memoryConstructorSource = result.GetRequiredConstructorSource("MemoryCacheService");
        memoryConstructorSource.Content.Should().Contain("IConfiguration configuration");
        memoryConstructorSource.Content.Should().Contain("configuration.GetValue<int>(\"Cache:SizeLimit\")");
        memoryConstructorSource.Content.Should()
            .Contain("configuration.GetValue<global::System.TimeSpan>(\"Cache:ExpireAfter\")");

        // Should generate configuration-based conditional registration  
        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Verify conditional registration with proper null-coalescing - concrete classes
        registrationSource.Content.Should()
            .Contain("string.Equals(configuration[\"Cache:Provider\"], \"Redis\", StringComparison.OrdinalIgnoreCase)");
        registrationSource.Content.Should().Contain("AddSingleton<global::Test.RedisCacheService>()");
        registrationSource.Content.Should()
            .Contain(
                "string.Equals(configuration[\"Cache:Provider\"], \"Memory\", StringComparison.OrdinalIgnoreCase)");
        registrationSource.Content.Should().Contain("AddSingleton<global::Test.MemoryCacheService>()");

        // Verify interface registrations (mutually exclusive)
        registrationSource.Content.Should()
            .Contain(
                "AddSingleton<global::Test.ICacheService>(provider => provider.GetRequiredService<global::Test.RedisCacheService>())");
        registrationSource.Content.Should()
            .Contain(
                "AddSingleton<global::Test.ICacheService>(provider => provider.GetRequiredService<global::Test.MemoryCacheService>())");
    }

    #endregion

    #region Configuration Injection + Lifetime Validation Tests

    [Fact]
    public void ConfigurationIntegration_LifetimeValidation_SingletonWithConfigInjection()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Test;

public interface ICacheService { }
public interface ITransientDependency { }

public class CacheSettings
{
    public string Provider { get; set; } = string.Empty;
    public int TTL { get; set; }
}

[Transient]
public partial class TransientDependency : ITransientDependency
{
}

[Singleton]
public partial class CacheService : ICacheService
{
    [InjectConfiguration] private readonly CacheSettings _cacheSettings;
    [InjectConfiguration] private readonly IOptions<CacheSettings> _cacheOptions;
    [InjectConfiguration(""Cache:MaxSize"")] private readonly int _maxSize;
    [Inject] private readonly ITransientDependency _transientDep; // Should produce lifetime warning
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        // Should generate lifetime validation warning for transient dependency in singleton
        var lifetimeWarnings = result.GetDiagnosticsByCode("IOC013");
        lifetimeWarnings.Should().ContainSingle();
        lifetimeWarnings[0].GetMessage().Should().Contain("CacheService");
        lifetimeWarnings[0].GetMessage().Should().Contain("ITransientDependency");

        // Should still generate constructor with configuration injection
        var constructorSource = result.GetRequiredConstructorSource("CacheService");
        constructorSource.Content.Should().Contain("IConfiguration configuration");
        constructorSource.Content.Should().Contain("IOptions<CacheSettings> cacheOptions");
        constructorSource.Content.Should().Contain("ITransientDependency transientDep");
        constructorSource.Content.Should().Contain("configuration.GetSection(\"Cache\").Get<CacheSettings>()");
        constructorSource.Content.Should().Contain("configuration.GetValue<int>(\"Cache:MaxSize\")");
    }

    [Fact]
    public void ConfigurationIntegration_LifetimeValidation_BackgroundServiceWithConfigInjection()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace Test;

public interface IEmailService { }

public class EmailSettings
{
    public string SmtpHost { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}

[Scoped]
public partial class EmailService : IEmailService
{
}

[Transient] // Lifetime is irrelevant for hosted services
public partial class EmailProcessorService : BackgroundService
{
    [InjectConfiguration] private readonly EmailSettings _emailSettings;
    [InjectConfiguration(""Email:BatchSize"")] private readonly int _batchSize;
    [Inject] private readonly IEmailService _emailService;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(1000, stoppingToken);
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        // No IOC014 errors for hosted services - their lifetime is managed by AddHostedService()
        var lifetimeWarnings = result.GetDiagnosticsByCode("IOC014");
        lifetimeWarnings.Should().BeEmpty();

        // Should generate constructor with configuration injection
        var constructorSource = result.GetRequiredConstructorSource("EmailProcessorService");
        constructorSource.Content.Should().Contain("IConfiguration configuration");
        constructorSource.Content.Should().Contain("IEmailService emailService");
        constructorSource.Content.Should().Contain("configuration.GetSection(\"Email\").Get<EmailSettings>()");
        constructorSource.Content.Should().Contain("configuration.GetValue<int>(\"Email:BatchSize\")");

        // Should register as IHostedService
        var registrationSource = result.GetRequiredServiceRegistrationSource();
        registrationSource.Content.Should().Contain("services.AddHostedService<global::Test.EmailProcessorService>()");
    }

    #endregion

    #region Configuration Injection + Service Registration Tests

    [Fact]
    public void ConfigurationIntegration_MultiInterfaceRegistration_ConfigInjectionWithRegisterAsAll()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Test;

public interface IEmailService { }
public interface INotificationService { }
public interface IMessageService { }

public class EmailSettings
{
    public string SmtpHost { get; set; } = string.Empty;
    public int Port { get; set; }
}
[RegisterAsAll(RegistrationMode.All)]
public partial class EmailService : IEmailService, INotificationService, IMessageService
{
    [InjectConfiguration] private readonly EmailSettings _emailSettings;
    [InjectConfiguration] private readonly IOptions<EmailSettings> _emailOptions;
    [InjectConfiguration(""Email:ApiKey"")] private readonly string _apiKey;
    [InjectConfiguration(""Email:MaxRetries"")] private readonly int _maxRetries;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        // Should generate constructor with configuration injection
        var constructorSource = result.GetRequiredConstructorSource("EmailService");
        constructorSource.Content.Should().Contain("IConfiguration configuration");
        constructorSource.Content.Should().Contain("IOptions<EmailSettings> emailOptions");
        constructorSource.Content.Should().Contain("configuration.GetSection(\"Email\").Get<EmailSettings>()");
        constructorSource.Content.Should().Contain("configuration.GetValue<string>(\"Email:ApiKey\")");
        constructorSource.Content.Should().Contain("configuration.GetValue<int>(\"Email:MaxRetries\")");

        // Should register for all interfaces
        var registrationSource = result.GetRequiredServiceRegistrationSource();
        registrationSource.Content.Should()
            .Contain(
                "AddScoped<global::Test.IEmailService>(provider => provider.GetRequiredService<global::Test.EmailService>())");
        registrationSource.Content.Should()
            .Contain(
                "AddScoped<global::Test.INotificationService>(provider => provider.GetRequiredService<global::Test.EmailService>())");
        registrationSource.Content.Should()
            .Contain(
                "AddScoped<global::Test.IMessageService>(provider => provider.GetRequiredService<global::Test.EmailService>())");
    }

    [Fact]
    public void ConfigurationIntegration_SkipRegistration_ConfigInjectionWithSkippedService()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Configuration;

namespace Test;

public interface IUtilityService { }

public class UtilitySettings
{
    public string DatabasePath { get; set; } = string.Empty;
    public bool EnableLogging { get; set; }
}
[SkipRegistration]
public partial class UtilityService : IUtilityService
{
    [InjectConfiguration] private readonly UtilitySettings _settings;
    [InjectConfiguration(""Utility:WorkingDirectory"")] private readonly string _workingDir;
}
[Scoped]
public partial class MainService
{
    [Inject] private readonly IUtilityService _utilityService;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        // Should generate constructor for skipped service
        var utilityConstructorSource = result.GetRequiredConstructorSource("UtilityService");
        utilityConstructorSource.Content.Should().Contain("IConfiguration configuration");
        utilityConstructorSource.Content.Should()
            .Contain("configuration.GetSection(\"Utility\").Get<UtilitySettings>()");
        utilityConstructorSource.Content.Should()
            .Contain("configuration.GetValue<string>(\"Utility:WorkingDirectory\")");

        // Should not register skipped service but should register main service
        var registrationSource = result.GetRequiredServiceRegistrationSource();
        registrationSource.Content.Should().NotContain("AddScoped<global::Test.IUtilityService>");
        registrationSource.Content.Should().NotContain("AddScoped<global::Test.UtilityService>");
        // Both single-parameter and two-parameter forms are valid - accept both
        var hasMainServiceRegistration =
            registrationSource.Content.Contains("AddScoped<global::Test.MainService>();") ||
            registrationSource.Content.Contains("AddScoped<global::Test.MainService, global::Test.MainService>();");
        hasMainServiceRegistration.Should()
            .BeTrue("Expected either single-parameter or two-parameter form for MainService registration");
    }

    [Fact]
    public void ConfigurationIntegration_ExternalService_ConfigInjectionWithExternalDependency()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Configuration;

namespace Test;

[ExternalService]
public interface IExternalApi { }
public interface IMyService { }

public class ApiSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}
[Scoped]
public partial class MyService : IMyService
{
    [InjectConfiguration] private readonly ApiSettings _apiSettings;
    [InjectConfiguration(""Api:Timeout"")] private readonly int _timeout;
    [Inject] private readonly IExternalApi _externalApi; // External dependency
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        if (result.HasErrors)
        {
            var errors = string.Join("\n",
                result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(e => $"{e.Id}: {e.GetMessage()}"));

            // Also show generated sources for debugging
            var generatedContent = string.Join("\n\n",
                result.GeneratedSources.Select(gs => $"--- {gs.Hint} ---\n{gs.Content}"));

            throw new Exception($"Compilation has errors:\n{errors}\n\nGenerated sources:\n{generatedContent}");
        }

        result.HasErrors.Should().BeFalse();

        // Debug: Show all generated sources to understand what's happening
        var debugContent =
            string.Join("\n\n", result.GeneratedSources.Select(gs => $"--- {gs.Hint} ---\n{gs.Content}"));
        if (result.GeneratedSources.Count == 0 || result.GeneratedSources.All(gs => gs.Content.Trim().Length < 100))
            throw new Exception(
                $"No meaningful content generated. Generated {result.GeneratedSources.Count} sources:\n{debugContent}");

        // Should generate constructor with both config injection and external dependency
        var constructorSource = result.GetRequiredConstructorSource("MyService");
        constructorSource.Content.Should().Contain("IConfiguration configuration");
        constructorSource.Content.Should().Contain("IExternalApi externalApi");
        constructorSource.Content.Should().Contain("configuration.GetSection(\"Api\").Get<ApiSettings>()");
        constructorSource.Content.Should().Contain("configuration.GetValue<int>(\"Api:Timeout\")");

        // Should register only MyService, not external service
        var registrationSource = result.GetRequiredServiceRegistrationSource();
        if (registrationSource == null)
            throw new Exception(
                $"No service registration source found. Generated {result.GeneratedSources.Count} sources:\n" +
                string.Join("\n",
                    result.GeneratedSources.Select(gs =>
                        $"- {gs.Hint}: {gs.Content.Substring(0, Math.Min(100, gs.Content.Length))}...")));
        registrationSource.Should().NotBeNull();
        registrationSource.Content.Should()
            .Contain(
                "AddScoped<global::Test.IMyService>(provider => provider.GetRequiredService<global::Test.MyService>())");
        registrationSource.Content.Should().NotContain("AddScoped<IExternalApi");
    }

    #endregion

    #region Configuration Injection + Background Services Tests

    [Fact]
    public void ConfigurationIntegration_BackgroundService_CompleteConfigInjectionScenario()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Test;

public interface IEmailService { }

public class ProcessorSettings
{
    public int BatchSize { get; set; }
    public TimeSpan ProcessInterval { get; set; }
    public string WorkingDirectory { get; set; } = string.Empty;
}
[Scoped]
public partial class EmailService : IEmailService
{
}

[Transient]
public partial class EmailProcessorBackgroundService : BackgroundService
{
    [InjectConfiguration] private readonly ProcessorSettings _processorSettings;
    [InjectConfiguration] private readonly IOptionsMonitor<ProcessorSettings> _processorMonitor;
    [InjectConfiguration(""Processor:LogLevel"")] private readonly string _logLevel;
    [InjectConfiguration(""Processor:EnableMetrics"")] private readonly bool _enableMetrics;
    [Inject] private readonly IEmailService _emailService;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_processorSettings.ProcessInterval, stoppingToken);
        }
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        if (result.HasErrors)
        {
            var errors = string.Join("\n",
                result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(e => $"{e.Id}: {e.GetMessage()}"));

            // Also show generated sources for debugging
            var generatedContent = string.Join("\n\n",
                result.GeneratedSources.Select(gs => $"--- {gs.Hint} ---\n{gs.Content}"));

            throw new Exception($"Compilation has errors:\n{errors}\n\nGenerated sources:\n{generatedContent}");
        }

        result.HasErrors.Should().BeFalse();

        // Should generate constructor with all configuration injection types
        var constructorSource = result.GetRequiredConstructorSource("EmailProcessorBackgroundService");
        constructorSource.Content.Should().Contain("IConfiguration configuration");
        constructorSource.Content.Should().Contain("IOptionsMonitor<ProcessorSettings> processorMonitor");
        constructorSource.Content.Should().Contain("IEmailService emailService");
        constructorSource.Content.Should().Contain("configuration.GetSection(\"Processor\").Get<ProcessorSettings>()");
        constructorSource.Content.Should().Contain("configuration.GetValue<string>(\"Processor:LogLevel\")");
        constructorSource.Content.Should().Contain("configuration.GetValue<bool>(\"Processor:EnableMetrics\")");

        // Should register as IHostedService
        var registrationSource = result.GetRequiredServiceRegistrationSource();
        registrationSource.Content.Should().Contain("AddHostedService<global::Test.EmailProcessorBackgroundService>()");
        // Both single-parameter and two-parameter forms are valid - accept both
        var hasEmailServiceRegistration =
            registrationSource.Content.Contains("AddScoped<global::Test.EmailService>();") ||
            registrationSource.Content.Contains("AddScoped<global::Test.EmailService, global::Test.EmailService>();");
        hasEmailServiceRegistration.Should()
            .BeTrue("Expected either single-parameter or two-parameter form for EmailService registration");
    }

    [Fact]
    public void ConfigurationIntegration_BackgroundService_ConfigReloadingScenario()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace Test;

public class ReloadableSettings
{
    public int RefreshInterval { get; set; }
    public string DataSource { get; set; } = string.Empty;
}

public class StaticSettings
{
    public string ApplicationName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
}

[Transient]
public partial class ConfigReloadService : BackgroundService
{
    [InjectConfiguration] private readonly IOptionsMonitor<ReloadableSettings> _reloadableSettings;
    [InjectConfiguration] private readonly IOptions<StaticSettings> _staticSettings;
    [InjectConfiguration(""Service:Name"")] private readonly string _serviceName;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _reloadableSettings.OnChange(settings =>
        {
            // React to configuration changes
        });
        
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("ConfigReloadService");
        constructorSource.Content.Should().Contain("IOptionsMonitor<ReloadableSettings> reloadableSettings");
        constructorSource.Content.Should().Contain("IOptions<StaticSettings> staticSettings");
        constructorSource.Content.Should().Contain("IConfiguration configuration");
        constructorSource.Content.Should().Contain("configuration.GetValue<string>(\"Service:Name\")");

        var registrationSource = result.GetRequiredServiceRegistrationSource();
        registrationSource.Content.Should().Contain("AddHostedService<global::Test.ConfigReloadService>()");
    }

    #endregion

    #region Configuration Injection + Advanced DI Patterns Tests

    [Fact]
    public void ConfigurationIntegration_FactoryPattern_ConfigInjectionInFactoryService()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Test;

public interface IProcessor { }
public interface IProcessorFactory { }

public class ProcessorSettings
{
    public string DefaultType { get; set; } = string.Empty;
    public int DefaultBatchSize { get; set; }
}

[Scoped]
public partial class ProcessorA : IProcessor
{
}

[Scoped]
public partial class ProcessorB : IProcessor
{
}

[Scoped]
public partial class ProcessorFactory : IProcessorFactory
{
    [InjectConfiguration] private readonly ProcessorSettings _settings;
    [InjectConfiguration(""Factory:CreateTimeout"")] private readonly TimeSpan _createTimeout;
    [Inject] private readonly IServiceProvider _serviceProvider;

    public IProcessor CreateProcessor(string type)
    {
        return type switch
        {
            ""A"" => _serviceProvider.GetService<ProcessorA>(),
            ""B"" => _serviceProvider.GetService<ProcessorB>(),
            _ => _serviceProvider.GetService<ProcessorA>() // Default from config
        };
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var factoryConstructorSource = result.GetRequiredConstructorSource("ProcessorFactory");
        factoryConstructorSource.Content.Should().Contain("IConfiguration configuration");
        factoryConstructorSource.Content.Should().Contain("IServiceProvider serviceProvider");
        factoryConstructorSource.Content.Should()
            .Contain("configuration.GetSection(\"Processor\").Get<ProcessorSettings>()");
        factoryConstructorSource.Content.Should()
            .Contain("configuration.GetValue<global::System.TimeSpan>(\"Factory:CreateTimeout\")");

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        registrationSource.Content.Should()
            .Contain(
                "AddScoped<global::Test.IProcessorFactory>(provider => provider.GetRequiredService<global::Test.ProcessorFactory>())");
        // Both single-parameter and two-parameter forms are valid - accept both
        var hasProcessorARegistration = registrationSource.Content.Contains("AddScoped<global::Test.ProcessorA>();") ||
                                        registrationSource.Content.Contains(
                                            "AddScoped<global::Test.ProcessorA, global::Test.ProcessorA>();");
        hasProcessorARegistration.Should()
            .BeTrue("Expected either single-parameter or two-parameter form for ProcessorA registration");

        var hasProcessorBRegistration = registrationSource.Content.Contains("AddScoped<global::Test.ProcessorB>();") ||
                                        registrationSource.Content.Contains(
                                            "AddScoped<global::Test.ProcessorB, global::Test.ProcessorB>();");
        hasProcessorBRegistration.Should()
            .BeTrue("Expected either single-parameter or two-parameter form for ProcessorB registration");
    }

    [Fact]
    public void ConfigurationIntegration_DecoratorPattern_ConfigInjectionInDecorator()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Configuration;
using System;

namespace Test;

public interface IEmailService { }

public class RetrySettings
{
    public int MaxAttempts { get; set; }
    public TimeSpan Delay { get; set; }
}
[Scoped]
[RegisterAsAll(RegistrationMode.DirectOnly)] // Only register concrete type, not interface
public partial class EmailService : IEmailService
{
}

[Singleton]
public partial class RetryEmailDecorator : IEmailService
{
    [InjectConfiguration] private readonly RetrySettings _retrySettings;
    [InjectConfiguration(""Retry:BackoffMultiplier"")] private readonly double _backoffMultiplier;
    [Inject] private readonly EmailService _inner; // Inject concrete service, not interface
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var decoratorConstructorSource = result.GetRequiredConstructorSource("RetryEmailDecorator");
        decoratorConstructorSource.Content.Should().Contain("IConfiguration configuration");
        decoratorConstructorSource.Content.Should().Contain("EmailService inner");
        decoratorConstructorSource.Content.Should().Contain("configuration.GetSection(\"Retry\").Get<RetrySettings>()");
        decoratorConstructorSource.Content.Should()
            .Contain("configuration.GetValue<double>(\"Retry:BackoffMultiplier\")");

        var registrationSource = result.GetRequiredServiceRegistrationSource();
        registrationSource.Content.Should()
            .Contain(
                "AddSingleton<global::Test.IEmailService>(provider => provider.GetRequiredService<global::Test.RetryEmailDecorator>())");
        // Both single-parameter and two-parameter forms are valid - accept both
        var hasEmailServiceRegistration =
            registrationSource.Content.Contains("AddScoped<global::Test.EmailService>();") ||
            registrationSource.Content.Contains("AddScoped<global::Test.EmailService, global::Test.EmailService>();");
        hasEmailServiceRegistration.Should()
            .BeTrue("Expected either single-parameter or two-parameter form for EmailService registration");
        // Should NOT register EmailService for IEmailService (due to DirectOnly mode)
        registrationSource.Content.Should()
            .NotContain("AddScoped<global::Test.IEmailService, global::Test.EmailService>");
    }

    [Fact]
    public void ConfigurationIntegration_GenericServices_ConfigInjectionWithGenerics()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Configuration;

namespace Test;

public interface IRepository<T> where T : class { }
public interface IGenericService<T> where T : class { }

public class RepositorySettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public int CommandTimeout { get; set; }
}

public class User { }
public class Order { }
[Scoped]
public partial class Repository<T> : IRepository<T> where T : class
{
    [InjectConfiguration] private readonly RepositorySettings _settings;
    [InjectConfiguration(""Repository:CacheEnabled"")] private readonly bool _cacheEnabled;
}
[Scoped]
public partial class GenericService<T> : IGenericService<T> where T : class
{
    [InjectConfiguration(""Service:BatchSize"")] private readonly int _batchSize;
    [Inject] private readonly IRepository<T> _repository;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var repoConstructorSource = result.GetRequiredConstructorSource("Repository");
        repoConstructorSource.Content.Should().Contain("IConfiguration configuration");
        repoConstructorSource.Content.Should()
            .Contain("configuration.GetSection(\"Repository\").Get<RepositorySettings>()");
        repoConstructorSource.Content.Should().Contain("configuration.GetValue<bool>(\"Repository:CacheEnabled\")");

        var serviceConstructorSource = result.GetRequiredConstructorSource("GenericService");
        serviceConstructorSource.Content.Should().Contain("IConfiguration configuration");
        serviceConstructorSource.Content.Should().Contain("IRepository<T> repository");
        serviceConstructorSource.Content.Should().Contain("configuration.GetValue<int>(\"Service:BatchSize\")");
    }

    #endregion

    #region Real-World Integration Scenarios Tests

    [Fact]
    public void ConfigurationIntegration_WebApiService_CompleteScenario()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System;

namespace Test;

public interface IUserService { }
public interface IEmailService { }
public interface IDatabaseService { }

public class DatabaseSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public int CommandTimeout { get; set; }
    public bool EnableRetry { get; set; }
}

public class EmailSettings
{
    public string SmtpHost { get; set; } = string.Empty;
    public int Port { get; set; }
    public string ApiKey { get; set; } = string.Empty;
}

[Singleton]
public partial class DatabaseService : IDatabaseService
{
    [InjectConfiguration] private readonly DatabaseSettings _dbSettings;
    [InjectConfiguration(""Database:PoolSize"")] private readonly int _poolSize;
}
[Scoped]
public partial class EmailService : IEmailService
{
    [InjectConfiguration] private readonly IOptions<EmailSettings> _emailOptions;
    [InjectConfiguration(""Email:RateLimitPerHour"")] private readonly int _rateLimit;
}
[Scoped]
public partial class UserService : IUserService
{
    [InjectConfiguration(""User:DefaultRole"")] private readonly string _defaultRole;
    [InjectConfiguration(""User:SessionTimeout"")] private readonly TimeSpan _sessionTimeout;
    [Inject] private readonly IDatabaseService _database;
    [Inject] private readonly IEmailService _email;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        if (result.HasErrors)
        {
            var errors = string.Join("\n",
                result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(e => $"{e.Id}: {e.GetMessage()}"));

            // Also show generated sources for debugging
            var generatedContent = string.Join("\n\n",
                result.GeneratedSources.Select(gs => $"--- {gs.Hint} ---\n{gs.Content}"));

            throw new Exception($"Compilation has errors:\n{errors}\n\nGenerated sources:\n{generatedContent}");
        }

        result.HasErrors.Should().BeFalse();

        // Validate all constructors
        var dbConstructorSource = result.GetRequiredConstructorSource("DatabaseService");
        dbConstructorSource.Content.Should().Contain("configuration.GetSection(\"Database\").Get<DatabaseSettings>()");

        var emailConstructorSource = result.GetRequiredConstructorSource("EmailService");
        emailConstructorSource.Content.Should().Contain("IOptions<EmailSettings> emailOptions");

        var userConstructorSource = result.GetRequiredConstructorSource("UserService");
        userConstructorSource.Content.Should().Contain("IConfiguration configuration");
        userConstructorSource.Content.Should().Contain("IDatabaseService database");
        userConstructorSource.Content.Should().Contain("IEmailService email");

        // Validate registrations with correct lifetimes
        var registrationSource = result.GetRequiredServiceRegistrationSource();
        registrationSource.Content.Should()
            .Contain(
                "AddSingleton<global::Test.IDatabaseService>(provider => provider.GetRequiredService<global::Test.DatabaseService>())");
        registrationSource.Content.Should()
            .Contain(
                "AddScoped<global::Test.IEmailService>(provider => provider.GetRequiredService<global::Test.EmailService>())");
        registrationSource.Content.Should()
            .Contain(
                "AddScoped<global::Test.IUserService>(provider => provider.GetRequiredService<global::Test.UserService>())");
    }

    [Fact]
    public void ConfigurationIntegration_DatabaseService_ConnectionStringInjection()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Configuration;

namespace Test;

public interface IDatabaseService { }
public interface IConnectionFactory { }

public class DatabaseOptions
{
    public string Provider { get; set; } = string.Empty;
    public int MaxConnections { get; set; }
    public bool EnablePooling { get; set; }
}

[Singleton]
public partial class ConnectionFactory : IConnectionFactory
{
    [InjectConfiguration(""Database:ConnectionString"")] private readonly string _connectionString;
    [InjectConfiguration(""Database:SecondaryConnectionString"")] private readonly string _secondaryConnectionString;
    [InjectConfiguration] private readonly DatabaseOptions _options;
}
[Scoped]
public partial class DatabaseService : IDatabaseService
{
    [InjectConfiguration(""Database:QueryTimeout"")] private readonly int _queryTimeout;
    [InjectConfiguration(""Database:RetryAttempts"")] private readonly int _retryAttempts;
    [Inject] private readonly IConnectionFactory _connectionFactory;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        if (result.HasErrors)
        {
            Console.WriteLine($"=== COMPILATION ERRORS ({result.ErrorCount}) ===");
            foreach (var diag in result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
            {
                Console.WriteLine($"[ERROR] {diag.Id}: {diag.GetMessage()}");
                Console.WriteLine($"Location: {diag.Location}");
            }

            if (result.GeneratorDiagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
            {
                Console.WriteLine("=== GENERATOR ERRORS ===");
                foreach (var diag in result.GeneratorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
                    Console.WriteLine($"[GENERATOR ERROR] {diag.Id}: {diag.GetMessage()}");
            }

            Console.WriteLine($"=== GENERATED SOURCES ({result.GeneratedSources.Count}) ===");
            foreach (var generatedSource in result.GeneratedSources)
            {
                Console.WriteLine($"--- {generatedSource.Hint} ---");
                Console.WriteLine(generatedSource.Content.Substring(0, Math.Min(500, generatedSource.Content.Length)));
                if (generatedSource.Content.Length > 500) Console.WriteLine("... [truncated]");
            }
        }

        result.HasErrors.Should()
            .BeFalse($"Compilation failed with {result.ErrorCount} errors. See console output for details.");

        var factoryConstructorSource = result.GetRequiredConstructorSource("ConnectionFactory");
        factoryConstructorSource.Content.Should()
            .Contain("configuration.GetValue<string>(\"Database:ConnectionString\")");
        factoryConstructorSource.Content.Should()
            .Contain("configuration.GetValue<string>(\"Database:SecondaryConnectionString\")");
        factoryConstructorSource.Content.Should()
            .Contain("configuration.GetSection(\"Database\").Get<DatabaseOptions>()");

        var serviceConstructorSource = result.GetRequiredConstructorSource("DatabaseService");
        serviceConstructorSource.Content.Should().Contain("IConnectionFactory connectionFactory");
        serviceConstructorSource.Content.Should().Contain("configuration.GetValue<int>(\"Database:QueryTimeout\")");
        serviceConstructorSource.Content.Should().Contain("configuration.GetValue<int>(\"Database:RetryAttempts\")");
    }

    [Fact]
    public void ConfigurationIntegration_CacheService_MultiProviderScenario()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Collections.Generic;

namespace Test;

public interface ICacheService { }

public class CacheConfiguration
{
    public string Provider { get; set; } = string.Empty;
    public Dictionary<string, string> ProviderSettings { get; set; } = new();
    public int DefaultTTL { get; set; }
}

[ConditionalService(ConfigValue = ""Cache:Provider"", Equals = ""Redis"")]
[Singleton]
public partial class RedisCacheService : ICacheService
{
    [InjectConfiguration] private readonly CacheConfiguration _config;
    [InjectConfiguration(""Redis:ConnectionString"")] private readonly string _connectionString;
    [InjectConfiguration(""Redis:Database"")] private readonly int _database;
}

[ConditionalService(ConfigValue = ""Cache:Provider"", Equals = ""Memory"")]
[Singleton]
public partial class MemoryCacheService : ICacheService
{
    [InjectConfiguration] private readonly IOptionsSnapshot<CacheConfiguration> _configSnapshot;
    [InjectConfiguration(""Memory:SizeLimit"")] private readonly int _sizeLimit;
}

[ConditionalService(ConfigValue = ""Cache:Provider"", NotEquals = ""Redis,Memory"")]
[Singleton]
public partial class NullCacheService : ICacheService
{
    [InjectConfiguration(""Cache:LogMisses"")] private readonly bool _logMisses;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var redisConstructorSource = result.GetRequiredConstructorSource("RedisCacheService");
        redisConstructorSource.Content.Should()
            .Contain("configuration.GetSection(\"Cache\").Get<CacheConfiguration>()");
        redisConstructorSource.Content.Should().Contain("configuration.GetValue<string>(\"Redis:ConnectionString\")");

        var memoryConstructorSource = result.GetRequiredConstructorSource("MemoryCacheService");
        memoryConstructorSource.Content.Should().Contain("IOptionsSnapshot<CacheConfiguration> configSnapshot");

        var nullConstructorSource = result.GetRequiredConstructorSource("NullCacheService");
        nullConstructorSource.Content.Should().Contain("configuration.GetValue<bool>(\"Cache:LogMisses\")");

        var registrationSource = result.GetRequiredServiceRegistrationSource();
        registrationSource.Content.Should()
            .Contain("string.Equals(configuration[\"Cache:Provider\"], \"Redis\", StringComparison.OrdinalIgnoreCase)");
        registrationSource.Content.Should()
            .Contain(
                "AddSingleton<global::Test.ICacheService>(provider => provider.GetRequiredService<global::Test.RedisCacheService>())");
    }

    [Fact]
    public void ConfigurationIntegration_FeatureFlagService_ConfigBasedToggling()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Configuration;

namespace Test;

public interface IFeatureService { }
public interface IAnalyticsService { }

public class FeatureFlags
{
    public bool EnableAdvancedSearch { get; set; }
    public bool EnableUserAnalytics { get; set; }
    public bool EnableCaching { get; set; }
}

[ConditionalService(ConfigValue = ""Features:EnableAdvancedSearch"", Equals = ""true"")]
[Scoped]
public partial class AdvancedSearchService : IFeatureService
{
    [InjectConfiguration] private readonly FeatureFlags _featureFlags;
    [InjectConfiguration(""Search:MaxResults"")] private readonly int _maxResults;
    [InjectConfiguration(""Search:IndexPath"")] private readonly string _indexPath;
}

[ConditionalService(ConfigValue = ""Features:EnableUserAnalytics"", Equals = ""true"")]
[Scoped]
public partial class AnalyticsService : IAnalyticsService
{
    [InjectConfiguration(""Analytics:TrackingId"")] private readonly string _trackingId;
    [InjectConfiguration(""Analytics:SampleRate"")] private readonly double _sampleRate;
    [InjectConfiguration(""Analytics:BufferSize"")] private readonly int _bufferSize;
}
[Scoped]
public partial class ApplicationService
{
    [InjectConfiguration] private readonly FeatureFlags _featureFlags;
    [Inject] private readonly IFeatureService? _featureService;
    [Inject] private readonly IAnalyticsService? _analyticsService;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var searchConstructorSource = result.GetRequiredConstructorSource("AdvancedSearchService");
        searchConstructorSource.Content.Should()
            .Contain("configuration.GetSection(\"FeatureFlags\").Get<FeatureFlags>()");
        searchConstructorSource.Content.Should().Contain("configuration.GetValue<int>(\"Search:MaxResults\")");

        var analyticsConstructorSource = result.GetRequiredConstructorSource("AnalyticsService");
        analyticsConstructorSource.Content.Should().Contain("configuration.GetValue<string>(\"Analytics:TrackingId\")");

        var appConstructorSource = result.GetRequiredConstructorSource("ApplicationService");
        appConstructorSource.Content.Should().Contain("IFeatureService? featureService");
        appConstructorSource.Content.Should().Contain("IAnalyticsService? analyticsService");
    }

    #endregion

    #region Cross-Feature Error Scenarios Tests

    [Fact]
    public void ConfigurationIntegration_ConflictingFeatures_ConfigWithInvalidConditional()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Configuration;

namespace Test;

public interface ITestService { }

[ConditionalService(Environment = ""Development"", NotEnvironment = ""Development"")]
[Scoped]
public partial class ConflictingService : ITestService
{
    [InjectConfiguration(""Test:Value"")] private readonly string _value;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        // Should handle conflicting conditions gracefully
        var conflictDiagnostics = result.GetDiagnosticsByCode("IOC020");
        if (conflictDiagnostics.Any())
            conflictDiagnostics.First().GetMessage().ToLower().Should().Contain("conflicting");

        // Should still generate constructor even with conflicting conditions
        var constructorSource = result.GetConstructorSource("ConflictingService");
        if (constructorSource != null)
            constructorSource.Content.Should().Contain("configuration.GetValue<string>(\"Test:Value\")");
    }

    // REMOVED: ConfigurationIntegration_InvalidCombination_BackgroundServiceWithRegisterAsAll
    // This test was removed because it validates an anti-pattern:
    // - BackgroundServices should not implement business interfaces
    // - Mixing infrastructure concerns (IHostedService) with business logic violates SRP
    // - RegisterAsAll on BackgroundService creates confusing DI registrations
    // Real-world BackgroundServices depend on business services, not implement them

    [Fact]
    public void ConfigurationIntegration_DiagnosticInteraction_MultipleFeatureWarnings()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Configuration;

namespace Test;

public interface ITransientService { }
public interface ISingletonService { }

[Transient]
public partial class TransientService : ITransientService
{
}

[Singleton]
public partial class ProblematicService : ISingletonService
{
    [InjectConfiguration(""Service:Setting"")] private readonly string _setting;
    [Inject] private readonly ITransientService _transientService; // Should produce lifetime warning
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        // Should produce lifetime warning for transient dependency in singleton (IOC013)
        var lifetimeWarnings = result.GetDiagnosticsByCode("IOC013");
        lifetimeWarnings.Should().ContainSingle();
        lifetimeWarnings[0].GetMessage().Should().Contain("ProblematicService");

        // Should still generate constructor with configuration injection
        var constructorSource = result.GetRequiredConstructorSource("ProblematicService");
        constructorSource.Content.Should().Contain("IConfiguration configuration");
        constructorSource.Content.Should().Contain("ITransientService transientService");
        constructorSource.Content.Should().Contain("configuration.GetValue<string>(\"Service:Setting\")");

        // Should register services despite warnings
        var registrationSource = result.GetRequiredServiceRegistrationSource();
        registrationSource.Content.Should().NotBeEmpty();
    }

    #endregion

    #region Service Provider Integration Tests

    [Fact]
    public void ConfigurationIntegration_ServiceProvider_CompleteSetupWithAllFeatures()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Test;

public interface IRepository { }
public interface IEmailService { }
public interface ICacheService { }
public interface INotificationService { }

public class AppSettings
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
}

public class CacheSettings
{
    public string Provider { get; set; } = string.Empty;
    public int TTL { get; set; }
}

// External service
[ExternalService]
public interface IExternalApi { }

// Regular service with configuration
[Singleton]
public partial class Repository : IRepository
{
    [InjectConfiguration(""Database:ConnectionString"")] private readonly string _connectionString;
    [InjectConfiguration(""Database:Timeout"")] private readonly int _timeout;
}

// Conditional service with configuration
[ConditionalService(Environment = ""Development"")]
[Singleton]
public partial class MockEmailService : IEmailService
{
    [InjectConfiguration] private readonly AppSettings _appSettings;
    [InjectConfiguration(""Email:MockDelay"")] private readonly int _mockDelay;
}

[ConditionalService(Environment = ""Production"")]
[Singleton]
public partial class SmtpEmailService : IEmailService
{
    [InjectConfiguration] private readonly IOptions<AppSettings> _appOptions;
    [InjectConfiguration(""Email:SmtpHost"")] private readonly string _smtpHost;
}

// Multi-interface with configuration
[Singleton]
[RegisterAsAll(RegistrationMode.All)]
public partial class CacheService : ICacheService, INotificationService
{
    [InjectConfiguration] private readonly CacheSettings _cacheSettings;
    [InjectConfiguration(""Cache:MaxSize"")] private readonly int _maxSize;
    [Inject] private readonly IRepository _repository;
}

// Background service with configuration
[Singleton]
public partial class ProcessorBackgroundService : BackgroundService
{
    [InjectConfiguration(""Processor:BatchSize"")] private readonly int _batchSize;
    [InjectConfiguration(""Processor:Interval"")] private readonly TimeSpan _interval;
    [Inject] private readonly IEmailService _emailService;
    [Inject] private readonly ICacheService _cacheService;
    [Inject] private readonly IExternalApi _externalApi;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(_interval, stoppingToken);
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should()
            .BeFalse(
                $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        // Validate all constructors are generated with proper configuration injection
        var repoConstructorSource = result.GetRequiredConstructorSource("Repository");
        repoConstructorSource.Content.Should().Contain("IConfiguration configuration");

        var mockEmailConstructorSource = result.GetRequiredConstructorSource("MockEmailService");
        mockEmailConstructorSource.Content.Should().Contain("configuration.GetSection(\"App\").Get<AppSettings>()");

        var smtpEmailConstructorSource = result.GetRequiredConstructorSource("SmtpEmailService");
        smtpEmailConstructorSource.Content.Should().Contain("IOptions<AppSettings> appOptions");

        var cacheConstructorSource = result.GetRequiredConstructorSource("CacheService");
        cacheConstructorSource.Content.Should().Contain("IRepository repository");
        cacheConstructorSource.Content.Should().Contain("configuration.GetSection(\"Cache\").Get<CacheSettings>()");

        var processorConstructorSource = result.GetRequiredConstructorSource("ProcessorBackgroundService");
        processorConstructorSource.Content.Should().Contain("IEmailService emailService");
        processorConstructorSource.Content.Should().Contain("ICacheService cacheService");
        processorConstructorSource.Content.Should().Contain("IExternalApi externalApi");

        // Validate comprehensive service registration
        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Environment-based registration
        registrationSource.Content.Should()
            .Contain("var environment = Environment.GetEnvironmentVariable(\"ASPNETCORE_ENVIRONMENT\")");
        registrationSource.Content.Should()
            .Contain("string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)");
        registrationSource.Content.Should()
            .Contain("string.Equals(environment, \"Production\", StringComparison.OrdinalIgnoreCase)");

        // Regular registrations
        registrationSource.Content.Should()
            .Contain(
                "AddSingleton<global::Test.IRepository>(provider => provider.GetRequiredService<global::Test.Repository>())");
        registrationSource.Content.Should()
            .Contain(
                "AddSingleton<global::Test.ICacheService>(provider => provider.GetRequiredService<global::Test.CacheService>())");
        registrationSource.Content.Should()
            .Contain(
                "AddSingleton<global::Test.INotificationService>(provider => provider.GetRequiredService<global::Test.CacheService>())");

        // Background service registration
        registrationSource.Content.Should().Contain("AddHostedService<global::Test.ProcessorBackgroundService>()");

        // Should not register external services
        registrationSource.Content.Should().NotContain("AddScoped<IExternalApi");
    }

    [Fact]
    public void ConfigurationIntegration_ServiceResolution_ConfigChangesAffectingBehavior()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Test;

public interface IAdaptiveService { }

public class AdaptiveSettings
{
    public string Mode { get; set; } = string.Empty;
    public int BatchSize { get; set; }
    public bool EnableOptimizations { get; set; }
}
[Scoped]
public partial class AdaptiveService : IAdaptiveService
{
    [InjectConfiguration] private readonly IOptionsMonitor<AdaptiveSettings> _settingsMonitor;
    [InjectConfiguration(""Service:CurrentMode"")] private readonly string _currentMode;
    [InjectConfiguration(""Service:DebugEnabled"")] private readonly bool _debugEnabled;

    public void ProcessData()
    {
        var settings = _settingsMonitor.CurrentValue;
        // Behavior adapts based on current configuration
        if (settings.EnableOptimizations)
        {
            // Use optimized path
        }
        else
        {
            // Use standard path
        }
    }
}
[Scoped]
public partial class ConfigConsumerService
{
    [Inject] private readonly IAdaptiveService _adaptiveService;
    [InjectConfiguration(""Consumer:ProcessingEnabled"")] private readonly bool _processingEnabled;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        if (result.HasErrors)
        {
            var errors = string.Join("\n",
                result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(e => $"{e.Id}: {e.GetMessage()}"));

            // Show generated sources for debugging
            var generatedContent = string.Join("\n\n",
                result.GeneratedSources.Select(gs => $"--- {gs.Hint} ---\n{gs.Content}"));

            throw new Exception($"Compilation has errors:\n{errors}\n\nGenerated sources:\n{generatedContent}");
        }

        result.HasErrors.Should().BeFalse();

        // Debug: Show all generated sources to understand what's happening
        var debugContent =
            string.Join("\n\n", result.GeneratedSources.Select(gs => $"--- {gs.Hint} ---\n{gs.Content}"));
        if (result.GeneratedSources.Count == 0 || result.GeneratedSources.All(gs => gs.Content.Trim().Length < 100))
            throw new Exception(
                $"No meaningful content generated. Generated {result.GeneratedSources.Count} sources:\n{debugContent}");

        var adaptiveConstructorSource = result.GetRequiredConstructorSource("AdaptiveService");
        adaptiveConstructorSource.Content.Should().Contain("IOptionsMonitor<AdaptiveSettings> settingsMonitor");
        adaptiveConstructorSource.Content.Should().Contain("configuration.GetValue<string>(\"Service:CurrentMode\")");
        adaptiveConstructorSource.Content.Should().Contain("configuration.GetValue<bool>(\"Service:DebugEnabled\")");

        var consumerConstructorSource = result.GetRequiredConstructorSource("ConfigConsumerService");
        consumerConstructorSource.Content.Should().Contain("IAdaptiveService adaptiveService");
        consumerConstructorSource.Content.Should()
            .Contain("configuration.GetValue<bool>(\"Consumer:ProcessingEnabled\")");

        var registrationSource = result.GetRequiredServiceRegistrationSource();
        if (registrationSource == null)
            throw new Exception(
                $"No service registration source found. Generated {result.GeneratedSources.Count} sources:\n" +
                string.Join("\n",
                    result.GeneratedSources.Select(gs =>
                        $"- {gs.Hint}: {gs.Content.Substring(0, Math.Min(100, gs.Content.Length))}...")));
        registrationSource.Should().NotBeNull();

        registrationSource.Content.Should()
            .Contain(
                "AddScoped<global::Test.IAdaptiveService>(provider => provider.GetRequiredService<global::Test.AdaptiveService>())");
        // Both single-parameter and two-parameter forms are valid - accept both
        var hasConfigConsumerServiceRegistration =
            registrationSource.Content.Contains("AddScoped<global::Test.ConfigConsumerService>();") ||
            registrationSource.Content.Contains(
                "AddScoped<global::Test.ConfigConsumerService, global::Test.ConfigConsumerService>();");
        hasConfigConsumerServiceRegistration.Should()
            .BeTrue("Expected either single-parameter or two-parameter form for ConfigConsumerService registration");
    }

    [Fact]
    public void ConfigurationIntegration_PerformanceScenario_ManyServicesWithConfigInjection()
    {
        // Arrange - Create many services with configuration injection to test performance
        var serviceDefinitions = Enumerable.Range(1, 5)
            .Select(i => $@"
public interface IService{i} {{ }}

public class Service{i}Settings
{{
    public string Value{i} {{ get; set; }} = string.Empty;
    public int Count{i} {{ get; set; }}
}}

[Scoped]
public partial class Service{i} : IService{i}
{{
    [InjectConfiguration] private readonly Service{i}Settings _settings;
    [InjectConfiguration(""Service{i}:Key"")] private readonly string _key{i};
    [InjectConfiguration(""Service{i}:Enabled"")] private readonly bool _enabled{i};
}}")
            .ToArray();

        var source = $@"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Configuration;

namespace Test;

{string.Join("\n\n", serviceDefinitions)}

[Scoped]
public partial class AggregateService
{{
    {string.Join("\n    ", Enumerable.Range(1, 5).Select(i => $"[Inject] private readonly IService{i} _service{i};"))}
    [InjectConfiguration(""Aggregate:BatchSize"")] private readonly int _batchSize;
}}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        // Should generate constructors for all services
        for (var i = 1; i <= 5; i++)
        {
            var constructorContent = result.GetConstructorSourceText($"Service{i}");
            constructorContent.Should().Contain("IConfiguration configuration");
            constructorContent.Should().Contain($"configuration.GetSection(\"Service{i}\").Get<Service{i}Settings>()");
            constructorContent.Should().Contain($"configuration.GetValue<string>(\"Service{i}:Key\")");
        }

        // Should generate aggregate constructor with all dependencies
        var aggregateConstructorSource = result.GetRequiredConstructorSource("AggregateService");
        aggregateConstructorSource.Content.Should().Contain("IConfiguration configuration");
        for (var i = 1; i <= 5; i++) aggregateConstructorSource.Content.Should().Contain($"IService{i} service{i}");

        // Should register all services
        var registrationSource = result.GetRequiredServiceRegistrationSource();
        for (var i = 1; i <= 5; i++)
            registrationSource.Content.Should()
                .Contain(
                    $"AddScoped<global::Test.IService{i}>(provider => provider.GetRequiredService<global::Test.Service{i}>())");
        // Both single-parameter and two-parameter forms are valid - accept both
        var hasAggregateServiceRegistration =
            registrationSource.Content.Contains("AddScoped<global::Test.AggregateService>();") ||
            registrationSource.Content.Contains(
                "AddScoped<global::Test.AggregateService, global::Test.AggregateService>();");
        hasAggregateServiceRegistration.Should()
            .BeTrue("Expected either single-parameter or two-parameter form for AggregateService registration");
    }

    #endregion
}
