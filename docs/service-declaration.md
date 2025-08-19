# Service Declaration

The `[Service]` attribute is the foundation of IoCTools, marking classes for automatic dependency injection registration. This guide covers all aspects of service declaration, lifetime management, and registration patterns.

## Basic Service Declaration

### Simple Service Registration

```csharp
[Service]
public partial class EmailService : IEmailService
{
    public async Task SendAsync(string to, string subject, string body)
    {
        // Implementation
    }
}
```

**Generated Registration:**
```csharp
services.AddScoped<EmailService, EmailService>();
services.AddScoped<IEmailService, EmailService>();
```

### Service Without Interface

```csharp
[Service]
public partial class BackgroundTaskProcessor
{
    public void ProcessTasks()
    {
        // Implementation
    }
}
```

**Generated Registration:**
```csharp
services.AddScoped<BackgroundTaskProcessor>();
```

## Lifetime Management

### Lifetime Options

```csharp
// Scoped (default)
[Service(Lifetime.Scoped)]
public partial class OrderService : IOrderService { }

// Singleton
[Service(Lifetime.Singleton)]
public partial class CacheService : ICacheService { }

// Transient
[Service(Lifetime.Transient)]
public partial class LoggerFactory : ILoggerFactory { }
```

### Lifetime Best Practices

**Singleton Services:**
- Application-wide shared state
- Expensive initialization
- Thread-safe implementations

```csharp
[Service(Lifetime.Singleton)]
public partial class ConfigurationCache : IConfigurationCache
{
    private readonly ConcurrentDictionary<string, object> _cache = new();
    
    public T GetValue<T>(string key) => (T)_cache[key];
}
```

**Scoped Services:**
- Per-request state in web applications
- Database contexts
- User-specific operations

```csharp
[Service(Lifetime.Scoped)]
public partial class UserContext : IUserContext
{
    [Inject] private readonly IHttpContextAccessor _httpContext;
    
    public string UserId => _httpContext.HttpContext.User.FindFirst("sub")?.Value;
}
```

**Transient Services:**
- Stateless operations
- Short-lived instances
- Lightweight services

```csharp
[Service(Lifetime.Transient)]
public partial class EmailValidator : IEmailValidator
{
    public bool IsValid(string email) => email.Contains("@");
}
```

## Registration Patterns

### Multiple Interface Implementation

```csharp
// Registers concrete class and ALL interfaces (not just the first)
[Service]
public partial class OrderService : IOrderService, IOrderValidator
{
    public Task ProcessAsync(Order order) => Task.CompletedTask;
    public bool IsValid(Order order) => true;
}
```

**Generated Registration:**
```csharp
services.AddScoped<OrderService, OrderService>();
services.AddScoped<IOrderService, OrderService>();
services.AddScoped<IOrderValidator, OrderService>();
```

**Note:** IoCTools registers the concrete class plus ALL implemented interfaces, not just the first interface. This enables dependency injection for any of the interfaces the service implements.

### Concrete Class Registration

```csharp
// Registers as concrete type when no interfaces
[Service]
public partial class BackgroundProcessor
{
    public void Process() { }
}
```

**Generated Registration:**
```csharp
services.AddScoped<BackgroundProcessor>();
```

## Class Requirements

### Partial Class Requirement

```csharp
// ✅ Correct - partial class enables constructor generation
[Service]
public partial class CorrectService : IService { }

// ❌ Error - non-partial class cannot have constructor generated
[Service]
public class IncorrectService : IService { } // IOC011 error
```

### Accessibility Modifiers

```csharp
// All accessibility levels supported
[Service] public partial class PublicService : IService { }
[Service] internal partial class InternalService : IService { }
[Service] public partial class NestedService : IService { }
```

### Generic Services

```csharp
// Generic service registration
[Service]
public partial class Repository<T> : IRepository<T> where T : class
{
    [Inject] private readonly IDbContext _context;
    
    public Task<T> GetByIdAsync(int id) => _context.Set<T>().FindAsync(id);
}
```

**Generated Registration:**
```csharp
services.AddScoped(typeof(Repository<>), typeof(Repository<>));
services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
```

## Special Attributes

### External Services

```csharp
// Mark as external when dependencies come from outside
[ExternalService]
public partial class ApiClientService : IApiClientService
{
    // Constructor still generated, but no automatic registration
    // All dependency validation is skipped
}
```

### Unregistered Services

```csharp
// Exclude from automatic registration but generate constructor
[UnregisteredService]
public partial class ManualService : IManualService
{
    // Constructor generated for [Inject] fields
    // Must be manually registered in Program.cs
    // Dependency validation still applies
}
```

## Advanced Patterns

### Base Classes and Inheritance

**Note**: Abstract classes are automatically skipped during service registration since they cannot be instantiated. Use concrete base classes for inheritance patterns.

```csharp
// ❌ This won't work - abstract classes are not registered
[Service]
public abstract partial class BaseRepository<T> : IRepository<T> where T : class
{
    [Inject] protected readonly IDbContext Context;
}

// ✅ Use concrete base classes instead
[Service]
public partial class BaseRepository<T> : IRepository<T> where T : class
{
    [Inject] protected readonly IDbContext Context;
    
    public virtual async Task<T> GetByIdAsync(int id)
    {
        return await Context.Set<T>().FindAsync(id);
    }
}

// Concrete implementation inherits registration
[Service]
public partial class UserRepository : BaseRepository<User>, IUserRepository
{
    public async Task<User> GetByEmailAsync(string email)
    {
        return await Context.Set<User>().FirstOrDefaultAsync(u => u.Email == email);
    }
}
```

### Factory Pattern Integration

```csharp
[Service]
public partial class ServiceFactory : IServiceFactory
{
    [Inject] private readonly IServiceProvider _provider;
    
    public T Create<T>() => _provider.GetRequiredService<T>();
}

[Service]
public partial class ProcessorService : IProcessorService
{
    [Inject] private readonly IServiceFactory _factory;
    
    public void ProcessWithDynamicService(string type)
    {
        var processor = _factory.Create<ISpecializedProcessor>();
        processor.Process();
    }
}
```

## Compilation-Time Validation

### Missing Implementation (IOC001)
```csharp
[Service]
public partial class OrderService
{
    [Inject] private readonly IMissingService _missing; // ⚠️ Warning: No implementation found
}
```

### Unregistered Implementation (IOC002)
```csharp
public class PaymentService : IPaymentService { } // ⚠️ Warning: Missing [Service] attribute

[Service]
public partial class OrderService
{
    [Inject] private readonly IPaymentService _payment;
}
```

### Circular Dependencies (IOC003)
```csharp
[Service] public partial class ServiceA { [Inject] private readonly ServiceB _b; }
[Service] public partial class ServiceB { [Inject] private readonly ServiceA _a; } // ⚠️ Warning: Circular dependency
```

## Best Practices

### Service Design

1. **Single Responsibility**: Each service should have one clear purpose
2. **Interface Segregation**: Implement focused interfaces
3. **Dependency Direction**: Services should depend on abstractions

```csharp
// ✅ Good - focused responsibility
[Service]
public partial class EmailSender : IEmailSender
{
    [Inject] private readonly IEmailConfiguration _config;
    [Inject] private readonly ILogger<EmailSender> _logger;
}

// ✅ Good - depends on abstraction
[Service]
public partial class NotificationService : INotificationService
{
    [Inject] private readonly IEmailSender _emailSender; // Abstraction, not concrete
}
```

### Lifetime Selection

1. **Default to Scoped** for most services
2. **Use Singleton** for expensive-to-create, thread-safe services
3. **Use Transient** for lightweight, stateless services

```csharp
// Expensive initialization → Singleton
[Service(Lifetime.Singleton)]
public partial class DatabaseConnectionPool : IConnectionPool { }

// Per-request state → Scoped
[Service(Lifetime.Scoped)]
public partial class CurrentUserService : ICurrentUserService { }

// Lightweight operations → Transient
[Service(Lifetime.Transient)]
public partial class HashCalculator : IHashCalculator { }
```

## Common Patterns

### Repository Pattern
```csharp
[Service]
public partial class UserRepository : IUserRepository
{
    [Inject] private readonly IDbContext _context;
    [Inject] private readonly ILogger<UserRepository> _logger;
}
```

### Service Layer
```csharp
[Service]
public partial class UserService : IUserService
{
    [Inject] private readonly IUserRepository _repository;
    [Inject] private readonly IEmailService _emailService;
    [Inject] private readonly IMapper _mapper;
}
```

### API Controllers
```csharp
[Service] // Can be used on controllers too
public partial class UsersController : ControllerBase
{
    [Inject] private readonly IUserService _userService;
    [Inject] private readonly ILogger<UsersController> _logger;
}
```

## Next Steps

- **[Dependency Injection](dependency-injection.md)** - Learn about `[Inject]` and `[DependsOn]` patterns
- **[Lifetime Management](lifetime-management.md)** - Deep dive into service lifetimes and validation
- **[Constructor Generation](constructor-generation.md)** - Understanding generated constructors
- **[Multi-Interface Registration](multi-interface-registration.md)** - Advanced registration patterns