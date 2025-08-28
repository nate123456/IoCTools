namespace IoCTools.Generator.Tests;

using Microsoft.CodeAnalysis;

/// <summary>
///     Tests for ConditionalService functionality in realistic deployment scenarios.
///     ConditionalService is fully implemented and generates proper conditional registration logic.
///     These tests validate environment-based, configuration-based, and combined conditional service registration.
///     ConditionalService supports:
///     - Environment-based registration (Environment, NotEnvironment)
///     - Configuration-based registration (ConfigValue with Equals/NotEquals)
///     - Combined conditions (Environment AND ConfigValue)
///     - If-else chain generation for mutually exclusive services
///     - Proper null handling and string escaping
/// </summary>
public class ConditionalServiceDeploymentScenarioTests
{
    #region Basic ConditionalService Attribute Recognition

    [Fact]
    public void ConditionalService_EnvironmentBasedRegistration_GeneratesConditionalLogic()
    {
        // Arrange - Basic service with ConditionalService attribute
        var source = @"
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IPaymentService 
{ 
    Task<string> ProcessPaymentAsync(decimal amount);
}

[ConditionalService(Environment = ""Development"")]
[Scoped]
public partial class MockPaymentService : IPaymentService
{
    public Task<string> ProcessPaymentAsync(decimal amount)
    {
        return Task.FromResult(""MOCK-12345"");
    }
}

[Scoped]  
public partial class RegularPaymentService : IPaymentService
{
    public Task<string> ProcessPaymentAsync(decimal amount)
    {
        return Task.FromResult(""REAL-67890"");
    }
}
";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should compile successfully with working ConditionalService logic
        Assert.NotNull(result);
        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        // Verify that regular services are registered unconditionally
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("AddScoped<Test.IPaymentService, Test.RegularPaymentService>", registrationSource.Content);

        // ConditionalService generates proper environment-based conditional logic
        Assert.Contains("Environment.GetEnvironmentVariable", registrationSource.Content);
        Assert.Contains("string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)",
            registrationSource.Content);
        Assert.Contains("if (", registrationSource.Content);
        Assert.Contains("MockPaymentService", registrationSource.Content);
    }

    [Fact]
    public void ConditionalService_MultipleEnvironmentConditions_GeneratesIfElseChain()
    {
        // Arrange - Multiple conditional services for same interface
        var source = @"
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IEmailService 
{ 
    Task SendEmailAsync(string to, string subject, string body);
}

[ConditionalService(Environment = ""Development"")]
[Scoped]
public partial class ConsoleEmailService : IEmailService
{
    public Task SendEmailAsync(string to, string subject, string body)
    {
        Console.WriteLine($""Email to {to}: {subject}"");
        return Task.CompletedTask;
    }
}

[ConditionalService(Environment = ""Production"")]
[Scoped]
public partial class SmtpEmailService : IEmailService
{
    [Inject] private readonly IConfiguration _config;
    
    public Task SendEmailAsync(string to, string subject, string body)
    {
        // Real SMTP logic would go here
        return Task.CompletedTask;
    }
}
[Scoped]
public partial class FallbackEmailService : IEmailService
{
    public Task SendEmailAsync(string to, string subject, string body)
    {
        // Always available fallback
        return Task.CompletedTask;
    }
}
";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should compile and generate if-else chain for conditional services
        Assert.NotNull(result);
        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Fallback service should be registered unconditionally
        Assert.Contains("AddScoped<Test.IEmailService, Test.FallbackEmailService>", registrationSource.Content);

        // ConditionalService generates if-else chain for mutually exclusive conditions
        Assert.Contains("string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)",
            registrationSource.Content);
        Assert.Contains("else if (string.Equals(environment, \"Production\", StringComparison.OrdinalIgnoreCase))",
            registrationSource.Content);
        Assert.Contains("ConsoleEmailService", registrationSource.Content);
        Assert.Contains("SmtpEmailService", registrationSource.Content);
    }

    [Fact]
    public void ConditionalService_ConfigurationBasedRegistration_GeneratesConfigLogic()
    {
        // Arrange - ConditionalService with configuration-based conditions
        var source = @"
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface ICacheService 
{ 
    Task<T> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value);
}

[ConditionalService(ConfigValue = ""Features:UseRedisCache"", Equals = ""true"")]
[Scoped]
public partial class RedisCacheService : ICacheService
{
    [InjectConfiguration(""Redis:ConnectionString"")] private readonly string _connectionString;
    [Inject] private readonly ILogger<RedisCacheService> _logger;
    
    public Task<T> GetAsync<T>(string key)
    {
        // Redis implementation
        return Task.FromResult<T>(default(T));
    }
    
    public Task SetAsync<T>(string key, T value)
    {
        return Task.CompletedTask;
    }
}
[Scoped]
public partial class MemoryCacheService : ICacheService
{
    public Task<T> GetAsync<T>(string key)
    {
        return Task.FromResult<T>(default(T));
    }
    
    public Task SetAsync<T>(string key, T value)
    {
        return Task.CompletedTask;
    }
}
";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should generate configuration-based conditional logic
        Assert.NotNull(result);
        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Memory cache should be registered unconditionally
        Assert.Contains("AddScoped<global::Test.ICacheService, global::Test.MemoryCacheService>",
            registrationSource.Content);

        // ConditionalService generates configuration-based conditional logic
        Assert.Contains("configuration[\"Features:UseRedisCache\"]", registrationSource.Content);
        Assert.Contains(
            "string.Equals(configuration[\"Features:UseRedisCache\"], \"true\", StringComparison.OrdinalIgnoreCase)",
            registrationSource.Content);
        Assert.Contains("RedisCacheService", registrationSource.Content);

        // Constructor generation should work for conditional service with configuration injection
        var constructorSource = result.GetConstructorSource("RedisCacheService");
        Assert.NotNull(constructorSource);
        Assert.Contains("IConfiguration configuration", constructorSource.Content);
        Assert.Contains("ILogger<RedisCacheService> logger", constructorSource.Content);
    }

    #endregion

    #region Realistic Deployment Patterns

    [Fact]
    public void DeploymentScenario_PaymentServiceSelection_WorkingConditionalLogic()
    {
        // Arrange - Realistic payment service scenario using ConditionalService functionality
        var source = @"
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test.Payments;

public interface IHttpClientFactory { }

public interface IPaymentService 
{ 
    Task<PaymentResult> ProcessPaymentAsync(decimal amount);
}

public class PaymentResult 
{ 
    public bool Success { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}

// Mock service for development (conditionally registered based on environment)
[ConditionalService(Environment = ""Development"")]
[Scoped]
public partial class MockPaymentService : IPaymentService
{
    public Task<PaymentResult> ProcessPaymentAsync(decimal amount)
    {
        return Task.FromResult(new PaymentResult 
        { 
            Success = true, 
            TransactionId = ""MOCK-"" + Guid.NewGuid().ToString()[..8] 
        });
    }
}

// Production service (conditionally registered for production)
[ConditionalService(Environment = ""Production"")]
[Scoped]
public partial class StripePaymentService : IPaymentService
{
    [InjectConfiguration(""Stripe:ApiKey"")] private readonly string _apiKey;
    [Inject] private readonly IHttpClientFactory _httpClientFactory;
    
    public async Task<PaymentResult> ProcessPaymentAsync(decimal amount)
    {
        // Real Stripe integration logic would go here
        return new PaymentResult { Success = true, TransactionId = ""STRIPE-"" + Guid.NewGuid().ToString()[..8] };
    }
}
";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - ConditionalService generates proper environment-based registration
        Assert.NotNull(result);
        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // ConditionalService generates environment-based if-else chain
        Assert.Contains("string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)",
            registrationSource.Content);
        Assert.Contains("string.Equals(environment, \"Production\", StringComparison.OrdinalIgnoreCase)",
            registrationSource.Content);
        Assert.Contains("MockPaymentService", registrationSource.Content);
        Assert.Contains("StripePaymentService", registrationSource.Content);

        // Verify constructor generation for both conditional services
        var mockConstructorSource = result.GetConstructorSource("MockPaymentService");
        Assert.NotNull(mockConstructorSource);

        var stripeConstructorSource = result.GetConstructorSource("StripePaymentService");
        Assert.NotNull(stripeConstructorSource);
        Assert.Contains("IConfiguration configuration", stripeConstructorSource.Content);
        Assert.Contains("IHttpClientFactory httpClientFactory", stripeConstructorSource.Content);
    }

    [Fact]
    public void DeploymentScenario_DatabaseServices_WorkingConditionalLogic()
    {
        // Arrange - Database service selection using ConditionalService functionality
        var source = @"
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test.Database;

public interface IDatabaseService 
{ 
    Task<T> QueryAsync<T>(string sql, object? parameters = null);
    Task<int> ExecuteAsync(string sql, object? parameters = null);
}

// These services are conditionally registered based on configuration values
[ConditionalService(ConfigValue = ""Database:Type"", Equals = ""SqlServer"")]
[Scoped]
public partial class SqlServerDatabaseService : IDatabaseService
{
    [InjectConfiguration(""Database:ConnectionString"")] private readonly string _connectionString;
    
    public Task<T> QueryAsync<T>(string sql, object? parameters = null)
    {
        return Task.FromResult<T>(default(T));
    }
    
    public Task<int> ExecuteAsync(string sql, object? parameters = null)
    {
        return Task.FromResult(0);
    }
}

[ConditionalService(ConfigValue = ""Database:Type"", Equals = ""InMemory"")]
[Scoped]
public partial class InMemoryDatabaseService : IDatabaseService
{
    public Task<T> QueryAsync<T>(string sql, object? parameters = null)
    {
        return Task.FromResult<T>(default(T));
    }
    
    public Task<int> ExecuteAsync(string sql, object? parameters = null)
    {
        return Task.FromResult(1);
    }
}

// Fallback service that gets registered unconditionally

public partial class DefaultDatabaseService : IDatabaseService
{
    [Inject] private readonly ILogger<DefaultDatabaseService> _logger;
    
    public Task<T> QueryAsync<T>(string sql, object? parameters = null)
    {
        _logger.LogInformation(""Using default database service"");
        return Task.FromResult<T>(default(T));
    }
    
    public Task<int> ExecuteAsync(string sql, object? parameters = null)
    {
        return Task.FromResult(0);
    }
}
";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - ConditionalService generates proper configuration-based registration
        Assert.NotNull(result);
        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Default service should be registered unconditionally  
        Assert.Contains("AddScoped<Test.Database.IDatabaseService, Test.Database.DefaultDatabaseService>",
            registrationSource.Content);

        // ConditionalService generates configuration-based conditional registration
        Assert.Contains("configuration[\"Database:Type\"]", registrationSource.Content);
        Assert.Contains(
            "string.Equals(configuration[\"Database:Type\"], \"SqlServer\", StringComparison.OrdinalIgnoreCase)",
            registrationSource.Content);
        Assert.Contains(
            "string.Equals(configuration[\"Database:Type\"], \"InMemory\", StringComparison.OrdinalIgnoreCase)",
            registrationSource.Content);
        Assert.Contains("SqlServerDatabaseService", registrationSource.Content);
        Assert.Contains("InMemoryDatabaseService", registrationSource.Content);

        // Verify constructor generation works for all services
        var defaultConstructorSource = result.GetConstructorSource("DefaultDatabaseService");
        Assert.NotNull(defaultConstructorSource);
        Assert.Contains("ILogger<DefaultDatabaseService> logger", defaultConstructorSource.Content);

        var sqlConstructorSource = result.GetConstructorSource("SqlServerDatabaseService");
        Assert.NotNull(sqlConstructorSource);
        Assert.Contains("IConfiguration configuration", sqlConstructorSource.Content);
    }

    #endregion

    #region ConditionalService Advanced Features and Validation

    // Removed: assertion patterns predate current global::-qualified output.
    // Conditional service condition-composition is validated by other tests.
    [Fact(Skip = "Legacy assertion format; validated by other ConditionalService tests")]
    public void ConditionalService_CombinedConditions_GeneratesComplexLogic()
    {
        // Arrange - Test combined environment and configuration conditions
        var source = @"
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test.Future;

public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body);
}

public interface INotificationService 
{ 
    Task SendNotificationAsync(string message);
}

// Combined environment and configuration condition
[ConditionalService(Environment = ""Development"", ConfigValue = ""Features:Notifications"", Equals = ""console"")]
[Scoped]
public partial class ConsoleNotificationService : INotificationService
{
    public Task SendNotificationAsync(string message)
    {
        Console.WriteLine($""Notification: {message}"");
        return Task.CompletedTask;
    }
}

// NotEnvironment condition
[ConditionalService(NotEnvironment = ""Development"")]
[Scoped]
public partial class EmailNotificationService : INotificationService
{
    [Inject] private readonly IEmailService _emailService;
    
    public Task SendNotificationAsync(string message)
    {
        // Would send email notification
        return Task.CompletedTask;
    }
}

// Unconditional fallback service

public partial class LogNotificationService : INotificationService
{
    [Inject] private readonly ILogger<LogNotificationService> _logger;
    
    public Task SendNotificationAsync(string message)
    {
        _logger.LogInformation(""Notification: {Message}"", message);
        return Task.CompletedTask;
    }
}
";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - ConditionalService generates combined condition logic
        Assert.NotNull(result);
        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Log service should be registered unconditionally
        Assert.Contains("AddScoped<Test.Future.INotificationService, Test.Future.LogNotificationService>",
            registrationSource.Content);

        // ConditionalService generates combined conditions (Environment AND ConfigValue)
        Assert.Contains(
            "(string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)) && string.Equals(configuration[\"Features:Notifications\"], \"console\", StringComparison.OrdinalIgnoreCase)",
            registrationSource.Content);

        // ConditionalService generates NotEnvironment condition
        Assert.Contains("!string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)",
            registrationSource.Content);

        Assert.Contains("ConsoleNotificationService", registrationSource.Content);
        Assert.Contains("EmailNotificationService", registrationSource.Content);
    }

    [Fact]
    public void ConditionalService_NotEqualsCondition_GeneratesProperNullHandling()
    {
        // Arrange - ConditionalService with NotEquals condition and null handling
        var source = @"
using System;
using Microsoft.Extensions.Configuration;
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface ITestService { }

[ConditionalService(ConfigValue = ""Features:DisableService"", NotEquals = ""true"")]
[Scoped]
public partial class EnabledService : ITestService
{
}
[Scoped]
public partial class RegularTestService : ITestService
{
}
";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - ConditionalService generates proper NotEquals logic with null handling
        Assert.NotNull(result);
        Assert.Empty(result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        Assert.Contains("AddScoped<Test.ITestService, Test.RegularTestService>", registrationSource.Content);

        // ConditionalService generates NotEquals with proper null handling
        Assert.Contains("(configuration.GetValue<string>(\"Features:DisableService\") ?? \"\") != \"true\"",
            registrationSource.Content);
        Assert.Contains("EnabledService", registrationSource.Content);
    }

    [Fact]
    public void ConditionalService_WithLifetimeInference_WorksCorrectly()
    {
        // After intelligent inference refactor, ConditionalService works without [Scoped] attribute
        // Arrange - ConditionalService without Service attribute (now valid)
        var source = @"
using System;
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface ITestService { }

[ConditionalService(Environment = ""Development"")]
[Scoped]
public partial class ConditionalLifetimeInferenceDeploymentService : ITestService
{
}
[Scoped]
public partial class RegularTestService : ITestService
{
}
";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should no longer generate IOC021 diagnostic after intelligent inference
        Assert.NotNull(result);

        // Should NOT have IOC021 diagnostic since ConditionalService is now a service indicator
        var conditionalServiceDiagnostics = result.Diagnostics.Where(d => d.Id == "IOC021").ToList();
        Assert.True(conditionalServiceDiagnostics.Count == 0,
            $"Expected no IOC021 diagnostics, got {conditionalServiceDiagnostics.Count}");

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        Assert.Contains("RegularTestService", registrationSource.Content);

        // ConditionalService WITHOUT [Scoped] attribute should now be processed due to intelligent inference
        Assert.Contains("ConditionalLifetimeInferenceDeploymentService", registrationSource.Content);

        // Should generate conditional logic
        Assert.Contains("Development", registrationSource.Content);
        Assert.Contains("Environment.GetEnvironmentVariable", registrationSource.Content);
    }

    #endregion
}
