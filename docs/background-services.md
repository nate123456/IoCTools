# Background Services

Any partial class that derives from `Microsoft.Extensions.Hosting.BackgroundService` or implements `IHostedService` is automatically registered as a hosted service.

Key points

- Mark the class `partial` so IoCTools can generate the constructor.
- Registration uses `AddHostedService<T>()`; lifetime attributes on the class are ignored for registration.
- Prefer singleton-compatible dependencies. Resolve scopes inside `ExecuteAsync` if you need scoped services.
- Prefer `[DependsOn<T...>]` to request dependencies as parameters; use `[Inject]` only when a field is truly required in the background worker.

Example

```csharp
public partial class EmailProcessor : BackgroundService
{
    [Inject] private readonly ILogger<EmailProcessor> _log;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _log.LogInformation("Processing email queue...");
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
```

Conditional registration works too:

```csharp
[ConditionalService(Environment = "Production")]
public partial class MetricsPusher : BackgroundService { /* ... */ }
```

Diagnostics highlight: missing `partial`, invalid lifetime assumptions, and unsafe scoped dependencies.
