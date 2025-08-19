# Dependency Injection

IoCTools provides multiple approaches to declare service dependencies: `[Inject]` attribute for field-based injection and `[DependsOn]` attribute for declaration-based injection. This guide covers all dependency injection patterns and best practices.

> **📝 IMPLEMENTATION STATUS**: `[Inject]` field injection is fully implemented and working. `[DependsOn]` attribute functionality for constructor parameter generation is implemented, but **automatic field generation is not yet implemented** - you must manually declare fields that DependsOn dependencies will be assigned to. The constructor generation and dependency resolution work correctly with manual field declarations.

## Field-Based Injection with `[Inject]`

### Basic Field Injection

```csharp
[Service]
public partial class OrderService : IOrderService
{
    [Inject] private readonly IPaymentService _paymentService;
    [Inject] private readonly IEmailService _emailService;
    [Inject] private readonly ILogger<OrderService> _logger;
    
    public async Task ProcessOrderAsync(Order order)
    {
        await _paymentService.ProcessAsync(order.Payment);
        await _emailService.SendConfirmationAsync(order.Email);
        _logger.LogInformation("Order {OrderId} processed", order.Id);
    }
}
```

**Generated Constructor:**
```csharp
public OrderService(IPaymentService paymentService, IEmailService emailService, ILogger<OrderService> logger)
{
    _paymentService = paymentService;
    _emailService = emailService;
    _logger = logger;
}
```

### Field Access Modifiers

```csharp
[Service]
public partial class FlexibleService
{
    [Inject] private readonly IService _private;       // ✅ Private
    [Inject] protected readonly IService _protected;   // ✅ Protected  
    [Inject] internal readonly IService _internal;     // ✅ Internal
    [Inject] public readonly IService _public;         // ✅ Public (not recommended)
}
```

### Generic Dependencies

```csharp
[Service]
public partial class GenericService<T> where T : class
{
    [Inject] private readonly IRepository<T> _repository;
    [Inject] private readonly ILogger<GenericService<T>> _logger;
    [Inject] private readonly IMapper<T> _mapper;
}
```

### Collection Dependencies

```csharp
[Service]
public partial class NotificationService : INotificationService
{
    [Inject] private readonly IEnumerable<INotificationProvider> _providers;
    [Inject] private readonly IList<IValidator> _validators;
    [Inject] private readonly IReadOnlyList<IFilter> _filters;
    
    public async Task SendNotificationAsync(string message)
    {
        foreach (var provider in _providers)
        {
            await provider.SendAsync(message);
        }
    }
}
```

## Declaration-Based Injection with `[DependsOn]`

### Basic Dependencies Declaration

```csharp
[Service]
[DependsOn<IPaymentService, IEmailService, ILogger<OrderService>>]
public partial class OrderService : IOrderService
{
    // Dependencies injected via constructor and assigned to manually declared fields
    public async Task ProcessOrderAsync(Order order)
    {
        await _paymentService.ProcessAsync(order.Payment);
        await _emailService.SendConfirmationAsync(order.Email);
        _logger.LogInformation("Order {OrderId} processed", order.Id);
    }
}
```

**Generated Constructor and Field Assignments:**
```csharp
// The source generator creates:
// 1. Constructor parameters
// 2. Field assignments (fields must be manually declared - automatic field generation not yet implemented)

public OrderService(IPaymentService paymentService, IEmailService emailService, ILogger<OrderService> logger)
{
    this._paymentService = paymentService;  // Generated assignment
    this._emailService = emailService;      // Generated assignment  
    this._logger = logger;                   // Generated assignment
}

// Note: You must declare fields manually:
private readonly IPaymentService _paymentService;
private readonly IEmailService _emailService;
private readonly ILogger<OrderService> _logger;
```

### Multiple DependsOn Attributes

```csharp
[Service]
[DependsOn<IRepository<User>, IRepository<Order>>]
[DependsOn<IEmailService, ISmsService>]
[DependsOn<ILogger<UserService>>]
public partial class UserService : IUserService
{
    public async Task ProcessUserAsync(int userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        var orders = await _orderRepository.GetByUserIdAsync(userId);
        
        await _emailService.SendWelcomeAsync(user.Email);
        _logger.LogInformation("User {UserId} processed", userId);
    }
    
    // Fields must be declared manually:
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<Order> _orderRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<UserService> _logger;
}
```

### DependsOn Advanced Parameters

**Complete DependsOn Syntax:**
```csharp
[DependsOn<T1, T2>(
    namingConvention: NamingConvention.CamelCase,  // Default: CamelCase
    stripI: true,                                  // Default: true - removes "I" from interface names
    prefix: "_",                                   // Default: "_" - adds underscore prefix
    external: false                                // Default: false - for external dependencies
)]
```

**Default Naming (most common):**
```csharp
[Service]
[DependsOn<IPaymentService, IEmailService>] // Uses defaults
public partial class DefaultNamingService
{
    public void DoWork()
    {
        // Generated constructor parameters and field assignments for: _paymentService, _emailService
        // stripI: true (IPaymentService → PaymentService → paymentService)
        // prefix: "_" (adds underscore)
        // namingConvention: CamelCase
        _paymentService.Process();
        _emailService.Send();
    }
    
    // Fields must be declared manually:
    private readonly IPaymentService _paymentService;
    private readonly IEmailService _emailService;
}
```

**Custom Naming Examples:**
```csharp
[Service]  
[DependsOn<IPaymentService, IEmailService>(NamingConvention.PascalCase, stripI: true, prefix: "")]
public partial class PascalCaseService
{
    public void DoWork()
    {
        // Generated constructor parameters and field assignments for: PaymentService, EmailService (no prefix, PascalCase)
        PaymentService.Process();
        EmailService.Send();
    }
    
    // Fields must be declared manually:
    private readonly IPaymentService PaymentService;
    private readonly IEmailService EmailService;
}

[Service]
[DependsOn<IPaymentService>(stripI: false, prefix: "svc")]
public partial class CustomPrefixService  
{
    public void DoWork()
    {
        // Generated constructor parameter and field assignment for: svcIPaymentService (keeps "I", custom prefix)
        svcIPaymentService.Process();
    }
    
    // Field must be declared manually:
    private readonly IPaymentService svcIPaymentService;
}

[Service]
[DependsOn<IExternalApi>(external: true)]
public partial class ExternalDependencyService
{
    public void DoWork()
    {
        // Generated constructor parameter and field assignment for: _externalApi
        // external: true prevents missing implementation diagnostics
        _externalApi.CallApi();
    }
    
    // Field must be declared manually:
    private readonly IExternalApi _externalApi;
}
```

## Mixed Injection Patterns

### Combining `[Inject]` and `[DependsOn]`

```csharp
[Service]
[DependsOn<IRepository<User>, IEmailService>] // Generated fields
public partial class UserService : IUserService
{
    [Inject] private readonly ILogger<UserService> _logger; // Manual field
    [Inject] private readonly IConfiguration _config;       // Manual field
    
    public async Task RegisterUserAsync(User user)
    {
        // Use generated field assignments from DependsOn (camelCase with underscore by default)
        await _userRepository.AddAsync(user);
        await _emailService.SendWelcomeAsync(user.Email);
        
        // Use [Inject] field assignments
        _logger.LogInformation("User {UserId} registered", user.Id);
        var welcomeMessage = _config["Messages:Welcome"];
    }
    
    // All fields must be declared manually:
    private readonly IRepository<User> _userRepository;  // From DependsOn
    private readonly IEmailService _emailService;        // From DependsOn
    // [Inject] fields declared above
}
```

## Advanced Dependency Patterns

### Optional Dependencies

```csharp
[Service]
public partial class CacheService : ICacheService
{
    [Inject] private readonly IMemoryCache _memoryCache;
    [Inject] private readonly IDistributedCache? _distributedCache; // Optional
    
    public async Task<T> GetAsync<T>(string key)
    {
        // Try distributed cache first if available
        if (_distributedCache != null)
        {
            return await GetFromDistributedAsync<T>(key);
        }
        
        return GetFromMemory<T>(key);
    }
}
```

### Factory Dependencies

```csharp
[Service]
public partial class DocumentProcessor : IDocumentProcessor
{
    [Inject] private readonly IServiceProvider _serviceProvider;
    [Inject] private readonly ILogger<DocumentProcessor> _logger;
    
    public async Task ProcessDocumentAsync(Document doc)
    {
        // Create processor based on document type
        var processorType = typeof(IDocumentProcessor<>).MakeGenericType(doc.GetType());
        var processor = _serviceProvider.GetRequiredService(processorType);
        
        await ((dynamic)processor).ProcessAsync(doc);
    }
}
```

### Lazy Dependencies

> **⚠️ MANUAL SETUP REQUIRED**: `Lazy<T>` dependencies require manual registration in your DI container. IoCTools does not automatically register `Lazy<T>` wrappers.

```csharp
// Manual DI container setup required:
// services.AddTransient<IHeavyResource, HeavyResource>();
// services.AddTransient<IComplexCalculator, ComplexCalculator>();
// services.AddTransient<Lazy<IHeavyResource>>(provider => 
//     new Lazy<IHeavyResource>(() => provider.GetRequiredService<IHeavyResource>()));
// services.AddTransient<Lazy<IComplexCalculator>>(provider => 
//     new Lazy<IComplexCalculator>(() => provider.GetRequiredService<IComplexCalculator>()));

[Service]
public partial class ExpensiveService : IExpensiveService
{
    [Inject] private readonly Lazy<IHeavyResource> _heavyResource;
    [Inject] private readonly Lazy<IComplexCalculator> _calculator;
    
    public async Task<Result> ProcessAsync()
    {
        // Heavy resources only created when accessed
        if (ShouldUseHeavyProcessing())
        {
            return await _heavyResource.Value.ProcessAsync();
        }
        
        return await _calculator.Value.CalculateAsync();
    }
}
```

### Func Dependencies

> **⚠️ MANUAL SETUP REQUIRED**: `Func<T>` dependencies require manual registration in your DI container. IoCTools does not automatically register factory delegates.

```csharp
// Manual DI container setup required:
// services.AddTransient<IUserValidator, UserValidator>();
// services.AddTransient<Func<IUserValidator>>(provider => 
//     () => provider.GetRequiredService<IUserValidator>());
// services.AddTransient<Func<string, IUserRepository>>(provider => 
//     database => provider.GetRequiredService<IUserRepositoryFactory>().Create(database));

[Service]
public partial class UserFactory : IUserFactory
{
    [Inject] private readonly Func<IUserValidator> _validatorFactory;
    [Inject] private readonly Func<string, IUserRepository> _repositoryFactory;
    
    public async Task<User> CreateValidatedUserAsync(string database, User user)
    {
        var validator = _validatorFactory();
        var repository = _repositoryFactory(database);
        
        await validator.ValidateAsync(user);
        return await repository.CreateAsync(user);
    }
}
```

## Inheritance and Dependencies

### Base Class Dependencies

```csharp
[Service]
[DependsOn<ILogger<BaseService>>]
public abstract partial class BaseService
{
    // Field must be declared manually:
    protected readonly ILogger<BaseService> _logger;
    
    protected async Task LogOperationAsync(string operation)
    {
        _logger.LogInformation("Operation: {Operation}", operation);
    }
}

[Service]
[DependsOn<IRepository<User>, IEmailService>]
public partial class UserService : BaseService, IUserService
{
    public async Task ProcessUserAsync(User user)
    {
        // Access base dependencies
        await LogOperationAsync("ProcessUser");
        
        // Access derived dependencies via generated field assignments
        await _userRepository.UpdateAsync(user);
        await _emailService.NotifyAsync(user.Email);
    }
    
    // Fields must be declared manually:
    private readonly IRepository<User> _userRepository;  // From DependsOn
    private readonly IEmailService _emailService;        // From DependsOn
}
```

**Generated Constructor:**
```csharp
public UserService(ILogger<BaseService> logger, IRepository<User> userRepository, IEmailService emailService) 
    : base(logger)
{
    this._userRepository = userRepository;  // Generated field assignment
    this._emailService = emailService;      // Generated field assignment
}
```

### Mixed Inheritance Patterns

```csharp
[Service]
public abstract partial class BaseRepository<T> where T : class
{
    [Inject] protected readonly IDbContext Context;
    [Inject] protected readonly ILogger<BaseRepository<T>> Logger;
}

[Service]
[DependsOn<IEmailService, IUserValidator>]
public partial class UserRepository : BaseRepository<User>, IUserRepository
{
    public async Task<User> CreateUserAsync(User user)
    {
        // Use inherited dependencies (via inherited fields)
        Logger.LogInformation("Creating user {Email}", user.Email);
        
        // Use declared dependencies via generated field assignments
        await _userValidator.ValidateAsync(user);
        
        Context.Users.Add(user);
        await Context.SaveChangesAsync();
        
        await _emailService.SendWelcomeAsync(user.Email);
        return user;
    }
    
    // Fields must be declared manually:
    private readonly IEmailService _emailService;      // From DependsOn
    private readonly IUserValidator _userValidator;    // From DependsOn
}
```

## Dependency Validation

### Missing Dependencies (IOC001)

```csharp
[Service]
public partial class OrderService
{
    [Inject] private readonly IMissingService _missing; // ⚠️ IOC001: No implementation found
}

// Solution: Create implementation or mark as external
[Service]
public partial class MissingService : IMissingService { }

// Or mark the consuming service as external
[ExternalService] // Dependencies provided externally
public partial class OrderService
{
    [Inject] private readonly IMissingService _missing;
}
```

### Circular Dependencies (IOC003)

```csharp
[Service] 
public partial class ServiceA 
{ 
    [Inject] private readonly ServiceB _b; 
}

[Service] 
public partial class ServiceB 
{ 
    [Inject] private readonly ServiceA _a; // ⚠️ IOC003: Circular dependency
}

// Solution: Break the cycle with an interface or redesign
[Service]
public partial class ServiceA : IServiceA
{
    [Inject] private readonly ServiceB _b;
}

[Service]
public partial class ServiceB
{
    [Inject] private readonly IServiceA _a; // ✅ No longer circular
}
```

### Dependency Conflicts (IOC007)

```csharp
[Service]
[DependsOn<IEmailService>] // ⚠️ IOC007: Conflicts with [Inject]
public partial class ConflictService
{
    [Inject] private readonly IEmailService _email; // Same dependency declared twice
}

// Solution: Use either [DependsOn] or [Inject], not both
[Service]
[DependsOn<IEmailService>]
public partial class FixedService
{
    public void SendEmail() => EmailService.SendAsync();
}
```

## Best Practices

### Dependency Organization

```csharp
[Service]
public partial class WellOrganizedService : IService
{
    // Group related dependencies
    [Inject] private readonly IRepository<User> _userRepository;
    [Inject] private readonly IRepository<Order> _orderRepository;
    
    // External services
    [Inject] private readonly IEmailService _emailService;
    [Inject] private readonly ISmsService _smsService;
    
    // Infrastructure
    [Inject] private readonly ILogger<WellOrganizedService> _logger;
    [Inject] private readonly IConfiguration _configuration;
}
```

### Naming Conventions

```csharp
// ✅ Good - Clear field names with underscore prefix
[Service]
public partial class GoodNamingService
{
    [Inject] private readonly IUserRepository _userRepository;
    [Inject] private readonly IEmailService _emailService;
}

// ✅ Good - Consistent DependsOn naming
[Service]
[DependsOn<IUserRepository, IEmailService>]
public partial class ConsistentService
{
    // Generated: UserRepository, EmailService (PascalCase)
    public void DoWork()
    {
        var users = UserRepository.GetAll();
        EmailService.SendBulk(users);
    }
}
```

### Interface Segregation

```csharp
// ✅ Good - Depend on focused interfaces
[Service]
public partial class FocusedService : IOrderProcessor
{
    [Inject] private readonly IPaymentProcessor _paymentProcessor;  // Focused on payments
    [Inject] private readonly IInventoryManager _inventoryManager;  // Focused on inventory
    [Inject] private readonly INotificationSender _notifications;   // Focused on notifications
}
```

## Performance Considerations

### Lazy Loading for Expensive Dependencies

> **⚠️ MANUAL SETUP REQUIRED**: See "Lazy Dependencies" section above for DI container configuration.

```csharp
[Service]
public partial class PerformantService
{
    [Inject] private readonly Lazy<IExpensiveService> _expensive;  // Requires manual DI setup
    [Inject] private readonly ILightweightService _lightweight;     // Auto-registered by IoCTools
    
    public async Task ProcessAsync(bool useExpensive)
    {
        await _lightweight.ProcessAsync(); // Always fast
        
        if (useExpensive)
        {
            await _expensive.Value.ProcessAsync(); // Only created when needed
        }
    }
}
```

### Collection Optimization

```csharp
[Service]  
public partial class OptimizedService
{
    [Inject] private readonly IReadOnlyList<IProcessor> _processors; // More efficient than IEnumerable
    
    public async Task ProcessAllAsync()
    {
        // Parallel processing when appropriate
        await Task.WhenAll(_processors.Select(p => p.ProcessAsync()));
    }
}
```

## Next Steps

- **[Constructor Generation](constructor-generation.md)** - Understanding how constructors are built
- **[Lifetime Management](lifetime-management.md)** - Service lifetime and dependency validation
- **[Configuration Injection](configuration-injection.md)** - Injecting configuration values directly
- **[Inheritance](inheritance.md)** - Advanced inheritance scenarios with dependencies