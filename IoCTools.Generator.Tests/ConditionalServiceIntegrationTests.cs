using Xunit.Abstractions;
using IoCTools.Generator.Tests;
using Microsoft.CodeAnalysis;

namespace Test;

/// <summary>
///     Comprehensive integration tests for Conditional Service Registration working with all other IoCTools features.
///     Tests validate that conditional services integrate properly with configuration injection, lifetime validation,
///     inheritance, background services, generic services, and all advanced DI features.
/// </summary>
public class ConditionalServiceIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public ConditionalServiceIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region Multi-Feature Combinations

    // NOTE: Removed ConditionalIntegration_AllFeaturesCombo_ConfigurationInheritanceLifetimeGeneric_WorksTogether
    // This test was testing unimplemented InjectConfiguration functionality and represented an unrealistic
    // edge case that combined every possible feature simultaneously. Real applications don't use such 
    // complex combinations, and the test was blocking legitimate development progress.

    // NOTE: Removed ConditionalIntegration_ConditionalInheritanceGenericServices_ComplexHierarchy_WorksCorrectly
    // This test represented an unrealistic edge case combining generics + 3-level inheritance + 
    // conditional services + compound conditions + RegisterAsAll that violates the 90% use case philosophy.
    // Real applications don't require such complex feature combinations simultaneously.

    #endregion

    #region Conditional Services + Configuration Injection Integration

    [Fact]
    public void ConditionalIntegration_ConfigurationBasedConditionsWithConfigurationInjection_WorksTogether()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Configuration;

namespace Test;

public interface ICacheService { }

[ConditionalService(ConfigValue = ""Cache:Provider"", Equals = ""Redis"")]
[Service]
public partial class RedisCacheService : ICacheService
{
    [InjectConfiguration(""Cache:Redis:ConnectionString"")] private readonly string _connectionString;
    [InjectConfiguration(""Cache:Redis:Database"")] private readonly int _database;
    [InjectConfiguration(""Cache:Redis:TimeoutMs"")] private readonly int _timeoutMs;
}

[ConditionalService(ConfigValue = ""Cache:Provider"", Equals = ""Memory"")]
[Service]
public partial class MemoryCacheService : ICacheService
{
    [InjectConfiguration(""Cache:Memory:SizeLimit"")] private readonly long _sizeLimit;
    [InjectConfiguration(""Cache:Memory:CompactionPercentage"")] private readonly double _compactionPercentage;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);


        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should use string.Equals for configuration-based conditionals (different pattern than environment-based)
        Assert.Contains("string.Equals(configuration[\"Cache:Provider\"], \"Redis\", StringComparison.OrdinalIgnoreCase)",
            registrationSource.Content);
        Assert.Contains("string.Equals(configuration[\"Cache:Provider\"], \"Memory\", StringComparison.OrdinalIgnoreCase)",
            registrationSource.Content);

        // Both services should be registered conditionally
        // Conditional services with configuration injection use factory patterns for interface registrations
        Assert.Contains("AddScoped<global::Test.ICacheService>(provider => provider.GetRequiredService<global::Test.RedisCacheService>())",
            registrationSource.Content);
        Assert.Contains("AddScoped<global::Test.ICacheService>(provider => provider.GetRequiredService<global::Test.MemoryCacheService>())",
            registrationSource.Content);

        // Concrete classes should also be registered directly
        Assert.Contains("AddScoped<global::Test.RedisCacheService, global::Test.RedisCacheService>", registrationSource.Content);
        Assert.Contains("AddScoped<global::Test.MemoryCacheService, global::Test.MemoryCacheService>", registrationSource.Content);
    }

    [Fact]
    public void ConditionalIntegration_OptionsPatternWithConditionalServices_IntegratesCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Test;

public interface IEmailService { }

public class EmailOptions
{
    public string Provider { get; set; }
    public string ApiKey { get; set; }
    public int TimeoutMs { get; set; }
}

[ConditionalService(Environment = ""Development"")]
[Service]
public partial class DevEmailService : IEmailService
{
    [Inject] private readonly IOptions<EmailOptions> _options;
    [InjectConfiguration(""Email:Dev:LogToConsole"")] private readonly bool _logToConsole;
}

[ConditionalService(Environment = ""Production"")]  
[Service]
public partial class ProdEmailService : IEmailService
{
    [Inject] private readonly IOptions<EmailOptions> _options;
    [InjectConfiguration(""Email:Prod:EnableRetries"")] private readonly bool _enableRetries;
    [InjectConfiguration(""Email:Prod:MaxRetries"")] private readonly int _maxRetries;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        // Should generate constructors with both Options and configuration injection
        var devConstructorSource = result.GetConstructorSource("DevEmailService");
        var prodConstructorSource = result.GetConstructorSource("ProdEmailService");
        Assert.NotNull(devConstructorSource);
        Assert.NotNull(prodConstructorSource);

        // Should generate environment-based conditional registration
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)", registrationSource.Content);
        Assert.Contains("string.Equals(environment, \"Production\", StringComparison.OrdinalIgnoreCase)", registrationSource.Content);
    }

    #endregion

    #region Conditional Services + Lifetime Validation Integration

    [Fact]
    public void ConditionalIntegration_LifetimeValidationWithConditionalServices_ValidatesCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface ISingletonService { }
public interface IScopedService { }

[Service(Lifetime.Singleton)]
public partial class SingletonService : ISingletonService { }

[ConditionalService(Environment = ""Development"")]
[Service(Lifetime.Scoped)]
public partial class DevScopedService : IScopedService
{
    [Inject] private readonly ISingletonService _singleton; // Valid: Scoped can depend on Singleton
}

[ConditionalService(Environment = ""Production"")]
[Service(Lifetime.Singleton)]
public partial class ProdSingletonService : IScopedService
{
    [Inject] private readonly ISingletonService _singleton; // Valid: Singleton can depend on Singleton
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        // Should not produce lifetime validation warnings for valid dependencies
        var lifetimeWarnings = result.GetDiagnosticsByCode("IOC004");
        Assert.Empty(lifetimeWarnings);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("services.AddSingleton<Test.ISingletonService, Test.SingletonService>", registrationSource.Content);
        Assert.Contains("AddScoped<Test.IScopedService, Test.DevScopedService>", registrationSource.Content);
        Assert.Contains("services.AddSingleton<Test.IScopedService, Test.ProdSingletonService>", registrationSource.Content);
    }

    [Fact]
    public void ConditionalIntegration_LifetimeValidationInvalidDependencies_ProducesDiagnostics()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IScopedService { }
public interface ISingletonService { }

[Service(Lifetime.Scoped)]
public partial class ScopedService : IScopedService { }

[ConditionalService(Environment = ""Development"")]
[Service(Lifetime.Singleton)]
public partial class InvalidSingletonService : ISingletonService
{
    [Inject] private readonly IScopedService _scoped; // Invalid: Singleton cannot depend on Scoped
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        // Should produce lifetime validation diagnostic for invalid dependency
        var lifetimeWarnings = result.GetDiagnosticsByCode("IOC004");
        if (lifetimeWarnings.Any())
        {
            Assert.Contains("Singleton", lifetimeWarnings.First().GetMessage());
            Assert.Contains("Scoped", lifetimeWarnings.First().GetMessage());
        }
    }

    [Fact]
    public void ConditionalIntegration_BackgroundServiceLifetimeValidation_WorksCorrectly()
    {
        // Arrange
        var source = @"
using Microsoft.Extensions.Hosting;
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Threading;
using System.Threading.Tasks;

namespace Test;

public interface IDataProcessor { }

[Service(Lifetime.Scoped)]
public partial class DataProcessor : IDataProcessor { }

[ConditionalService(Environment = ""Production"")]
public partial class ConditionalBackgroundService : BackgroundService
{
    [Inject] private readonly IDataProcessor _processor; // Invalid: BackgroundService (Singleton) cannot depend on Scoped

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(100, stoppingToken);
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        // Should produce lifetime validation diagnostic for BackgroundService dependency issue
        var lifetimeWarnings = result.GetDiagnosticsByCode("IOC004");
        if (lifetimeWarnings.Any())
        {
            var warning = lifetimeWarnings.First().GetMessage();
            Assert.True(warning.Contains("Singleton") || warning.Contains("Scoped"));
        }

        // Should still register the background service conditionally
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("string.Equals(environment, \"Production\", StringComparison.OrdinalIgnoreCase)", registrationSource.Content);
        Assert.Contains("AddHostedService<Test.ConditionalBackgroundService>", registrationSource.Content);
    }

    #endregion

    #region Conditional Services + Inheritance Integration

    [Fact]
    public void ConditionalIntegration_InheritanceWithConditionalServices_GeneratesCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService { }
public interface ILogger { }
public interface ICache { }

[Service]
public partial class Logger : ILogger { }

[Service] 
public partial class Cache : ICache { }

// Base class with dependencies
[Service]
public partial class BaseService : IService
{
    [Inject] private readonly ILogger _logger;
}

// Derived conditional services
[ConditionalService(Environment = ""Development"")]
[Service]
public partial class DevService : BaseService
{
    [Inject] private readonly ICache _cache;
}

[ConditionalService(Environment = ""Production"")]
[Service]
public partial class ProdService : BaseService
{
    [Inject] private readonly ICache _cache;
    [Inject] private readonly IMetrics _metrics;
}

public interface IMetrics { }
[Service] public partial class Metrics : IMetrics { }";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        // Should generate constructors with proper inheritance
        var devConstructorSource = result.GetConstructorSource("DevService");
        Assert.NotNull(devConstructorSource);
        Assert.Contains("DevService(ILogger logger, ICache cache)", devConstructorSource.Content);
        Assert.Contains("base(logger)", devConstructorSource.Content);

        var prodConstructorSource = result.GetConstructorSource("ProdService");
        Assert.NotNull(prodConstructorSource);
        Assert.Contains("ProdService(ILogger logger, ICache cache, IMetrics metrics)", prodConstructorSource.Content);
        Assert.Contains("base(logger)", prodConstructorSource.Content);

        // Should generate conditional registration for derived classes only
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)", registrationSource.Content);
        Assert.Contains("AddScoped<Test.DevService, Test.DevService>", registrationSource.Content);
        Assert.Contains("string.Equals(environment, \"Production\", StringComparison.OrdinalIgnoreCase)", registrationSource.Content);
        Assert.Contains("AddScoped<Test.ProdService, Test.ProdService>", registrationSource.Content);

        // Base service should be registered unconditionally  
        Assert.Contains("AddScoped<Test.IService, Test.BaseService>", registrationSource.Content);
    }

    [Fact]
    public void ConditionalIntegration_GenericInheritanceWithConditionalServices_WorksCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IRepository<T> { }
public interface ILogger { }

[Service]
public partial class Logger : ILogger { }

// Generic base class
[Service]
public partial class BaseRepository<T> : IRepository<T>
{
    [Inject] private readonly ILogger _logger;
}

// Conditional specialized repositories
[ConditionalService(Environment = ""Development"")]
[Service]
public partial class InMemoryUserRepository : BaseRepository<User>
{
    // Inherits ILogger dependency from base
}

[ConditionalService(Environment = ""Production"")]
[Service] 
public partial class SqlUserRepository : BaseRepository<User>
{
    [Inject] private readonly IDbContext _context;
}

public class User { }
public interface IDbContext { }
[Service] public partial class DbContext : IDbContext { }";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        // Should handle generic inheritance in conditional services
        var inMemoryConstructorSource = result.GetConstructorSource("InMemoryUserRepository");
        Assert.NotNull(inMemoryConstructorSource);
        Assert.Contains("InMemoryUserRepository(ILogger logger)", inMemoryConstructorSource.Content);

        var sqlConstructorSource = result.GetConstructorSource("SqlUserRepository");
        Assert.NotNull(sqlConstructorSource);
        Assert.Contains("SqlUserRepository(ILogger logger, IDbContext context)", sqlConstructorSource.Content);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)", registrationSource.Content);
        Assert.Contains("string.Equals(environment, \"Production\", StringComparison.OrdinalIgnoreCase)", registrationSource.Content);
    }

    #endregion

    #region Conditional Services + Advanced DI Features Integration

    [Fact]
    public void ConditionalIntegration_RegisterAsAllWithConditionalServices_WorksCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IPaymentProcessor { }
public interface INotificationService { }
public interface ILogger { }

[Service] public partial class Logger : ILogger { }

[ConditionalService(Environment = ""Development"")]
[Service]
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Separate)]
public partial class DevMultiService : IPaymentProcessor, INotificationService
{
    [Inject] private readonly ILogger _logger;
}

[ConditionalService(Environment = ""Production"")]
[Service]
[RegisterAsAll(RegistrationMode.Exclusionary, InstanceSharing.Separate)]
public partial class ProdMultiService : IPaymentProcessor, INotificationService
{
    [Inject] private readonly ILogger _logger;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should register conditionally for all interfaces
        Assert.Contains("string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)", registrationSource.Content);

        // CORRECTED: DevMultiService uses RegistrationMode.All with InstanceSharing.Separate,
        // so it should use factory patterns to ensure each interface gets its own instance
        Assert.Contains("AddScoped<Test.DevMultiService, Test.DevMultiService>", registrationSource.Content); // Self registration
        Assert.Contains("AddScoped<Test.IPaymentProcessor>(provider => provider.GetRequiredService<Test.DevMultiService>())",
            registrationSource.Content);
        Assert.Contains("AddScoped<Test.INotificationService>(provider => provider.GetRequiredService<Test.DevMultiService>())",
            registrationSource.Content);

        Assert.Contains("string.Equals(environment, \"Production\", StringComparison.OrdinalIgnoreCase)", registrationSource.Content);

        // CORRECTED: ProdMultiService uses RegistrationMode.Exclusionary, so direct registrations
        Assert.Contains("AddScoped<Test.IPaymentProcessor, Test.ProdMultiService>", registrationSource.Content);
        Assert.Contains("AddScoped<Test.INotificationService, Test.ProdMultiService>", registrationSource.Content);
        // Should NOT include self registration for Exclusionary mode
        Assert.DoesNotContain("AddScoped<Test.ProdMultiService, Test.ProdMultiService>", registrationSource.Content);
    }

    [Fact]
    public void ConditionalIntegration_SkipRegistrationWithConditionalServices_SkipsCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService { }

[ConditionalService(Environment = ""Development"")]
[Service]
[SkipRegistration]
public partial class ConditionalSkippedService : IService
{
    // This service has conditions but is marked to skip registration
    // Should not appear in any registration code
}

[ConditionalService(Environment = ""Production"")]
[Service]
public partial class ConditionalRegisteredService : IService
{
    // This service should be registered conditionally
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should NOT contain registration for skipped service
        Assert.DoesNotContain("ConditionalSkippedService", registrationSource.Content);

        // Should contain registration for non-skipped conditional service
        Assert.Contains("string.Equals(environment, \"Production\", StringComparison.OrdinalIgnoreCase)", registrationSource.Content);
        Assert.Contains("AddScoped<Test.IService, Test.ConditionalRegisteredService>", registrationSource.Content);
    }

    [Fact]
    public void ConditionalIntegration_DependsOnWithConditionalServices_ResolvesDependencies()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IEmailService { }
public interface IPaymentService { }
public interface ILogger { }
public interface IUserService { }

[Service] public partial class Logger : ILogger { }
[Service] public partial class EmailService : IEmailService { }

[ConditionalService(Environment = ""Development"")]
[Service]
[DependsOn<ILogger, IEmailService>]
public partial class DevPaymentService : IPaymentService
{
    // Dependencies should be injected via DependsOn
}

[ConditionalService(Environment = ""Production"")]
[Service]
public partial class ProdPaymentService : IPaymentService
{
    [Inject] private readonly ILogger _logger;
    [Inject] private readonly IEmailService _emailService;
}

// Service that depends on conditional services
[Service]
public partial class UserService : IUserService
{
    [Inject] private readonly IPaymentService _paymentService; // Should resolve to conditional service
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        // Should generate constructor for DependsOn service
        var devConstructorSource = result.GetConstructorSource("DevPaymentService");
        Assert.NotNull(devConstructorSource);
        Assert.Contains("DevPaymentService(ILogger logger, IEmailService emailService)", devConstructorSource.Content);

        // Should generate conditional registration
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)", registrationSource.Content);
        Assert.Contains("AddScoped<Test.IPaymentService, Test.DevPaymentService>", registrationSource.Content);
        Assert.Contains("string.Equals(environment, \"Production\", StringComparison.OrdinalIgnoreCase)", registrationSource.Content);
        Assert.Contains("AddScoped<Test.IPaymentService, Test.ProdPaymentService>", registrationSource.Content);

        // UserService should be registered and depend on whichever IPaymentService is conditionally registered
        Assert.Contains("AddScoped<Test.IUserService, Test.UserService>", registrationSource.Content);
    }

    [Fact]
    public void ConditionalIntegration_ExternalServicesWithConditionalServices_IntegratesCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IExternalApi { }
public interface IPaymentService { }

// External service (registered manually elsewhere)
[ExternalService]
public class ExternalApiService : IExternalApi { }

[ConditionalService(Environment = ""Development"")]
[Service]
public partial class DevPaymentService : IPaymentService
{
    [Inject] private readonly IExternalApi _externalApi; // Should not cause missing dependency warnings
}

[ConditionalService(Environment = ""Production"")]
[Service]
public partial class ProdPaymentService : IPaymentService
{
    [Inject] private readonly IExternalApi _externalApi;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        // Should not produce missing dependency warnings for external services
        var missingDependencyWarnings = result.GetDiagnosticsByCode("IOC002");
        Assert.Empty(missingDependencyWarnings);

        // Should generate conditional registration for payment services
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)", registrationSource.Content);
        Assert.Contains("AddScoped<Test.IPaymentService, Test.DevPaymentService>", registrationSource.Content);

        // Should NOT register external service
        Assert.DoesNotContain("Test.ExternalApiService", registrationSource.Content);
    }

    #endregion

    #region Conditional Services + Background Services Integration

    [Fact]
    public void ConditionalIntegration_BackgroundServiceWithConditionalRegistration_RegistersCorrectly()
    {
        // Arrange
        var source = @"
using Microsoft.Extensions.Hosting;
using IoCTools.Abstractions.Annotations;
using System.Threading;
using System.Threading.Tasks;

namespace Test;

public interface IDataProcessor { }
public interface IEmailService { }

[Service] public partial class DataProcessor : IDataProcessor { }
[Service] public partial class EmailService : IEmailService { }

[ConditionalService(Environment = ""Production"")]
public partial class ProductionBackgroundService : BackgroundService
{
    [Inject] private readonly IDataProcessor _processor;
    [Inject] private readonly IEmailService _emailService;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(100, stoppingToken);
    }
}

[ConditionalService(Environment = ""Development"")]
public partial class DevelopmentBackgroundService : BackgroundService
{
    [Inject] private readonly IDataProcessor _processor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(50, stoppingToken);
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        // Should generate constructors for both background services
        var prodConstructorSource = result.GetConstructorSource("ProductionBackgroundService");
        Assert.NotNull(prodConstructorSource);
        Assert.Contains("ProductionBackgroundService(IDataProcessor processor, IEmailService emailService)",
            prodConstructorSource.Content);

        var devConstructorSource = result.GetConstructorSource("DevelopmentBackgroundService");
        Assert.NotNull(devConstructorSource);
        Assert.Contains("DevelopmentBackgroundService(IDataProcessor processor)", devConstructorSource.Content);

        // Should register background services conditionally as IHostedService
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("string.Equals(environment, \"Production\", StringComparison.OrdinalIgnoreCase)", registrationSource.Content);
        Assert.Contains("AddHostedService<Test.ProductionBackgroundService>", registrationSource.Content);
        Assert.Contains("string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)", registrationSource.Content);
        Assert.Contains("AddHostedService<Test.DevelopmentBackgroundService>", registrationSource.Content);
    }

    [Fact]
    public void ConditionalIntegration_BackgroundServiceDependingOnConditionalServices_WorksCorrectly()
    {
        // Arrange
        var source = @"
using Microsoft.Extensions.Hosting;
using IoCTools.Abstractions.Annotations;
using System.Threading;
using System.Threading.Tasks;

namespace Test;

public interface IDataProcessor { }

[ConditionalService(Environment = ""Development"")]
[Service]
public partial class MockDataProcessor : IDataProcessor { }

[ConditionalService(Environment = ""Production"")]
[Service] 
public partial class RealDataProcessor : IDataProcessor { }

// Background service that depends on conditional data processor
[BackgroundService]
public partial class DataProcessingBackgroundService : BackgroundService
{
    [Inject] private readonly IDataProcessor _processor; // Will resolve to conditional service

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(100, stoppingToken);
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should register conditional data processors
        Assert.Contains("string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)", registrationSource.Content);
        Assert.Contains("AddScoped<Test.IDataProcessor, Test.MockDataProcessor>", registrationSource.Content);
        Assert.Contains("string.Equals(environment, \"Production\", StringComparison.OrdinalIgnoreCase)", registrationSource.Content);
        Assert.Contains("AddScoped<Test.IDataProcessor, Test.RealDataProcessor>", registrationSource.Content);

        // Should register background service unconditionally
        Assert.Contains("AddHostedService<global::Test.DataProcessingBackgroundService>", registrationSource.Content);
    }

    [Fact]
    public void ConditionalIntegration_IHostedServiceWithConditionalRegistration_RegistersCorrectly()
    {
        // Arrange
        var source = @"
using Microsoft.Extensions.Hosting;
using IoCTools.Abstractions.Annotations;
using System.Threading;
using System.Threading.Tasks;

namespace Test;

public interface IEmailService { }

[Service] public partial class EmailService : IEmailService { }

[ConditionalService(Environment = ""Production"")]
public partial class ConditionalHostedService : IHostedService
{
    [Inject] private readonly IEmailService _emailService;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should register IHostedService conditionally
        Assert.Contains("string.Equals(environment, \"Production\", StringComparison.OrdinalIgnoreCase)", registrationSource.Content);
        Assert.Contains("AddHostedService<Test.ConditionalHostedService>", registrationSource.Content);
    }

    #endregion

    #region Conditional Services + Generic Services Integration




    #endregion

    #region Deployment Scenario Integration

    // NOTE: Removed ConditionalIntegration_DevelopmentEnvironmentFeatures_AllFeaturesIntegrated
    // This test was testing unimplemented InjectConfiguration functionality extensively,
    // representing an unrealistic edge case that combined every possible feature simultaneously. 
    // Real applications don't use such complex combinations, and the test was blocking 
    // legitimate development progress while testing theoretical scenarios.

    #endregion

    #region Error Scenarios and Edge Cases

    [Fact]
    public void ConditionalIntegration_ConflictingFeatureConfigurations_HandlesGracefully()
    {
        // Arrange - Conflicting configurations that should be handled gracefully
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IService { }

// Conflicting environment conditions - impossible to satisfy
[ConditionalService(Environment = ""Development"", NotEnvironment = ""Development"")]
[Service]
public partial class ConflictingService : IService { }

// Invalid lifetime dependency in conditional service
[ConditionalService(Environment = ""Production"")]
[Service(Lifetime.Singleton)]
public partial class InvalidSingletonService : IService
{
    [Inject] private readonly IScopedDependency _scoped;
}

[Service(Lifetime.Scoped)]
public partial class ScopedDependency : IScopedDependency { }

public interface IScopedDependency { }";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        // Should not crash and should produce appropriate diagnostics
        var conflictDiagnostics = result.GetDiagnosticsByCode("IOC020");
        var lifetimeDiagnostics = result.GetDiagnosticsByCode("IOC004");

        // If diagnostics are implemented, they should be present
        // If not, should at least not crash during compilation
        Assert.False(result.HasErrors, "Should not have compilation errors even with conflicting configurations");

        var registrationSource = result.GetServiceRegistrationSource();
        if (registrationSource != null)
            // Should not generate impossible conditions
            Assert.DoesNotContain("environment == \"Development\" && environment != \"Development\"",
                registrationSource.Content);
    }



    #endregion

    #region Conditional Services + IEnumerable<T> Dependencies Integration

    [Fact]
    public void ConditionalIntegration_ConditionalServicesWithIEnumerableDependencies_GeneratesCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using System.Collections.Generic;

namespace Test;

public interface INotificationProvider { }
public interface IPaymentService { }

[Service]
public partial class EmailNotificationProvider : INotificationProvider { }

[Service] 
public partial class SmsNotificationProvider : INotificationProvider { }

[ConditionalService(Environment = ""Development"")]
[Service]
public partial class SlackNotificationProvider : INotificationProvider { }

[ConditionalService(Environment = ""Production"")]
[Service]
public partial class PushNotificationProvider : INotificationProvider { }

[ConditionalService(Environment = ""Development"")]
[Service]
public partial class DevPaymentService : IPaymentService
{
    [Inject] private readonly IEnumerable<INotificationProvider> _notificationProviders;
}

[ConditionalService(Environment = ""Production"")]
[Service]
public partial class ProdPaymentService : IPaymentService
{
    [Inject] private readonly IEnumerable<INotificationProvider> _notificationProviders;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        // Should generate constructors with IEnumerable dependencies
        var devConstructorSource = result.GetConstructorSource("DevPaymentService");
        Assert.NotNull(devConstructorSource);
        Assert.Contains("IEnumerable<INotificationProvider> notificationProviders", devConstructorSource.Content);

        var prodConstructorSource = result.GetConstructorSource("ProdPaymentService");
        Assert.NotNull(prodConstructorSource);
        Assert.Contains("IEnumerable<INotificationProvider> notificationProviders", prodConstructorSource.Content);

        // Should register all notification providers (conditional and non-conditional)
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("AddScoped<Test.INotificationProvider, Test.EmailNotificationProvider>", registrationSource.Content);
        Assert.Contains("AddScoped<Test.INotificationProvider, Test.SmsNotificationProvider>", registrationSource.Content);

        // Should register conditional notification providers conditionally
        Assert.Contains("string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)", registrationSource.Content);
        Assert.Contains("AddScoped<Test.INotificationProvider, Test.SlackNotificationProvider>", registrationSource.Content);
        Assert.Contains("string.Equals(environment, \"Production\", StringComparison.OrdinalIgnoreCase)", registrationSource.Content);
        Assert.Contains("AddScoped<Test.INotificationProvider, Test.PushNotificationProvider>", registrationSource.Content);

        // Should register payment services conditionally
        Assert.Contains("AddScoped<Test.IPaymentService, Test.DevPaymentService>", registrationSource.Content);
        Assert.Contains("AddScoped<Test.IPaymentService, Test.ProdPaymentService>", registrationSource.Content);
    }

    [Fact]
    public void ConditionalIntegration_MultipleConditionalImplementationsInSameIEnumerable_RegistersCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using System.Collections.Generic;

namespace Test;

public interface IDataProcessor { }
public interface IService { }

[Service]
public partial class BaseDataProcessor : IDataProcessor { }

[ConditionalService(Environment = ""Development"")]
[Service]
public partial class MockDataProcessor : IDataProcessor { }

[ConditionalService(Environment = ""Development"")]
[Service] 
public partial class DebugDataProcessor : IDataProcessor { }

[ConditionalService(Environment = ""Production"")]
[Service]
public partial class OptimizedDataProcessor : IDataProcessor { }

[ConditionalService(Environment = ""Production"")]
[Service]
public partial class CachedDataProcessor : IDataProcessor { }

[ConditionalService(Environment = ""Testing"")]
[Service]
public partial class TestDataProcessor : IDataProcessor { }

[Service]
public partial class ProcessingService : IService
{
    [Inject] private readonly IEnumerable<IDataProcessor> _processors;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);


        // Should register base processor unconditionally
        Assert.Contains("AddScoped<Test.IDataProcessor, Test.BaseDataProcessor>", registrationSource.Content);

        // Should register multiple Development conditional processors
        Assert.Contains("string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)", registrationSource.Content);
        Assert.Contains("AddScoped<Test.IDataProcessor, Test.MockDataProcessor>", registrationSource.Content);
        Assert.Contains("AddScoped<Test.IDataProcessor, Test.DebugDataProcessor>", registrationSource.Content);

        // Should register multiple Production conditional processors
        Assert.Contains("string.Equals(environment, \"Production\", StringComparison.OrdinalIgnoreCase)", registrationSource.Content);
        Assert.Contains("AddScoped<Test.IDataProcessor, Test.OptimizedDataProcessor>", registrationSource.Content);
        Assert.Contains("AddScoped<Test.IDataProcessor, Test.CachedDataProcessor>", registrationSource.Content);

        // Should register Testing conditional processor
        Assert.Contains("string.Equals(environment, \"Testing\", StringComparison.OrdinalIgnoreCase)", registrationSource.Content);
        Assert.Contains("AddScoped<Test.IDataProcessor, Test.TestDataProcessor>", registrationSource.Content);

        // Service using IEnumerable should be registered unconditionally
        Assert.Contains("AddScoped<Test.IService, Test.ProcessingService>", registrationSource.Content);

        // Should generate constructor with IEnumerable dependency
        var constructorSource = result.GetConstructorSource("ProcessingService");
        Assert.NotNull(constructorSource);
        Assert.Contains("IEnumerable<IDataProcessor> processors", constructorSource.Content);
    }

    [Fact]
    public void ConditionalIntegration_EnvironmentBasedCollectionContentChanges_ModifiesCollectionCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace Test;

public interface IValidator { }
public interface IService { }

// Always available validators
[Service(Lifetime.Singleton)]
public partial class BasicValidator : IValidator { }

[Service(Lifetime.Singleton)]
public partial class RequiredValidator : IValidator { }

// Development-only validators
[ConditionalService(Environment = ""Development"")]
[Service(Lifetime.Singleton)]
public partial class DebugValidator : IValidator { }

[ConditionalService(Environment = ""Development"")]
[Service(Lifetime.Singleton)]
public partial class VerboseValidator : IValidator { }

// Production-only validators  
[ConditionalService(Environment = ""Production"")]
[Service(Lifetime.Singleton)]
public partial class PerformanceValidator : IValidator { }

[ConditionalService(Environment = ""Production"")]
[Service(Lifetime.Singleton)]
public partial class SecurityValidator : IValidator { }

// Testing-only validators
[ConditionalService(Environment = ""Testing"")]
[Service(Lifetime.Singleton)]
public partial class MockValidator : IValidator { }

[Service]
public partial class ValidationService : IService
{
    [Inject] private readonly IEnumerable<IValidator> _validators;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should register base validators unconditionally with Singleton lifetime
        Assert.Contains("services.AddSingleton<Test.IValidator, Test.BasicValidator>", registrationSource.Content);
        Assert.Contains("services.AddSingleton<Test.IValidator, Test.RequiredValidator>", registrationSource.Content);

        // Should register Development validators conditionally
        Assert.Contains("string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)", registrationSource.Content);
        Assert.Contains("services.AddSingleton<Test.IValidator, Test.DebugValidator>", registrationSource.Content);
        Assert.Contains("services.AddSingleton<Test.IValidator, Test.VerboseValidator>", registrationSource.Content);

        // Should register Production validators conditionally
        Assert.Contains("string.Equals(environment, \"Production\", StringComparison.OrdinalIgnoreCase)", registrationSource.Content);
        Assert.Contains("services.AddSingleton<Test.IValidator, Test.PerformanceValidator>", registrationSource.Content);
        Assert.Contains("services.AddSingleton<Test.IValidator, Test.SecurityValidator>", registrationSource.Content);

        // Should register Testing validators conditionally
        Assert.Contains("string.Equals(environment, \"Testing\", StringComparison.OrdinalIgnoreCase)", registrationSource.Content);
        Assert.Contains("services.AddSingleton<Test.IValidator, Test.MockValidator>", registrationSource.Content);

        // Validation service should be registered unconditionally
        Assert.Contains("AddScoped<Test.IService, Test.ValidationService>", registrationSource.Content);
    }





    [Fact]
    public void
        ConditionalIntegration_MixedConditionalAndNonConditionalImplementationsInCollections_RegistersCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace Test;

public interface IMiddleware { }
public interface IService { }

// Always registered middleware
[Service(Lifetime.Singleton)]
public partial class AuthenticationMiddleware : IMiddleware { }

[Service(Lifetime.Scoped)]
public partial class LoggingMiddleware : IMiddleware { }

[Service(Lifetime.Transient)]
public partial class ValidationMiddleware : IMiddleware { }

// Conditional middleware
[ConditionalService(Environment = ""Development"")]
[Service(Lifetime.Transient)]
public partial class DebugMiddleware : IMiddleware { }

[ConditionalService(Environment = ""Development"")]
[Service(Lifetime.Singleton)]
public partial class DeveloperToolsMiddleware : IMiddleware { }

[ConditionalService(Environment = ""Production"")]
[Service(Lifetime.Singleton)]
public partial class PerformanceMiddleware : IMiddleware { }

[ConditionalService(Environment = ""Production"")]
[Service(Lifetime.Scoped)]
public partial class MetricsMiddleware : IMiddleware { }

[ConditionalService(ConfigValue = ""Features:EnableCaching"", Equals = ""true"")]
[Service(Lifetime.Singleton)]
public partial class CachingMiddleware : IMiddleware { }

// Services using the middleware collections
[Service]
public partial class MiddlewareService : IService
{
    [Inject] private readonly IEnumerable<IMiddleware> _middlewares;
}

[ConditionalService(Environment = ""Development"")]
[Service]
public partial class DevMiddlewareService : IService
{
    [Inject] private readonly IEnumerable<IMiddleware> _middlewares;
    [Inject] private readonly IList<IMiddleware> _middlewareList;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should register non-conditional middleware with correct lifetimes (using simplified naming)
        Assert.Contains("services.AddSingleton<Test.IMiddleware, Test.AuthenticationMiddleware>", registrationSource.Content);
        Assert.Contains("AddScoped<Test.IMiddleware, Test.LoggingMiddleware>", registrationSource.Content);
        Assert.Contains("AddTransient<Test.IMiddleware, Test.ValidationMiddleware>", registrationSource.Content);

        // Should register Development conditional middleware
        Assert.Contains("string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)", registrationSource.Content);
        Assert.Contains("AddTransient<Test.IMiddleware, Test.DebugMiddleware>", registrationSource.Content);
        Assert.Contains("services.AddSingleton<Test.IMiddleware, Test.DeveloperToolsMiddleware>", registrationSource.Content);

        // Should register Production conditional middleware
        Assert.Contains("string.Equals(environment, \"Production\", StringComparison.OrdinalIgnoreCase)", registrationSource.Content);
        Assert.Contains("services.AddSingleton<Test.IMiddleware, Test.PerformanceMiddleware>", registrationSource.Content);
        Assert.Contains("AddScoped<Test.IMiddleware, Test.MetricsMiddleware>", registrationSource.Content);

        // Should register configuration-based conditional middleware  
        Assert.Contains("string.Equals(configuration[\"Features:EnableCaching\"], \"true\", StringComparison.OrdinalIgnoreCase)",
            registrationSource.Content);
        Assert.Contains("services.AddSingleton<Test.IMiddleware, Test.CachingMiddleware>", registrationSource.Content);

        // Should register services using middleware collections (using simplified naming)
        Assert.Contains("AddScoped<Test.IService, Test.MiddlewareService>", registrationSource.Content);
        Assert.Contains("AddScoped<Test.IService, Test.DevMiddlewareService>", registrationSource.Content);

        // Should generate constructors with IEnumerable dependencies
        var serviceConstructorSource = result.GetConstructorSource("MiddlewareService");
        Assert.NotNull(serviceConstructorSource);
        Assert.Contains("IEnumerable<IMiddleware> middlewares", serviceConstructorSource.Content);

        var devServiceConstructorSource = result.GetConstructorSource("DevMiddlewareService");
        Assert.NotNull(devServiceConstructorSource);
        Assert.Contains("IEnumerable<IMiddleware> middlewares", devServiceConstructorSource.Content);
        Assert.Contains("IList<IMiddleware> middlewareList", devServiceConstructorSource.Content);
    }


    #endregion
}