# Recipes

Small, copy-pasteable examples for common tasks.

## Composite Pattern (IEnumerable<T>)

```csharp
public interface INotifier { Task SendAsync(string m); }

[Transient] public partial class EmailNotifier : INotifier { }
[Transient] public partial class SmsNotifier   : INotifier { }

[Scoped]
public partial class CompositeNotifier : INotifier
{
    [Inject] private readonly IEnumerable<INotifier> _providers;
    public Task SendAsync(string m) => Task.WhenAll(_providers.Select(p => p.SendAsync(m)));
}
```

## Register As Two Interfaces (Shared Instance)

```csharp
[RegisterAs<IFoo, IBar>(InstanceSharing.Shared)] // Scoped by default
public partial class FooBar : IFoo, IBar { }
```

## Conditional Service by Environment

```csharp
[Singleton]
[ConditionalService(Environment = "Production,Staging")]
public partial class MetricsCollector : IMetrics { }
```

## Options Pattern with Reloading

```csharp
public class MyOptions { public int IntervalSec { get; set; } }

[Singleton]
public partial class Scheduler
{
    [InjectConfiguration] private readonly IOptionsMonitor<MyOptions> _opt; // change notifications
}
```

## Background Service with Scoped Work

```csharp
public partial class CleanupService : BackgroundService
{
    [Inject] private readonly IServiceScopeFactory _scopes;
    protected override async Task ExecuteAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            using var s = _scopes.CreateScope();
            var repo = s.ServiceProvider.GetRequiredService<IRepository>();
            await repo.CleanupAsync(token);
            await Task.Delay(TimeSpan.FromMinutes(5), token);
        }
    }
}
```
## Prefer DependsOn over Inject

```csharp
// Preferred — no fields, constructor parameter only (defaults to Scoped)
public partial class PaymentService : IPaymentService
{
    [DependsOn<ILogger<PaymentService>>]
    public Task PayAsync(decimal amount) => Task.CompletedTask;
}

// Last resort — field is needed across methods or for explicit naming
public partial class PaymentAuditor
{
    [Inject] private readonly ILogger<PaymentAuditor> _log; // stored and reused
}

```

> 💡 **IOC035** enforces this guidance automatically. If your `[Inject]` field is just `_logger` or `_service`, delete it and add `[DependsOn<TDependency>]` so the generator emits the backing field. Keep `[Inject]` only for bespoke names like `_primaryLogger` or when the field must stay mutable.

## Avoid Redundant Attribute Combinations

```csharp
// ❌ Triggers IOC033 – [Scoped] is redundant because DependsOn already implies a Scoped service
[Scoped]
[DependsOn<IMetricsCollector>]
public partial class RedundantMetricsService : IMetricsService { }

// ✅ Let the default Scoped lifetime kick in automatically
[DependsOn<IMetricsCollector>]
public partial class CleanMetricsService : IMetricsService { }

// ❌ Triggers IOC034 – RegisterAsAll already covers every interface
[RegisterAsAll]
[RegisterAs<IIntegrationEventService>]
public partial class ConflictingRegistration : IIntegrationEventService { }

// ✅ Keep only the intent you need
[RegisterAsAll]
public partial class AllInterfacesRegistration : IIntegrationEventService, IAuditService { }

// ❌ Triggers IOC036 – multiple lifetime attributes fight each other
[Scoped]
[Singleton]
public partial class ConflictingLifetimeService : IService { }

// ❌ Triggers IOC037 – SkipRegistration overrides the lifetime altogether
[Scoped]
[SkipRegistration]
public partial class ManualRegistrationService : IService { }

// ❌ Triggers IOC038 – SkipRegistration<IService> does nothing when RegisterAsAll is DirectOnly
[RegisterAsAll(RegistrationMode.DirectOnly)]
[SkipRegistration<IService>]
public partial class DirectOnlyService : IService { }
```
