# Basic Usage

## Your First IoCTools Service

Let's create a simple service step by step.

### 1. Create a Service Interface

```csharp
public interface IGreetingService
{
    string GetGreeting(string name);
}
```

### 2. Implement the Service with IoCTools

```csharp
using IoCTools.Abstractions.Annotations;

[Service]  // ← This registers the service automatically
public partial class GreetingService : IGreetingService
{
    [Inject] private readonly ILogger<GreetingService> _logger;  // ← Auto-injected dependency

    public string GetGreeting(string name)
    {
        _logger.LogInformation("Creating greeting for {Name}", name);
        return $"Hello, {name}!";
    }
}
```

### 3. Register Services in Your Application

```csharp
var builder = WebApplication.CreateBuilder(args);

// This discovers and registers all [Service] marked classes
// Method name is based on your project/assembly name
builder.Services.AddIoCTools[ProjectName]RegisteredServices(builder.Configuration);

var app = builder.Build();
```

### 4. Use the Service

```csharp
app.MapGet("/hello/{name}", (string name, IGreetingService greeting) => 
    greeting.GetGreeting(name));

app.Run();
```

## What IoCTools Generated

When you build, IoCTools automatically creates:

### Constructor Generation

IoCTools generates constructors behind the scenes at compile time. You don't write them - they're created automatically to inject your `[Inject]` fields:

```csharp
// This constructor is generated for you automatically:
// public GreetingService(ILogger<GreetingService> logger)
// {
//     this._logger = logger;
// }
```

The generated constructor handles dependency injection so your service works seamlessly with the DI container.

### Generated Registration
```csharp
// Generated registration method (you don't write this)
// Method name includes your project/assembly name
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIoCTools[ProjectName]RegisteredServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IGreetingService, GreetingService>();
        return services;
    }
}
```

**Note:** The `IConfiguration configuration` parameter is always required in the generated extension method signature, regardless of whether services use configuration injection.

## Key Concepts

### `[Service]` Attribute
- Marks a class for automatic DI registration
- Class must be `partial` for constructor generation
- Automatically detects implemented interfaces

### `[Inject]` Attribute  
- Marks fields for dependency injection
- Works with any access modifier (`private`, `protected`, `internal`)
- Supports generic types, collections, and nullable types

### Automatic Registration
- `AddIoCTools[ProjectName]RegisteredServices()` finds all `[Service]` classes
- Registers them with their interfaces
- Uses Scoped lifetime by default

## Common Patterns

### Service with Multiple Dependencies

```csharp
[Service]
public partial class OrderService : IOrderService
{
    [Inject] private readonly IPaymentService _paymentService;
    [Inject] private readonly IEmailService _emailService;
    [Inject] private readonly ILogger<OrderService> _logger;
    [Inject] private readonly IConfiguration _configuration;

    public async Task ProcessOrderAsync(Order order)
    {
        _logger.LogInformation("Processing order {OrderId}", order.Id);
        
        var paymentResult = await _paymentService.ProcessPaymentAsync(order.Payment);
        if (paymentResult.Success)
        {
            await _emailService.SendConfirmationAsync(order.CustomerEmail);
        }
    }
}
```

### Service with Lifetime Control

```csharp
[Service(Lifetime.Singleton)]  // ← Override default Scoped lifetime
public partial class CacheService : ICacheService
{
    [Inject] private readonly IMemoryCache _cache;
    
    public T GetOrSet<T>(string key, Func<T> factory)
    {
        return _cache.GetOrCreate(key, _ => factory());
    }
}
```

### Service without Interface

```csharp
[Service]  // ← Registers as concrete type only
public partial class BackgroundTaskService
{
    [Inject] private readonly IServiceProvider _serviceProvider;
    
    public async Task ProcessTasksAsync()
    {
        // Implementation
    }
}
```

## Generated Code Location

Generated code exists only at compile time and isn't saved as persistent files. To view generated code for debugging:

### Build with Generated File Output
```bash
dotnet build --property EmitCompilerGeneratedFiles=true --property CompilerGeneratedFilesOutputPath=generated
```

### View in IDE
- **Visual Studio**: Solution Explorer → Dependencies → Analyzers → IoCTools.Generator → Generated files
- **Rider**: External Libraries → IoCTools.Generator → Generated sources

### Debugging Tips
- Generated files are cleaned up automatically after builds
- Use the build command above to persist files temporarily for inspection
- Generated constructors appear in IntelliSense even though you don't see the source

## Common Diagnostics

IoCTools provides helpful warnings during build to catch dependency issues:

### IOC001: Missing Implementation
```csharp
[Service]
public partial class OrderService : IOrderService
{
    [Inject] private readonly IPaymentService _paymentService; // ⚠️ IOC001 if no IPaymentService implementation found
}
```

**Fix:** Register an implementation or mark the missing service with `[Service]`.

### IOC002: Unregistered Implementation
```csharp
public class PaymentService : IPaymentService { } // ⚠️ IOC002: Missing [Service] attribute

[Service]
public partial class OrderService : IOrderService
{
    [Inject] private readonly IPaymentService _paymentService; // Will trigger IOC002
}
```

**Fix:** Add `[Service]` attribute to the implementation class.

### IOC003: Circular Dependencies
```csharp
[Service] public partial class ServiceA { [Inject] private readonly ServiceB _b; }
[Service] public partial class ServiceB { [Inject] private readonly ServiceA _a; } // ⚠️ IOC003
```

**Fix:** Break the circular dependency by using interfaces or redesigning the relationship.

### IOC004-IOC005: Attribute Combination Issues
```csharp
[RegisterAsAll] // ⚠️ IOC004: Missing [Service] attribute
public partial class InvalidService : IService1, IService2 { }

[Service]
[SkipRegistration<IUnrelatedInterface>] // ⚠️ IOC005: Interface not in RegisterAsAll
public partial class SkipService : IService1, IService2 { }
```

**Fix:** Ensure proper attribute combinations and interface relationships.

### IOC006-IOC009: Dependency Declaration Issues
```csharp
[Service]
[DependsOn<IService1, IService1>] // ⚠️ IOC008: Duplicate in single DependsOn
[DependsOn<IService1>] // ⚠️ IOC006: Duplicate across DependsOn attributes
public partial class ConflictService
{
    [Inject] private readonly IService1 _service; // ⚠️ IOC007: Conflicts with DependsOn
}
```

**Fix:** Remove duplicate dependencies and avoid conflicts between `[Inject]` and `[DependsOn]`.

### IOC010-IOC015: Service Configuration Issues
```csharp
[Service(Lifetime.Scoped)]
[BackgroundService] // ⚠️ IOC014: BackgroundService should be Singleton
public partial class BackgroundProcessor : BackgroundService { }

[Service(Lifetime.Singleton)]
public partial class SingletonService
{
    [Inject] private readonly IScopedService _scoped; // ⚠️ IOC012: Invalid lifetime dependency
}
```

**Fix:** Use correct lifetimes and follow BackgroundService patterns.

### IOC016-IOC019: Configuration Injection Issues
```csharp
[Service]
public partial class ConfigService
{
    [InjectConfiguration("")] // ⚠️ IOC016: Empty configuration key
    private readonly string _value;
    
    [InjectConfiguration("Key")]
    private static readonly string _static; // ⚠️ IOC019: Static field not supported
}
```

**Fix:** Use valid configuration keys and avoid static fields.

### IOC020-IOC026: Conditional Service Issues
```csharp
[ConditionalService(Environment = "Dev", NotEnvironment = "Dev")] // ⚠️ IOC020: Conflicting
public partial class ConflictService : IService { }

[ConditionalService] // ⚠️ IOC022: Empty conditions
public partial class EmptyService : IService { }

[ConditionalService(Environment = "Production")]
[ConditionalService(ConfigValue = "Feature", Equals = "true")] // ⚠️ IOC026: Multiple attributes
public partial class MultiService : IService { }
```

**Fix:** Resolve conflicting conditions and use single ConditionalService attributes.

### Configuring Diagnostic Severity
```xml
<PropertyGroup>
  <!-- Configure missing implementation diagnostics (IOC001) -->
  <IoCToolsNoImplementationSeverity>Error</IoCToolsNoImplementationSeverity>
  
  <!-- Configure unregistered implementation diagnostics (IOC002) -->
  <IoCToolsUnregisteredSeverity>Info</IoCToolsUnregisteredSeverity>
  
  <!-- Configure lifetime validation diagnostics (IOC012-IOC015) -->
  <IoCToolsLifetimeValidationSeverity>Warning</IoCToolsLifetimeValidationSeverity>
  
  <!-- Disable all IoCTools diagnostics -->
  <IoCToolsDisableDiagnostics>true</IoCToolsDisableDiagnostics>
</PropertyGroup>
```

**Available MSBuild Properties:**
- `IoCToolsNoImplementationSeverity` - Controls IOC001 diagnostics
- `IoCToolsUnregisteredSeverity` - Controls IOC002 diagnostics  
- `IoCToolsLifetimeValidationSeverity` - Controls IOC012-IOC015 diagnostics
- `IoCToolsDisableDiagnostics` - Disables all diagnostics

**Severity Options:** `Error`, `Warning`, `Info`, `Hidden`

## Next Steps

- **[Service Declaration](service-declaration.md)** - Learn about lifetime options and service configuration
- **[Dependency Injection](dependency-injection.md)** - Advanced dependency patterns
- **[Constructor Generation](constructor-generation.md)** - How constructors are created and customized