# Configuration Injection

> **⚠️ IMPLEMENTATION STATUS**: The `[InjectConfiguration]` attribute is **partially implemented**. The Options pattern integration (`IOptions<T>`, `IOptionsSnapshot<T>`, `IOptionsMonitor<T>`) is fully functional, but direct configuration value binding is **not yet implemented** - the generator recognizes `[InjectConfiguration]` attributes but does not generate constructor binding code. Use Options pattern or manual `IConfiguration` injection for reliable configuration access.

IoCTools aims to provide direct configuration injection through the `[InjectConfiguration]` attribute, eliminating the need for `IOptions<T>` patterns in many scenarios. However, **direct configuration binding is not yet implemented** - only the Options pattern integration currently works.

## Current Implementation Status

### Fully Working Features

1. **Options Pattern Integration** - IOptions<T>, IOptionsSnapshot<T>, IOptionsMonitor<T> (PERFECT)
2. **Attribute Recognition** - Generator recognizes `[InjectConfiguration]` attributes without errors (WORKING)  
3. **Diagnostic Validation** - IOC016-IOC019 validate configuration attributes properly (WORKING)

### Options Pattern Integration

The `[InjectConfiguration]` attribute works perfectly with the Options pattern:

```csharp
[Service]
public partial class MyService
{
    [Inject] private readonly IOptions<EmailSettings> _emailOptions;
    [Inject] private readonly IOptionsSnapshot<DatabaseSettings> _dbOptions;
    [Inject] private readonly IOptionsMonitor<CacheSettings> _cacheOptions;
}
```

### Not Yet Implemented: Direct Value Binding  

Direct configuration value binding is recognized by the generator but **does not generate constructor parameters or assignments**. The generator currently only handles `[Inject]` dependencies. For configuration access, use manual `IConfiguration` injection:

```csharp
[Service]
public partial class EmailService : IEmailService
{
    [Inject] private readonly IConfiguration _configuration;
    [Inject] private readonly ILogger<EmailService> _logger;
    
    public async Task SendEmailAsync(string to, string subject, string body)
    {
        var smtpHost = _configuration["Smtp:Host"];
        var smtpPort = _configuration.GetValue<int>("Smtp:Port");
        var useSsl = _configuration.GetValue<bool>("Smtp:UseSsl");
        
        using var client = new SmtpClient(smtpHost, smtpPort);
        client.EnableSsl = useSsl;
        // Send email...
    }
}
```

## InjectConfiguration Attribute Properties

The `[InjectConfiguration]` attribute supports several properties for advanced scenarios, but automatic binding may not work:

```csharp
[InjectConfiguration(
    configurationKey: "Section:Key",     // Configuration path
    defaultValue: "fallback",            // Default value if configuration is missing
    required: true,                      // Whether the configuration value is required (default: true)
    supportsReloading: false            // Whether to support hot-reloading (default: false)
)]
```

## Recommended Approach (Options Pattern)

Use the Options pattern for reliable configuration access:

```csharp
[Service]
public partial class EmailService : IEmailService
{
    [Inject] private readonly IOptionsSnapshot<EmailSettings> _emailOptions;
    [Inject] private readonly ILogger<EmailService> _logger;
    
    public async Task SendEmailAsync(string to, string subject, string body)
    {
        var settings = _emailOptions.Value;
        using var client = new SmtpClient(settings.SmtpHost, settings.SmtpPort);
        client.EnableSsl = settings.UseSsl;
        // Send email...
    }
}
```

## Alternative: Manual IConfiguration Injection

For direct configuration access, use manual `IConfiguration` injection:

```csharp
[Service]
public partial class EmailService : IEmailService
{
    [Inject] private readonly IConfiguration _configuration;
    [Inject] private readonly ILogger<EmailService> _logger;
    
    public async Task SendEmailAsync(string to, string subject, string body)
    {
        var smtpHost = _configuration["Email:SmtpHost"];
        var smtpPort = _configuration.GetValue<int>("Email:SmtpPort");
        var useSsl = _configuration.GetValue<bool>("Email:UseSsl");
        
        using var client = new SmtpClient(smtpHost, smtpPort);
        client.EnableSsl = useSsl;
        // Send email...
    }
}
```

## Current Implementation Evidence

### What Actually Gets Generated

**Sample Code (from IoCTools.Sample):**
```csharp
[Service]
public partial class DatabaseConnectionService
{
    [InjectConfiguration("Database:ConnectionString")] 
    private readonly string _connectionString;
    
    [InjectConfiguration("Database:TimeoutSeconds")] 
    private readonly int _timeoutSeconds;
    
    [Inject] 
    private readonly ILogger<DatabaseConnectionService> _logger;
}
```

**Actually Generated Constructor:**
```csharp
public DatabaseConnectionService(ILogger<DatabaseConnectionService> logger)
{
    this._logger = logger;
    // NOTE: [InjectConfiguration] fields are NOT generated
    // _connectionString and _timeoutSeconds remain uninitialized
}
```

**What's Missing:**
- No `IConfiguration configuration` parameter
- No binding assignments for configuration fields  
- Only `[Inject]` dependencies are handled

**Current Status:** The generator recognizes `[InjectConfiguration]` attributes for diagnostic validation (IOC016-IOC019) but does not generate any constructor parameters or field assignments for them.

## Planned: Direct Value Injection (Not Yet Implemented)

### Simple Value Injection

```csharp
[Service]
public partial class EmailService : IEmailService
{
    [InjectConfiguration("Smtp:Host")]
    private readonly string _smtpHost;
    
    [InjectConfiguration("Smtp:Port")]
    private readonly int _smtpPort;
    
    [InjectConfiguration("Smtp:UseSsl")]
    private readonly bool _useSsl;
    
    public async Task SendEmailAsync(string to, string subject, string body)
    {
        // Note: Direct binding is not yet generating constructor assignments
        using var client = new SmtpClient(_smtpHost, _smtpPort);
        client.EnableSsl = _useSsl;
        // Send email...
    }
}
```

**Configuration (appsettings.json):**
```json
{
  "Smtp": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "UseSsl": true
  }
}
```

### Section Object Injection

```csharp
public class DatabaseOptions
{
    public string ConnectionString { get; set; }
    public int TimeoutSeconds { get; set; }
    public bool EnableRetry { get; set; }
}

[Service]
public partial class DatabaseService : IDatabaseService
{
    [InjectConfiguration("Database")]
    private readonly DatabaseOptions _options;
    
    public async Task<Data> GetDataAsync()
    {
        using var connection = new SqlConnection(_options.ConnectionString);
        connection.Open();
        // Use _options.TimeoutSeconds, _options.EnableRetry...
    }
}
```

**Configuration:**
```json
{
  "Database": {
    "ConnectionString": "Server=localhost;Database=MyApp",
    "TimeoutSeconds": 30,
    "EnableRetry": true
  }
}
```

## Supported Types

### Primitive Types

```csharp
[Service]
public partial class ConfigurationService
{
    [InjectConfiguration("App:Name")]
    private readonly string _appName;
    
    [InjectConfiguration("App:Version")]
    private readonly int _version;
    
    [InjectConfiguration("App:IsProduction")]
    private readonly bool _isProduction;
    
    [InjectConfiguration("App:Timeout")]
    private readonly TimeSpan _timeout;
    
    [InjectConfiguration("App:Price")]
    private readonly decimal _price;
}
```

### Nullable Types

```csharp
[Service]
public partial class OptionalConfigService
{
    [InjectConfiguration("Optional:Value")]
    private readonly string? _optionalValue;
    
    [InjectConfiguration("Optional:Count")]
    private readonly int? _optionalCount;
    
    public void Process()
    {
        if (_optionalValue != null)
        {
            // Process optional value
        }
    }
}
```

### Collections

```csharp
[Service]
public partial class CollectionConfigService
{
    [InjectConfiguration("AllowedHosts")]
    private readonly string[] _allowedHosts;
    
    [InjectConfiguration("SupportedLanguages")]
    private readonly List<string> _languages;
    
    [InjectConfiguration("FeatureFlags")]
    private readonly IReadOnlyList<string> _features;
}
```

**Configuration:**
```json
{
  "AllowedHosts": ["localhost", "example.com", "api.example.com"],
  "SupportedLanguages": ["en", "es", "fr"],
  "FeatureFlags": ["NewUI", "AdvancedSearch", "BetaFeatures"]
}
```

### Complex Objects

```csharp
public class LoggingOptions
{
    public string Level { get; set; }
    public bool IncludeTimestamp { get; set; }
    public FileOptions File { get; set; }
}

public class FileOptions  
{
    public string Path { get; set; }
    public int MaxSizeInMB { get; set; }
    public int RetainDays { get; set; }
}

[Service]
public partial class LoggingService : ILoggingService
{
    [InjectConfiguration("Logging")]
    private readonly LoggingOptions _loggingOptions;
    
    public void LogMessage(string message)
    {
        // Access nested configuration
        var filePath = _loggingOptions.File.Path;
        var maxSize = _loggingOptions.File.MaxSizeInMB;
        // Implementation...
    }
}
```

## Advanced Configuration Patterns

### Environment-Specific Configuration

```csharp
[Service]
public partial class EnvironmentConfigService
{
    [InjectConfiguration("ConnectionStrings:DefaultConnection")]
    private readonly string _connectionString;
    
    [InjectConfiguration("Environment:Name")]
    private readonly string _environmentName;
    
    [InjectConfiguration("Features:EnableAdvancedLogging")]
    private readonly bool _advancedLogging;
    
    public void ConfigureService()
    {
        if (_environmentName == "Development" && _advancedLogging)
        {
            // Enable development-specific logging
        }
    }
}
```

**Configuration (appsettings.Development.json):**
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=MyApp_Dev"
  },
  "Environment": {
    "Name": "Development"
  },
  "Features": {
    "EnableAdvancedLogging": true
  }
}
```

### Default Values and Fallbacks

**Using Attribute DefaultValue Property:**
```csharp
[Service]
public partial class DefaultValueService
{
    [InjectConfiguration("Cache:ExpirationMinutes", defaultValue: "60")]
    private readonly int _cacheExpiration;
    
    [InjectConfiguration("Cache:MaxItems", defaultValue: "1000")]
    private readonly int _maxItems;
    
    public void ConfigureCache()
    {
        // Uses configured value or attribute default
        ConfigureCacheWithSettings(_cacheExpiration, _maxItems);
    }
}
```

**Using Field Initialization (Legacy Pattern):**
```csharp
[Service]
public partial class LegacyDefaultService
{
    [InjectConfiguration("Cache:ExpirationMinutes")]
    private readonly int _cacheExpiration = 60; // Field default (fallback behavior)
    
    public void ConfigureCache()
    {
        // Uses configured value or field default to 60
        ConfigureCacheWithSettings(_cacheExpiration);
    }
}
```

### Combining with Regular Dependencies

```csharp
[Service]
public partial class HybridConfigService : IConfigService
{
    // Regular dependency injection
    [Inject] private readonly ILogger<HybridConfigService> _logger;
    [Inject] private readonly IHttpClientFactory _httpClientFactory;
    
    // Configuration injection
    [InjectConfiguration("Api:BaseUrl")]
    private readonly string _apiBaseUrl;
    
    [InjectConfiguration("Api:TimeoutSeconds")]
    private readonly int _timeoutSeconds;
    
    [InjectConfiguration("Api:RetryPolicy")]
    private readonly RetryPolicyOptions _retryPolicy;
    
    public async Task<ApiResponse> CallApiAsync(string endpoint)
    {
        _logger.LogInformation("Calling API: {BaseUrl}{Endpoint}", _apiBaseUrl, endpoint);
        
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(_apiBaseUrl);
        client.Timeout = TimeSpan.FromSeconds(_timeoutSeconds);
        
        // Use retry policy configuration
        return await CallWithRetry(client, endpoint, _retryPolicy);
    }
}
```

## Configuration Validation

### Required Configuration (IOC016)

```csharp
[Service]
public partial class ValidatedService
{
    [InjectConfiguration("")] // ❌ IOC016: Empty configuration key
    private readonly string _empty;
    
    [InjectConfiguration("   ")] // ❌ IOC016: Whitespace-only key
    private readonly string _whitespace;
    
    [InjectConfiguration("Valid:Key")] // ✅ Valid
    private readonly string _valid;
}
```

### Type Compatibility (IOC017)

```csharp
public interface IUnsupportedInterface
{
    void DoSomething();
}

[Service]
public partial class TypeValidationService
{
    [InjectConfiguration("SomeSection")]
    private readonly IUnsupportedInterface _unsupported; // ❌ IOC017: Unsupported type
    
    [InjectConfiguration("ValidSection")]
    private readonly SupportedClass _supported; // ✅ Class with parameterless constructor
}
```

### Class Requirements (IOC018)

```csharp
[InjectConfiguration("MySection")] // ❌ IOC018: Non-partial class
public class NonPartialService
{
    // Cannot generate constructor
}

[InjectConfiguration("MySection")] // ✅ Correct
public partial class PartialService
{
    // Constructor can be generated
}
```

### Static Field Restrictions (IOC019)

```csharp
[Service]
public partial class StaticFieldService
{
    [InjectConfiguration("Value")]
    private static readonly string _staticValue; // ❌ IOC019: Static not supported
    
    [InjectConfiguration("Value")]
    private readonly string _instanceValue; // ✅ Instance field
}
```

## Generated Code Examples

### Simple Value Injection

**Source:**
```csharp
[Service]
public partial class EmailService
{
    [InjectConfiguration("Smtp:Host")]
    private readonly string _host;
    
    [InjectConfiguration("Smtp:Port")]
    private readonly int _port;
}
```

**Generated Constructor:**
```csharp
public EmailService(IConfiguration configuration)
{
    _host = configuration["Smtp:Host"];
    _port = configuration.GetValue<int>("Smtp:Port");
}
```

**Note:** Direct configuration binding is **not yet implemented**. The generator recognizes `[InjectConfiguration]` attributes for validation purposes but does not generate constructor parameters or binding assignments. Only `[Inject]` dependencies are currently generated in constructors.

### Complex Object Injection

**Source:**
```csharp
[Service]
public partial class DatabaseService
{
    [InjectConfiguration("Database")]
    private readonly DatabaseOptions _options;
}
```

**Generated Constructor:**
```csharp
public DatabaseService(IConfiguration configuration)
{
    _options = new DatabaseOptions();
    configuration.GetSection("Database").Bind(_options);
}
```

### Mixed Dependencies

**Source:**
```csharp
[Service]
public partial class MixedService
{
    [Inject] private readonly ILogger<MixedService> _logger;
    [InjectConfiguration("App:Name")] private readonly string _appName;
    [InjectConfiguration("Settings")] private readonly AppSettings _settings;
}
```

**Generated Constructor:**
```csharp
public MixedService(ILogger<MixedService> logger, IConfiguration configuration)
{
    _logger = logger;
    _appName = configuration["App:Name"];
    _settings = new AppSettings();
    configuration.GetSection("Settings").Bind(_settings);
}
```

## Best Practices

### Configuration Organization

```csharp
[Service]
public partial class WellOrganizedService
{
    // Group related configuration
    [InjectConfiguration("Database:ConnectionString")]
    private readonly string _connectionString;
    
    [InjectConfiguration("Database:TimeoutSeconds")]
    private readonly int _timeoutSeconds;
    
    // Feature flags
    [InjectConfiguration("Features:EnableCache")]
    private readonly bool _enableCache;
    
    [InjectConfiguration("Features:EnableLogging")]
    private readonly bool _enableLogging;
    
    // Complex configuration objects
    [InjectConfiguration("Email")]
    private readonly EmailOptions _emailOptions;
    
    [InjectConfiguration("Security")]
    private readonly SecurityOptions _securityOptions;
}
```

### Environment-Specific Services

```csharp
[Service]
[ConditionalService(Environment = "Development")]
public partial class DevelopmentEmailService : IEmailService
{
    [InjectConfiguration("Development:Email:LogOnly")]
    private readonly bool _logOnly;
    
    [InjectConfiguration("Development:Email:OutputPath")]
    private readonly string _outputPath;
    
    public async Task SendEmailAsync(string to, string subject, string body)
    {
        if (_logOnly)
        {
            File.WriteAllText($"{_outputPath}/email_{DateTime.Now:yyyyMMdd_HHmmss}.txt", 
                $"To: {to}\nSubject: {subject}\n\n{body}");
        }
    }
}

[Service]
[ConditionalService(NotEnvironment = "Development")]
public partial class ProductionEmailService : IEmailService
{
    [InjectConfiguration("Email:SmtpHost")]
    private readonly string _smtpHost;
    
    [InjectConfiguration("Email:Credentials")]
    private readonly EmailCredentials _credentials;
    
    public async Task SendEmailAsync(string to, string subject, string body)
    {
        // Send real email using SMTP configuration
    }
}
```

### Configuration Classes Design

```csharp
// ✅ Good - Clear property names, parameterless constructor
public class ApiOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public bool EnableRetry { get; set; } = true;
    public RetryOptions Retry { get; set; } = new();
}

public class RetryOptions  
{
    public int MaxAttempts { get; set; } = 3;
    public int DelaySeconds { get; set; } = 1;
    public bool ExponentialBackoff { get; set; } = true;
}

[Service]
public partial class ApiService : IApiService
{
    [InjectConfiguration("Api")]
    private readonly ApiOptions _apiOptions;
}
```

### Validation Integration

```csharp
public class DatabaseOptions : IValidatable
{
    public string ConnectionString { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            throw new InvalidOperationException("ConnectionString is required");
            
        if (TimeoutSeconds <= 0)
            throw new InvalidOperationException("TimeoutSeconds must be positive");
    }
}

[Service]
public partial class ValidatedDatabaseService
{
    [InjectConfiguration("Database")]
    private readonly DatabaseOptions _options;
    
    // Generated constructor will include validation call
    public async Task InitializeAsync()
    {
        _options.Validate(); // Ensure configuration is valid
        // Initialize database connection...
    }
}
```

## Integration with IOptions Pattern

### When to Use Each Approach

**Use `[InjectConfiguration]` when:**
- Configuration is simple and doesn't change frequently
- You want direct access without wrapper objects
- Configuration is used throughout service lifetime

**Use `IOptions<T>` when:**
- Configuration can change at runtime (hot reload)
- You need validation attributes
- Multiple services share the same configuration type

```csharp
// Direct injection for simple, stable config
[Service]
public partial class CacheService
{
    [InjectConfiguration("Cache:DefaultTtlMinutes")]
    private readonly int _defaultTtl;
}

// IOptions for complex, changeable config
[Service]
public partial class EmailService
{
    [Inject] private readonly IOptionsMonitor<SmtpOptions> _smtpOptions;
    
    public async Task SendAsync(string to, string subject, string body)
    {
        var currentOptions = _smtpOptions.CurrentValue; // Can change at runtime
        // Send email with current options
    }
}
```

## Performance Considerations

### Configuration Binding Cost

```csharp
// ✅ Efficient - Simple value binding
[Service]
public partial class EfficientService
{
    [InjectConfiguration("SimpleValue")]
    private readonly string _value;
}

// ⚠️ Consider cost - Complex object binding
[Service]
public partial class ComplexService
{
    [InjectConfiguration("LargeConfiguration")]
    private readonly LargeConfigurationObject _config; // Binding cost at startup
}
```

### Caching Configuration

```csharp
[Service(Lifetime.Singleton)] // Cache configuration in singleton
public partial class ConfigurationCacheService
{
    [InjectConfiguration("ExpensiveToCalculate")]
    private readonly ComplexCalculatedOptions _options;
    
    public ComplexCalculatedOptions GetOptions() => _options; // Cached
}

[Service]
public partial class ConsumerService
{
    [Inject] private readonly ConfigurationCacheService _configCache;
    
    public void DoWork()
    {
        var options = _configCache.GetOptions(); // No recalculation
    }
}
```

## Summary: What Actually Works

### ✅ Use These Patterns (Working)

**1. Options Pattern (Recommended):**
```csharp
[Service]
public partial class MyService
{
    [Inject] private readonly IOptions<MySettings> _options;
    [Inject] private readonly IOptionsSnapshot<MySettings> _snapshot;
    [Inject] private readonly IOptionsMonitor<MySettings> _monitor;
}
```

**2. Manual IConfiguration Injection:**
```csharp
[Service] 
public partial class MyService
{
    [Inject] private readonly IConfiguration _config;
    
    public void DoWork()
    {
        var value = _config["MySection:MySetting"];
        var typedValue = _config.GetValue<int>("MySection:MyNumber");
    }
}
```

### ❌ Don't Use These (Not Implemented)

```csharp
[Service]
public partial class MyService  
{
    // These will be ignored by the generator:
    [InjectConfiguration("Database:ConnectionString")]
    private readonly string _connectionString;
    
    [InjectConfiguration("Settings")]
    private readonly MySettings _settings;
}
```

**Current Reality:** `[InjectConfiguration]` attributes are recognized for validation but do not generate any constructor code. Use Options pattern or manual `IConfiguration` injection instead.

## Next Steps

- **[Conditional Services](conditional-services.md)** - Environment and configuration-based service registration
- **[Service Declaration](service-declaration.md)** - Core service registration patterns
- **[MSBuild Configuration](msbuild-configuration.md)** - Configuring IoCTools behavior
- **[Advanced Generics](advanced-generics.md)** - Generic services with configuration injection