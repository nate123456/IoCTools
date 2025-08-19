# Inheritance

IoCTools provides comprehensive inheritance support with automatic constructor generation, dependency propagation, and lifetime validation across inheritance hierarchies. This guide covers inheritance patterns, best practices, and architectural considerations for reliable dependency injection in inheritance chains.

> **Note**: While IoCTools supports most inheritance scenarios, some complex edge cases have architectural limits. See [Architectural Limits](#architectural-limits) for details on complex scenarios and alternatives.

## Basic Inheritance Patterns

### Simple Base Class with Dependencies

**Base Class:**
```csharp
[Service]
[DependsOn<ILogger<BaseService>>]
public partial class BaseService
{
    protected void LogOperation(string operation)
    {
        _logger.LogInformation("Operation: {Operation}", operation);
    }
    
    protected async Task<T> ExecuteWithLoggingAsync<T>(Func<Task<T>> operation, string operationName)
    {
        LogOperation($"Starting {operationName}");
        try
        {
            var result = await operation();
            LogOperation($"Completed {operationName}");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed {OperationName}", operationName);
            throw;
        }
    }
}
```

**Derived Class:**
```csharp
[Service]
[DependsOn<IRepository<User>, IEmailService>]
public partial class UserService : BaseService, IUserService
{
    public async Task<User> CreateUserAsync(User user)
    {
        return await ExecuteWithLoggingAsync(async () =>
        {
            var created = await _userRepository.CreateAsync(user);
            await _emailService.SendWelcomeAsync(user.Email);
            return created;
        }, "CreateUser");
    }
}
```

**Generated Constructors:**
```csharp
// BaseService constructor
public BaseService(ILogger<BaseService> logger)
{
    this._logger = logger;
}

// UserService constructor with proper base call
public UserService(
    ILogger<BaseService> logger,
    IRepository<User> userRepository,
    IEmailService emailService) 
    : base(logger)
{
    this._userRepository = userRepository;
    this._emailService = emailService;
}
```

## Multi-Level Inheritance

### Three-Level Hierarchy

**Level 1 - Base Entity:**
```csharp
[Service]
[DependsOn<ILogger<BaseEntity>, IDateTimeProvider>]
public abstract partial class BaseEntity
{
    protected void LogEntityChange(string entityType, string change)
    {
        _logger.LogInformation("{EntityType}: {Change} at {Timestamp}", 
            entityType, change, _dateTimeProvider.Now);
    }
}
```

**Level 2 - Base Repository:**
```csharp
[Service]
[DependsOn<IDbContext, IMapper>]
public abstract partial class BaseRepository<T> : BaseEntity where T : class
{
    protected async Task<T> FindByIdAsync(int id)
    {
        LogEntityChange(typeof(T).Name, $"Finding entity with ID: {id}");
        return await _dbContext.Set<T>().FindAsync(id);
    }
    
    protected async Task<T> SaveAsync(T entity)
    {
        LogEntityChange(typeof(T).Name, "Saving entity");
        var entry = _dbContext.Set<T>().Update(entity);
        await _dbContext.SaveChangesAsync();
        return entry.Entity;
    }
}
```

**Level 3 - Concrete Repository:**
```csharp
[Service]
[DependsOn<IEmailService, IUserValidator, ICacheService>]
public partial class UserRepository : BaseRepository<User>, IUserRepository
{
    public async Task<User> CreateUserAsync(User user)
    {
        await _userValidator.ValidateAsync(user);
        var saved = await SaveAsync(user);
        await _emailService.SendWelcomeAsync(user.Email);
        await _cacheService.InvalidateUserCacheAsync();
        return saved;
    }
    
    public async Task<User> GetUserByEmailAsync(string email)
    {
        var cacheKey = $"user:email:{email}";
        var cached = await _cacheService.GetAsync<User>(cacheKey);
        if (cached != null) return cached;
        
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user != null)
        {
            await _cacheService.SetAsync(cacheKey, user, TimeSpan.FromMinutes(15));
        }
        return user;
    }
}
```

**Generated Constructor Chain:**
```csharp
// BaseEntity constructor
protected BaseEntity(ILogger<BaseEntity> logger, IDateTimeProvider dateTimeProvider)
{
    this._logger = logger;
    this._dateTimeProvider = dateTimeProvider;
}

// BaseRepository<T> constructor
protected BaseRepository(
    ILogger<BaseEntity> logger, 
    IDateTimeProvider dateTimeProvider,
    IDbContext dbContext, 
    IMapper mapper) 
    : base(logger, dateTimeProvider)
{
    this._dbContext = dbContext;
    this._mapper = mapper;
}

// UserRepository constructor (final level)
public UserRepository(
    ILogger<BaseEntity> logger,
    IDateTimeProvider dateTimeProvider, 
    IDbContext dbContext,
    IMapper mapper,
    IEmailService emailService,
    IUserValidator userValidator,
    ICacheService cacheService) 
    : base(logger, dateTimeProvider, dbContext, mapper)
{
    this._emailService = emailService;
    this._userValidator = userValidator;
    this._cacheService = cacheService;
}
```

## Abstract Classes and Inheritance

### Abstract Base Classes (Not Registered)

**Note**: Abstract classes are automatically skipped during service registration since they cannot be instantiated.

```csharp
// ❌ This won't be registered (abstract classes are skipped)
[Service]
[DependsOn<ILogger<AbstractBaseService>>]
public abstract partial class AbstractBaseService
{
    protected abstract Task ProcessAsync();
    
    protected void LogProcess(string process)
    {
        _logger.LogInformation("Processing: {Process}", process);
    }
}

// ✅ This will be registered (concrete implementation)
[Service]
[DependsOn<IDataProcessor>]
public partial class ConcreteProcessorService : AbstractBaseService, IProcessorService
{
    protected override async Task ProcessAsync()
    {
        LogProcess("Data processing");
        await _dataProcessor.ProcessDataAsync();
    }
    
    public async Task ExecuteProcessingAsync()
    {
        await ProcessAsync();
    }
}
```

**Generated Registration:**
```csharp
// Only concrete classes are registered
services.AddScoped<IProcessorService, ConcreteProcessorService>();
// AbstractBaseService is NOT registered
```

## Generic Inheritance

### Generic Base Classes

> **Note**: Generic inheritance works well with simple constraints. Complex generic constraints may need simplification for reliability.

```csharp
[Service]
[DependsOn<ILogger<BaseService<T>>, IValidator<T>>]
public abstract partial class BaseService<T> where T : class
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

[Service]
[DependsOn<IRepository<User>, IEmailService>]
public partial class UserService : BaseService<User>, IUserService
{
    public async Task<User> ProcessUserAsync(User user)
    {
        var validation = await ValidateAsync(user);
        if (!validation.IsValid)
        {
            throw new ValidationException("User validation failed");
        }
        
        LogEntityOperation(user, "Processing");
        var saved = await _repository.SaveAsync(user);
        await _emailService.SendNotificationAsync(user.Email);
        
        return saved;
    }
}
```

**Complex Generic Constraints (Use with Caution):**
```csharp
// Simple constraints work reliably
[Service]
public partial class SimpleGenericService<T> where T : class, IEntity
{
    [Inject] private readonly IRepository<T> _repository;
}

// Very complex constraints may hit architectural limits
[Service]
public partial class ComplexGenericService<T, U> 
    where T : struct, IComparable<T>, IConvertible
    where U : class, IEnumerable<T>, IDisposable, new()
{
    // Consider: Do you need all these constraints?
    // Alternative: Use concrete types or simpler generics
}
```

**Generated Constructors:**
```csharp
// BaseService<T> constructor
protected BaseService(ILogger<BaseService<T>> logger, IValidator<T> validator)
{
    this._logger = logger;
    this._validator = validator;
}

// UserService constructor (T = User)
public UserService(
    ILogger<BaseService<User>> logger,
    IValidator<User> validator,
    IRepository<User> repository,
    IEmailService emailService) 
    : base(logger, validator)
{
    this._repository = repository;
    this._emailService = emailService;
}
```

## Configuration Inheritance

### Configuration in Inheritance Chains

```csharp
[Service]
[DependsOn<ILogger<BaseConfigService>>]
public abstract partial class BaseConfigService
{
    [InjectConfiguration("Base:Settings")]
    private readonly BaseSettings _baseSettings;
    
    protected void LogWithBaseConfig(string message)
    {
        _logger.LogInformation("{Message} | Environment: {Environment}", 
            message, _baseSettings.Environment);
    }
}

[Service]
[DependsOn<IEmailService>]
public partial class EmailConfigService : BaseConfigService, IEmailConfigService
{
    [InjectConfiguration("Email:Settings")]
    private readonly EmailSettings _emailSettings;
    
    public async Task SendConfiguredEmailAsync(string to, string message)
    {
        LogWithBaseConfig("Sending email");
        
        var settings = new EmailConfiguration
        {
            SmtpHost = _emailSettings.SmtpHost,
            Port = _emailSettings.Port,
            Environment = _baseSettings.Environment
        };
        
        await _emailService.SendWithSettingsAsync(to, message, settings);
    }
}
```

**Generated Constructors:**
```csharp
// BaseConfigService constructor
protected BaseConfigService(ILogger<BaseConfigService> logger, IConfiguration configuration)
{
    this._logger = logger;
    this._baseSettings = new BaseSettings();
    configuration.GetSection("Base:Settings").Bind(this._baseSettings);
}

// EmailConfigService constructor
public EmailConfigService(
    ILogger<BaseConfigService> logger,
    IConfiguration configuration,
    IEmailService emailService) 
    : base(logger, configuration)
{
    this._emailService = emailService;
    this._emailSettings = new EmailSettings();
    configuration.GetSection("Email:Settings").Bind(this._emailSettings);
}
```

## Lifetime Validation in Inheritance

### IOC015: Inheritance Chain Lifetime Validation

```csharp
[Service(Lifetime.Scoped)]
public abstract partial class ScopedBaseService
{
    [DependsOn<IDbContext>]
    protected void UseDbContext() { /* Implementation */ }
}

[Service(Lifetime.Singleton)] // ❌ IOC015: Cannot be more restrictive than base
public partial class SingletonDerivedService : ScopedBaseService
{
    // This will generate IOC015 error
}
```

**Solution - Align Lifetimes:**
```csharp
[Service(Lifetime.Singleton)]
public abstract partial class SingletonBaseService
{
    [DependsOn<IServiceProvider>] // Use IServiceProvider for scoped dependencies
    protected void UseDbContext()
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IDbContext>();
        // Use dbContext
    }
}

[Service(Lifetime.Singleton)] // ✅ Compatible with base class
public partial class SingletonDerivedService : SingletonBaseService
{
    // This works correctly
}
```

## Multi-Interface Inheritance

### Interfaces and Base Classes

```csharp
public interface IBaseOperations
{
    Task<bool> ValidateAsync();
}

public interface IAdvancedOperations : IBaseOperations
{
    Task<Result> ProcessAsync();
}

[Service]
[RegisterAsAll] // Register for all implemented interfaces
[DependsOn<ILogger<BaseOperationsService>, IValidator>]
public partial class BaseOperationsService : IBaseOperations
{
    public virtual async Task<bool> ValidateAsync()
    {
        _logger.LogInformation("Base validation");
        return await _validator.ValidateBasicAsync();
    }
}

[Service]
[RegisterAsAll] // Register for all implemented interfaces
[DependsOn<IProcessor, INotificationService>]
public partial class AdvancedOperationsService : BaseOperationsService, IAdvancedOperations
{
    public override async Task<bool> ValidateAsync()
    {
        // Call base validation first
        var baseValid = await base.ValidateAsync();
        if (!baseValid) return false;
        
        // Additional validation
        return await _validator.ValidateAdvancedAsync();
    }
    
    public async Task<Result> ProcessAsync()
    {
        if (!await ValidateAsync()) 
            return Result.ValidationFailed();
        
        var result = await _processor.ProcessAsync();
        await _notificationService.NotifyAsync("Processing completed");
        
        return result;
    }
}
```

**Generated Registration:**
```csharp
// BaseOperationsService registrations
services.AddScoped<BaseOperationsService>();
services.AddScoped<IBaseOperations>(provider => provider.GetRequiredService<BaseOperationsService>());

// AdvancedOperationsService registrations
services.AddScoped<AdvancedOperationsService>();
services.AddScoped<IBaseOperations>(provider => provider.GetRequiredService<AdvancedOperationsService>());
services.AddScoped<IAdvancedOperations>(provider => provider.GetRequiredService<AdvancedOperationsService>());
```

## Complex Inheritance Scenarios

> **Note**: The following examples demonstrate advanced patterns. Some may approach architectural limits in very complex implementations. Consider simpler alternatives for production code.

### Diamond Dependency Pattern

```csharp
[Service]
[DependsOn<ILogger<CommonBase>>]
public abstract partial class CommonBase
{
    protected void LogCommon(string message) => _logger.LogInformation(message);
}

[Service]
[DependsOn<IDataService>]
public abstract partial class DataBase : CommonBase
{
    protected async Task<T> LoadDataAsync<T>() => await _dataService.LoadAsync<T>();
}

[Service]
[DependsOn<INotificationService>]
public abstract partial class NotificationBase : CommonBase
{
    protected async Task NotifyAsync(string message) => await _notificationService.SendAsync(message);
}

// Composition approach (recommended for complex scenarios)
[Service]
[DependsOn<IEmailService>]
public partial class UnifiedService : DataBase, IUnifiedService
{
    [Inject] private readonly INotificationService _notificationService;
    
    public async Task ProcessWithNotificationAsync<T>()
    {
        LogCommon("Starting unified process");
        var data = await LoadDataAsync<T>();
        await _emailService.SendDataAsync(data);
        await _notificationService.SendAsync("Process completed");
        LogCommon("Unified process completed");
    }
}
```

> **Performance Note**: Complex inheritance patterns like this work but can impact compilation performance. Consider composition over inheritance for complex dependency graphs.

### Conditional Inheritance

> **Note**: Conditional inheritance with complex attribute combinations may approach architectural limits. Prefer simple conditional patterns.

```csharp
// Simple conditional base services (recommended)
[Service]
[ConditionalService(Environment = "Production")]
[DependsOn<IProductionLogger, IMetricsCollector>]
public partial class ProductionBaseService : IBaseService
{
    protected void LogMetrics(string operation, TimeSpan duration)
    {
        _productionLogger.LogOperation(operation, duration);
        _metricsCollector.RecordDuration(operation, duration);
    }
}

[Service]
[ConditionalService(NotEnvironment = "Production")]
[DependsOn<IConsoleLogger>]
public partial class DevelopmentBaseService : IBaseService
{
    protected void LogMetrics(string operation, TimeSpan duration)
    {
        _consoleLogger.WriteLine($"{operation} took {duration.TotalMilliseconds}ms");
    }
}

// Consumer service with clear interface dependency
[Service]
public partial class BusinessService : IBusinessService
{
    [Inject] private readonly IBaseService _baseService;
    
    public async Task ProcessBusinessLogicAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        // Business logic here
        stopwatch.Stop();
        
        // Safe casting with interface method
        if (_baseService is ProductionBaseService prod)
            prod.LogMetrics("BusinessProcess", stopwatch.Elapsed);
        else if (_baseService is DevelopmentBaseService dev)
            dev.LogMetrics("BusinessProcess", stopwatch.Elapsed);
    }
}
```

> **Best Practice**: Avoid dynamic casting in production code. Use interfaces or strategy patterns for environment-specific behavior.

## Best Practices

### Inheritance Design Patterns

**1. Layered Architecture:**
```csharp
// Infrastructure layer
[Service]
[DependsOn<ILogger<InfrastructureBase>>]
public abstract partial class InfrastructureBase
{
    protected void LogInfrastructure(string message) => _logger.LogInformation(message);
}

// Domain layer
[Service]
[DependsOn<IValidator>]
public abstract partial class DomainBase : InfrastructureBase
{
    protected async Task<bool> ValidateAsync<T>(T entity) => await _validator.ValidateAsync(entity);
}

// Application layer
[Service]
[DependsOn<IMapper, IMediator>]
public abstract partial class ApplicationBase : DomainBase
{
    protected async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request)
    {
        return await _mediator.Send(request);
    }
}
```

**2. Template Method Pattern:**
```csharp
[Service]
[DependsOn<ILogger<ProcessorBase<T>>>]
public abstract partial class ProcessorBase<T> where T : class
{
    public async Task<ProcessResult> ProcessAsync(T input)
    {
        _logger.LogInformation("Starting process for {Type}", typeof(T).Name);
        
        if (!await ValidateInputAsync(input))
            return ProcessResult.Invalid();
            
        var result = await ProcessInternalAsync(input);
        await PostProcessAsync(result);
        
        _logger.LogInformation("Process completed for {Type}", typeof(T).Name);
        return result;
    }
    
    protected abstract Task<bool> ValidateInputAsync(T input);
    protected abstract Task<ProcessResult> ProcessInternalAsync(T input);
    protected virtual Task PostProcessAsync(ProcessResult result) => Task.CompletedTask;
}

[Service]
[DependsOn<IUserValidator, IUserRepository, IEmailService>]
public partial class UserProcessor : ProcessorBase<User>
{
    protected override async Task<bool> ValidateInputAsync(User input)
    {
        return await _userValidator.IsValidAsync(input);
    }
    
    protected override async Task<ProcessResult> ProcessInternalAsync(User input)
    {
        var saved = await _userRepository.SaveAsync(input);
        return ProcessResult.Success(saved.Id);
    }
    
    protected override async Task PostProcessAsync(ProcessResult result)
    {
        if (result.Success)
        {
            await _emailService.SendWelcomeAsync(result.Data);
        }
    }
}
```

### Performance Considerations

**Deep Inheritance Chain Performance:**

Inheritance chains with 5+ levels can impact both compilation and runtime performance:

```csharp
// ❌ Very deep inheritance (5+ levels) - Performance concerns
Level1 (3 deps) -> Level2 (4 deps) -> Level3 (5 deps) -> Level4 (6 deps) -> Level5 (8 deps)
// Total: 26 dependencies in final constructor
// Compilation: Slower source generation
// Runtime: Complex constructor chain resolution

// ⚠️ Moderate inheritance (3-4 levels) - Generally acceptable
Base (2 deps) -> Middle (3 deps) -> Concrete (2 deps)
// Total: 7 dependencies - Reasonable complexity

// ✅ Shallow inheritance (2-3 levels) - Optimal performance
BaseService (2 deps) -> ConcreteService (3 deps)
// Total: 5 dependencies - Fast compilation and resolution
```

**Dependency Resolution Optimization:**
```csharp
// ✅ Efficient - Dependencies grouped by concern
[Service]
[DependsOn<ILogger<BaseService>, ICommonService>]
public abstract partial class BaseService
{
    // Common cross-cutting dependencies
}

[Service] 
[DependsOn<ISpecificService>] // Only domain-specific dependencies
public partial class DerivedService : BaseService
{
    // Business logic dependencies only
}
```

**Memory and GC Considerations:**
```csharp
// ⚠️ Many dependencies increase object size and GC pressure
[Service]
public partial class HeavyService : BaseService
{
    // Each [Inject] field adds to object overhead
    [Inject] private readonly IDep1 _dep1;
    [Inject] private readonly IDep2 _dep2;
    // ... 15 more dependencies
    
    // Consider: Does this service need all these dependencies?
    // Alternative: Split into focused services
}
```

### Common Pitfalls

**1. Lifetime Misalignment:**
```csharp
// ❌ Singleton base with Scoped dependencies
[Service(Lifetime.Singleton)]
public abstract partial class SingletonBase
{
    [DependsOn<IDbContext>] // Scoped service!
}

// ✅ Use factory pattern for lifetime mismatches
[Service(Lifetime.Singleton)]
public abstract partial class SingletonBase
{
    [DependsOn<IServiceProvider>] // Create scopes when needed
}
```

**2. Over-complicated Hierarchies:**
```csharp
// ❌ Too many levels and dependencies
Level1 (5 deps) -> Level2 (8 deps) -> Level3 (12 deps) -> Concrete (6 deps) = 31 total dependencies
// Problems: Slow compilation, complex constructor chains, high memory usage

// ✅ Composition over deep inheritance
[Service]
public partial class CompositeService
{
    [Inject] private readonly IDataService _dataService;      // Handles data concerns
    [Inject] private readonly IValidationService _validation; // Handles validation
    [Inject] private readonly ILoggingService _logging;       // Handles logging
    
    // Benefits: Clear responsibilities, faster compilation, easier testing
}
```

**3. Complex Access Patterns:**
```csharp
// ❌ Complex field access modifiers may hit architectural limits
[Service]
public partial class ComplexAccessService
{
    [Inject] protected internal readonly IDependency _dependency;
    [Inject] public readonly IAnotherDep _another;
}

// ✅ Use standard access patterns for reliability
[Service]
public partial class StandardService
{
    [Inject] private readonly IDependency _dependency;
    [Inject] private readonly IAnotherDep _another;
}

// Alternative for complex scenarios
[Service]
[DependsOn<IDependency, IAnotherDep>]
public partial class AlternativeService
{
    // Constructor auto-generated with proper access patterns
}
```

## Testing Inheritance Hierarchies

### Testing Base Class Behavior

```csharp
[TestClass]
public class InheritanceTests
{
    [TestMethod]
    public void BaseService_Should_InjectDependencies()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<ILogger<BaseService>, NullLogger<BaseService>>();
        services.AddScoped<ICommonService, MockCommonService>();
        services.AddScoped<ConcreteService>();
        
        var provider = services.BuildServiceProvider();
        
        // Act
        var service = provider.GetRequiredService<ConcreteService>();
        
        // Assert - Base dependencies should be available
        Assert.IsNotNull(service);
        // Verify base class functionality works
        service.CallBaseMethod(); // Should not throw
    }
    
    [TestMethod]
    public void InheritanceChain_Should_ResolveAllDependencies()
    {
        // Test that all levels of inheritance work correctly
        var services = new ServiceCollection();
        RegisterAllDependencies(services);
        
        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<FinalDerivedService>();
        
        // Test that all inherited functionality works
        service.TestLevel1Functionality();
        service.TestLevel2Functionality();
        service.TestLevel3Functionality();
    }
}
```

## Architectural Limits

While IoCTools supports most inheritance scenarios reliably, some complex edge cases have architectural limits. These are intentional design boundaries that maintain generator stability and performance.

### When You Might Hit Limits

**Complex Field Access Patterns:**
```csharp
// May require manual constructor implementation
[Service]
public partial class ComplexFieldService
{
    [Inject] protected internal readonly IDependency _dep1;
    [Inject] public readonly IAnotherDep _dep2;
    [Inject] private protected readonly IThirdDep _dep3;
}
```

**Very Deep Generic Inheritance:**
```csharp
// May hit complexity limits in deep chains
public partial class Level5Service<T, U, V> : 
    Level4Service<T, U> where T : IComparable<U>, IEquatable<V>
    where U : struct, IConvertible
    where V : class, IEnumerable<T>, new()
{
    // Complex constraint combinations may need simplification
}
```

**Configuration + Inheritance + Generics:**
```csharp
// May require alternative approaches
[Service]
public partial class ComplexConfigService<T> : BaseGenericService<T>
{
    [InjectConfiguration] private readonly IEnumerable<ComplexConfig<T>> _configs;
    [Inject] private readonly IProcessor<T> _processor;
}
```

### Alternatives for Complex Scenarios

**1. Simplify Access Patterns:**
```csharp
// Instead of complex field modifiers
[Service]
public partial class SimpleService
{
    [Inject] private readonly IDependency _dependency;
    
    // Provide controlled access through properties if needed
    protected IDependency Dependency => _dependency;
}
```

**2. Use DependsOn for Complex Cases:**
```csharp
// Instead of complex [Inject] patterns
[Service]
[DependsOn<IDep1, IDep2, IDep3>]
public partial class ReliableService : BaseService
{
    // Constructor generated with standard patterns
}
```

**3. Manual Implementation for Edge Cases:**
```csharp
// For the most complex scenarios
[Service]
public partial class ManualService
{
    private readonly IComplexDependency _dependency;
    
    public ManualService(IComplexDependency dependency)
    {
        _dependency = dependency;
    }
}
```

### Best Practices for Reliability

**Keep Inheritance Shallow:**
- Limit to 2-4 levels for optimal performance
- Consider composition over deep inheritance
- Monitor constructor parameter counts (< 10 recommended)

**Use Standard Patterns:**
- Prefer `private readonly` fields with `[Inject]`
- Use `[DependsOn<T>]` for complex scenarios
- Follow documented patterns in examples

**Test Complex Hierarchies:**
- Verify dependency resolution in integration tests
- Test all levels of inheritance functionality
- Monitor performance with deep chains

### Enhanced Sample Coverage Coming

The IoCTools team is working on expanded samples that demonstrate:
- Complex inheritance scenarios and their solutions
- Performance comparison of different patterns
- Migration strategies for hitting architectural limits
- Real-world examples from production applications

These enhanced samples will provide practical guidance for edge cases and demonstrate alternatives when standard patterns don't fit your specific needs.

For complete details on architectural limits and workarounds, see [ARCHITECTURAL_LIMITS.md](../ARCHITECTURAL_LIMITS.md).

## Next Steps

- **[Background Services](background-services.md)** - Inheritance patterns for long-running services
- **[Multi-Interface Registration](multi-interface-registration.md)** - Combining inheritance with multi-interface patterns
- **[Constructor Generation](constructor-generation.md)** - Understanding constructor generation in inheritance chains
- **[Testing](testing.md)** - Advanced testing strategies for inheritance hierarchies
- **[Architectural Limits](../ARCHITECTURAL_LIMITS.md)** - Complete guide to generator limits and alternatives