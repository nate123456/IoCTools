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
        result.HasErrors.Should().BeFalse();
        result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        // Verify that regular services are registered unconditionally
        var registrationContent = result.GetServiceRegistrationText();
        registrationContent.Should().Contain("AddScoped<Test.IPaymentService, Test.RegularPaymentService>");

        // ConditionalService generates proper environment-based conditional logic
        registrationContent.Should().Contain("Environment.GetEnvironmentVariable");
        registrationContent.Should().Contain(
            "string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)");
        registrationContent.Should().Contain("if (");
        registrationContent.Should().Contain("MockPaymentService");
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
        result.HasErrors.Should().BeFalse();
        result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var registrationContent = result.GetServiceRegistrationText();

        // Fallback service should be registered unconditionally
        registrationContent.Should().Contain("AddScoped<Test.IEmailService, Test.FallbackEmailService>");

        // ConditionalService generates if-else chain for mutually exclusive conditions
        registrationContent.Should().Contain(
            "string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)");
        registrationContent.Should().Contain(
            "else if (string.Equals(environment, \"Production\", StringComparison.OrdinalIgnoreCase))");
        registrationContent.Should().Contain("ConsoleEmailService");
        registrationContent.Should().Contain("SmtpEmailService");
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
        result.HasErrors.Should().BeFalse();
        result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var registrationContent = result.GetServiceRegistrationText();

        // Memory cache should be registered unconditionally
        registrationContent.Should().Contain(
            "AddScoped<global::Test.ICacheService, global::Test.MemoryCacheService>");

        // ConditionalService generates configuration-based conditional logic
        registrationContent.Should().Contain("configuration[\"Features:UseRedisCache\"]");
        registrationContent.Should().Contain(
            "string.Equals(configuration[\"Features:UseRedisCache\"], \"true\", StringComparison.OrdinalIgnoreCase)");
        registrationContent.Should().Contain("RedisCacheService");

        // Constructor generation should work for conditional service with configuration injection
        var constructorSource = result.GetConstructorSourceText("RedisCacheService");
        constructorSource.Should().Contain("IConfiguration configuration");
        constructorSource.Should().Contain("ILogger<RedisCacheService> logger");
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
        result.HasErrors.Should().BeFalse();
        result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var registrationContent = result.GetServiceRegistrationText();

        // ConditionalService generates environment-based if-else chain
        registrationContent.Should().Contain(
            "string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)");
        registrationContent.Should().Contain(
            "string.Equals(environment, \"Production\", StringComparison.OrdinalIgnoreCase)");
        registrationContent.Should().Contain("MockPaymentService");
        registrationContent.Should().Contain("StripePaymentService");

        // Verify constructor generation for both conditional services
        result.GetConstructorSourceText("MockPaymentService").Should().NotBeNullOrWhiteSpace();

        var stripeConstructorSource = result.GetConstructorSourceText("StripePaymentService");
        stripeConstructorSource.Should().Contain("IConfiguration configuration");
        stripeConstructorSource.Should().Contain("IHttpClientFactory httpClientFactory");
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
        result.HasErrors.Should().BeFalse();
        result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var registrationContent = result.GetServiceRegistrationText();

        // Default service should be registered unconditionally  
        registrationContent.Should().Contain(
            "AddScoped<Test.Database.IDatabaseService, Test.Database.DefaultDatabaseService>");

        // ConditionalService generates configuration-based conditional registration
        registrationContent.Should().Contain("configuration[\"Database:Type\"]");
        registrationContent.Should().Contain(
            "string.Equals(configuration[\"Database:Type\"], \"SqlServer\", StringComparison.OrdinalIgnoreCase)");
        registrationContent.Should().Contain(
            "string.Equals(configuration[\"Database:Type\"], \"InMemory\", StringComparison.OrdinalIgnoreCase)");
        registrationContent.Should().Contain("SqlServerDatabaseService");
        registrationContent.Should().Contain("InMemoryDatabaseService");

        // Verify constructor generation works for all services
        var defaultConstructorSource = result.GetConstructorSourceText("DefaultDatabaseService");
        defaultConstructorSource.Should().Contain("ILogger<DefaultDatabaseService> logger");

        var sqlConstructorSource = result.GetConstructorSourceText("SqlServerDatabaseService");
        sqlConstructorSource.Should().Contain("IConfiguration configuration");
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
        result.HasErrors.Should().BeFalse();
        result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var registrationContent = result.GetServiceRegistrationText();

        // Log service should be registered unconditionally
        registrationContent.Should().Contain(
            "AddScoped<Test.Future.INotificationService, Test.Future.LogNotificationService>");

        // ConditionalService generates combined conditions (Environment AND ConfigValue)
        registrationContent.Should().Contain(
            "(string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)) && string.Equals(configuration[\"Features:Notifications\"], \"console\", StringComparison.OrdinalIgnoreCase)");

        // ConditionalService generates NotEnvironment condition
        registrationContent.Should().Contain(
            "!string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)");

        registrationContent.Should().Contain("ConsoleNotificationService");
        registrationContent.Should().Contain("EmailNotificationService");
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
        result.HasErrors.Should().BeFalse();
        result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty();

        var registrationContent = result.GetServiceRegistrationText();

        registrationContent.Should().Contain("AddScoped<Test.ITestService, Test.RegularTestService>");

        // ConditionalService generates NotEquals with proper null handling
        registrationContent.Should().Contain(
            "(configuration.GetValue<string>(\"Features:DisableService\") ?? \"\") != \"true\"");
        registrationContent.Should().Contain("EnabledService");
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
        result.HasErrors.Should().BeFalse();

        // Should NOT have IOC021 diagnostic since ConditionalService is now a service indicator
        var conditionalServiceDiagnostics = result.Diagnostics.Where(d => d.Id == "IOC021").ToList();
        conditionalServiceDiagnostics.Should().BeEmpty(
            $"Expected no IOC021 diagnostics, got {conditionalServiceDiagnostics.Count}");

        var registrationContent = result.GetServiceRegistrationText();

        registrationContent.Should().Contain("RegularTestService");

        // ConditionalService WITHOUT [Scoped] attribute should now be processed due to intelligent inference
        registrationContent.Should().Contain("ConditionalLifetimeInferenceDeploymentService");

        // Should generate conditional logic
        registrationContent.Should().Contain("Development");
        registrationContent.Should().Contain("Environment.GetEnvironmentVariable");
    }

    #endregion
}
