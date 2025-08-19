# Generic Services

IoCTools provides reliable support for generic services, enabling type-safe repository patterns, service factories, and proven generic dependency injection scenarios. This guide covers tested generic patterns, implementation status, and architectural considerations for successful generic service development.

## Implementation Status

IoCTools has been extensively tested with generic services and supports the majority of real-world generic patterns. However, there are documented [architectural limits](#architectural-limits) for complex edge cases that are intentionally not supported to maintain generator stability.

## Basic Generic Service Registration

### Simple Generic Service

```csharp
[Service(Lifetime.Scoped)]
public partial class Repository<T> : IRepository<T> where T : class
{
    [Inject] private readonly IDbContext _context;
    [Inject] private readonly ILogger<Repository<T>> _logger;
    
    public async Task<T?> GetByIdAsync(int id)
    {
        _logger.LogDebug("Fetching {EntityType} with ID: {Id}", typeof(T).Name, id);
        return await _context.Set<T>().FindAsync(id);
    }
    
    public async Task<int> CreateAsync(T entity)
    {
        _logger.LogInformation("Creating {EntityType}", typeof(T).Name);
        _context.Set<T>().Add(entity);
        return await _context.SaveChangesAsync();
    }
}
```

**Generated Registration:**
```csharp
services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
```

### Generic Service with Multiple Constraints

> **⚠️ Note**: This example shows multiple constraints which generally work but should be tested thoroughly in your specific environment. For guaranteed reliability, prefer simpler constraint patterns.

```csharp
[Service(Lifetime.Scoped)]
public partial class ValidatedRepository<T> : IRepository<T> 
    where T : class, IEntity, new()
{
    [Inject] private readonly IDbContext _context;
    [Inject] private readonly IValidator<T> _validator;
    [Inject] private readonly ILogger<ValidatedRepository<T>> _logger;
    
    public async Task<int> CreateAsync(T entity)
    {
        // Constraint enables direct validation
        var validationResult = await _validator.ValidateAsync(entity);
        if (!validationResult.IsValid)
        {
            throw new ValidationException($"Invalid {typeof(T).Name}");
        }
        
        // Constraint enables parameterless constructor
        var audit = new T { CreatedAt = DateTime.UtcNow };
        
        _context.Set<T>().Add(entity);
        return await _context.SaveChangesAsync();
    }
}
```

## Generic Service Inheritance

### Generic Base Class

```csharp
[Service]
[DependsOn<ILogger<BaseService<T>>, IValidator<T>>]
public partial class BaseService<T> where T : class
{
    protected async Task<ValidationResult> ValidateAsync(T entity)
    {
        _logger.LogDebug("Validating entity of type {Type}", typeof(T).Name);
        return await _validator.ValidateAsync(entity);
    }
    
    protected void LogEntityOperation(T entity, string operation)
    {
        _logger.LogInformation("Entity {Type} {Operation}: {Entity}", 
            typeof(T).Name, operation, entity);
    }
}
```

### Generic Derived Service

```csharp
[Service(Lifetime.Scoped)]
[DependsOn<IRepository<T>, IEmailService>]
public partial class BusinessService<T> : BaseService<T>, IBusinessService<T> 
    where T : class, IBusinessEntity
{
    public async Task<ProcessResult<T>> ProcessEntityAsync(T entity)
    {
        // Use inherited validation
        var validationResult = await ValidateAsync(entity);
        if (!validationResult.IsValid)
        {
            return ProcessResult<T>.Failed(validationResult.Errors);
        }
        
        // Log operation using inherited method
        LogEntityOperation(entity, "Processing");
        
        // Use declared dependencies
        var saved = await _repository.SaveAsync(entity);
        
        // Send business notification
        if (entity.RequiresNotification)
        {
            await _emailService.SendBusinessNotificationAsync(entity.NotificationEmail);
        }
        
        return ProcessResult<T>.Success(saved);
    }
}
```

**Generated Constructor:**
```csharp
public BusinessService(
    ILogger<BaseService<T>> logger,
    IValidator<T> validator,
    IRepository<T> repository,
    IEmailService emailService) 
    : base(logger, validator)
{
    this._repository = repository;
    this._emailService = emailService;
}
```

## Configuration Injection with Generics

> **⚠️ Implementation Note**: Configuration injection with generics works for basic patterns but may hit [architectural limits](#architectural-limits) in complex scenarios. Test thoroughly or use `IOptions<T>` alternatives for complex configurations.

### Type-Specific Configuration

```csharp
[Service(Lifetime.Scoped)]
public partial class CachingService<T> : ICachingService<T> where T : class
{
    [Inject] private readonly IMemoryCache _cache;
    [Inject] private readonly IRepository<T> _repository;
    [Inject] private readonly ILogger<CachingService<T>> _logger;
    
    // Configuration specific to the entity type
    [InjectConfiguration($"Caching:{typeof(T).Name}:ExpirationMinutes", DefaultValue = "30")]
    private readonly int _cacheExpirationMinutes;
    
    [InjectConfiguration($"Caching:{typeof(T).Name}:MaxItems", DefaultValue = "1000")]
    private readonly int _maxCacheItems;
    
    [InjectConfiguration($"Caching:{typeof(T).Name}:Enabled", DefaultValue = "true")]
    private readonly bool _cachingEnabled;
    
    public async Task<T?> GetByIdAsync(string id)
    {
        if (!_cachingEnabled)
        {
            return await _repository.GetByIdAsync(id);
        }
        
        var cacheKey = GetCacheKey(id);
        if (_cache.TryGetValue(cacheKey, out T? cachedItem))
        {
            _logger.LogDebug("Cache hit for {Type}:{Id}", typeof(T).Name, id);
            return cachedItem;
        }
        
        var item = await _repository.GetByIdAsync(id);
        if (item != null)
        {
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cacheExpirationMinutes)
            };
            _cache.Set(cacheKey, item, options);
        }
        
        return item;
    }
    
    private string GetCacheKey(string id) => $"{typeof(T).Name}:{id}";
}
```

**Configuration Example:**
```json
{
  "Caching": {
    "User": {
      "ExpirationMinutes": 60,
      "MaxItems": 5000,
      "Enabled": true
    },
    "Product": {
      "ExpirationMinutes": 120,
      "MaxItems": 10000,
      "Enabled": true
    },
    "Order": {
      "ExpirationMinutes": 15,
      "MaxItems": 2000,
      "Enabled": false
    }
  }
}
```

## Advanced Generic Patterns

> **⚠️ Advanced Pattern Warning**: The following examples demonstrate sophisticated generic patterns that may approach [architectural limits](#architectural-limits). Use simpler patterns where possible and test complex scenarios thoroughly.

### Generic Factory Services

```csharp
[Service(Lifetime.Singleton)]
public partial class GenericServiceFactory : IGenericServiceFactory
{
    [Inject] private readonly IServiceProvider _serviceProvider;
    [Inject] private readonly ILogger<GenericServiceFactory> _logger;
    
    public T CreateService<T>() where T : class
    {
        _logger.LogDebug("Creating service of type {ServiceType}", typeof(T).Name);
        return _serviceProvider.GetRequiredService<T>();
    }
    
    public IRepository<T> CreateRepository<T>() where T : class
    {
        _logger.LogDebug("Creating repository for entity type {EntityType}", typeof(T).Name);
        return _serviceProvider.GetRequiredService<IRepository<T>>();
    }
    
    public IValidator<T> CreateValidator<T>() where T : class
    {
        var validatorType = typeof(IValidator<>).MakeGenericType(typeof(T));
        _logger.LogDebug("Creating validator for type {Type}", typeof(T).Name);
        return (IValidator<T>)_serviceProvider.GetRequiredService(validatorType);
    }
}
```

### Generic Service with Dynamic Type Resolution

```csharp
[Service(Lifetime.Scoped)]
public partial class DynamicEntityProcessor : IDynamicEntityProcessor
{
    [Inject] private readonly IServiceProvider _serviceProvider;
    [Inject] private readonly ILogger<DynamicEntityProcessor> _logger;
    
    public async Task<ProcessResult> ProcessEntityAsync(Type entityType, object entity)
    {
        _logger.LogInformation("Processing entity of type {EntityType}", entityType.Name);
        
        // Create generic repository at runtime
        var repositoryType = typeof(IRepository<>).MakeGenericType(entityType);
        var repository = _serviceProvider.GetRequiredService(repositoryType);
        
        // Create generic validator at runtime  
        var validatorType = typeof(IValidator<>).MakeGenericType(entityType);
        var validator = _serviceProvider.GetService(validatorType);
        
        // Use reflection to call generic methods
        if (validator != null)
        {
            var validateMethod = validatorType.GetMethod("ValidateAsync");
            var validationTask = (Task<ValidationResult>)validateMethod!.Invoke(validator, new[] { entity });
            var validationResult = await validationTask;
            
            if (!validationResult.IsValid)
            {
                return ProcessResult.Failed(validationResult.Errors);
            }
        }
        
        // Save entity
        var saveMethod = repositoryType.GetMethod("SaveAsync");
        var saveTask = (Task<int>)saveMethod!.Invoke(repository, new[] { entity });
        var result = await saveTask;
        
        return ProcessResult.Success($"Processed {entityType.Name} with result: {result}");
    }
}
```

## Generic Collection Services

### Generic Collection Repository

```csharp
[Service(Lifetime.Scoped)]
public partial class CollectionRepository<T> : ICollectionRepository<T> where T : class
{
    [Inject] private readonly IDbContext _context;
    [Inject] private readonly ILogger<CollectionRepository<T>> _logger;
    
    [InjectConfiguration("Repository:DefaultPageSize", DefaultValue = "50")]
    private readonly int _defaultPageSize;
    
    public async Task<PagedResult<T>> GetPagedAsync(int page = 1, int? pageSize = null)
    {
        var actualPageSize = pageSize ?? _defaultPageSize;
        var skip = (page - 1) * actualPageSize;
        
        _logger.LogDebug("Fetching page {Page} of {EntityType} (size: {PageSize})", 
            page, typeof(T).Name, actualPageSize);
        
        var query = _context.Set<T>();
        var total = await query.CountAsync();
        var items = await query.Skip(skip).Take(actualPageSize).ToListAsync();
        
        return new PagedResult<T>
        {
            Items = items,
            Page = page,
            PageSize = actualPageSize,
            TotalCount = total,
            TotalPages = (int)Math.Ceiling(total / (double)actualPageSize)
        };
    }
    
    public async Task<IEnumerable<T>> GetByIdsAsync(IEnumerable<int> ids)
    {
        _logger.LogDebug("Fetching {EntityType} by IDs: {Ids}", 
            typeof(T).Name, string.Join(", ", ids));
        
        return await _context.Set<T>()
            .Where(entity => ids.Contains(EF.Property<int>(entity, "Id")))
            .ToListAsync();
    }
    
    public async Task<BulkResult> CreateManyAsync(IEnumerable<T> entities)
    {
        var entitiesList = entities.ToList();
        _logger.LogInformation("Bulk creating {Count} {EntityType} entities", 
            entitiesList.Count, typeof(T).Name);
        
        _context.Set<T>().AddRange(entitiesList);
        var affectedRows = await _context.SaveChangesAsync();
        
        return new BulkResult
        {
            TotalProcessed = entitiesList.Count,
            SuccessfullyProcessed = affectedRows,
            Errors = new List<string>()
        };
    }
}
```

### Generic Event Handling

```csharp
[Service(Lifetime.Scoped)]
public partial class GenericEventHandler<T> : IEventHandler<T> where T : class, IEvent
{
    [Inject] private readonly ILogger<GenericEventHandler<T>> _logger;
    [Inject] private readonly IEventStore _eventStore;
    [Inject] private readonly IServiceProvider _serviceProvider;
    
    [InjectConfiguration($"Events:{typeof(T).Name}:RetryAttempts", DefaultValue = "3")]
    private readonly int _retryAttempts;
    
    [InjectConfiguration($"Events:{typeof(T).Name}:ProcessingTimeoutSeconds", DefaultValue = "30")]
    private readonly int _processingTimeoutSeconds;
    
    public async Task<EventResult> HandleAsync(T eventInstance)
    {
        _logger.LogInformation("Handling event of type {EventType} with ID {EventId}", 
            typeof(T).Name, eventInstance.Id);
        
        var cancellationToken = new CancellationTokenSource(
            TimeSpan.FromSeconds(_processingTimeoutSeconds)).Token;
        
        for (int attempt = 1; attempt <= _retryAttempts; attempt++)
        {
            try
            {
                // Store event
                await _eventStore.StoreAsync(eventInstance);
                
                // Find and execute specific handlers
                var handlerType = typeof(ISpecificEventHandler<>).MakeGenericType(typeof(T));
                var handlers = _serviceProvider.GetServices(handlerType);
                
                var tasks = handlers.Select(handler => 
                {
                    var handleMethod = handlerType.GetMethod("HandleAsync");
                    return (Task)handleMethod!.Invoke(handler, new object[] { eventInstance, cancellationToken });
                });
                
                await Task.WhenAll(tasks);
                
                _logger.LogInformation("Successfully processed event {EventType}:{EventId}", 
                    typeof(T).Name, eventInstance.Id);
                
                return EventResult.Success($"Processed {typeof(T).Name} event");
            }
            catch (Exception ex) when (attempt < _retryAttempts)
            {
                _logger.LogWarning(ex, "Attempt {Attempt}/{MaxAttempts} failed for event {EventType}:{EventId}", 
                    attempt, _retryAttempts, typeof(T).Name, eventInstance.Id);
                
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt))); // Exponential backoff
            }
        }
        
        _logger.LogError("Failed to process event {EventType}:{EventId} after {Attempts} attempts", 
            typeof(T).Name, eventInstance.Id, _retryAttempts);
        
        return EventResult.Failed($"Failed to process {typeof(T).Name} event after {_retryAttempts} attempts");
    }
}
```

## Complex Generic Scenarios

> **⚠️ Complex Scenario Warning**: These examples show advanced multi-generic patterns that work in many cases but may hit [architectural limits](#architectural-limits) with certain constraint combinations. Consider simpler alternatives for production use.

### Multiple Generic Parameters

```csharp
[Service(Lifetime.Scoped)]
public partial class GenericMapper<TSource, TDestination> : IMapper<TSource, TDestination> 
    where TSource : class 
    where TDestination : class, new()
{
    [Inject] private readonly ILogger<GenericMapper<TSource, TDestination>> _logger;
    [Inject] private readonly IServiceProvider _serviceProvider;
    
    [InjectConfiguration("Mapping:EnableValidation", DefaultValue = "true")]
    private readonly bool _enableValidation;
    
    [InjectConfiguration("Mapping:EnableCaching", DefaultValue = "false")]
    private readonly bool _enableCaching;
    
    public async Task<TDestination> MapAsync(TSource source)
    {
        _logger.LogDebug("Mapping {SourceType} to {DestinationType}", 
            typeof(TSource).Name, typeof(TDestination).Name);
        
        if (_enableValidation)
        {
            var validator = _serviceProvider.GetService<IValidator<TSource>>();
            if (validator != null)
            {
                var validation = await validator.ValidateAsync(source);
                if (!validation.IsValid)
                {
                    throw new ValidationException($"Source {typeof(TSource).Name} validation failed");
                }
            }
        }
        
        var destination = new TDestination();
        
        // Get specific mapper if available
        var specificMapperType = typeof(ISpecificMapper<,>).MakeGenericType(typeof(TSource), typeof(TDestination));
        var specificMapper = _serviceProvider.GetService(specificMapperType);
        
        if (specificMapper != null)
        {
            var mapMethod = specificMapperType.GetMethod("MapAsync");
            destination = await (Task<TDestination>)mapMethod!.Invoke(specificMapper, new object[] { source, destination });
        }
        else
        {
            // Fallback to property mapping
            destination = MapProperties(source, destination);
        }
        
        _logger.LogDebug("Successfully mapped {SourceType} to {DestinationType}", 
            typeof(TSource).Name, typeof(TDestination).Name);
        
        return destination;
    }
    
    private TDestination MapProperties(TSource source, TDestination destination)
    {
        var sourceProps = typeof(TSource).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var destProps = typeof(TDestination).GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.SetProperty);
        
        foreach (var sourceProp in sourceProps)
        {
            var destProp = destProps.FirstOrDefault(p => 
                p.Name == sourceProp.Name && 
                p.PropertyType.IsAssignableFrom(sourceProp.PropertyType));
            
            if (destProp != null)
            {
                var value = sourceProp.GetValue(source);
                destProp.SetValue(destination, value);
            }
        }
        
        return destination;
    }
}
```

### Generic Service with Conditional Registration

```csharp
[Service(Lifetime.Scoped)]
[ConditionalService("Features:GenericCaching:Enabled", "true")]
public partial class GenericCachingService<T> : IGenericCachingService<T> where T : class
{
    [Inject] private readonly IMemoryCache _memoryCache;
    [Inject] private readonly IDistributedCache? _distributedCache;
    [Inject] private readonly ILogger<GenericCachingService<T>> _logger;
    
    [InjectConfiguration($"Caching:Generic:{typeof(T).Name}:Strategy", DefaultValue = "Memory")]
    private readonly string _cacheStrategy;
    
    [InjectConfiguration($"Caching:Generic:{typeof(T).Name}:ExpirationMinutes", DefaultValue = "30")]
    private readonly int _expirationMinutes;
    
    public async Task<T?> GetAsync(string key)
    {
        var fullKey = GetFullKey(key);
        
        if (_cacheStrategy == "Distributed" && _distributedCache != null)
        {
            var distributedValue = await _distributedCache.GetStringAsync(fullKey);
            if (!string.IsNullOrEmpty(distributedValue))
            {
                _logger.LogDebug("Distributed cache hit for {Type}:{Key}", typeof(T).Name, key);
                return JsonSerializer.Deserialize<T>(distributedValue);
            }
        }
        
        if (_memoryCache.TryGetValue(fullKey, out T? cachedValue))
        {
            _logger.LogDebug("Memory cache hit for {Type}:{Key}", typeof(T).Name, key);
            return cachedValue;
        }
        
        _logger.LogDebug("Cache miss for {Type}:{Key}", typeof(T).Name, key);
        return null;
    }
    
    public async Task SetAsync(string key, T value)
    {
        var fullKey = GetFullKey(key);
        var expiration = TimeSpan.FromMinutes(_expirationMinutes);
        
        // Always cache in memory
        _memoryCache.Set(fullKey, value, expiration);
        
        // Cache in distributed cache if configured
        if (_cacheStrategy == "Distributed" && _distributedCache != null)
        {
            var serialized = JsonSerializer.Serialize(value);
            await _distributedCache.SetStringAsync(fullKey, serialized, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            });
        }
        
        _logger.LogDebug("Cached {Type}:{Key} using {Strategy} strategy", 
            typeof(T).Name, key, _cacheStrategy);
    }
    
    private string GetFullKey(string key) => $"Generic:{typeof(T).Name}:{key}";
}
```

## Architectural Limits

### Complex Generic Constraints

While IoCTools supports basic generic constraints reliably, some advanced constraint combinations have architectural limitations:

```csharp
// ✅ WORKS: Basic constraints (reliably supported)
[Service]
public partial class Repository<T> : IRepository<T> where T : class, new()
{
    [Inject] private readonly ILogger<Repository<T>> _logger;
}

// ⚠️ MAY FAIL: Complex constraint combinations
[Service] 
public partial class ConstrainedService<T> where T : struct, IComparable<T>, IEquatable<T>
{
    [Inject] private readonly IProcessor<T> _processor;
}

// ❌ ARCHITECTURAL LIMIT: Unmanaged constraints with complex generics
[Service]
public partial class UnmanagedService<T> where T : unmanaged
{
    [Inject] private readonly IComplexProcessor<T> _processor;
}
```

**Root Cause**: Complex constraint preservation across inheritance hierarchies and code generation requires deep compiler integration that conflicts with source generator architecture.

**Recommended Workarounds**:
1. Use concrete types for complex constraint scenarios
2. Simplify generic patterns where possible
3. Manual constructor implementation for edge cases

### Configuration Injection Limitations

Generic services with configuration injection have some limitations in complex scenarios:

```csharp
// ✅ WORKS: Simple configuration injection with generics
[Service]
public partial class CachingService<T> where T : class
{
    [InjectConfiguration("Cache:DefaultTimeout", DefaultValue = "30")] 
    private readonly int _timeout;
    [Inject] private readonly IRepository<T> _repository;
}

// ⚠️ COMPLEX: Type-specific configuration may require testing
[Service]
public partial class TypeSpecificService<T> where T : class
{
    // This pattern works but should be tested thoroughly
    [InjectConfiguration("Services:Generic:ExpirationMinutes", DefaultValue = "30")]
    private readonly int _expiration;
}

// ❌ ARCHITECTURAL LIMIT: Complex configuration + inheritance + generics
[Service]
public partial class ComplexConfigService<T> : BaseGenericService<T>
{
    [InjectConfiguration] private readonly IEnumerable<ComplexConfig<T>> _configs;
}
```

**Alternative Approach**:
```csharp
// ✅ Reliable alternative using IOptions<T>
[Service]
public partial class ConfigurableService<T> where T : class
{
    [Inject] private readonly IOptions<GenericServiceConfig> _config;
    [Inject] private readonly IRepository<T> _repository;
}
```

## Performance Considerations

### Generic Service Registration

Generic services are registered as open generic types, providing excellent performance:

```csharp
// Generated registration - efficient open generic type
services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

// DI container creates closed generics on-demand
var userRepo = serviceProvider.GetService<IRepository<User>>();  // Repository<User>
var productRepo = serviceProvider.GetService<IRepository<Product>>(); // Repository<Product>
```

### Memory and Performance Impact

- **Memory**: Each closed generic type (e.g., `Repository<User>`) is a separate type in memory
- **Startup**: Minimal impact - open generic registration is very fast
- **Resolution**: Standard DI container performance for closed generic types
- **Recommendation**: Use generic services judiciously - they're efficient but not free

### Best Practices for Performance

```csharp
// ✅ Good: Generic service for common patterns
[Service(Lifetime.Scoped)]
public partial class Repository<T> : IRepository<T> where T : class
{
    // Shared logic across all entity types
}

// ❌ Avoid: Over-generalization where concrete types would suffice
[Service]
public partial class OverGenericService<T, U, V> : IService<T, U, V> 
    where T : class where U : struct where V : IEnumerable<T>
{
    // Too complex for most real-world scenarios
}
```

## Generated Code Analysis

### Constructor Generation for Generic Services

IoCTools generates optimized constructors for generic services:

```csharp
// Source
[Service(Lifetime.Scoped)]
public partial class Repository<T> : IRepository<T> where T : class
{
    [Inject] private readonly IDbContext _context;
    [Inject] private readonly ILogger<Repository<T>> _logger;
    [Inject] private readonly IValidator<T>? _validator;
}

// Generated Constructor
public Repository(IDbContext context, ILogger<Repository<T>> logger, IValidator<T> validator = null)
{
    this._context = context;
    this._logger = logger;
    this._validator = validator;
}
```

### Service Registration Generation

```csharp
// Generated in ServiceRegistrations.g.cs
public static IServiceCollection AddIoCToolsRegisteredServices(
    this IServiceCollection services, 
    IConfiguration configuration)
{
    // Generic services registered with open generic types
    services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
    services.AddScoped(typeof(IValidator<>), typeof(Validator<>));
    services.AddScoped(typeof(ICachingService<>), typeof(CachingService<>));
    
    // Conditional generic services
    if (configuration.GetValue<bool>("Features:GenericCaching:Enabled"))
    {
        services.AddScoped(typeof(IGenericCachingService<>), typeof(GenericCachingService<>));
    }
    
    return services;
}
```

## Best Practices for Generic Service Design

### Reliable Generic Constraint Patterns

```csharp
// ✅ EXCELLENT: Simple, well-tested constraints
[Service]
public partial class EntityRepository<T> : IRepository<T> 
    where T : class, new()
{
    // Basic constraints that work reliably across all scenarios
    [Inject] private readonly ILogger<EntityRepository<T>> _logger;
    [Inject] private readonly IDbContext _context;
}

// ✅ GOOD: Interface constraints for functionality
[Service]
public partial class ValidatedService<T> : IService<T>
    where T : class, IValidatable
{
    // Single interface constraint - reliable pattern
    [Inject] private readonly IValidator<T> _validator;
}

// ⚠️ USE CAUTION: Multiple constraints (test thoroughly)
[Service]
public partial class ComplexService<T> : IService<T>
    where T : class, IEntity, IComparable<T>, new()
{
    // Multiple constraints may work but need thorough testing
    // Consider simplifying or using concrete types
}

// ❌ AVOID: Overly complex constraints (architectural limits)
[Service]
public partial class OverConstrainedService<T> : IService<T>
    where T : struct, IComparable<T>, IEquatable<T>, IFormattable
{
    // Complex value type constraints hit architectural limits
    // Use concrete types or manual implementation instead
}
```

### Generic Service Lifetime Selection

```csharp
// ✅ Repository Pattern: Scoped for data consistency
[Service(Lifetime.Scoped)]
public partial class Repository<T> : IRepository<T> where T : class
{
    // Scoped ensures consistent data context within request
}

// ✅ Factory Pattern: Singleton for performance
[Service(Lifetime.Singleton)]
public partial class Factory<T> : IFactory<T> where T : class, new()
{
    // Singleton for stateless creation logic
}

// ✅ Processing Services: Transient for isolation
[Service(Lifetime.Transient)]
public partial class Processor<TInput, TOutput> : IProcessor<TInput, TOutput>
    where TInput : class where TOutput : class, new()
{
    // Transient for stateless processing with isolation
}
```

### Generic Inheritance Best Practices

```csharp
// ✅ RECOMMENDED: Simple generic inheritance
[Service]
public partial class BaseService<T> where T : class
{
    [Inject] private readonly ILogger<BaseService<T>> _logger;
    [Inject] private readonly IRepository<T> _repository;
}

[Service(Lifetime.Scoped)]
public partial class AdvancedService<T> : BaseService<T> where T : class, new()
{
    [Inject] private readonly IValidator<T> _validator;
    // Inherits logger and repository from base
}

// ⚠️ COMPLEX: Deep inheritance (test thoroughly)
[Service]
public partial class Level1<T> where T : class { }

[Service]
public partial class Level2<T> : Level1<T> where T : class { }

[Service]
public partial class Level3<T> : Level2<T> where T : class, new() { }
// Deep inheritance may work but needs extensive testing
```

### Configuration Organization for Generic Services

```json
{
  "Services": {
    "Generic": {
      "Repository": {
        "DefaultPageSize": 50,
        "EnableCaching": true,
        "CacheExpirationMinutes": 30
      },
      "Validator": {
        "EnableStrictValidation": false,
        "ValidationTimeout": 5000
      }
    },
    "Processing": {
      "BatchSize": 100,
      "EnableCaching": true,
      "TimeoutSeconds": 30
    }
  }
}
```

**Configuration Injection Patterns**:
```csharp
// ✅ RELIABLE: Simple configuration injection
[Service]
public partial class ConfigurableService<T> where T : class
{
    [InjectConfiguration("Services:Generic:Repository:DefaultPageSize", DefaultValue = "50")]
    private readonly int _pageSize;
    
    [InjectConfiguration("Services:Generic:Repository:EnableCaching", DefaultValue = "true")]
    private readonly bool _enableCaching;
}

// ✅ ALTERNATIVE: Use IOptions<T> for complex scenarios  
[Service]
public partial class OptionsBasedService<T> where T : class
{
    [Inject] private readonly IOptions<GenericServiceOptions> _options;
    // More reliable for complex configuration scenarios
}
```

## Troubleshooting Generic Services

### Common Issues and Solutions

**1. Constructor Generation Failures**
```csharp
// ❌ Problem: Complex field access modifiers
[Service]
public partial class ProblematicService<T> where T : class
{
    [Inject] protected internal readonly IDependency<T> _dep;
}

// ✅ Solution: Use standard access patterns
[Service]
public partial class WorkingService<T> where T : class
{
    [Inject] private readonly IDependency<T> _dep;
}

// ✅ Alternative: Use DependsOn attribute
[Service]
[DependsOn<IDependency<T>>]
public partial class AlternativeService<T> where T : class
{
    // Constructor auto-generated without field complexity
}
```

**2. Generic Constraint Issues**
```csharp
// ❌ Problem: Complex constraint combinations
[Service]
public partial class FailingService<T> where T : struct, IComparable<T>, IEquatable<T>
{
    [Inject] private readonly IProcessor<T> _processor;
}

// ✅ Solution: Simplify constraints or use concrete types
[Service]
public partial class WorkingService<T> where T : class
{
    [Inject] private readonly IProcessor<T> _processor;
}

// ✅ Alternative: Use specific concrete implementations
[Service]
public partial class ConcreteIntService
{
    [Inject] private readonly IProcessor<int> _processor;
}
```

**3. Registration Issues**
```csharp
// Check that both interface and implementation are generic
services.AddScoped(typeof(IRepository<>), typeof(Repository<>)); // ✅ Correct
services.AddScoped(typeof(IRepository<User>), typeof(Repository<>)); // ❌ Mismatch
```

### Performance Diagnostics

```csharp
// Monitor generic service creation
[Service]
public partial class DiagnosticService<T> where T : class
{
    [Inject] private readonly ILogger<DiagnosticService<T>> _logger;
    
    // Constructor will log type information
    public DiagnosticService(ILogger<DiagnosticService<T>> logger)
    {
        _logger = logger;
        _logger.LogDebug("Created DiagnosticService for type {TypeName}", typeof(T).Name);
    }
}
```

## Enhanced Sample Coverage

The IoCTools.Sample project includes comprehensive generic service examples demonstrating proven patterns:

### Working Generic Patterns (Sample App Examples)

**1. Basic Generic Repository** (`GenericRepository<T>`)
```csharp
[Service(Lifetime.Scoped)]
public partial class GenericRepository<T> : IRepository<T> where T : class, new()
{
    [Inject] private readonly ILogger<GenericRepository<T>> _logger;
    [Inject] private readonly IMemoryCache _cache;
    // Full implementation with CRUD operations
}
```

**2. Generic Inheritance Chain** (`BaseBusinessService<T>` → `AdvancedBusinessService<T>`)
```csharp
[Service]
public partial class BaseBusinessService<T> where T : class
{
    [Inject] private readonly ILogger<BaseBusinessService<T>> _logger;
    [Inject] private readonly IRepository<T> _repository;
}

[Service(Lifetime.Scoped)]
public partial class AdvancedBusinessService<T> : BaseBusinessService<T> where T : class, new()
{
    [Inject] private readonly IGenericValidator<T> _validator;
    [Inject] private readonly ICache<T> _cache;
}
```

**3. Multi-Parameter Generics** (`DataProcessor<TInput, TOutput>`)
```csharp
[Service(Lifetime.Transient)]
public partial class DataProcessor<TInput, TOutput> : IProcessor<TInput, TOutput> 
    where TInput : class where TOutput : class, new()
{
    [Inject] private readonly ILogger<DataProcessor<TInput, TOutput>> _logger;
}
```

**4. Enhanced Generic Processing** (`EnhancedGenericProcessor<T>`)
```csharp
[Service(Lifetime.Scoped)]
public partial class EnhancedGenericProcessor<T> where T : class, new()
{
    [Inject] private readonly ILogger<EnhancedGenericProcessor<T>> _logger;
    [InjectConfiguration("Processing:BatchSize", DefaultValue = "100")]
    private readonly int _batchSize;
    // Includes batch processing with configuration injection
}
```

### Demonstration Service

The sample includes `GenericServiceDemonstrator` that exercises all generic patterns:
- Repository operations with `IRepository<User>`
- Generic validation with `IGenericValidator<User>`
- Multi-generic processing with `IProcessor<User, User>`
- Generic caching with `ICache<User>`
- Factory patterns with `IFactory<User>`
- Inheritance chains with `AdvancedBusinessService<User>`
- Complex processing with `EnhancedGenericProcessor<User>`

## Generated Code Analysis

### Constructor Generation for Generic Services

IoCTools generates optimized constructors for generic services:

```csharp
// Source
[Service(Lifetime.Scoped)]
public partial class Repository<T> : IRepository<T> where T : class
{
    [Inject] private readonly IDbContext _context;
    [Inject] private readonly ILogger<Repository<T>> _logger;
    [Inject] private readonly IValidator<T>? _validator;
}

// Generated Constructor
public Repository(IDbContext context, ILogger<Repository<T>> logger, IValidator<T> validator = null)
{
    this._context = context;
    this._logger = logger;
    this._validator = validator;
}
```

### Service Registration Generation

```csharp
// Generated in ServiceRegistrations.g.cs
public static IServiceCollection AddIoCToolsRegisteredServices(
    this IServiceCollection services, 
    IConfiguration configuration)
{
    // Generic services registered with open generic types
    services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
    services.AddScoped(typeof(IValidator<>), typeof(Validator<>));
    services.AddScoped(typeof(ICachingService<>), typeof(CachingService<>));
    
    // Conditional generic services
    if (configuration.GetValue<bool>("Features:GenericCaching:Enabled"))
    {
        services.AddScoped(typeof(IGenericCachingService<>), typeof(GenericCachingService<>));
    }
    
    return services;
}
```

## Documentation vs. Implementation Reality

**Important**: Some examples in this documentation demonstrate complex generic scenarios that may hit architectural limits. These examples serve educational purposes and show the theoretical capabilities of generic dependency injection patterns.

**For Production Use**:
- Start with the [Enhanced Sample Coverage](#enhanced-sample-coverage) examples
- Test thoroughly when implementing complex constraint combinations
- Refer to [Architectural Limits](#architectural-limits) for known boundaries
- Use the [Troubleshooting](#troubleshooting-generic-services) section when issues arise

**Example Classification**:
- **Basic Generic Service Registration** ✅ Fully supported and tested
- **Generic Service with Multiple Constraints** ⚠️ May work, needs testing
- **Configuration Injection with Generics** ⚠️ Simple patterns work, complex may fail
- **Multiple Generic Parameters** ✅ Well supported pattern
- **Complex Generic Scenarios** ⚠️ Educational - test in your environment

## Recommendation Summary

### ✅ Reliable Patterns (Use Confidently)
- Basic generic services with `class` and `new()` constraints
- Generic repositories, validators, factories, and processors
- Simple generic inheritance (2-3 levels)
- Basic configuration injection with default values
- Standard lifetime management (Scoped, Singleton, Transient)

### ⚠️ Use with Testing (May Work)
- Complex generic constraints (multiple interfaces, comparability)
- Deep inheritance hierarchies (4+ levels)
- Type-specific configuration patterns
- Advanced constraint combinations

### ❌ Architectural Limits (Use Alternatives)
- Unmanaged and complex value type constraints
- Complex configuration + inheritance + generics combinations
- Advanced field access modifier patterns
- Extreme constraint combinations

### Migration Path for Limited Scenarios
1. **Simplify constraints** where possible
2. **Use concrete types** for complex constraint scenarios
3. **Manual constructors** for edge cases
4. **IOptions<T> patterns** for complex configuration

## Next Steps

- **[Service Declaration](service-declaration.md)** - Basic service registration patterns
- **[Dependency Injection](dependency-injection.md)** - Dependency management strategies
- **[Configuration Injection](configuration-injection.md)** - Configuration patterns and alternatives
- **[Inheritance](inheritance.md)** - Inheritance hierarchies and best practices
- **[Architectural Limits](../ARCHITECTURAL_LIMITS.md)** - Detailed architectural constraints
- **[Sample Applications](../IoCTools.Sample/)** - Working examples of all patterns