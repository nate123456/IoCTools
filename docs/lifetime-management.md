# Lifetime Management

IoCTools provides comprehensive service lifetime management with compile-time validation to prevent common dependency injection pitfalls. This guide covers all lifetime patterns, validation rules, and best practices.

## Service Lifetimes

### Scoped (Default)

```csharp
[Service] // Defaults to Scoped
public partial class OrderService : IOrderService
{
    [Inject] private readonly IDbContext _dbContext;
    [Inject] private readonly ILogger<OrderService> _logger;
    
    public async Task ProcessOrderAsync(Order order)
    {
        // Service lives for the duration of the HTTP request/scope
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();
    }
}
```

**Generated Registration:**
```csharp
services.AddScoped<IOrderService, OrderService>();
```

**When to Use:**
- Web applications (per-request services)
- Database contexts and repositories
- Services that maintain state during a request
- Default choice for most business logic services

### Singleton

```csharp
[Service(Lifetime.Singleton)]
public partial class ConfigurationCache : IConfigurationCache
{
    private readonly ConcurrentDictionary<string, object> _cache = new();
    
    [Inject] private readonly ILogger<ConfigurationCache> _logger;
    
    public T GetValue<T>(string key)
    {
        return _cache.GetOrAdd(key, _ => {
            _logger.LogInformation("Cache miss for {Key}", key);
            return LoadConfiguration<T>(key);
        });
    }
}
```

**Generated Registration:**
```csharp
services.AddSingleton<IConfigurationCache, ConfigurationCache>();
```

**When to Use:**
- Application-wide shared state
- Expensive-to-create services (connection pools, caches)
- Stateless services that are thread-safe
- Configuration and settings services

### Transient

```csharp
[Service(Lifetime.Transient)]
public partial class EmailValidator : IEmailValidator
{
    [Inject] private readonly ILogger<EmailValidator> _logger;
    
    public ValidationResult Validate(string email)
    {
        _logger.LogDebug("Validating email: {Email}", email);
        // Stateless validation logic
        return email.Contains("@") ? ValidationResult.Valid : ValidationResult.Invalid;
    }
}
```

**Generated Registration:**
```csharp
services.AddTransient<IEmailValidator, EmailValidator>();
```

**When to Use:**
- Lightweight, stateless services
- Services created frequently with short lifespans
- Services that don't hold state between calls
- Validation and utility services

## Lifetime Validation

### IOC012: Singleton Depends on Scoped

```csharp
[Service(Lifetime.Scoped)]
public partial class DatabaseContext : IDbContext { }

[Service(Lifetime.Singleton)] // ❌ IOC012: Singleton depending on Scoped
public partial class CacheService : ICacheService
{
    [Inject] private readonly IDbContext _context; // Scoped dependency
}
```

**Problem:** Singleton services outlive scoped services, potentially capturing disposed resources.

**Solutions:**

1. **Change service lifetime:**
```csharp
[Service(Lifetime.Scoped)] // ✅ Match dependency lifetime
public partial class CacheService : ICacheService
{
    [Inject] private readonly IDbContext _context;
}
```

2. **Use factory pattern:**
```csharp
[Service(Lifetime.Singleton)]
public partial class CacheService : ICacheService
{
    [Inject] private readonly IServiceProvider _serviceProvider;
    
    public async Task<Data> GetDataAsync(string key)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IDbContext>();
        return await context.GetDataAsync(key);
    }
}
```

3. **Change dependency lifetime:**
```csharp
[Service(Lifetime.Singleton)] // ✅ Make dependency singleton-safe
public partial class DatabaseConnectionFactory : IDbContext { }
```

### IOC013: Singleton Depends on Transient

```csharp
[Service(Lifetime.Transient)]
public partial class EmailSender : IEmailSender { }

[Service(Lifetime.Singleton)] // ⚠️ IOC013: May capture transient instance
public partial class NotificationService : INotificationService
{
    [Inject] private readonly IEmailSender _emailSender; // Transient dependency
}
```

**Problem:** Singleton may capture and hold onto a single transient instance, defeating the purpose of transient lifetime.

**Solutions:**

1. **Use factory pattern:**
```csharp
[Service(Lifetime.Singleton)]
public partial class NotificationService : INotificationService
{
    [Inject] private readonly Func<IEmailSender> _emailSenderFactory;
    
    public async Task SendNotificationAsync(string message)
    {
        var emailSender = _emailSenderFactory(); // New instance each time
        await emailSender.SendAsync(message);
    }
}
```

2. **Use IServiceProvider:**
```csharp
[Service(Lifetime.Singleton)]
public partial class NotificationService : INotificationService
{
    [Inject] private readonly IServiceProvider _serviceProvider;
    
    public async Task SendNotificationAsync(string message)
    {
        var emailSender = _serviceProvider.GetRequiredService<IEmailSender>();
        await emailSender.SendAsync(message);
    }
}
```

### IOC015: Inheritance Chain Lifetime

```csharp
[Service(Lifetime.Scoped)]
public partial class BaseRepository<T> : IRepository<T> where T : class
{
    [Inject] protected readonly IDbContext Context;
}

[Service(Lifetime.Singleton)] // ❌ IOC015: More restrictive than base
public partial class UserRepository : BaseRepository<User>, IUserRepository
{
    // Cannot be Singleton when base class is Scoped
}
```

**Problem:** Derived classes cannot have more restrictive lifetimes than their base classes.

**Solution:**
```csharp
[Service(Lifetime.Singleton)] // ✅ Make base class compatible
public partial class BaseRepository<T> : IRepository<T> where T : class { }

[Service(Lifetime.Singleton)] // ✅ Now compatible
public partial class UserRepository : BaseRepository<User>, IUserRepository { }
```

## Background Service Lifetimes

### IOC014: Background Service Lifetime

```csharp
[Service(Lifetime.Scoped)] // ❌ IOC014: Background services should be Singleton
[BackgroundService]
public partial class EmailProcessorService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Background service logic
    }
}
```

**Problem:** Background services run for the application lifetime and should be Singleton.

**Solution:**
```csharp
[Service(Lifetime.Singleton)] // ✅ Correct for background services
[BackgroundService]
public partial class EmailProcessorService : BackgroundService
{
    [Inject] private readonly IServiceProvider _serviceProvider;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _serviceProvider.CreateScope();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            await ProcessEmailsAsync(emailService);
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
```

## Advanced Lifetime Patterns

### Scoped Factory Pattern

```csharp
[Service(Lifetime.Singleton)]
public partial class ProcessorFactory : IProcessorFactory
{
    [Inject] private readonly IServiceProvider _serviceProvider;
    
    public IProcessor CreateProcessor(string type)
    {
        using var scope = _serviceProvider.CreateScope();
        return type switch
        {
            "email" => scope.ServiceProvider.GetRequiredService<IEmailProcessor>(),
            "sms" => scope.ServiceProvider.GetRequiredService<ISmsProcessor>(),
            _ => throw new ArgumentException($"Unknown processor type: {type}")
        };
    }
}

[Service(Lifetime.Scoped)]
public partial class EmailProcessor : IEmailProcessor
{
    [Inject] private readonly IDbContext _context; // Scoped dependency
    
    public async Task ProcessAsync()
    {
        // Can safely use scoped dependencies
        var emails = await _context.PendingEmails.ToListAsync();
        foreach (var email in emails)
        {
            await SendEmailAsync(email);
        }
    }
}
```

### Conditional Lifetime Based on Environment

```csharp
[Service(Lifetime.Singleton)]
[ConditionalService(Environment = "Production")]
public partial class ProductionCacheService : ICacheService
{
    [Inject] private readonly IDistributedCache _distributedCache;
    
    public async Task<T> GetAsync<T>(string key)
    {
        // Production: Use distributed cache (singleton-safe)
        return await _distributedCache.GetAsync<T>(key);
    }
}

[Service(Lifetime.Scoped)]
[ConditionalService(NotEnvironment = "Production")]
public partial class DevelopmentCacheService : ICacheService
{
    [Inject] private readonly IMemoryCache _memoryCache;
    
    public async Task<T> GetAsync<T>(string key)
    {
        // Development: Use memory cache (scoped for easier debugging)
        return _memoryCache.Get<T>(key);
    }
}
```

### Generic Service Lifetimes

```csharp
[Service(Lifetime.Scoped)]
public partial class Repository<T> : IRepository<T> where T : class
{
    [Inject] private readonly IDbContext _context;
    
    public async Task<T> GetByIdAsync(int id)
    {
        return await _context.Set<T>().FindAsync(id);
    }
}

[Service(Lifetime.Singleton)]
public partial class CacheRepository<T> : IRepository<T> where T : class
{
    [Inject] private readonly IMemoryCache _cache;
    [Inject] private readonly IServiceProvider _serviceProvider; // For creating scoped dependencies
    
    public async Task<T> GetByIdAsync(int id)
    {
        var cacheKey = $"{typeof(T).Name}:{id}";
        if (_cache.TryGetValue<T>(cacheKey, out var cached))
            return cached;
            
        // Create scoped dependency when needed
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<Repository<T>>();
        var entity = await repository.GetByIdAsync(id);
        
        _cache.Set(cacheKey, entity, TimeSpan.FromMinutes(5));
        return entity;
    }
}
```

## Factory Pattern Examples for Lifetime Compatibility

### Singleton Services Accessing Scoped Dependencies

```csharp
// Pattern 1: Using IServiceProvider directly
[Service(Lifetime.Singleton)]
public partial class SingletonEmailService : IEmailService
{
    [Inject] private readonly IServiceProvider _serviceProvider;
    
    public async Task SendEmailAsync(string to, string message)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IDbContext>();
        var template = await context.EmailTemplates.FirstAsync();
        
        // Send email with template
        await SendWithTemplateAsync(to, message, template);
    }
}

// Pattern 2: Using IServiceScopeFactory
[Service(Lifetime.Singleton)]
public partial class SingletonReportService : IReportService
{
    [Inject] private readonly IServiceScopeFactory _scopeFactory;
    
    public async Task GenerateReportAsync(int reportId)
    {
        using var scope = _scopeFactory.CreateScope();
        var reportRepo = scope.ServiceProvider.GetRequiredService<IReportRepository>();
        var report = await reportRepo.GetByIdAsync(reportId);
        
        // Process report
        await ProcessReportAsync(report);
    }
}

// Pattern 3: Using Func<T> factory for transient dependencies
[Service(Lifetime.Singleton)]
public partial class SingletonProcessorService : IProcessorService
{
    [Inject] private readonly Func<ITransientProcessor> _processorFactory;
    
    public async Task ProcessBatchAsync(IEnumerable<Task> tasks)
    {
        await Task.Run(async () =>
        {
            foreach (var task in tasks)
            {
                var processor = _processorFactory(); // New instance each time
                await processor.ProcessAsync(task);
            }
        });
    }
}
```

### Background Service Lifetime Patterns

```csharp
[Service(Lifetime.Singleton)]
[BackgroundService]
public partial class DataSyncService : BackgroundService
{
    [Inject] private readonly IServiceProvider _serviceProvider;
    [Inject] private readonly ILogger<DataSyncService> _logger;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var syncRepository = scope.ServiceProvider.GetRequiredService<ISyncRepository>();
                var apiClient = scope.ServiceProvider.GetRequiredService<IApiClient>();
                
                var pendingSync = await syncRepository.GetPendingSyncAsync();
                if (pendingSync != null)
                {
                    await apiClient.SyncDataAsync(pendingSync);
                    await syncRepository.MarkCompletedAsync(pendingSync.Id);
                }
                
                _logger.LogInformation("Data sync completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Data sync failed");
            }
            
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
```

## MSBuild Configuration

### Lifetime Validation Settings

```xml
<PropertyGroup>
  <!-- Configure lifetime validation severity (default: Warning) -->
  <IoCToolsLifetimeValidationSeverity>Error</IoCToolsLifetimeValidationSeverity>
  
  <!-- Disable all lifetime validation (default: false) -->
  <IoCToolsDisableLifetimeValidation>true</IoCToolsDisableLifetimeValidation>
</PropertyGroup>
```

**Available Severity Options:** `Error`, `Warning`, `Info`, `Hidden`

**Note:** Individual diagnostic configuration (per-diagnostic severity settings) is a planned feature. Currently, the `IoCToolsLifetimeValidationSeverity` setting applies to all lifetime validation diagnostics (IOC012, IOC013, IOC014, IOC015).

## Architectural Limits for Complex Lifetime Scenarios

### Complex Inheritance Chain Lifetimes

Some advanced inheritance scenarios with mixed lifetimes may encounter limitations:

```csharp
// ⚠️ Complex inheritance with deep generic constraints may have limits
[Service(Lifetime.Scoped)]
public partial class BaseProcessor<T, U> : IProcessor<T, U> 
    where T : class, IEntity, new() 
    where U : class, IValidatable
{
    [Inject] protected readonly IComplexDependency<T, U> _dependency;
}

[Service(Lifetime.Singleton)] // May trigger lifetime validation complexity
public partial class DerivedProcessor : BaseProcessor<MyEntity, MyValidatable>
{
    // Complex inheritance chains with generic constraints and mixed lifetimes
    // may require manual constructor implementation
}
```

**Workarounds for Complex Scenarios:**
- Use factory patterns instead of direct inheritance for complex lifetime mixing
- Consider composition over inheritance for complex generic lifetime scenarios
- Manually implement constructors when the generator encounters complex inheritance limits

### High-Frequency Transient Services

For performance-critical applications with high-frequency transient service creation:

```csharp
// Consider object pooling for high-frequency transient services
[Service(Lifetime.Scoped)] // Use scoped instead of transient for pooling
public partial class HighFrequencyService : IHighFrequencyService, IDisposable
{
    private bool _isReset = false;
    
    public void Reset() // Reset instead of recreating
    {
        _isReset = true;
        // Reset internal state
    }
    
    public void Dispose()
    {
        // Clean up resources
    }
}
```

## Best Practices

### Choosing the Right Lifetime

**Decision Tree:**

1. **Is it a BackgroundService?** → Singleton
2. **Does it hold application-wide state?** → Singleton
3. **Is it expensive to create?** → Singleton (if thread-safe)
4. **Does it need per-request state?** → Scoped
5. **Is it lightweight and stateless?** → Transient
6. **When in doubt** → Scoped (safest default)

### Thread Safety Considerations

```csharp
// ✅ Thread-safe Singleton
[Service(Lifetime.Singleton)]
public partial class ThreadSafeCacheService : ICacheService
{
    private readonly ConcurrentDictionary<string, object> _cache = new();
    
    public T GetOrAdd<T>(string key, Func<T> factory)
    {
        return (T)_cache.GetOrAdd(key, _ => factory());
    }
}

// ❌ NOT thread-safe - don't use as Singleton
[Service(Lifetime.Scoped)] // Use Scoped instead
public partial class StatefulService : IStatefulService
{
    private readonly List<string> _items = new(); // Not thread-safe
    
    public void AddItem(string item) => _items.Add(item);
}
```

### Resource Management

```csharp
[Service(Lifetime.Scoped)]
public partial class ResourceService : IResourceService, IDisposable
{
    [Inject] private readonly ILogger<ResourceService> _logger;
    private readonly HttpClient _httpClient = new();
    
    public async Task<string> FetchDataAsync(string url)
    {
        return await _httpClient.GetStringAsync(url);
    }
    
    public void Dispose()
    {
        _httpClient?.Dispose();
        _logger.LogInformation("ResourceService disposed");
    }
}
```

### Lifetime Composition

```csharp
// Singleton service managing scoped operations
[Service(Lifetime.Singleton)]
public partial class JobScheduler : IJobScheduler
{
    [Inject] private readonly IServiceProvider _serviceProvider;
    [Inject] private readonly ILogger<JobScheduler> _logger;
    
    public async Task ExecuteJobAsync<TJob>() where TJob : class, IJob
    {
        using var scope = _serviceProvider.CreateScope();
        var job = scope.ServiceProvider.GetRequiredService<TJob>();
        
        try
        {
            await job.ExecuteAsync();
            _logger.LogInformation("Job {JobType} completed successfully", typeof(TJob).Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobType} failed", typeof(TJob).Name);
        }
    }
}

[Service(Lifetime.Scoped)]
public partial class EmailJob : IJob
{
    [Inject] private readonly IDbContext _context;
    [Inject] private readonly IEmailService _emailService;
    
    public async Task ExecuteAsync()
    {
        var pendingEmails = await _context.PendingEmails.ToListAsync();
        foreach (var email in pendingEmails)
        {
            await _emailService.SendAsync(email);
        }
    }
}
```

### Common Lifetime Violation Scenarios

```csharp
// Scenario 1: Captive Dependency (IOC012)
[Service(Lifetime.Singleton)]
public partial class CaptiveDependencyService : ICacheService
{
    [Inject] private readonly IDbContext _context; // ❌ Scoped in Singleton
    
    // Problem: DbContext will be captured at singleton creation
    // and never disposed, leading to resource leaks
}

// Scenario 2: Transient Waste (IOC013)
[Service(Lifetime.Singleton)]
public partial class TransientWasteService : INotificationService
{
    [Inject] private readonly IEmailSender _emailSender; // ⚠️ Transient in Singleton
    
    // Problem: Only one instance of the transient service is created
    // and held for the application lifetime, defeating the purpose
}

// Scenario 3: Background Service Scope Issues (IOC014)
[Service(Lifetime.Scoped)] // ❌ Background services should be Singleton
[BackgroundService]
public partial class ScopedBackgroundService : BackgroundService
{
    // Problem: Background services run for the application lifetime
    // but scoped services are expected to have limited lifespans
}

// Scenario 4: Inheritance Lifetime Conflicts (IOC015)
[Service(Lifetime.Scoped)]
public partial class BaseService : IBaseService { }

[Service(Lifetime.Singleton)] // ❌ More restrictive than base
public partial class DerivedService : BaseService, IDerivedService
{
    // Problem: Derived service cannot have a more restrictive lifetime
    // than its base class dependencies
}
```

## Performance Implications

### Memory Usage

- **Singleton**: One instance per application (lowest memory usage)
- **Scoped**: One instance per scope (moderate memory usage)
- **Transient**: New instance per resolution (highest memory usage)

### Creation Overhead

- **Singleton**: Created once (lowest creation overhead)
- **Scoped**: Created once per scope (moderate creation overhead)  
- **Transient**: Created every time (highest creation overhead)

### Recommendation Matrix

| Service Type | Singleton | Scoped | Transient |
|--------------|-----------|--------|-----------|
| Configuration Services | ✅ | ❌ | ❌ |
| Database Contexts | ❌ | ✅ | ❌ |
| Repositories | ❌ | ✅ | ❌ |
| Business Logic Services | ❌ | ✅ | ❌ |
| Validation Services | ❌ | ❌ | ✅ |
| Utility Services | ✅ | ❌ | ✅ |
| Background Services | ✅ | ❌ | ❌ |
| HTTP Clients | ✅ | ❌ | ❌ |

## Next Steps

- **[Constructor Generation](constructor-generation.md)** - How constructors are built with different lifetimes
- **[Background Services](background-services.md)** - Specialized patterns for long-running services
- **[Testing](testing.md)** - Testing strategies for different service lifetimes
- **[Performance](performance.md)** - Optimizing service registration and resolution