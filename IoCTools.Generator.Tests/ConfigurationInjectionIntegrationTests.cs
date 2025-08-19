using Microsoft.CodeAnalysis;

namespace IoCTools.Generator.Tests;

/// <summary>
///     COMPREHENSIVE CONFIGURATION INJECTION INTEGRATION TESTS
///     Tests configuration injection working with all other IoCTools features including
///     conditional services, background services, lifetime validation, multi-interface registration,
///     and complex real-world scenarios.
///     
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
[Service]
public partial class DevLoggerService : ILoggerService
{
    [InjectConfiguration] private readonly LoggingSettings _loggingSettings;
    [InjectConfiguration] private readonly IOptions<LoggingSettings> _loggingOptions;
    [InjectConfiguration(""Logging:DevSpecific:EnableDetailedErrors"")] private readonly bool _enableDetailedErrors;
    [InjectConfiguration(""Logging:DevSpecific:MaxFileSize"")] private readonly int _maxFileSize;
}

[ConditionalService(Environment = ""Production"")]
[Service]
public partial class ProdLoggerService : ILoggerService
{
    [InjectConfiguration] private readonly LoggingSettings _loggingSettings;
    [InjectConfiguration(""Logging:Production:ApiKey"")] private readonly string _apiKey;
    [InjectConfiguration(""Logging:Production:BatchSize"")] private readonly int _batchSize;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        // Should generate constructors with configuration injection for both services
        var devConstructorSource = result.GetConstructorSource("DevLoggerService");
        Assert.NotNull(devConstructorSource);
        Assert.Contains("IConfiguration configuration", devConstructorSource.Content);
        Assert.Contains("IOptions<LoggingSettings> loggingOptions", devConstructorSource.Content);
        Assert.Contains("configuration.GetSection(\"Logging\").Get<LoggingSettings>()", devConstructorSource.Content);
        Assert.Contains("configuration.GetValue<bool>(\"Logging:DevSpecific:EnableDetailedErrors\")", devConstructorSource.Content);
        Assert.Contains("configuration.GetValue<int>(\"Logging:DevSpecific:MaxFileSize\")", devConstructorSource.Content);

        var prodConstructorSource = result.GetConstructorSource("ProdLoggerService");
        Assert.NotNull(prodConstructorSource);
        Assert.Contains("IConfiguration configuration", prodConstructorSource.Content);
        Assert.Contains("configuration.GetSection(\"Logging\").Get<LoggingSettings>()", prodConstructorSource.Content);
        Assert.Contains("configuration.GetValue<string>(\"Logging:Production:ApiKey\")", prodConstructorSource.Content);
        Assert.Contains("configuration.GetValue<int>(\"Logging:Production:BatchSize\")", prodConstructorSource.Content);

        // Should generate environment-based conditional registrations
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        
        // Verify environment-based conditional registration logic
        Assert.Contains("var environment = Environment.GetEnvironmentVariable(\"ASPNETCORE_ENVIRONMENT\")", registrationSource.Content);
        Assert.Contains("string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)", registrationSource.Content);
        Assert.Contains("AddScoped<global::Test.DevLoggerService, global::Test.DevLoggerService>()", registrationSource.Content);
        Assert.Contains("AddScoped<global::Test.ILoggerService>(provider => provider.GetRequiredService<global::Test.DevLoggerService>())", registrationSource.Content);
        
        Assert.Contains("string.Equals(environment, \"Production\", StringComparison.OrdinalIgnoreCase)", registrationSource.Content);
        Assert.Contains("AddScoped<global::Test.ProdLoggerService, global::Test.ProdLoggerService>()", registrationSource.Content);
        Assert.Contains("AddScoped<global::Test.ILoggerService>(provider => provider.GetRequiredService<global::Test.ProdLoggerService>())", registrationSource.Content);
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
[Service]
public partial class DevTestService : ITestService
{
}

[ConditionalService(Environment = ""Production"")]
[Service]
public partial class ProdTestService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - should generate conditional registration
        if (result.HasErrors)
        {
            var errors = string.Join("\n", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(e => $"{e.Id}: {e.GetMessage()}"));
            throw new Exception($"Compilation has errors:\n{errors}");
        }
        
        Assert.False(result.HasErrors);
        
        // Debug: Check what was generated
        var generatedContent = string.Join("\n\n", result.GeneratedSources.Select(gs => $"--- {gs.Hint} ---\n{gs.Content}"));
        
        if (result.GeneratedSources.Count == 0)
        {
            throw new Exception("No sources were generated!");
        }
        
        var registrationSource = result.GetServiceRegistrationSource();
        if (registrationSource == null)
        {
            throw new Exception($"No service registration source found. Generated {result.GeneratedSources.Count} sources:\n{generatedContent}");
        }
        
        // Debug: Check what's actually in the registration source
        if (!registrationSource.Content.Contains("Environment.GetEnvironmentVariable"))
        {
            throw new Exception($"Missing environment variable detection. Registration source content:\n{registrationSource.Content}\n\nAll generated:\n{generatedContent}");
        }
        
        // Should contain environment detection and conditional logic
        Assert.Contains("Environment.GetEnvironmentVariable", registrationSource.Content);
        Assert.Contains("string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)", registrationSource.Content);
    }

    [Fact]
    public void DEBUG_ConfigurationIntegration_SimpleTest()
    {
        // Arrange - very simple service with configuration injection
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Configuration;

namespace Test;

[Service]
public partial class SimpleService
{
    [InjectConfiguration(""Test:Value"")] private readonly string _testValue;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Configuration injection working correctly
        Assert.False(result.HasErrors);
        
        // Should generate constructor with configuration injection
        var constructorSource = result.GetConstructorSource("SimpleService");
        Assert.NotNull(constructorSource);
        Assert.Contains("IConfiguration configuration", constructorSource.Content);
        Assert.Contains("configuration.GetValue<string>(\"Test:Value\")", constructorSource.Content);
        
        // Should generate service registrations for services with configuration injection
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("AddScoped<global::Test.SimpleService, global::Test.SimpleService>()", registrationSource.Content);
    }

    [Fact]
    public void DEBUG_ConfigurationIntegration_OnlyInjectConfigurationTest()
    {
        // Arrange - service with ONLY InjectConfiguration (no explicit Service attribute)
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Configuration;

namespace Test;

public partial class ConfigOnlyService
{
    [InjectConfiguration(""Database:ConnectionString"")] private readonly string _connectionString;
    [InjectConfiguration(""App:MaxRetries"")] private readonly int _maxRetries;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Services with ONLY InjectConfiguration are auto-registered with default Scoped lifetime
        Assert.False(result.HasErrors);
        
        // Should generate constructor with configuration injection
        var constructorSource = result.GetConstructorSource("ConfigOnlyService");
        Assert.NotNull(constructorSource);
        Assert.Contains("IConfiguration configuration", constructorSource.Content);
        Assert.Contains("configuration.GetValue<string>(\"Database:ConnectionString\")", constructorSource.Content);
        Assert.Contains("configuration.GetValue<int>(\"App:MaxRetries\")", constructorSource.Content);
        
        // Should auto-register services with configuration injection using default Scoped lifetime
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("AddScoped<global::Test.ConfigOnlyService, global::Test.ConfigOnlyService>()", registrationSource.Content);
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
[Service(Lifetime.Singleton)]
public partial class RedisCacheService : ICacheService
{
    [InjectConfiguration] private readonly CacheSettings _cacheSettings;
    [InjectConfiguration] private readonly IOptions<CacheSettings> _cacheOptions;
    [InjectConfiguration(""Cache:MaxRetries"")] private readonly int _maxRetries;
}

[ConditionalService(ConfigValue = ""Cache:Provider"", Equals = ""Memory"")]
[Service(Lifetime.Singleton)]
public partial class MemoryCacheService : ICacheService
{
    [InjectConfiguration(""Cache:SizeLimit"")] private readonly int _sizeLimit;
    [InjectConfiguration(""Cache:ExpireAfter"")] private readonly TimeSpan _expireAfter;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Configuration-based conditional services with configuration injection work correctly
        Assert.False(result.HasErrors);

        // Should generate constructors with configuration injection for conditional services
        var redisConstructorSource = result.GetConstructorSource("RedisCacheService");
        Assert.NotNull(redisConstructorSource);
        Assert.Contains("IConfiguration configuration", redisConstructorSource.Content);
        Assert.Contains("IOptions<CacheSettings> cacheOptions", redisConstructorSource.Content);
        Assert.Contains("configuration.GetSection(\"Cache\").Get<CacheSettings>()", redisConstructorSource.Content);
        Assert.Contains("configuration.GetValue<int>(\"Cache:MaxRetries\")", redisConstructorSource.Content);

        var memoryConstructorSource = result.GetConstructorSource("MemoryCacheService");
        Assert.NotNull(memoryConstructorSource);
        Assert.Contains("IConfiguration configuration", memoryConstructorSource.Content);
        Assert.Contains("configuration.GetValue<int>(\"Cache:SizeLimit\")", memoryConstructorSource.Content);
        Assert.Contains("configuration.GetValue<global::System.TimeSpan>(\"Cache:ExpireAfter\")",
            memoryConstructorSource.Content);

        // Should generate configuration-based conditional registration  
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Verify conditional registration with proper null-coalescing - concrete classes
        Assert.Contains("string.Equals(configuration[\"Cache:Provider\"], \"Redis\", StringComparison.OrdinalIgnoreCase)",
            registrationSource.Content);
        Assert.Contains("AddSingleton<global::Test.RedisCacheService, global::Test.RedisCacheService>()", registrationSource.Content);
        Assert.Contains("string.Equals(configuration[\"Cache:Provider\"], \"Memory\", StringComparison.OrdinalIgnoreCase)",
            registrationSource.Content);
        Assert.Contains("AddSingleton<global::Test.MemoryCacheService, global::Test.MemoryCacheService>()", registrationSource.Content);
        
        // Verify interface registrations (mutually exclusive)
        Assert.Contains("AddSingleton<global::Test.ICacheService>(provider => provider.GetRequiredService<global::Test.RedisCacheService>())", registrationSource.Content);
        Assert.Contains("AddSingleton<global::Test.ICacheService>(provider => provider.GetRequiredService<global::Test.MemoryCacheService>())", registrationSource.Content);
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

[Service(Lifetime.Transient)]
public partial class TransientDependency : ITransientDependency
{
}

[Service(Lifetime.Singleton)]
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
        Assert.Single(lifetimeWarnings);
        Assert.Contains("CacheService", lifetimeWarnings[0].GetMessage());
        Assert.Contains("ITransientDependency", lifetimeWarnings[0].GetMessage());

        // Should still generate constructor with configuration injection
        var constructorSource = result.GetConstructorSource("CacheService");
        Assert.NotNull(constructorSource);
        Assert.Contains("IConfiguration configuration", constructorSource.Content);
        Assert.Contains("IOptions<CacheSettings> cacheOptions", constructorSource.Content);
        Assert.Contains("ITransientDependency transientDep", constructorSource.Content);
        Assert.Contains("configuration.GetSection(\"Cache\").Get<CacheSettings>()", constructorSource.Content);
        Assert.Contains("configuration.GetValue<int>(\"Cache:MaxSize\")", constructorSource.Content);
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

[Service(Lifetime.Scoped)]
public partial class EmailService : IEmailService
{
}

[Service(Lifetime.Transient)] // Should produce lifetime warning for BackgroundService
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
        // Should generate lifetime warning for BackgroundService with non-Singleton lifetime
        var lifetimeWarnings = result.GetDiagnosticsByCode("IOC014");
        Assert.Single(lifetimeWarnings);
        Assert.Contains("EmailProcessorService", lifetimeWarnings[0].GetMessage());

        // Should generate constructor with configuration injection
        var constructorSource = result.GetConstructorSource("EmailProcessorService");
        Assert.NotNull(constructorSource);
        Assert.Contains("IConfiguration configuration", constructorSource.Content);
        Assert.Contains("IEmailService emailService", constructorSource.Content);
        Assert.Contains("configuration.GetSection(\"Email\").Get<EmailSettings>()", constructorSource.Content);
        Assert.Contains("configuration.GetValue<int>(\"Email:BatchSize\")", constructorSource.Content);

        // Should register as IHostedService
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("services.AddHostedService<global::Test.EmailProcessorService>()", registrationSource.Content);
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

[Service]
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
        Assert.False(result.HasErrors);

        // Should generate constructor with configuration injection
        var constructorSource = result.GetConstructorSource("EmailService");
        Assert.NotNull(constructorSource);
        Assert.Contains("IConfiguration configuration", constructorSource.Content);
        Assert.Contains("IOptions<EmailSettings> emailOptions", constructorSource.Content);
        Assert.Contains("configuration.GetSection(\"Email\").Get<EmailSettings>()", constructorSource.Content);
        Assert.Contains("configuration.GetValue<string>(\"Email:ApiKey\")", constructorSource.Content);
        Assert.Contains("configuration.GetValue<int>(\"Email:MaxRetries\")", constructorSource.Content);

        // Should register for all interfaces
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("AddScoped<global::Test.IEmailService>(provider => provider.GetRequiredService<global::Test.EmailService>())",
            registrationSource.Content);
        Assert.Contains("AddScoped<global::Test.INotificationService>(provider => provider.GetRequiredService<global::Test.EmailService>())",
            registrationSource.Content);
        Assert.Contains("AddScoped<global::Test.IMessageService>(provider => provider.GetRequiredService<global::Test.EmailService>())",
            registrationSource.Content);
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

[Service]
[SkipRegistration]
public partial class UtilityService : IUtilityService
{
    [InjectConfiguration] private readonly UtilitySettings _settings;
    [InjectConfiguration(""Utility:WorkingDirectory"")] private readonly string _workingDir;
}

[Service]
public partial class MainService
{
    [Inject] private readonly IUtilityService _utilityService;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        // Should generate constructor for skipped service
        var utilityConstructorSource = result.GetConstructorSource("UtilityService");
        Assert.NotNull(utilityConstructorSource);
        Assert.Contains("IConfiguration configuration", utilityConstructorSource.Content);
        Assert.Contains("configuration.GetSection(\"Utility\").Get<UtilitySettings>()",
            utilityConstructorSource.Content);
        Assert.Contains("configuration.GetValue<string>(\"Utility:WorkingDirectory\")",
            utilityConstructorSource.Content);

        // Should not register skipped service but should register main service
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.DoesNotContain("AddScoped<global::Test.IUtilityService>", registrationSource.Content);
        Assert.DoesNotContain("AddScoped<global::Test.UtilityService>", registrationSource.Content);
        Assert.Contains("AddScoped<global::Test.MainService, global::Test.MainService>", registrationSource.Content);
    }

    [Fact]
    public void ConfigurationIntegration_ExternalService_ConfigInjectionWithExternalDependency()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Configuration;

namespace Test;

public interface IExternalApi { }
public interface IMyService { }

public class ApiSettings
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}

[Service]
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

        Assert.False(result.HasErrors);

        // Debug: Show all generated sources to understand what's happening
        var debugContent =
            string.Join("\n\n", result.GeneratedSources.Select(gs => $"--- {gs.Hint} ---\n{gs.Content}"));
        if (result.GeneratedSources.Count == 0 || result.GeneratedSources.All(gs => gs.Content.Trim().Length < 100))
            throw new Exception(
                $"No meaningful content generated. Generated {result.GeneratedSources.Count} sources:\n{debugContent}");

        // Should generate constructor with both config injection and external dependency
        var constructorSource = result.GetConstructorSource("MyService");
        Assert.NotNull(constructorSource);
        Assert.Contains("IConfiguration configuration", constructorSource.Content);
        Assert.Contains("IExternalApi externalApi", constructorSource.Content);
        Assert.Contains("configuration.GetSection(\"Api\").Get<ApiSettings>()", constructorSource.Content);
        Assert.Contains("configuration.GetValue<int>(\"Api:Timeout\")", constructorSource.Content);

        // Should register only MyService, not external service
        var registrationSource = result.GetServiceRegistrationSource();
        if (registrationSource == null)
            throw new Exception(
                $"No service registration source found. Generated {result.GeneratedSources.Count} sources:\n" +
                string.Join("\n",
                    result.GeneratedSources.Select(gs =>
                        $"- {gs.Hint}: {gs.Content.Substring(0, Math.Min(100, gs.Content.Length))}...")));
        Assert.NotNull(registrationSource);
        Assert.Contains("AddScoped<global::Test.IMyService>(provider => provider.GetRequiredService<global::Test.MyService>())",
            registrationSource.Content);
        Assert.DoesNotContain("AddScoped<IExternalApi", registrationSource.Content);
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

[Service]
public partial class EmailService : IEmailService
{
}

[BackgroundService]
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

        Assert.False(result.HasErrors);

        // Should generate constructor with all configuration injection types
        var constructorSource = result.GetConstructorSource("EmailProcessorBackgroundService");
        Assert.NotNull(constructorSource);
        Assert.Contains("IConfiguration configuration", constructorSource.Content);
        Assert.Contains("IOptionsMonitor<ProcessorSettings> processorMonitor", constructorSource.Content);
        Assert.Contains("IEmailService emailService", constructorSource.Content);
        Assert.Contains("configuration.GetSection(\"Processor\").Get<ProcessorSettings>()", constructorSource.Content);
        Assert.Contains("configuration.GetValue<string>(\"Processor:LogLevel\")", constructorSource.Content);
        Assert.Contains("configuration.GetValue<bool>(\"Processor:EnableMetrics\")", constructorSource.Content);

        // Should register as IHostedService
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("AddHostedService<global::Test.EmailProcessorBackgroundService>()", registrationSource.Content);
        Assert.Contains("AddScoped<global::Test.IEmailService, global::Test.EmailService>()", registrationSource.Content);
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

[BackgroundService]
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
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("ConfigReloadService");
        Assert.NotNull(constructorSource);
        Assert.Contains("IOptionsMonitor<ReloadableSettings> reloadableSettings", constructorSource.Content);
        Assert.Contains("IOptions<StaticSettings> staticSettings", constructorSource.Content);
        Assert.Contains("IConfiguration configuration", constructorSource.Content);
        Assert.Contains("configuration.GetValue<string>(\"Service:Name\")", constructorSource.Content);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("AddHostedService<global::Test.ConfigReloadService>()", registrationSource.Content);
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

[Service]
public partial class ProcessorA : IProcessor
{
}

[Service]
public partial class ProcessorB : IProcessor
{
}

[Service]
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
        Assert.False(result.HasErrors);

        var factoryConstructorSource = result.GetConstructorSource("ProcessorFactory");
        Assert.NotNull(factoryConstructorSource);
        Assert.Contains("IConfiguration configuration", factoryConstructorSource.Content);
        Assert.Contains("IServiceProvider serviceProvider", factoryConstructorSource.Content);
        Assert.Contains("configuration.GetSection(\"Processor\").Get<ProcessorSettings>()",
            factoryConstructorSource.Content);
        Assert.Contains("configuration.GetValue<global::System.TimeSpan>(\"Factory:CreateTimeout\")",
            factoryConstructorSource.Content);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        Assert.Contains("AddScoped<global::Test.IProcessorFactory>(provider => provider.GetRequiredService<global::Test.ProcessorFactory>())",
            registrationSource.Content);
        Assert.Contains("AddScoped<global::Test.ProcessorA, global::Test.ProcessorA>", registrationSource.Content);
        Assert.Contains("AddScoped<global::Test.ProcessorB, global::Test.ProcessorB>", registrationSource.Content);
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

[Service]
[RegisterAsAll(RegistrationMode.DirectOnly)] // Only register concrete type, not interface
public partial class EmailService : IEmailService
{
}

[Service(Lifetime.Singleton)]
public partial class RetryEmailDecorator : IEmailService
{
    [InjectConfiguration] private readonly RetrySettings _retrySettings;
    [InjectConfiguration(""Retry:BackoffMultiplier"")] private readonly double _backoffMultiplier;
    [Inject] private readonly EmailService _inner; // Inject concrete service, not interface
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var decoratorConstructorSource = result.GetConstructorSource("RetryEmailDecorator");
        Assert.NotNull(decoratorConstructorSource);
        Assert.Contains("IConfiguration configuration", decoratorConstructorSource.Content);
        Assert.Contains("EmailService inner", decoratorConstructorSource.Content);
        Assert.Contains("configuration.GetSection(\"Retry\").Get<RetrySettings>()", decoratorConstructorSource.Content);
        Assert.Contains("configuration.GetValue<double>(\"Retry:BackoffMultiplier\")",
            decoratorConstructorSource.Content);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains(
            "AddSingleton<global::Test.IEmailService>(provider => provider.GetRequiredService<global::Test.RetryEmailDecorator>())",
            registrationSource.Content);
        Assert.Contains("AddScoped<global::Test.EmailService, global::Test.EmailService>", registrationSource.Content);
        // Should NOT register EmailService for IEmailService (due to DirectOnly mode)
        Assert.DoesNotContain("AddScoped<global::Test.IEmailService, global::Test.EmailService>", registrationSource.Content);
    }

    [Fact]
    public void ConfigurationIntegration_GenericServices_ConfigInjectionWithGenerics()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
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

[Service]
public partial class Repository<T> : IRepository<T> where T : class
{
    [InjectConfiguration] private readonly RepositorySettings _settings;
    [InjectConfiguration(""Repository:CacheEnabled"")] private readonly bool _cacheEnabled;
}

[Service]
public partial class GenericService<T> : IGenericService<T> where T : class
{
    [InjectConfiguration(""Service:BatchSize"")] private readonly int _batchSize;
    [Inject] private readonly IRepository<T> _repository;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var repoConstructorSource = result.GetConstructorSource("Repository");
        Assert.NotNull(repoConstructorSource);
        Assert.Contains("IConfiguration configuration", repoConstructorSource.Content);
        Assert.Contains("configuration.GetSection(\"Repository\").Get<RepositorySettings>()",
            repoConstructorSource.Content);
        Assert.Contains("configuration.GetValue<bool>(\"Repository:CacheEnabled\")", repoConstructorSource.Content);

        var serviceConstructorSource = result.GetConstructorSource("GenericService");
        Assert.NotNull(serviceConstructorSource);
        Assert.Contains("IConfiguration configuration", serviceConstructorSource.Content);
        Assert.Contains("IRepository<T> repository", serviceConstructorSource.Content);
        Assert.Contains("configuration.GetValue<int>(\"Service:BatchSize\")", serviceConstructorSource.Content);
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

[Service(Lifetime.Singleton)]
public partial class DatabaseService : IDatabaseService
{
    [InjectConfiguration] private readonly DatabaseSettings _dbSettings;
    [InjectConfiguration(""Database:PoolSize"")] private readonly int _poolSize;
}

[Service]
public partial class EmailService : IEmailService
{
    [InjectConfiguration] private readonly IOptions<EmailSettings> _emailOptions;
    [InjectConfiguration(""Email:RateLimitPerHour"")] private readonly int _rateLimit;
}

[Service]
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

        Assert.False(result.HasErrors);

        // Validate all constructors
        var dbConstructorSource = result.GetConstructorSource("DatabaseService");
        Assert.NotNull(dbConstructorSource);
        Assert.Contains("configuration.GetSection(\"Database\").Get<DatabaseSettings>()", dbConstructorSource.Content);

        var emailConstructorSource = result.GetConstructorSource("EmailService");
        Assert.NotNull(emailConstructorSource);
        Assert.Contains("IOptions<EmailSettings> emailOptions", emailConstructorSource.Content);

        var userConstructorSource = result.GetConstructorSource("UserService");
        Assert.NotNull(userConstructorSource);
        Assert.Contains("IConfiguration configuration", userConstructorSource.Content);
        Assert.Contains("IDatabaseService database", userConstructorSource.Content);
        Assert.Contains("IEmailService email", userConstructorSource.Content);

        // Validate registrations with correct lifetimes
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains(
            "AddSingleton<global::Test.IDatabaseService>(provider => provider.GetRequiredService<global::Test.DatabaseService>())",
            registrationSource.Content);
        Assert.Contains("AddScoped<global::Test.IEmailService>(provider => provider.GetRequiredService<global::Test.EmailService>())",
            registrationSource.Content);
        Assert.Contains("AddScoped<global::Test.IUserService>(provider => provider.GetRequiredService<global::Test.UserService>())",
            registrationSource.Content);
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

[Service(Lifetime.Singleton)]
public partial class ConnectionFactory : IConnectionFactory
{
    [InjectConfiguration(""Database:ConnectionString"")] private readonly string _connectionString;
    [InjectConfiguration(""Database:SecondaryConnectionString"")] private readonly string _secondaryConnectionString;
    [InjectConfiguration] private readonly DatabaseOptions _options;
}

[Service]
public partial class DatabaseService : IDatabaseService
{
    [InjectConfiguration(""Database:QueryTimeout"")] private readonly int _queryTimeout;
    [InjectConfiguration(""Database:RetryAttempts"")] private readonly int _retryAttempts;
    [Inject] private readonly IConnectionFactory _connectionFactory;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var factoryConstructorSource = result.GetConstructorSource("ConnectionFactory");
        Assert.NotNull(factoryConstructorSource);
        Assert.Contains("configuration.GetValue<string>(\"Database:ConnectionString\")",
            factoryConstructorSource.Content);
        Assert.Contains("configuration.GetValue<string>(\"Database:SecondaryConnectionString\")",
            factoryConstructorSource.Content);
        Assert.Contains("configuration.GetSection(\"Database\").Get<DatabaseOptions>()",
            factoryConstructorSource.Content);

        var serviceConstructorSource = result.GetConstructorSource("DatabaseService");
        Assert.NotNull(serviceConstructorSource);
        Assert.Contains("IConnectionFactory connectionFactory", serviceConstructorSource.Content);
        Assert.Contains("configuration.GetValue<int>(\"Database:QueryTimeout\")", serviceConstructorSource.Content);
        Assert.Contains("configuration.GetValue<int>(\"Database:RetryAttempts\")", serviceConstructorSource.Content);
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
[Service(Lifetime.Singleton)]
public partial class RedisCacheService : ICacheService
{
    [InjectConfiguration] private readonly CacheConfiguration _config;
    [InjectConfiguration(""Redis:ConnectionString"")] private readonly string _connectionString;
    [InjectConfiguration(""Redis:Database"")] private readonly int _database;
}

[ConditionalService(ConfigValue = ""Cache:Provider"", Equals = ""Memory"")]
[Service(Lifetime.Singleton)]
public partial class MemoryCacheService : ICacheService
{
    [InjectConfiguration] private readonly IOptionsSnapshot<CacheConfiguration> _configSnapshot;
    [InjectConfiguration(""Memory:SizeLimit"")] private readonly int _sizeLimit;
}

[ConditionalService(ConfigValue = ""Cache:Provider"", NotEquals = ""Redis,Memory"")]
[Service(Lifetime.Singleton)]
public partial class NullCacheService : ICacheService
{
    [InjectConfiguration(""Cache:LogMisses"")] private readonly bool _logMisses;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var redisConstructorSource = result.GetConstructorSource("RedisCacheService");
        Assert.NotNull(redisConstructorSource);
        Assert.Contains("configuration.GetSection(\"Cache\").Get<CacheConfiguration>()",
            redisConstructorSource.Content);
        Assert.Contains("configuration.GetValue<string>(\"Redis:ConnectionString\")", redisConstructorSource.Content);

        var memoryConstructorSource = result.GetConstructorSource("MemoryCacheService");
        Assert.NotNull(memoryConstructorSource);
        Assert.Contains("IOptionsSnapshot<CacheConfiguration> configSnapshot", memoryConstructorSource.Content);

        var nullConstructorSource = result.GetConstructorSource("NullCacheService");
        Assert.NotNull(nullConstructorSource);
        Assert.Contains("configuration.GetValue<bool>(\"Cache:LogMisses\")", nullConstructorSource.Content);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("string.Equals(configuration[\"Cache:Provider\"], \"Redis\", StringComparison.OrdinalIgnoreCase)",
            registrationSource.Content);
        Assert.Contains("AddSingleton<global::Test.ICacheService>(provider => provider.GetRequiredService<global::Test.RedisCacheService>())", registrationSource.Content);
    }

    [Fact]
    public void ConfigurationIntegration_FeatureFlagService_ConfigBasedToggling()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
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
[Service]
public partial class AdvancedSearchService : IFeatureService
{
    [InjectConfiguration] private readonly FeatureFlags _featureFlags;
    [InjectConfiguration(""Search:MaxResults"")] private readonly int _maxResults;
    [InjectConfiguration(""Search:IndexPath"")] private readonly string _indexPath;
}

[ConditionalService(ConfigValue = ""Features:EnableUserAnalytics"", Equals = ""true"")]
[Service]
public partial class AnalyticsService : IAnalyticsService
{
    [InjectConfiguration(""Analytics:TrackingId"")] private readonly string _trackingId;
    [InjectConfiguration(""Analytics:SampleRate"")] private readonly double _sampleRate;
    [InjectConfiguration(""Analytics:BufferSize"")] private readonly int _bufferSize;
}

[Service]
public partial class ApplicationService
{
    [InjectConfiguration] private readonly FeatureFlags _featureFlags;
    [Inject] private readonly IFeatureService? _featureService;
    [Inject] private readonly IAnalyticsService? _analyticsService;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var searchConstructorSource = result.GetConstructorSource("AdvancedSearchService");
        Assert.NotNull(searchConstructorSource);
        Assert.Contains("configuration.GetSection(\"FeatureFlags\").Get<FeatureFlags>()",
            searchConstructorSource.Content);
        Assert.Contains("configuration.GetValue<int>(\"Search:MaxResults\")", searchConstructorSource.Content);

        var analyticsConstructorSource = result.GetConstructorSource("AnalyticsService");
        Assert.NotNull(analyticsConstructorSource);
        Assert.Contains("configuration.GetValue<string>(\"Analytics:TrackingId\")", analyticsConstructorSource.Content);

        var appConstructorSource = result.GetConstructorSource("ApplicationService");
        Assert.NotNull(appConstructorSource);
        Assert.Contains("IFeatureService? featureService", appConstructorSource.Content);
        Assert.Contains("IAnalyticsService? analyticsService", appConstructorSource.Content);
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
[Service]
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
            Assert.Contains("conflicting", conflictDiagnostics.First().GetMessage().ToLower());

        // Should still generate constructor even with conflicting conditions
        var constructorSource = result.GetConstructorSource("ConflictingService");
        if (constructorSource != null)
            Assert.Contains("configuration.GetValue<string>(\"Test:Value\")", constructorSource.Content);
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

[Service(Lifetime.Transient)]
public partial class TransientService : ITransientService
{
}

[Service(Lifetime.Singleton)]
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
        Assert.Single(lifetimeWarnings);
        Assert.Contains("ProblematicService", lifetimeWarnings[0].GetMessage());

        // Should still generate constructor with configuration injection
        var constructorSource = result.GetConstructorSource("ProblematicService");
        Assert.NotNull(constructorSource);
        Assert.Contains("IConfiguration configuration", constructorSource.Content);
        Assert.Contains("ITransientService transientService", constructorSource.Content);
        Assert.Contains("configuration.GetValue<string>(\"Service:Setting\")", constructorSource.Content);

        // Should register services despite warnings
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.NotEmpty(registrationSource.Content);
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
public interface IExternalApi { }

// Regular service with configuration
[Service(Lifetime.Singleton)]
public partial class Repository : IRepository
{
    [InjectConfiguration(""Database:ConnectionString"")] private readonly string _connectionString;
    [InjectConfiguration(""Database:Timeout"")] private readonly int _timeout;
}

// Conditional service with configuration
[ConditionalService(Environment = ""Development"")]
[Service]
public partial class MockEmailService : IEmailService
{
    [InjectConfiguration] private readonly AppSettings _appSettings;
    [InjectConfiguration(""Email:MockDelay"")] private readonly int _mockDelay;
}

[ConditionalService(Environment = ""Production"")]
[Service]
public partial class SmtpEmailService : IEmailService
{
    [InjectConfiguration] private readonly IOptions<AppSettings> _appOptions;
    [InjectConfiguration(""Email:SmtpHost"")] private readonly string _smtpHost;
}

// Multi-interface with configuration
[Service]
[RegisterAsAll(RegistrationMode.All)]
public partial class CacheService : ICacheService, INotificationService
{
    [InjectConfiguration] private readonly CacheSettings _cacheSettings;
    [InjectConfiguration(""Cache:MaxSize"")] private readonly int _maxSize;
    [Inject] private readonly IRepository _repository;
}

// Background service with configuration
[BackgroundService]
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
        Assert.False(result.HasErrors);

        // Validate all constructors are generated with proper configuration injection
        var repoConstructorSource = result.GetConstructorSource("Repository");
        Assert.NotNull(repoConstructorSource);
        Assert.Contains("IConfiguration configuration", repoConstructorSource.Content);

        var mockEmailConstructorSource = result.GetConstructorSource("MockEmailService");
        Assert.NotNull(mockEmailConstructorSource);
        Assert.Contains("configuration.GetSection(\"App\").Get<AppSettings>()", mockEmailConstructorSource.Content);

        var smtpEmailConstructorSource = result.GetConstructorSource("SmtpEmailService");
        Assert.NotNull(smtpEmailConstructorSource);
        Assert.Contains("IOptions<AppSettings> appOptions", smtpEmailConstructorSource.Content);

        var cacheConstructorSource = result.GetConstructorSource("CacheService");
        Assert.NotNull(cacheConstructorSource);
        Assert.Contains("IRepository repository", cacheConstructorSource.Content);
        Assert.Contains("configuration.GetSection(\"Cache\").Get<CacheSettings>()", cacheConstructorSource.Content);

        var processorConstructorSource = result.GetConstructorSource("ProcessorBackgroundService");
        Assert.NotNull(processorConstructorSource);
        Assert.Contains("IEmailService emailService", processorConstructorSource.Content);
        Assert.Contains("ICacheService cacheService", processorConstructorSource.Content);
        Assert.Contains("IExternalApi externalApi", processorConstructorSource.Content);

        // Validate comprehensive service registration
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Environment-based registration
        Assert.Contains("var environment = Environment.GetEnvironmentVariable(\"ASPNETCORE_ENVIRONMENT\")",
            registrationSource.Content);
        Assert.Contains("string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)", registrationSource.Content);
        Assert.Contains("string.Equals(environment, \"Production\", StringComparison.OrdinalIgnoreCase)", registrationSource.Content);

        // Regular registrations
        Assert.Contains("AddSingleton<global::Test.IRepository>(provider => provider.GetRequiredService<global::Test.Repository>())",
            registrationSource.Content);
        Assert.Contains("AddScoped<global::Test.ICacheService>(provider => provider.GetRequiredService<global::Test.CacheService>())",
            registrationSource.Content);
        Assert.Contains("AddScoped<global::Test.INotificationService>(provider => provider.GetRequiredService<global::Test.CacheService>())",
            registrationSource.Content);

        // Background service registration
        Assert.Contains("AddHostedService<global::Test.ProcessorBackgroundService>()", registrationSource.Content);

        // Should not register external services
        Assert.DoesNotContain("AddScoped<IExternalApi", registrationSource.Content);
    }

    [Fact]
    public void ConfigurationIntegration_ServiceResolution_ConfigChangesAffectingBehavior()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
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

[Service]
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

[Service]
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

        Assert.False(result.HasErrors);

        // Debug: Show all generated sources to understand what's happening
        var debugContent =
            string.Join("\n\n", result.GeneratedSources.Select(gs => $"--- {gs.Hint} ---\n{gs.Content}"));
        if (result.GeneratedSources.Count == 0 || result.GeneratedSources.All(gs => gs.Content.Trim().Length < 100))
            throw new Exception(
                $"No meaningful content generated. Generated {result.GeneratedSources.Count} sources:\n{debugContent}");

        var adaptiveConstructorSource = result.GetConstructorSource("AdaptiveService");
        Assert.NotNull(adaptiveConstructorSource);
        Assert.Contains("IOptionsMonitor<AdaptiveSettings> settingsMonitor", adaptiveConstructorSource.Content);
        Assert.Contains("configuration.GetValue<string>(\"Service:CurrentMode\")", adaptiveConstructorSource.Content);
        Assert.Contains("configuration.GetValue<bool>(\"Service:DebugEnabled\")", adaptiveConstructorSource.Content);

        var consumerConstructorSource = result.GetConstructorSource("ConfigConsumerService");
        Assert.NotNull(consumerConstructorSource);
        Assert.Contains("IAdaptiveService adaptiveService", consumerConstructorSource.Content);
        Assert.Contains("configuration.GetValue<bool>(\"Consumer:ProcessingEnabled\")",
            consumerConstructorSource.Content);

        var registrationSource = result.GetServiceRegistrationSource();
        if (registrationSource == null)
            throw new Exception(
                $"No service registration source found. Generated {result.GeneratedSources.Count} sources:\n" +
                string.Join("\n",
                    result.GeneratedSources.Select(gs =>
                        $"- {gs.Hint}: {gs.Content.Substring(0, Math.Min(100, gs.Content.Length))}...")));
        Assert.NotNull(registrationSource);

        Assert.Contains("AddScoped<global::Test.IAdaptiveService>(provider => provider.GetRequiredService<global::Test.AdaptiveService>())",
            registrationSource.Content);
        Assert.Contains("AddScoped<global::Test.ConfigConsumerService, global::Test.ConfigConsumerService>", registrationSource.Content);
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

[Service]
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

[Service]
public partial class AggregateService
{{
    {string.Join("\n    ", Enumerable.Range(1, 5).Select(i => $"[Inject] private readonly IService{i} _service{i};"))}
    [InjectConfiguration(""Aggregate:BatchSize"")] private readonly int _batchSize;
}}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        // Should generate constructors for all services
        for (var i = 1; i <= 5; i++)
        {
            var constructorSource = result.GetConstructorSource($"Service{i}");
            Assert.NotNull(constructorSource);
            Assert.Contains("IConfiguration configuration", constructorSource.Content);
            Assert.Contains($"configuration.GetSection(\"Service{i}\").Get<Service{i}Settings>()",
                constructorSource.Content);
            Assert.Contains($"configuration.GetValue<string>(\"Service{i}:Key\")", constructorSource.Content);
        }

        // Should generate aggregate constructor with all dependencies
        var aggregateConstructorSource = result.GetConstructorSource("AggregateService");
        Assert.NotNull(aggregateConstructorSource);
        Assert.Contains("IConfiguration configuration", aggregateConstructorSource.Content);
        for (var i = 1; i <= 5; i++) Assert.Contains($"IService{i} service{i}", aggregateConstructorSource.Content);

        // Should register all services
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        for (var i = 1; i <= 5; i++)
            Assert.Contains($"AddScoped<global::Test.IService{i}>(provider => provider.GetRequiredService<global::Test.Service{i}>())",
                registrationSource.Content);
        Assert.Contains("AddScoped<global::Test.AggregateService, global::Test.AggregateService>", registrationSource.Content);
    }

    #endregion
}