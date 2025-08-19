# Multi-Interface Registration

IoCTools provides sophisticated multi-interface registration through the `[RegisterAsAll]` attribute, allowing services to be registered for multiple interfaces with fine-grained control over instance sharing and registration modes.

## Basic Multi-Interface Registration

### Register for All Implemented Interfaces

```csharp
[Service]
[RegisterAsAll]
public partial class NotificationService : IEmailNotifier, ISmsNotifier, IPushNotifier
{
    [Inject] private readonly ILogger<NotificationService> _logger;
    
    public async Task SendEmailAsync(string to, string subject, string body)
    {
        _logger.LogInformation("Sending email to {To}", to);
        // Email implementation
    }
    
    public async Task SendSmsAsync(string to, string message)
    {
        _logger.LogInformation("Sending SMS to {To}", to);
        // SMS implementation
    }
    
    public async Task SendPushAsync(string deviceId, string message)
    {
        _logger.LogInformation("Sending push to {DeviceId}", deviceId);
        // Push implementation
    }
}
```

**Generated Registration:**
```csharp
// Single instance registered for all interfaces
services.AddScoped<global::Test.NotificationService, global::Test.NotificationService>();
services.AddScoped<global::Test.IEmailNotifier, global::Test.NotificationService>();
services.AddScoped<global::Test.ISmsNotifier, global::Test.NotificationService>();
services.AddScoped<global::Test.IPushNotifier, global::Test.NotificationService>();
```

### Consuming Multiple Interfaces

```csharp
[Service]
public partial class CommunicationService : ICommunicationService
{
    [Inject] private readonly IEmailNotifier _emailNotifier;
    [Inject] private readonly ISmsNotifier _smsNotifier;
    [Inject] private readonly IPushNotifier _pushNotifier;
    
    public async Task NotifyUserAsync(User user, string message)
    {
        // All three services are the SAME instance of NotificationService
        await _emailNotifier.SendEmailAsync(user.Email, "Notification", message);
        await _smsNotifier.SendSmsAsync(user.Phone, message);
        await _pushNotifier.SendPushAsync(user.DeviceId, message);
    }
}
```

## Registration Modes

### RegistrationMode.All (Default)

```csharp
[Service]
[RegisterAsAll(RegistrationMode.All)]
public partial class AllInterfacesService : IServiceA, IServiceB, IServiceC
{
    // Registered for all interfaces: IServiceA, IServiceB, IServiceC
}
```

### RegistrationMode.Exclusionary

```csharp
[Service]
[RegisterAsAll(RegistrationMode.Exclusionary)]
public partial class ExclusionaryService : IServiceA, IServiceB
{
    // Registered ONLY for: IServiceA, IServiceB
    // NOT registered as: ExclusionaryService (concrete type)
}

// This will fail - concrete type not registered
[Service]
public partial class ConsumerService
{
    [Inject] private readonly ExclusionaryService _service; // ❌ Not available
    [Inject] private readonly IServiceA _serviceA;          // ✅ Available
}
```

### RegistrationMode.DirectOnly

```csharp
[Service]
[RegisterAsAll(RegistrationMode.DirectOnly)]
public partial class DirectOnlyService : IServiceA, IServiceB
{
    // Registered ONLY as: DirectOnlyService
    // NOT registered for: IServiceA, IServiceB
}

// Usage pattern
[Service]
public partial class ConsumerService
{
    [Inject] private readonly DirectOnlyService _service; // ✅ Available
    [Inject] private readonly IServiceA _serviceA;        // ❌ Not available
}
```

### Registration Mode Defaults

The default `InstanceSharing` behavior varies by `RegistrationMode`:

- **`RegistrationMode.All`**: Defaults to `InstanceSharing.Separate` - each interface gets its own service instance
- **`RegistrationMode.Exclusionary`**: Defaults to `InstanceSharing.Shared` - all interfaces share the same service instance  
- **`RegistrationMode.DirectOnly`**: Only registers the concrete type, no interface sharing

## Instance Sharing Control

### InstanceSharing.Separate (Default for RegistrationMode.All)

```csharp
[Service]
[RegisterAsAll(InstanceSharing.Separate)]
public partial class SeparateInstanceService : IServiceA, IServiceB, IServiceC
{
    private int _callCount = 0;
    
    public void IncrementCounter() => _callCount++;
    public int GetCounter() => _callCount;
}
```

**Generated Registration:**
```csharp
// Separate instances - direct registration for each interface
services.AddScoped<global::Test.SeparateInstanceService, global::Test.SeparateInstanceService>();
services.AddScoped<global::Test.IServiceA, global::Test.SeparateInstanceService>();
services.AddScoped<global::Test.IServiceB, global::Test.SeparateInstanceService>();
services.AddScoped<global::Test.IServiceC, global::Test.SeparateInstanceService>();
```

**Usage:**
```csharp
[Service]
public partial class ConsumerService
{
    [Inject] private readonly IServiceA _serviceA;
    [Inject] private readonly IServiceB _serviceB;
    
    public void TestSeparation()
    {
        ((SeparateInstanceService)_serviceA).IncrementCounter();
        ((SeparateInstanceService)_serviceB).IncrementCounter();
        
        // Different instances, independent state
        var countA = ((SeparateInstanceService)_serviceA).GetCounter(); // countA = 1
        var countB = ((SeparateInstanceService)_serviceB).GetCounter(); // countB = 1
    }
}
```

### InstanceSharing.Shared (Default for RegistrationMode.Exclusionary)

```csharp
[Service]
[RegisterAsAll(InstanceSharing.Shared)]
public partial class SharedInstanceService : IServiceA, IServiceB
{
    private int _callCount = 0;
    
    public void IncrementCounter() => _callCount++;
    public int GetCounter() => _callCount;
}
```

**Generated Registration:**
```csharp
// Single instance shared across all interfaces
services.AddScoped<global::Test.SharedInstanceService, global::Test.SharedInstanceService>();
services.AddScoped<global::Test.IServiceA>(provider => provider.GetRequiredService<global::Test.SharedInstanceService>());
services.AddScoped<global::Test.IServiceB>(provider => provider.GetRequiredService<global::Test.SharedInstanceService>());
```

**Usage Result:**
```csharp
[Service]
public partial class ConsumerService
{
    [Inject] private readonly IServiceA _serviceA;
    [Inject] private readonly IServiceB _serviceB;
    
    public void TestSharing()
    {
        ((SharedInstanceService)_serviceA).IncrementCounter();
        ((SharedInstanceService)_serviceB).IncrementCounter();
        
        // Both calls affect the SAME instance
        var count = ((SharedInstanceService)_serviceA).GetCounter(); // count = 2
    }
}
```

## Advanced Registration Patterns

### Selective Interface Registration

```csharp
[Service]
[RegisterAsAll]
[SkipRegistration<IInternalService>] // Don't register for this interface
public partial class SelectiveService : IPublicService, IInternalService, IAuditService
{
    // Registered for: IPublicService, IAuditService
    // NOT registered for: IInternalService
    
    public void PublicMethod() { }
    public void InternalMethod() { } // Available but not injectable
    public void AuditMethod() { }
}
```

### Complex Multi-Service Architecture

```csharp
// Base interfaces
public interface IDataReader { Task<T> ReadAsync<T>(string key); }
public interface IDataWriter { Task WriteAsync<T>(string key, T value); }
public interface IDataValidator { Task<bool> ValidateAsync<T>(T value); }
public interface ICacheManager { Task ClearAsync(); }

// Unified data service
[Service]
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class UnifiedDataService : IDataReader, IDataWriter, IDataValidator, ICacheManager
{
    [Inject] private readonly IDbContext _dbContext;
    [Inject] private readonly IMemoryCache _cache;
    [Inject] private readonly ILogger<UnifiedDataService> _logger;
    
    // Shared state across all interfaces
    private readonly Dictionary<string, DateTime> _accessLog = new();
    
    public async Task<T> ReadAsync<T>(string key)
    {
        _accessLog[key] = DateTime.Now; // Shared state
        _logger.LogInformation("Reading {Key}", key);
        
        // Check cache first, then database
        var cached = _cache.Get<T>(key);
        if (cached != null) return cached;
        
        var value = await _dbContext.Set<T>().FindAsync(key);
        _cache.Set(key, value);
        return value;
    }
    
    public async Task WriteAsync<T>(string key, T value)
    {
        _accessLog[key] = DateTime.Now; // Same shared state
        _logger.LogInformation("Writing {Key}", key);
        
        await _dbContext.Set<T>().AddAsync(value);
        await _dbContext.SaveChangesAsync();
        _cache.Set(key, value);
    }
    
    public Task<bool> ValidateAsync<T>(T value)
    {
        // Validation logic with access to shared state
        return Task.FromResult(value != null);
    }
    
    public Task ClearAsync()
    {
        _accessLog.Clear(); // Clear shared state
        _cache.Clear();
        return Task.CompletedTask;
    }
}

// Different consumers using different interfaces of the same service
[Service]
public partial class ReaderService : IReaderService
{
    [Inject] private readonly IDataReader _reader; // Same instance
    [Inject] private readonly ICacheManager _cache; // Same instance
    
    public async Task<T> GetAsync<T>(string key)
    {
        var data = await _reader.ReadAsync<T>(key);
        await _cache.ClearAsync(); // Affects the same UnifiedDataService instance
        return data;
    }
}

[Service]
public partial class WriterService : IWriterService  
{
    [Inject] private readonly IDataWriter _writer;    // Same instance
    [Inject] private readonly IDataValidator _validator; // Same instance
    
    public async Task SaveAsync<T>(string key, T value)
    {
        if (await _validator.ValidateAsync(value))
        {
            await _writer.WriteAsync(key, value);
        }
    }
}
```

## Inheritance and Multi-Interface Registration

### Base Class with Interfaces

```csharp
[Service]
[RegisterAsAll]
public abstract partial class BaseNotificationService : INotificationService, ILoggable
{
    [Inject] protected readonly ILogger<BaseNotificationService> Logger;
    
    public abstract Task NotifyAsync(string message);
    
    public void LogActivity(string activity)
    {
        Logger.LogInformation("Activity: {Activity}", activity);
    }
}

[Service]
[RegisterAsAll] // Inherits base registration behavior
public partial class EmailNotificationService : BaseNotificationService, IEmailSpecific
{
    [InjectConfiguration("Email:SmtpHost")]
    private readonly string _smtpHost;
    
    public override async Task NotifyAsync(string message)
    {
        await SendEmailAsync("admin@example.com", "Notification", message);
    }
    
    public async Task SendEmailAsync(string to, string subject, string body)
    {
        LogActivity($"Sending email to {to}");
        // Email implementation using _smtpHost
    }
}
```

**Generated Registration:**
```csharp
// EmailNotificationService registered for all interfaces:
services.AddScoped<global::Test.EmailNotificationService, global::Test.EmailNotificationService>();
services.AddScoped<global::Test.INotificationService>(provider => provider.GetRequiredService<global::Test.EmailNotificationService>());
services.AddScoped<global::Test.ILoggable>(provider => provider.GetRequiredService<global::Test.EmailNotificationService>());
services.AddScoped<global::Test.IEmailSpecific>(provider => provider.GetRequiredService<global::Test.EmailNotificationService>());
```

## Validation and Error Handling

### RegisterAsAll Without Service (IOC004)

```csharp
[RegisterAsAll] // ❌ IOC004: Missing [Service] attribute
public partial class InvalidService : IServiceA, IServiceB { }

// ✅ Correct
[Service]
[RegisterAsAll]
public partial class ValidService : IServiceA, IServiceB { }
```

### SkipRegistration Without RegisterAsAll (IOC005)

```csharp
[Service]
[SkipRegistration<IServiceA>] // ❌ IOC005: Missing [RegisterAsAll]
public partial class InvalidSkipService : IServiceA, IServiceB { }

// ✅ Correct  
[Service]
[RegisterAsAll]
[SkipRegistration<IServiceA>]
public partial class ValidSkipService : IServiceA, IServiceB { }
```

### Invalid SkipRegistration Interface (IOC009)

```csharp
[Service]
[RegisterAsAll]
[SkipRegistration<INonExistentInterface>] // ℹ️ IOC009: Interface not implemented
public partial class SkipNonExistentService : IServiceA { }

// ✅ Correct
[Service]
[RegisterAsAll]
[SkipRegistration<IServiceA>] // Interface actually implemented
public partial class ValidSkipService : IServiceA, IServiceB { }
```

## Real-World Scenarios

### Repository Pattern with Multiple Interfaces

```csharp
public interface IReadRepository<T> { Task<T> GetByIdAsync(int id); }
public interface IWriteRepository<T> { Task<int> CreateAsync(T entity); }
public interface ICacheableRepository<T> { Task InvalidateCacheAsync(int id); }

[Service]
[RegisterAsAll(InstanceSharing.Shared)]
public partial class UserRepository : IReadRepository<User>, IWriteRepository<User>, ICacheableRepository<User>
{
    [Inject] private readonly IDbContext _context;
    [Inject] private readonly IMemoryCache _cache;
    [Inject] private readonly ILogger<UserRepository> _logger;
    
    // Shared cache state across all interfaces
    private readonly string CACHE_PREFIX = "user:";
    
    public async Task<User> GetByIdAsync(int id)
    {
        var cacheKey = $"{CACHE_PREFIX}{id}";
        var cached = _cache.Get<User>(cacheKey);
        if (cached != null) return cached;
        
        var user = await _context.Users.FindAsync(id);
        _cache.Set(cacheKey, user, TimeSpan.FromMinutes(5));
        return user;
    }
    
    public async Task<int> CreateAsync(User entity)
    {
        _context.Users.Add(entity);
        await _context.SaveChangesAsync();
        
        // Cache the new entity immediately
        _cache.Set($"{CACHE_PREFIX}{entity.Id}", entity, TimeSpan.FromMinutes(5));
        return entity.Id;
    }
    
    public Task InvalidateCacheAsync(int id)
    {
        _cache.Remove($"{CACHE_PREFIX}{id}");
        return Task.CompletedTask;
    }
}

// Different services using different aspects
[Service]
public partial class UserQueryService : IUserQueryService
{
    [Inject] private readonly IReadRepository<User> _readRepo;    // Same instance
    [Inject] private readonly ICacheableRepository<User> _cache; // Same instance
    
    public async Task<User> GetUserAsync(int id)
    {
        var user = await _readRepo.GetByIdAsync(id);
        if (user == null)
        {
            await _cache.InvalidateCacheAsync(id); // Clear stale cache
        }
        return user;
    }
}

[Service]
public partial class UserCommandService : IUserCommandService
{
    [Inject] private readonly IWriteRepository<User> _writeRepo; // Same instance
    [Inject] private readonly ICacheableRepository<User> _cache; // Same instance
    
    public async Task<int> CreateUserAsync(User user)
    {
        var id = await _writeRepo.CreateAsync(user);
        // Cache is automatically updated by the same instance
        return id;
    }
}
```

### Event Handling with Multiple Interfaces

```csharp
public interface IOrderEventHandler { Task HandleOrderCreatedAsync(OrderCreatedEvent evt); }
public interface IPaymentEventHandler { Task HandlePaymentProcessedAsync(PaymentProcessedEvent evt); }
public interface INotificationEventHandler { Task HandleNotificationSentAsync(NotificationSentEvent evt); }

[Service]
[RegisterAsAll(InstanceSharing.Shared)]
public partial class UnifiedEventHandler : IOrderEventHandler, IPaymentEventHandler, INotificationEventHandler
{
    [Inject] private readonly ILogger<UnifiedEventHandler> _logger;
    [Inject] private readonly IEventStore _eventStore;
    
    // Shared correlation tracking across all event types
    private readonly Dictionary<string, List<string>> _correlationMap = new();
    
    public async Task HandleOrderCreatedAsync(OrderCreatedEvent evt)
    {
        _logger.LogInformation("Order created: {OrderId}", evt.OrderId);
        await _eventStore.SaveAsync(evt);
        
        // Track correlation
        _correlationMap[evt.CorrelationId] = new List<string> { "OrderCreated" };
    }
    
    public async Task HandlePaymentProcessedAsync(PaymentProcessedEvent evt)
    {
        _logger.LogInformation("Payment processed: {PaymentId}", evt.PaymentId);
        await _eventStore.SaveAsync(evt);
        
        // Add to existing correlation
        if (_correlationMap.TryGetValue(evt.CorrelationId, out var events))
        {
            events.Add("PaymentProcessed");
        }
    }
    
    public async Task HandleNotificationSentAsync(NotificationSentEvent evt)
    {
        _logger.LogInformation("Notification sent: {NotificationId}", evt.NotificationId);
        await _eventStore.SaveAsync(evt);
        
        // Complete correlation tracking
        if (_correlationMap.TryGetValue(evt.CorrelationId, out var events))
        {
            events.Add("NotificationSent");
            _logger.LogInformation("Workflow completed: {Events}", string.Join(" -> ", events));
            _correlationMap.Remove(evt.CorrelationId);
        }
    }
}

// Event publishers inject specific handler interfaces
[Service]
public partial class OrderService : IOrderService
{
    [Inject] private readonly IOrderEventHandler _eventHandler; // Same instance handles all events
    
    public async Task CreateOrderAsync(Order order)
    {
        // Create order logic...
        await _eventHandler.HandleOrderCreatedAsync(new OrderCreatedEvent(order.Id, order.CorrelationId));
    }
}
```

## Performance Considerations

### Shared vs. Separate Instance Performance

```csharp
// ✅ Good for stateful services - shared instance reduces memory usage
[Service]
[RegisterAsAll(InstanceSharing.Shared)]
public partial class CacheService : IReadCache, IWriteCache, ICacheStatistics
{
    private readonly ConcurrentDictionary<string, object> _cache = new();
    
    // All interfaces share the same cache dictionary
    public T Get<T>(string key) => (T)_cache[key];
    public void Set<T>(string key, T value) => _cache[key] = value;
    public int Count => _cache.Count; // Same state across all interfaces
}

// ✅ Good for stateless services - separate instances for isolation
[Service]
[RegisterAsAll(InstanceSharing.Separate)]
public partial class ValidationService : IEmailValidator, IPhoneValidator, IAddressValidator
{
    // Each interface gets its own instance for thread safety
    public bool ValidateEmail(string email) => /* stateless validation */;
    public bool ValidatePhone(string phone) => /* stateless validation */;
    public bool ValidateAddress(Address address) => /* stateless validation */;
}
```

### Registration Mode Selection

```csharp
// ✅ Use Exclusionary when concrete type should not be injected directly
[Service]
[RegisterAsAll(RegistrationMode.Exclusionary)]
public partial class InternalService : IPublicApi, IInternalApi
{
    // Only IPublicApi and IInternalApi available for injection
    // InternalService concrete type not available (prevents misuse)
    // Default: InstanceSharing.Shared for Exclusionary mode
}

// ✅ Use All when consumers need both interface and concrete access  
[Service]
[RegisterAsAll(RegistrationMode.All)]
public partial class FlexibleService : IServiceInterface
{
    // Both IServiceInterface and FlexibleService available
    // Useful for scenarios requiring concrete type access
}
```

## Next Steps

- **[Background Services](background-services.md)** - Multi-interface background services
- **[Constructor Generation](constructor-generation.md)** - How constructors work with multiple interfaces
- **[Inheritance](inheritance.md)** - Complex inheritance scenarios with multi-interface registration
- **[Testing](testing.md)** - Testing strategies for multi-interface services