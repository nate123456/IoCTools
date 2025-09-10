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
[Scoped]
[RegisterAs<IFoo, IBar>(InstanceSharing.Shared)]
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
// Preferred — no fields, constructor parameter only
[Scoped]
public partial class PaymentService : IPaymentService
{
    [DependsOn<ILogger<PaymentService>>]
    public Task PayAsync(decimal amount) => Task.CompletedTask;
}

// Last resort — field is needed across methods or for explicit naming
[Scoped]
public partial class PaymentAuditor
{
    [Inject] private readonly ILogger<PaymentAuditor> _log; // stored and reused
}
```
