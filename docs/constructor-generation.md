# Constructor Generation

> **Status Update**: This documentation has been revised to reflect the **actual current implementation** rather than planned features. Some advanced patterns described here are still in development.

IoCTools automatically generates constructors for partial classes marked with `[Service]`, handling dependency injection and inheritance chains. The generator currently focuses on `[Inject]` field injection with reliable constructor parameter ordering.

## Implementation Status

✅ **Fully Working**:
- Basic `[Inject]` field injection with standard .NET services (ILogger, IConfiguration, etc.)
- Constructor generation for partial classes with `[Service]` attribute
- Inheritance chain support with proper base constructor calls
- Service registration with multiple interfaces

⚠️ **Partially Implemented**:
- `[InjectConfiguration]` attribute exists but automatic constructor binding is limited - use manual IConfiguration injection instead
- Complex `[DependsOn]` patterns - constructor generation works but field generation is manual
- `[RegisterAsAll]` with instance sharing patterns

✅ **Working Features**:
- `[ConditionalService]` attribute is fully implemented and working
- `[BackgroundService]` attribute registration is implemented

## Basic Constructor Generation

### Simple Service Constructor

**Source Code:**
```csharp
[Service]
public partial class EmailService : IEmailService
{
    [Inject] private readonly ILogger<EmailService> _logger;
    [Inject] private readonly IConfiguration _configuration;
    
    public async Task SendEmailAsync(string to, string subject, string body)
    {
        _logger.LogInformation("Sending email to {To}", to);
        // Implementation...
    }
}
```

**Generated Constructor:**
```csharp
public EmailService(ILogger<EmailService> logger, IConfiguration configuration)
{
    this._logger = logger;
    this._configuration = configuration;
}
```

### DependsOn Pattern Constructor

**Source Code:**
```csharp
[Service]
[DependsOn<IEmailService, ISmsService, ILogger<NotificationService>>]
public partial class NotificationService : INotificationService
{
    public async Task NotifyAsync(string message)
    {
        await _emailService.SendAsync(message);
        await _smsService.SendAsync(message);
        _logger.LogInformation("Notification sent: {Message}", message);
    }
}
```

**Generated Constructor and Fields:**
```csharp
// Generated fields (camelCase with underscore prefix by default)
private readonly IEmailService _emailService;
private readonly ISmsService _smsService;
private readonly ILogger<NotificationService> _logger;

public NotificationService(IEmailService emailService, ISmsService smsService, ILogger<NotificationService> logger)
{
    this._emailService = emailService;
    this._smsService = smsService;
    this._logger = logger;
}
```

## Configuration Injection in Constructors

> **Note:** InjectConfiguration is implemented in IoCTools.Abstractions but **configuration binding in constructors is not yet fully implemented** in the generator. Currently, services requiring configuration should manually inject `IConfiguration` and handle binding in the constructor or use the Options pattern.

### Current Configuration Pattern (Working)

**Source Code:**
```csharp
[Service]
public partial class CacheService : ICacheService
{
    [Inject] private readonly IMemoryCache _memoryCache;
    [Inject] private readonly IConfiguration _configuration;
    [Inject] private readonly ILogger<CacheService> _logger;
    
    public T GetOrSet<T>(string key, Func<T> factory)
    {
        // Manually access configuration as needed
        var ttl = _configuration.GetValue<int>("Cache:TTL", 300);
        return _memoryCache.GetOrCreate(key, entry => 
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(ttl);
            return factory();
        });
    }
}
```

**Generated Constructor:**
```csharp
public CacheService(IMemoryCache memoryCache, IConfiguration configuration, ILogger<CacheService> logger)
{
    this._memoryCache = memoryCache;
    this._configuration = configuration;
    this._logger = logger;
}
```

### Planned Configuration Pattern (InjectConfiguration - Limited Implementation)

**Source Code (Limited Implementation - Use Manual IConfiguration Instead):**
```csharp
[Service]
public partial class ApiService : IApiService
{
    [Inject] private readonly HttpClient _httpClient;
    [Inject] private readonly ILogger<ApiService> _logger;
    
    [InjectConfiguration("Api:BaseUrl")]
    private readonly string _baseUrl;
    
    [InjectConfiguration("Api:Settings")]
    private readonly ApiSettings _settings;
}
```

**Expected Generated Constructor (May Not Work - Use IConfiguration Manually):**
```csharp
public ApiService(HttpClient httpClient, ILogger<ApiService> logger, IConfiguration configuration)
{
    this._httpClient = httpClient;
    this._logger = logger;
    
    // Configuration binding (planned feature)
    this._baseUrl = configuration["Api:BaseUrl"];
    
    this._settings = new ApiSettings();
    configuration.GetSection("Api:Settings").Bind(this._settings);
}
```

## Inheritance Chain Constructors

### Simple Inheritance (Working Pattern)

**Source Code:**
```csharp
[Service]
public abstract partial class BasePaymentProcessor
{
    [Inject] protected readonly ILogger<BasePaymentProcessor> Logger;
    
    protected async Task LogPaymentAttemptAsync(string paymentMethod, decimal amount)
    {
        Logger.LogInformation("Processing {Method} payment for ${Amount}", paymentMethod, amount);
    }
}

[Service]
public partial class AdvancedPaymentBase : BasePaymentProcessor
{
    [Inject] private readonly ILogger<AdvancedPaymentBase> _advancedLogger;
    
    protected async Task<bool> ValidatePaymentAsync(decimal amount)
    {
        _advancedLogger.LogInformation("Validating payment amount: ${Amount}", amount);
        return amount > 0;
    }
}
```

**Generated Constructors:**
```csharp
// BasePaymentProcessor constructor
protected BasePaymentProcessor(ILogger<BasePaymentProcessor> logger)
{
    this.Logger = logger;
}

// AdvancedPaymentBase constructor with base constructor call
public AdvancedPaymentBase(ILogger<BasePaymentProcessor> logger, ILogger<AdvancedPaymentBase> advancedLogger) 
    : base(logger)
{
    this._advancedLogger = advancedLogger;
}
```

### Three-Level Inheritance Chain (Working Pattern)

**Source Code:**
```csharp
[Service]
public abstract partial class BasePaymentProcessor
{
    [Inject] protected readonly ILogger<BasePaymentProcessor> Logger;
}

[Service]
public partial class AdvancedPaymentBase : BasePaymentProcessor
{
    [Inject] private readonly ILogger<AdvancedPaymentBase> _advancedLogger;
}

[Service]
public partial class EnterprisePaymentProcessor : AdvancedPaymentBase, IEnterprisePaymentProcessor
{
    [Inject] private readonly ILogger<EnterprisePaymentProcessor> _enterpriseLogger;
    
    public async Task<PaymentResult> ProcessEnterprisePaymentAsync(Payment payment, int orderId)
    {
        Logger.LogInformation("Processing enterprise payment for order {OrderId}", orderId);
        return new PaymentResult(true, "Enterprise payment processed");
    }
}
```

**Generated Constructors:**
```csharp
// BasePaymentProcessor constructor
protected BasePaymentProcessor(ILogger<BasePaymentProcessor> logger)
{
    this.Logger = logger;
}

// AdvancedPaymentBase constructor
public AdvancedPaymentBase(ILogger<BasePaymentProcessor> logger, ILogger<AdvancedPaymentBase> advancedLogger) 
    : base(logger)
{
    this._advancedLogger = advancedLogger;
}

// EnterprisePaymentProcessor constructor
public EnterprisePaymentProcessor(
    ILogger<BasePaymentProcessor> logger, 
    ILogger<AdvancedPaymentBase> advancedLogger, 
    ILogger<EnterprisePaymentProcessor> enterpriseLogger) 
    : base(logger, advancedLogger)
{
    this._enterpriseLogger = enterpriseLogger;
}
```

## Constructor Parameter Ordering

### Actual Dependency Ordering Rules

Based on the current generator implementation, IoCTools follows this parameter ordering:

1. **[Inject] dependencies** (in alphabetical order by type name)
2. **IConfiguration** (when configuration injection is needed)

**Example from Working Code:**
```csharp
[Service]
public partial class OrderService : IOrderService
{
    [Inject] private readonly IEmailService _emailService;
    [Inject] private readonly ILogger<OrderService> _logger;
    [Inject] private readonly IPaymentService _paymentService;
}
```

**Actual Generated Constructor:**
```csharp
// Parameters are ordered alphabetically by service type
public OrderService(
    IEmailService emailService,           // E comes first alphabetically
    ILogger<OrderService> logger,          // L comes second
    IPaymentService paymentService         // P comes third
)
{
    this._emailService = emailService;
    this._logger = logger;
    this._paymentService = paymentService;
}
```

**With Configuration:**
```csharp
[Service]
public partial class CacheService : ICacheService
{
    [Inject] private readonly IMemoryCache _memoryCache;
    [Inject] private readonly ILogger<CacheService> _logger;
    // IConfiguration injected automatically when configuration is used
}
```

**Generated Constructor:**
```csharp
public CacheService(
    IMemoryCache memoryCache,              // Alphabetical ordering
    IConfiguration configuration,          // Configuration parameter
    ILogger<CacheService> logger           // Logger comes after configuration
)
{
    this._memoryCache = memoryCache;
    this._configuration = configuration;
    this._logger = logger;
}
```

## Specialized Constructor Patterns

### Background Service Constructors

> **Note:** BackgroundServiceAttribute is implemented but automatic IHostedService registration may not be fully working yet.

**Source Code:**
```csharp
[Service(Lifetime.Singleton)]
[BackgroundService]
public partial class EmailProcessorService : BackgroundService
{
    [Inject] private readonly IServiceProvider _serviceProvider;
    [Inject] private readonly ILogger<EmailProcessorService> _logger;
    
    // Manual configuration access until InjectConfiguration is implemented
    [Inject] private readonly IConfiguration _configuration;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = _configuration.GetValue<int>("EmailProcessor:IntervalSeconds", 30);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _serviceProvider.CreateScope();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            // Process emails...
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }
}
```

**Generated Constructor:**
```csharp
public EmailProcessorService(
    IConfiguration configuration,
    ILogger<EmailProcessorService> logger,
    IServiceProvider serviceProvider)
{
    this._configuration = configuration;
    this._logger = logger;
    this._serviceProvider = serviceProvider;
}
```

### Generic Service Constructors

**Source Code:**
```csharp
[Service]
public partial class Repository<T> : IRepository<T> where T : class
{
    [Inject] private readonly IDbContext _context;
    [Inject] private readonly ILogger<Repository<T>> _logger;
    
    [InjectConfiguration("Database:DefaultTimeout")]
    private readonly int _timeoutSeconds;
    
    public async Task<T> GetByIdAsync(int id)
    {
        _context.Database.SetCommandTimeout(_timeoutSeconds);
        return await _context.Set<T>().FindAsync(id);
    }
}
```

**Generated Constructor:**
```csharp
public Repository(
    IDbContext context, 
    ILogger<Repository<T>> logger,
    IConfiguration configuration)
{
    this._context = context;
    this._logger = logger;
    this._timeoutSeconds = configuration.GetValue<int>("Database:DefaultTimeout");
}
```

### Conditional Service Constructors

> **Note:** ConditionalServiceAttribute is implemented but runtime condition evaluation may have limitations.

**Source Code:**
```csharp
[Service]
[ConditionalService(Environment = "Production")]
public partial class SmtpEmailService : IEnvironmentEmailService
{
    [Inject] private readonly ILogger<SmtpEmailService> _logger;
    [Inject] private readonly IConfiguration _configuration;
    
    public async Task SendAsync(string to, string subject, string body)
    {
        var smtpHost = _configuration["Email:SmtpHost"];
        _logger.LogInformation("Sending email via SMTP: {SmtpHost}", smtpHost);
        // Production SMTP implementation
    }
}

[Service]
[ConditionalService(NotEnvironment = "Production")]
public partial class ConsoleEmailService : IEnvironmentEmailService
{
    [Inject] private readonly ILogger<ConsoleEmailService> _logger;
    
    public async Task SendAsync(string to, string subject, string body)
    {
        _logger.LogInformation("Console Email - To: {To}, Subject: {Subject}", to, subject);
        Console.WriteLine($"EMAIL: {to} - {subject}\n{body}");
    }
}
```

**Generated Constructors:**
```csharp
// SMTP constructor (registered when Environment = "Production")
public SmtpEmailService(IConfiguration configuration, ILogger<SmtpEmailService> logger)
{
    this._configuration = configuration;
    this._logger = logger;
}

// Console constructor (registered when Environment != "Production")
public ConsoleEmailService(ILogger<ConsoleEmailService> logger)
{
    this._logger = logger;
}
```

## Advanced Constructor Scenarios

### Multi-Interface Registration with Shared Constructor

> **Note:** RegisterAsAllAttribute exists but instance sharing behavior may not be fully implemented.

**Source Code:**
```csharp
[Service]
[RegisterAsAll(InstanceSharing.Shared)]
public partial class UnifiedDataService : IDataReader, IDataWriter, ICacheManager
{
    [Inject] private readonly IMemoryCache _cache;
    [Inject] private readonly IConfiguration _configuration;
    [Inject] private readonly ILogger<UnifiedDataService> _logger;
    
    // Shared state across all interfaces
    private readonly Dictionary<string, DateTime> _accessLog = new();
    
    public async Task<T> ReadAsync<T>(string key) 
    { 
        _logger.LogInformation("Reading key: {Key}", key);
        return _cache.Get<T>(key); 
    }
    
    public async Task WriteAsync<T>(string key, T value) 
    { 
        _logger.LogInformation("Writing key: {Key}", key);
        _cache.Set(key, value); 
    }
    
    public Task ClearCacheAsync() 
    { 
        _cache.Clear();
        return Task.CompletedTask; 
    }
}
```

**Generated Constructor:**
```csharp
// Single constructor serves all interface registrations
public UnifiedDataService(
    IMemoryCache cache,
    IConfiguration configuration, 
    ILogger<UnifiedDataService> logger)
{
    this._cache = cache;
    this._configuration = configuration;
    this._logger = logger;
}
```

### External Service Dependencies

> **Note:** ExternalServiceAttribute exists but may not affect service registration behavior as expected.

**Source Code:**
```csharp
[ExternalService] // Dependencies provided externally
public partial class ApiClientService : IApiClientService
{
    [Inject] private readonly HttpClient _httpClient;        // Registered manually
    [Inject] private readonly ILogger<ApiClientService> _logger;
    [Inject] private readonly IConfiguration _configuration;  // For accessing settings
    
    public async Task<ApiResponse> CallApiAsync(string endpoint)
    {
        var apiKey = _configuration["Api:ClientSettings:ApiKey"];
        _httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKey);
        _logger.LogInformation("Calling API endpoint: {Endpoint}", endpoint);
        // API call implementation
        return new ApiResponse { Success = true };
    }
}
```

**Generated Constructor:**
```csharp
// Constructor generated even for [ExternalService] classes
public ApiClientService(
    IConfiguration configuration,
    HttpClient httpClient, 
    ILogger<ApiClientService> logger)
{
    this._configuration = configuration;
    this._httpClient = httpClient;
    this._logger = logger;
}
```

## Constructor Customization

### Existing Constructor Prevention

```csharp
[Service]
public partial class CustomConstructorService : ICustomService
{
    [Inject] private readonly ILogger<CustomConstructorService> _logger;
    
    // Custom constructor prevents generation
    public CustomConstructorService(ILogger<CustomConstructorService> logger, string customParam)
    {
        _logger = logger;
        // Custom initialization logic
    }
    
    public void DoWork() => _logger.LogInformation("Custom work");
}
```

**Result:** No constructor is generated because a constructor already exists.

### Partial Constructor Enhancement

```csharp
[Service]
public partial class EnhancedService : IEnhancedService
{
    [Inject] private readonly ILogger<EnhancedService> _logger;
    [Inject] private readonly IConfiguration _configuration;
    
    // Generated constructor will be created, then enhanced with partial methods
    partial void OnConstructorCompleted()
    {
        // Custom initialization after dependency injection
        var setting = _configuration["CustomSetting"];
        InitializeCustomLogic(setting);
    }
    
    private void InitializeCustomLogic(string setting)
    {
        _logger.LogInformation("Service initialized with setting: {Setting}", setting);
    }
}
```

## Performance Considerations

### Constructor Compilation

**Efficient Constructor:**
```csharp
[Service]
public partial class EfficientService
{
    [Inject] private readonly IService1 _service1;
    [Inject] private readonly IService2 _service2;
    [Inject] private readonly IService3 _service3;
}

// Generated: Simple, direct assignment
public EfficientService(IService1 service1, IService2 service2, IService3 service3)
{
    this._service1 = service1;
    this._service2 = service2;
    this._service3 = service3;
}
```

**Complex Constructor (Higher Overhead):**
```csharp
[Service]
public partial class ComplexService : BaseService
{
    [InjectConfiguration("Section1")] private readonly Settings1 _settings1;
    [InjectConfiguration("Section2")] private readonly Settings2 _settings2;
    [InjectConfiguration("Section3")] private readonly Settings3 _settings3;
}

// Generated: Multiple configuration binding operations
public ComplexService(IBaseService baseService, IConfiguration configuration) 
    : base(baseService)
{
    this._settings1 = new Settings1();
    configuration.GetSection("Section1").Bind(this._settings1);
    
    this._settings2 = new Settings2();
    configuration.GetSection("Section2").Bind(this._settings2);
    
    this._settings3 = new Settings3();
    configuration.GetSection("Section3").Bind(this._settings3);
}
```

### Dependency Resolution Order

Constructor parameters are resolved in the order they appear, which can affect performance with circular references or complex dependency graphs.

**Optimized Ordering:**
```csharp
[Service]
[DependsOn<ILightweightService, IHeavyService>] // Lightweight first
public partial class OptimizedService
{
    // ILightweightService resolved first (faster)
    // IHeavyService resolved second (slower but necessary)
}
```

## Debugging Generated Constructors

### Viewing Generated Code

Generated constructors can be viewed by building with compiler generated files enabled:

```bash
# Clean build with generated files output
dotnet clean
dotnet build --property EmitCompilerGeneratedFiles=true --property CompilerGeneratedFilesOutputPath=generated
```

Generated files appear in:
- **Generated folder**: `generated/IoCTools.Generator/IoCTools.Generator.DependencyInjectionGenerator/`
- **Constructor files**: Named like `[Namespace]_[ClassName]_Constructor.g.cs`
- **Registration file**: `ServiceRegistrations.g.cs`

### Common Generation Issues

1. **Missing Partial Keyword**: Constructor won't be generated - class must be `partial`
2. **Existing Constructor**: Generation is skipped if any constructor already exists
3. **Abstract Classes**: Constructors generated but classes not automatically registered
4. **Build Errors**: Sample app may have intentional broken examples for testing
5. **Missing Dependencies**: IOC001 warnings indicate missing service implementations

### Current Limitations

> **Important:** This documentation describes the intended behavior. Some features are still being implemented:

- **Configuration Injection**: `[InjectConfiguration]` attribute exists but constructor generation with automatic binding is not fully implemented
- **Complex Inheritance**: Deep inheritance chains with mixed DependsOn patterns may have edge cases
- **Conditional Services**: Runtime condition evaluation may not work in all scenarios
- **Background Services**: Automatic IHostedService registration may be incomplete
- **Sample App Issues**: Some examples in IoCTools.Sample have intentional compile errors for testing purposes

## Best Practices

### Constructor Design

1. **Keep constructors simple** - complex logic should be in methods
2. **Minimize configuration binding** - use fewer, larger configuration objects
3. **Order dependencies logically** - lightweight dependencies first
4. **Use inheritance wisely** - deep chains increase constructor complexity

### Dependency Organization

```csharp
// ✅ Good - Grouped dependencies
[Service]
public partial class WellOrganizedService
{
    // Core dependencies
    [Inject] private readonly IRepository<User> _userRepository;
    [Inject] private readonly IRepository<Order> _orderRepository;
    
    // External services
    [Inject] private readonly IEmailService _emailService;
    
    // Infrastructure
    [Inject] private readonly ILogger<WellOrganizedService> _logger;
    
    // Configuration (minimal)
    [InjectConfiguration("Service:Settings")]
    private readonly ServiceSettings _settings;
}
```

### Testing Constructor Generation

```csharp
// Test that generated constructors work correctly
[TestMethod]
public void GeneratedConstructor_Should_InjectDependencies()
{
    // Arrange
    var services = new ServiceCollection();
    services.AddScoped<ILogger<TestService>, NullLogger<TestService>>();
    services.AddScoped<ITestDependency, MockTestDependency>();
    
    var serviceProvider = services.BuildServiceProvider();
    
    // Act
    var service = serviceProvider.GetRequiredService<TestService>();
    
    // Assert
    Assert.IsNotNull(service);
    // Verify dependencies are properly injected
}
```

## Working Examples

For reliable examples of working constructor generation patterns, see:

1. **Basic Services**: `/IoCTools.Sample/Services/BasicUsageExamples.cs`
2. **Generated Constructors**: Build sample project and check `generated/` folder
3. **Simple Patterns**: Focus on `[Inject]` field injection with standard .NET services
4. **Inheritance**: See `EnterprisePaymentProcessor` inheritance chain in sample

## Next Steps

- **[Basic Usage](basic-usage.md)** - Start with simple service patterns that are fully working
- **[Service Registration](service-registration.md)** - Understand how services are registered automatically
- **[Troubleshooting](troubleshooting.md)** - Common issues and solutions when constructor generation fails
- **[Architecture Limits](../ARCHITECTURAL_LIMITS.md)** - Understanding intentional design limitations