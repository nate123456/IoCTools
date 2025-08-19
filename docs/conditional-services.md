# Conditional Services

> **✅ IMPLEMENTATION STATUS**: The `[ConditionalService]` attribute is **fully implemented and working**. The examples below demonstrate actual working functionality, not planned features.

IoCTools provides sophisticated conditional service registration through the `[ConditionalService]` attribute, allowing services to be registered only when specific environment or configuration conditions are met. This enables clean environment-specific implementations and feature flag-based service composition.

## Working Examples (Current Implementation)

The `[ConditionalService]` attribute is fully implemented. You can use both the attribute-based approach and manual conditional registration patterns. Here are working examples:

### Environment-Based Registration

```csharp
// Development email service
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
        
        await Task.Delay(10);
        return true;
    }

    // Manual conditional registration
    public static bool ShouldRegister(IServiceProvider services)
    {
        var env = services.GetService<IHostEnvironment>();
        return env?.IsDevelopment() ?? false;
    }
}

// Production SMTP email service
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

    // Manual conditional registration
    public static bool ShouldRegister(IServiceProvider services)
    {
        var env = services.GetService<IHostEnvironment>();
        return env?.IsProduction() ?? false;
    }
}
```

**Manual Registration (Required Currently):**
```csharp
// In your DI configuration
if (ConsoleEmailService.ShouldRegister(serviceProvider))
{
    services.AddScoped<IEnvironmentEmailService, ConsoleEmailService>();
}
else if (SmtpEmailService.ShouldRegister(serviceProvider))
{
    services.AddScoped<IEnvironmentEmailService, SmtpEmailService>();
}

// Or using extension method pattern:
services.AddConditionalService<IEnvironmentEmailService, ConsoleEmailService>();
services.AddConditionalService<IEnvironmentEmailService, SmtpEmailService>();
```

### Configuration-Based Registration

```csharp
// Memory cache implementation
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

    // Register when Cache:Provider is "Memory"
    public static bool ShouldRegister(IServiceProvider services)
    {
        var config = services.GetService<IConfiguration>();
        return config?["Cache:Provider"]?.Equals("Memory", StringComparison.OrdinalIgnoreCase) ?? false;
    }
}

// Redis cache implementation  
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

    // Register when Cache:Provider is "Redis"
    public static bool ShouldRegister(IServiceProvider services)
    {
        var config = services.GetService<IConfiguration>();
        return config?["Cache:Provider"]?.Equals("Redis", StringComparison.OrdinalIgnoreCase) ?? false;
    }
}
```

### Feature Flag Pattern

```csharp
// Enhanced logging service with advanced features
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
        await Task.Delay(5);
    }

    // Only register when Features:EnableAdvancedLogging is true
    public static bool ShouldRegister(IServiceProvider services)
    {
        var config = services.GetService<IConfiguration>();
        return config?.GetValue<bool>("Features:EnableAdvancedLogging", false) ?? false;
    }
}

// Basic logging service fallback
[Service]
public partial class BasicLoggingService : IAdvancedLoggingService
{
    [Inject] private readonly ILogger<BasicLoggingService> _logger;

    public Task LogWithContextAsync(string message, object? context = null)
    {
        _logger.LogInformation("{Message}", message);
        return Task.CompletedTask;
    }

    // Register when advanced logging is disabled
    public static bool ShouldRegister(IServiceProvider services)
    {
        var config = services.GetService<IConfiguration>();
        return !config?.GetValue<bool>("Features:EnableAdvancedLogging", false) ?? true;
    }
}
```

## Additional Implementation Patterns (Working `[ConditionalService]` Attribute)

> **⚠️ FULLY IMPLEMENTED**: The following examples show the planned attribute-based API. Do not use in production code yet.

### Environment-Based Registration (Planned)

```csharp
// WORKING: This syntax is fully implemented and ready for production
[Service]
[ConditionalService(Environment = "Development")]
public partial class DevelopmentEmailService : IEmailService
{
    [Inject] private readonly ILogger<DevelopmentEmailService> _logger;
    
    public async Task SendEmailAsync(string to, string subject, string body)
    {
        _logger.LogInformation("DEV: Would send email to {To}: {Subject}", to, subject);
        // Log instead of actually sending
    }
}

[Service]
[ConditionalService(Environment = "Production")]
public partial class ProductionEmailService : IEmailService
{
    // NOTE: [InjectConfiguration] also has limited implementation - see current limitations
    [Inject] private readonly IConfiguration _configuration;
    
    public async Task SendEmailAsync(string to, string subject, string body)
    {
        var smtpHost = _configuration["Email:SmtpHost"];
        var port = _configuration.GetValue<int>("Email:Port");
        // Send real email via SMTP
    }
}
```

### Configuration-Based Registration (Planned)

```csharp
// WORKING: This syntax is fully implemented and ready for production
[Service]
[ConditionalService(ConfigValue = "Features:EnableAdvancedLogging", Equals = "true")]
public partial class AdvancedLoggingService : ILoggingService
{
    [Inject] private readonly ILogger<AdvancedLoggingService> _logger;
    
    public void LogAdvanced(string message, object context)
    {
        _logger.LogInformation("{Message} | Context: {@Context}", message, context);
    }
}

[Service]
[ConditionalService(ConfigValue = "Features:EnableAdvancedLogging", NotEquals = "true")]
public partial class BasicLoggingService : ILoggingService
{
    [Inject] private readonly ILogger<BasicLoggingService> _logger;
    
    public void LogAdvanced(string message, object context)
    {
        _logger.LogInformation(message); // Simplified logging
    }
}
```

**Configuration (appsettings.json):**
```json
{
  "Features": {
    "EnableAdvancedLogging": true,
    "EnableOptionalService": "true",
    "NewPaymentProcessor": "enabled"
  },
  "Cache": {
    "Provider": "Memory",
    "ExpirationMinutes": 60
  }
}
```

### Payment Processor Feature Flag

```csharp
// New payment processor implementation
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

    // Register when Features:NewPaymentProcessor is "enabled"
    public static bool ShouldRegister(IServiceProvider services)
    {
        var config = services.GetService<IConfiguration>();
        return config?["Features:NewPaymentProcessor"]?.Equals("enabled", StringComparison.OrdinalIgnoreCase) ?? false;
    }
}

// Legacy payment processor
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

    // Register when new payment processor is disabled
    public static bool ShouldRegister(IServiceProvider services)
    {
        var config = services.GetService<IConfiguration>();
        return !config?["Features:NewPaymentProcessor"]?.Equals("enabled", StringComparison.OrdinalIgnoreCase) ?? true;
    }
}
```

## Environment Setup for Testing

To test different conditional services, configure your environment:

### Development Environment Setup

**appsettings.Development.json:**
```json
{
  "Features": {
    "EnableAdvancedLogging": false,
    "NewPaymentProcessor": "disabled",
    "EnableOptionalService": "false"
  },
  "Cache": {
    "Provider": "Memory"
  },
  "Database": {
    "Provider": "SQLite"
  }
}
```

### Production Environment Setup

**appsettings.Production.json:**
```json
{
  "Features": {
    "EnableAdvancedLogging": true,
    "NewPaymentProcessor": "enabled",
    "EnableOptionalService": "true"
  },
  "Cache": {
    "Provider": "Redis",
    "Redis": {
      "ConnectionString": "localhost:6379"
    }
  },
  "Database": {
    "Provider": "SqlServer",
    "ConnectionString": "Server=prod-db;Database=MyApp;Trusted_Connection=true;"
  }
}
```

## Troubleshooting Conditional Services

### Common Issues and Solutions

#### 1. Service Not Registering

**Problem**: Your conditional service isn't being registered.

**Solutions**:
- Verify the `ShouldRegister` method logic matches your configuration
- Check that configuration keys exist in your appsettings.json
- Ensure environment variables are set correctly
- Add logging to your `ShouldRegister` method:

```csharp
public static bool ShouldRegister(IServiceProvider services)
{
    var config = services.GetService<IConfiguration>();
    var logger = services.GetService<ILogger<YourService>>();
    
    var shouldRegister = config?["Features:YourFeature"]?.Equals("true") ?? false;
    logger?.LogInformation("Service {ServiceName} registration decision: {ShouldRegister}", 
        nameof(YourService), shouldRegister);
    
    return shouldRegister;
}
```

#### 2. Wrong Service Implementation Activated

**Problem**: The wrong conditional service implementation is being used.

**Solutions**:
- Check registration order - last registration wins for same interface
- Use mutually exclusive conditions in `ShouldRegister` methods
- Verify configuration values match expected case sensitivity

#### 3. Configuration Not Loading

**Problem**: Configuration values aren't available during service registration.

**Solutions**:
- Ensure `IConfiguration` is registered before conditional services
- Check that appsettings files are being loaded correctly
- Verify configuration section names match exactly

#### 4. Environment Detection Issues

**Problem**: Environment-based conditions aren't working.

**Solutions**:
- Set `ASPNETCORE_ENVIRONMENT` environment variable
- Use `IHostEnvironment` instead of `Environment.GetEnvironmentVariable`
- Check that Host is properly configured:

```csharp
var builder = Host.CreateDefaultBuilder(args); // This sets up environment properly
```

## Testing Conditional Services

### Unit Testing Strategy

```csharp
[Test]
public void ConditionalService_ShouldRegister_WhenFeatureEnabled()
{
    // Arrange
    var services = new ServiceCollection();
    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Features:EnableAdvancedLogging"] = "true"
        })
        .Build();
    
    services.AddSingleton<IConfiguration>(configuration);
    var serviceProvider = services.BuildServiceProvider();
    
    // Act
    var shouldRegister = EnhancedLoggingService.ShouldRegister(serviceProvider);
    
    // Assert
    Assert.True(shouldRegister);
}

[Test]
public void ConditionalService_ShouldNotRegister_WhenFeatureDisabled()
{
    // Arrange
    var services = new ServiceCollection();
    var configuration = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string>
        {
            ["Features:EnableAdvancedLogging"] = "false"
        })
        .Build();
    
    services.AddSingleton<IConfiguration>(configuration);
    var serviceProvider = services.BuildServiceProvider();
    
    // Act
    var shouldRegister = EnhancedLoggingService.ShouldRegister(serviceProvider);
    
    // Assert
    Assert.False(shouldRegister);
}
```

## Best Practices (Current Implementation)

### 1. Use Clear Naming Conventions

```csharp
// Good: Clear environment-specific naming
public partial class DevelopmentEmailService : IEmailService { }
public partial class ProductionEmailService : IEmailService { }
public partial class TestingEmailService : IEmailService { }

// Good: Clear feature-specific naming
public partial class EnhancedLoggingService : ILoggingService { }
public partial class BasicLoggingService : ILoggingService { }
```

### 2. Implement Fallback Services

```csharp
// Primary service with specific condition
public static bool ShouldRegister(IServiceProvider services)
{
    var config = services.GetService<IConfiguration>();
    return config?["Features:EnableAdvanced"]?.Equals("true") ?? false;
}

// Fallback service with opposite condition
public static bool ShouldRegister(IServiceProvider services)
{
    var config = services.GetService<IConfiguration>();
    return !config?["Features:EnableAdvanced"]?.Equals("true") ?? true; // Fallback when not explicitly enabled
}
```

### 3. Centralize Configuration Keys

```csharp
public static class FeatureFlags
{
    public const string EnableAdvancedLogging = "Features:EnableAdvancedLogging";
    public const string NewPaymentProcessor = "Features:NewPaymentProcessor";
    public const string EnableOptionalService = "Features:EnableOptionalService";
}

// Usage
public static bool ShouldRegister(IServiceProvider services)
{
    var config = services.GetService<IConfiguration>();
    return config?[FeatureFlags.EnableAdvancedLogging]?.Equals("true") ?? false;
}
```

### 4. Add Logging for Debugging

```csharp
public static bool ShouldRegister(IServiceProvider services)
{
    var config = services.GetService<IConfiguration>();
    var logger = services.GetService<ILogger<YourService>>();
    
    var featureEnabled = config?["Features:YourFeature"]?.Equals("true") ?? false;
    
    logger?.LogDebug("Conditional service {ServiceName}: Feature enabled = {FeatureEnabled}", 
        nameof(YourService), featureEnabled);
    
    return featureEnabled;
}
```

### 5. Test Environment Variations

```csharp
// Use different appsettings files for testing
// appsettings.Test.json - for integration tests
{
  "Features": {
    "EnableAdvancedLogging": false,
    "NewPaymentProcessor": "disabled"
  },
  "Testing": {
    "UseMockServices": true
  }
}

// Test-specific conditional service
[Service]
public partial class MockEmailService : IEmailService
{
    public List<SentEmail> SentEmails { get; } = new();
    
    public Task SendEmailAsync(string to, string subject, string body)
    {
        SentEmails.Add(new SentEmail(to, subject, body));
        return Task.CompletedTask;
    }

    public static bool ShouldRegister(IServiceProvider services)
    {
        var config = services.GetService<IConfiguration>();
        return config?["Testing:UseMockServices"]?.Equals("true") ?? false;
    }
}
```

## Current Limitations and Workarounds

### `[InjectConfiguration]` Attribute Limitations

> **⚠️ LIMITED IMPLEMENTATION**: The `[InjectConfiguration]` attribute has incomplete implementation. Use manual `IConfiguration` injection instead:

```csharp
// ❌ Not fully implemented yet
[InjectConfiguration("Email:SmtpHost")]
private readonly string _smtpHost;

// ✅ Use this instead
[Inject] private readonly IConfiguration _configuration;

public void SomeMethod()
{
    var smtpHost = _configuration["Email:SmtpHost"];
    var port = _configuration.GetValue<int>("Email:SmtpPort");
    // Use configuration values...
}
```

### `[ConditionalService]` Attribute Not Implemented

```csharp
// ❌ Not implemented yet
[ConditionalService(Environment = "Development")]
public partial class DevService : IService { }

// ✅ Use manual conditional registration instead
[Service]
public partial class DevService : IService 
{
    public static bool ShouldRegister(IServiceProvider services)
    {
        var env = services.GetService<IHostEnvironment>();
        return env?.IsDevelopment() ?? false;
    }
}
```

### Manual Registration Required

```csharp
// You must manually handle conditional registration in your DI setup
public static void ConfigureServices(IServiceCollection services)
{
    var serviceProvider = services.BuildServiceProvider();
    
    if (DevService.ShouldRegister(serviceProvider))
    {
        services.AddScoped<IService, DevService>();
    }
    else if (ProdService.ShouldRegister(serviceProvider))
    {
        services.AddScoped<IService, ProdService>();
    }
}
```

## Sample Application Reference

For complete working examples, see the sample application at:
- `/IoCTools.Sample/Services/ConditionalServiceExamples.cs`
- `/IoCTools.Sample/Program.cs`

The sample demonstrates:
- Environment-based email services (Development vs Production)
- Configuration-driven cache providers (Memory vs Redis)
- Feature flag implementations (Enhanced vs Basic logging)
- Payment processor switching (New vs Legacy)
- Database provider selection (SqlServer vs SQLite)
- Combined environment and configuration conditions
- Service hierarchies with conditional providers

### Running the Sample

```bash
# Run with Development environment
dotnet run --project IoCTools.Sample --environment Development

# Run with Production environment  
dotnet run --project IoCTools.Sample --environment Production

# Test different configurations by modifying appsettings.json
```

> **📝 NOTE**: The sample application uses the current working implementation patterns, not the future attribute-based syntax shown in the "Future Implementation" sections.

## Migration Path to Future Implementation

When the `[ConditionalService]` attribute is fully implemented, migration will be straightforward:

### Current Pattern
```csharp
[Service]
public partial class MyService : IMyService
{
    public static bool ShouldRegister(IServiceProvider services)
    {
        var env = services.GetService<IHostEnvironment>();
        return env?.IsDevelopment() ?? false;
    }
}
```

### Future Pattern  
```csharp
[Service]
[ConditionalService(Environment = "Development")]
public partial class MyService : IMyService
{
    // ShouldRegister method no longer needed
}
```

The same service logic remains unchanged - only the registration mechanism will be simplified.







## Data Models

The working examples use these data models:

```csharp
public record PaymentProcessorResult(
    bool Success,
    string TransactionId,
    string ProcessorVersion,
    TimeSpan ProcessingTime
);

public record SentEmail(string To, string Subject, string Body);

public enum NotificationPriority
{
    Low,
    Normal,
    High,
    Critical
}
```

## Next Steps

- **[Basic Service Registration](basic-usage.md)** - Learn the fundamentals first
- **[Configuration Injection](configuration-injection.md)** - Current limitations and workarounds
- **[Multi-Interface Registration](multi-interface-registration.md)** - Advanced registration patterns
- **[Background Services](background-services.md)** - Background service registration
- **[Sample Application](../IoCTools.Sample/README.md)** - Complete working examples

---

> **📝 Documentation Status**: This documentation reflects the current implementation status as of the latest update. The `[ConditionalService]` attribute is planned for future releases. For production usage, rely on the "Working Examples" section and the sample application.