using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace IoCTools.Sample.Services;

// COMPREHENSIVE CONDITIONAL SERVICES EXAMPLES
// Demonstrates environment-based, configuration-driven, and feature flag conditional registration

// === 1. ENVIRONMENT-BASED SERVICE SELECTION ===

public interface IEnvironmentEmailService
{
    Task<bool> SendEmailAsync(string to, string subject, string body);
}

// Production SMTP email service
[ConditionalService(Environment = "Production")]
[Service]
public partial class SmtpEmailService : IEnvironmentEmailService
{
    [Inject] private readonly IConfiguration _configuration;
    [Inject] private readonly ILogger<SmtpEmailService> _logger;

    public async Task<bool> SendEmailAsync(string to, string subject, string body)
    {
        var smtpHost = _configuration["Email:SmtpHost"];
        var smtpPort = _configuration.GetValue<int>("Email:SmtpPort");
        
        _logger.LogInformation("Sending email via SMTP {SmtpHost}:{SmtpPort} to {To}", smtpHost, smtpPort, to);
        
        // Simulate SMTP sending
        await Task.Delay(200);
        return true;
    }

}

// Development console email service
[ConditionalService(Environment = "Development")]
[Service]
public partial class ConsoleEmailService : IEnvironmentEmailService
{
    [Inject] private readonly ILogger<ConsoleEmailService> _logger;

    public async Task<bool> SendEmailAsync(string to, string subject, string body)
    {
        _logger.LogInformation("=== DEVELOPMENT EMAIL ===");
        _logger.LogInformation("To: {To}", to);
        _logger.LogInformation("Subject: {Subject}", subject);
        _logger.LogInformation("Body: {Body}", body);
        _logger.LogInformation("=========================");
        
        await Task.Delay(10);
        return true;
    }

}

// === 2. CONFIGURATION-DRIVEN CACHE PROVIDERS ===

public interface IConfigurableCacheService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null);
    Task RemoveAsync(string key);
}

// Memory cache implementation
[ConditionalService(ConfigValue = "Cache:Provider", Equals = "Memory")]
[Service]
public partial class MemoryCacheService : IConfigurableCacheService
{
    [Inject] private readonly IMemoryCache _memoryCache;
    [Inject] private readonly IConfiguration _configuration;
    [Inject] private readonly ILogger<MemoryCacheService> _logger;

    public Task<T?> GetAsync<T>(string key)
    {
        _logger.LogDebug("Getting key {Key} from memory cache", key);
        var value = _memoryCache.Get<T>(key);
        return Task.FromResult(value);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        var exp = expiration ?? TimeSpan.FromMinutes(_configuration.GetValue<int>("Cache:ExpirationMinutes", 60));
        _logger.LogDebug("Setting key {Key} in memory cache with expiration {Expiration}", key, exp);
        _memoryCache.Set(key, value, exp);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        _logger.LogDebug("Removing key {Key} from memory cache", key);
        _memoryCache.Remove(key);
        return Task.CompletedTask;
    }

}

// Redis cache implementation (mock for demonstration)
[ConditionalService(ConfigValue = "Cache:Provider", Equals = "Redis")]
[Service]
public partial class RedisCacheService : IConfigurableCacheService
{
    [Inject] private readonly IConfiguration _configuration;
    [Inject] private readonly ILogger<RedisCacheService> _logger;

    public Task<T?> GetAsync<T>(string key)
    {
        var connectionString = _configuration["Cache:Redis:ConnectionString"];
        _logger.LogDebug("Getting key {Key} from Redis at {ConnectionString}", key, connectionString);
        
        // Mock Redis operation
        return Task.FromResult(default(T));
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        var connectionString = _configuration["Cache:Redis:ConnectionString"];
        var exp = expiration ?? TimeSpan.FromMinutes(_configuration.GetValue<int>("Cache:ExpirationMinutes", 60));
        _logger.LogDebug("Setting key {Key} in Redis at {ConnectionString} with expiration {Expiration}", key, connectionString, exp);
        
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        var connectionString = _configuration["Cache:Redis:ConnectionString"];
        _logger.LogDebug("Removing key {Key} from Redis at {ConnectionString}", key, connectionString);
        
        return Task.CompletedTask;
    }

}

// === 3. FEATURE FLAG DEMONSTRATIONS ===

public interface IAdvancedLoggingService
{
    Task LogWithContextAsync(string message, object? context = null);
    Task LogPerformanceAsync(string operation, TimeSpan duration);
}

// Enhanced logging service with advanced features
[ConditionalService(ConfigValue = "Features:EnableAdvancedLogging", Equals = "true")]
[Service]
public partial class EnhancedLoggingService : IAdvancedLoggingService
{
    [Inject] private readonly ILogger<EnhancedLoggingService> _logger;
    [Inject] private readonly IConfiguration _configuration;

    public async Task LogWithContextAsync(string message, object? context = null)
    {
        var contextJson = context != null ? JsonSerializer.Serialize(context) : "null";
        var includeTimestamp = _configuration.GetValue<bool>("Logging:Custom:IncludeTimestamp", true);
        var timestamp = includeTimestamp ? DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff UTC") : "";
        
        _logger.LogInformation("[{Timestamp}] {Message} | Context: {Context}", timestamp, message, contextJson);
        
        // Simulate advanced logging features
        await Task.Delay(5);
    }

    public async Task LogPerformanceAsync(string operation, TimeSpan duration)
    {
        _logger.LogInformation("PERF: Operation '{Operation}' completed in {Duration}ms", operation, duration.TotalMilliseconds);
        
        // Could send to performance monitoring service
        await Task.Delay(2);
    }

}

// Basic logging service fallback
[ConditionalService(ConfigValue = "Features:EnableAdvancedLogging", NotEquals = "true")]
[Service]
public partial class BasicLoggingService : IAdvancedLoggingService
{
    [Inject] private readonly ILogger<BasicLoggingService> _logger;

    public Task LogWithContextAsync(string message, object? context = null)
    {
        _logger.LogInformation("{Message}", message);
        return Task.CompletedTask;
    }

    public Task LogPerformanceAsync(string operation, TimeSpan duration)
    {
        _logger.LogDebug("Operation '{Operation}' completed in {Duration}ms", operation, duration.TotalMilliseconds);
        return Task.CompletedTask;
    }

}

// === 4. PAYMENT PROCESSOR FEATURE FLAG ===

public interface IPaymentProcessor
{
    Task<PaymentProcessorResult> ProcessPaymentAsync(decimal amount, string method);
}

// New payment processor implementation
[ConditionalService(ConfigValue = "Features:NewPaymentProcessor", Equals = "enabled")]
[Service]
public partial class NewPaymentProcessor : IPaymentProcessor
{
    [Inject] private readonly ILogger<NewPaymentProcessor> _logger;
    [Inject] private readonly IConfiguration _configuration;

    public async Task<PaymentProcessorResult> ProcessPaymentAsync(decimal amount, string method)
    {
        _logger.LogInformation("Processing ${Amount} payment via NEW processor using {Method}", amount, method);
        
        // Simulate enhanced payment processing
        await Task.Delay(150);
        
        return new PaymentProcessorResult(
            Success: true,
            TransactionId: Guid.NewGuid().ToString(),
            ProcessorVersion: "v2.0",
            ProcessingTime: TimeSpan.FromMilliseconds(150)
        );
    }

}

// Legacy payment processor
[ConditionalService(ConfigValue = "Features:NewPaymentProcessor", NotEquals = "enabled")]
[Service]
public partial class LegacyPaymentProcessor : IPaymentProcessor
{
    [Inject] private readonly ILogger<LegacyPaymentProcessor> _logger;

    public async Task<PaymentProcessorResult> ProcessPaymentAsync(decimal amount, string method)
    {
        _logger.LogInformation("Processing ${Amount} payment via LEGACY processor using {Method}", amount, method);
        
        // Simulate legacy payment processing
        await Task.Delay(300);
        
        return new PaymentProcessorResult(
            Success: true,
            TransactionId: Guid.NewGuid().ToString("N")[..8].ToUpper(),
            ProcessorVersion: "v1.0",
            ProcessingTime: TimeSpan.FromMilliseconds(300)
        );
    }

}

// === 5. DATABASE PROVIDER SELECTION ===

public interface IDatabaseService
{
    Task<T?> GetByIdAsync<T>(int id) where T : class;
    Task<bool> SaveAsync<T>(T entity) where T : class;
    Task<IEnumerable<T>> GetAllAsync<T>() where T : class;
}

// SQL Server implementation
[ConditionalService(ConfigValue = "Database:Provider", Equals = "SqlServer")]
[Service]
public partial class SqlServerDatabaseService : IDatabaseService
{
    [Inject] private readonly IConfiguration _configuration;
    [Inject] private readonly ILogger<SqlServerDatabaseService> _logger;

    public async Task<T?> GetByIdAsync<T>(int id) where T : class
    {
        var connectionString = _configuration["Database:ConnectionString"];
        _logger.LogInformation("SQL Server: Getting {EntityType} with ID {Id} from {ConnectionString}", 
            typeof(T).Name, id, connectionString?.Substring(0, Math.Min(20, connectionString.Length)) + "...");
        
        await Task.Delay(50);
        return default(T);
    }

    public async Task<bool> SaveAsync<T>(T entity) where T : class
    {
        var connectionString = _configuration["Database:ConnectionString"];
        _logger.LogInformation("SQL Server: Saving {EntityType} to {ConnectionString}", 
            typeof(T).Name, connectionString?.Substring(0, Math.Min(20, connectionString.Length)) + "...");
        
        await Task.Delay(30);
        return true;
    }

    public async Task<IEnumerable<T>> GetAllAsync<T>() where T : class
    {
        _logger.LogInformation("SQL Server: Getting all {EntityType} records", typeof(T).Name);
        await Task.Delay(100);
        return Enumerable.Empty<T>();
    }

}

// SQLite implementation (for development/testing)
[ConditionalService(ConfigValue = "Database:Provider", Equals = "SQLite")]
[Service]
public partial class SqliteDatabaseService : IDatabaseService
{
    [Inject] private readonly IConfiguration _configuration;
    [Inject] private readonly ILogger<SqliteDatabaseService> _logger;

    public async Task<T?> GetByIdAsync<T>(int id) where T : class
    {
        _logger.LogInformation("SQLite: Getting {EntityType} with ID {Id}", typeof(T).Name, id);
        await Task.Delay(20);
        return default(T);
    }

    public async Task<bool> SaveAsync<T>(T entity) where T : class
    {
        _logger.LogInformation("SQLite: Saving {EntityType}", typeof(T).Name);
        await Task.Delay(15);
        return true;
    }

    public async Task<IEnumerable<T>> GetAllAsync<T>() where T : class
    {
        _logger.LogInformation("SQLite: Getting all {EntityType} records", typeof(T).Name);
        await Task.Delay(40);
        return Enumerable.Empty<T>();
    }

}

// === 6. OPTIONAL SERVICES BASED ON FEATURE FLAGS ===

public interface IOptionalFeatureService
{
    Task<string> ExecuteOptionalFeatureAsync(string input);
    bool IsFeatureEnabled { get; }
}

// Premium features service
[ConditionalService(ConfigValue = "Features:EnableOptionalService", Equals = "true")]
[Service]
public partial class PremiumFeaturesService : IOptionalFeatureService
{
    [Inject] private readonly ILogger<PremiumFeaturesService> _logger;
    [Inject] private readonly IConfiguration _configuration;

    public bool IsFeatureEnabled => true;

    public async Task<string> ExecuteOptionalFeatureAsync(string input)
    {
        _logger.LogInformation("Executing PREMIUM feature with input: {Input}", input);
        
        // Simulate premium processing
        await Task.Delay(100);
        
        var result = $"PREMIUM: Enhanced processing of '{input}' with advanced algorithms";
        _logger.LogInformation("Premium feature result: {Result}", result);
        
        return result;
    }

}

// Fallback service when optional features are disabled
[ConditionalService(ConfigValue = "Features:EnableOptionalService", NotEquals = "true")]
[Service]
public partial class StandardFeaturesService : IOptionalFeatureService
{
    [Inject] private readonly ILogger<StandardFeaturesService> _logger;

    public bool IsFeatureEnabled => false;

    public async Task<string> ExecuteOptionalFeatureAsync(string input)
    {
        _logger.LogInformation("Executing STANDARD feature with input: {Input}", input);
        
        await Task.Delay(30);
        
        var result = $"STANDARD: Basic processing of '{input}'";
        _logger.LogInformation("Standard feature result: {Result}", result);
        
        return result;
    }

}

// === 7. COMBINED ENVIRONMENT + CONFIGURATION CONDITIONS ===

public interface IStorageService
{
    Task<bool> StoreFileAsync(string fileName, byte[] content);
    Task<byte[]?> RetrieveFileAsync(string fileName);
    Task<bool> DeleteFileAsync(string fileName);
}

// Cloud storage for production
[ConditionalService(Environment = "Production", ConfigValue = "Features:EnableDistributedCache", Equals = "true")]
[Service]
public partial class CloudStorageService : IStorageService
{
    [Inject] private readonly IConfiguration _configuration;
    [Inject] private readonly ILogger<CloudStorageService> _logger;

    public async Task<bool> StoreFileAsync(string fileName, byte[] content)
    {
        _logger.LogInformation("Storing file {FileName} ({Size} bytes) to cloud storage", fileName, content.Length);
        await Task.Delay(200); // Simulate cloud upload
        return true;
    }

    public async Task<byte[]?> RetrieveFileAsync(string fileName)
    {
        _logger.LogInformation("Retrieving file {FileName} from cloud storage", fileName);
        await Task.Delay(150); // Simulate cloud download
        return new byte[] { 1, 2, 3, 4, 5 }; // Mock content
    }

    public async Task<bool> DeleteFileAsync(string fileName)
    {
        _logger.LogInformation("Deleting file {FileName} from cloud storage", fileName);
        await Task.Delay(100);
        return true;
    }

}

// Local file storage for development
[ConditionalService(NotEnvironment = "Production")]
[Service]
public partial class LocalFileStorageService : IStorageService
{
    [Inject] private readonly ILogger<LocalFileStorageService> _logger;

    public async Task<bool> StoreFileAsync(string fileName, byte[] content)
    {
        var path = Path.Combine("./temp", fileName);
        _logger.LogInformation("Storing file {FileName} ({Size} bytes) to local path {Path}", fileName, content.Length, path);
        
        Directory.CreateDirectory("./temp");
        await File.WriteAllBytesAsync(path, content);
        return true;
    }

    public async Task<byte[]?> RetrieveFileAsync(string fileName)
    {
        var path = Path.Combine("./temp", fileName);
        _logger.LogInformation("Retrieving file {FileName} from local path {Path}", fileName, path);
        
        if (!File.Exists(path)) return null;
        return await File.ReadAllBytesAsync(path);
    }

    public async Task<bool> DeleteFileAsync(string fileName)
    {
        var path = Path.Combine("./temp", fileName);
        _logger.LogInformation("Deleting file {FileName} from local path {Path}", fileName, path);
        
        if (File.Exists(path))
        {
            File.Delete(path);
            return true;
        }
        return false;
    }

}

// === 8. SERVICE HIERARCHY WITH CONDITIONAL SERVICES ===

public interface INotificationProvider
{
    Task<bool> SendNotificationAsync(string recipient, string message, NotificationPriority priority = NotificationPriority.Normal);
    string ProviderName { get; }
}

public enum NotificationPriority
{
    Low,
    Normal,
    High,
    Critical
}

// Base notification service that coordinates multiple providers
[Service]
public partial class ConditionalCompositeNotificationService
{
    [Inject] private readonly IEnumerable<INotificationProvider> _providers;
    [Inject] private readonly ILogger<ConditionalCompositeNotificationService> _logger;

    public async Task SendNotificationToAllAsync(string recipient, string message, NotificationPriority priority = NotificationPriority.Normal)
    {
        _logger.LogInformation("Sending notification to {Recipient} via {ProviderCount} providers with priority {Priority}", 
            recipient, _providers.Count(), priority);

        var tasks = _providers.Select(provider => SendWithProviderAsync(provider, recipient, message, priority));
        var results = await Task.WhenAll(tasks);

        var successCount = results.Count(r => r);
        _logger.LogInformation("Notification sent successfully via {SuccessCount}/{TotalCount} providers", 
            successCount, _providers.Count());
    }

    private async Task<bool> SendWithProviderAsync(INotificationProvider provider, string recipient, string message, NotificationPriority priority)
    {
        try
        {
            return await provider.SendNotificationAsync(recipient, message, priority);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification via provider {ProviderName}", provider.ProviderName);
            return false;
        }
    }
}

// Email notification provider
[Service]
public partial class EmailNotificationProvider : INotificationProvider
{
    [Inject] private readonly IEnvironmentEmailService _emailService;
    [Inject] private readonly ILogger<EmailNotificationProvider> _logger;

    public string ProviderName => "Email";

    public async Task<bool> SendNotificationAsync(string recipient, string message, NotificationPriority priority = NotificationPriority.Normal)
    {
        var subject = $"[{priority}] Notification";
        _logger.LogDebug("Sending email notification to {Recipient} with priority {Priority}", recipient, priority);
        
        return await _emailService.SendEmailAsync(recipient, subject, message);
    }
}

// SMS notification provider (conditional on configuration)
[ConditionalService(ConfigValue = "Notification:Settings:Provider", Equals = "SMS")]
[Service]
public partial class SmsNotificationProvider : INotificationProvider
{
    [Inject] private readonly IConfiguration _configuration;
    [Inject] private readonly ILogger<SmsNotificationProvider> _logger;

    public string ProviderName => "SMS";

    public async Task<bool> SendNotificationAsync(string recipient, string message, NotificationPriority priority = NotificationPriority.Normal)
    {
        _logger.LogInformation("Sending SMS to {Recipient}: {Message} (Priority: {Priority})", recipient, message, priority);
        
        // Simulate SMS sending
        await Task.Delay(100);
        return true;
    }

}

// Push notification provider (conditional on premium features)
[ConditionalService(ConfigValue = "Features:EnablePremiumFeatures", Equals = "true")]
[Service]
public partial class PushNotificationProvider : INotificationProvider
{
    [Inject] private readonly ILogger<PushNotificationProvider> _logger;
    [Inject] private readonly IConfiguration _configuration;

    public string ProviderName => "Push";

    public async Task<bool> SendNotificationAsync(string recipient, string message, NotificationPriority priority = NotificationPriority.Normal)
    {
        _logger.LogInformation("Sending push notification to {Recipient}: {Message} (Priority: {Priority})", recipient, message, priority);
        
        // Simulate push notification
        await Task.Delay(50);
        return true;
    }

}

// === DEMONSTRATION SERVICE ===

[Service]
public partial class ConditionalServicesDemonstrationService
{
    [Inject] private readonly IEnvironmentEmailService _emailService;
    [Inject] private readonly IConfigurableCacheService _cacheService;
    [Inject] private readonly IAdvancedLoggingService _loggingService;
    [Inject] private readonly IPaymentProcessor _paymentProcessor;
    [Inject] private readonly IDatabaseService _databaseService;
    [Inject] private readonly IOptionalFeatureService _optionalFeatureService;
    [Inject] private readonly IStorageService _storageService;
    [Inject] private readonly ConditionalCompositeNotificationService _notificationService;
    [Inject] private readonly ILogger<ConditionalServicesDemonstrationService> _logger;

    public async Task DemonstrateConditionalServicesAsync()
    {
        _logger.LogInformation("=== CONDITIONAL SERVICES DEMONSTRATION ===");

        // 1. Email Service (Environment-based)
        await _loggingService.LogWithContextAsync("Testing environment-based email service");
        await _emailService.SendEmailAsync("test@example.com", "Conditional Service Test", "This email service was selected based on environment.");

        // 2. Cache Service (Configuration-driven)
        await _loggingService.LogWithContextAsync("Testing configuration-driven cache service");
        await _cacheService.SetAsync("demo-key", "demo-value", TimeSpan.FromMinutes(5));
        var cachedValue = await _cacheService.GetAsync<string>("demo-key");
        _logger.LogInformation("Retrieved from cache: {CachedValue}", cachedValue);

        // 3. Payment Processor (Feature flag)
        await _loggingService.LogWithContextAsync("Testing feature flag payment processor");
        var startTime = DateTimeOffset.UtcNow;
        var paymentResult = await _paymentProcessor.ProcessPaymentAsync(99.99m, "CreditCard");
        var endTime = DateTimeOffset.UtcNow;
        await _loggingService.LogPerformanceAsync("PaymentProcessing", endTime - startTime);
        _logger.LogInformation("Payment Result: {Success}, Version: {Version}, ID: {TransactionId}", 
            paymentResult.Success, paymentResult.ProcessorVersion, paymentResult.TransactionId);

        // 4. Database Service (Configuration-driven)
        await _loggingService.LogWithContextAsync("Testing configuration-driven database service");
        var entities = await _databaseService.GetAllAsync<object>();
        _logger.LogInformation("Database query returned {Count} entities", entities.Count());

        // 5. Optional Feature Service (Feature flag)
        await _loggingService.LogWithContextAsync("Testing optional feature service", new { IsEnabled = _optionalFeatureService.IsFeatureEnabled });
        var featureResult = await _optionalFeatureService.ExecuteOptionalFeatureAsync("test-input");
        _logger.LogInformation("Optional feature result: {Result}", featureResult);

        // 6. Storage Service (Combined conditions)
        await _loggingService.LogWithContextAsync("Testing combined condition storage service");
        var testFile = System.Text.Encoding.UTF8.GetBytes("Test file content for conditional storage demonstration");
        await _storageService.StoreFileAsync("conditional-test.txt", testFile);
        var retrievedFile = await _storageService.RetrieveFileAsync("conditional-test.txt");
        _logger.LogInformation("Storage test: Stored {StoredSize} bytes, Retrieved {RetrievedSize} bytes", 
            testFile.Length, retrievedFile?.Length ?? 0);

        // 7. Notification Service (Service hierarchy)
        await _loggingService.LogWithContextAsync("Testing service hierarchy with conditional providers");
        await _notificationService.SendNotificationToAllAsync("user@example.com", "Conditional services demonstration complete!", NotificationPriority.Normal);

        _logger.LogInformation("=== CONDITIONAL SERVICES DEMONSTRATION COMPLETED ===");
    }
}

// === DATA MODELS ===

public record PaymentProcessorResult(
    bool Success,
    string TransactionId,
    string ProcessorVersion,
    TimeSpan ProcessingTime
);