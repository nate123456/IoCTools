# Lifetime Management

Choose a lifetime attribute per service and let IoCTools handle registration. Background services are a special case noted below.

## When To Use Each Lifetime

- Scoped: web requests, units of work, DbContexts, per-operation caches
- Singleton: stateless services, configuration readers, long-lived coordinators
- Transient: cheap, stateless, short-lived helpers

```csharp
[Scoped]   public partial class UserService : IUserService { [Inject] private readonly DbContext _db; }
[Singleton]public partial class AppInfo     : IAppInfo     { [InjectConfiguration] private readonly AppSettings _s; }
[Transient]public partial class Slugify    : ISlugify    { }
```

## Collections

`IEnumerable<T>` (and common list variants) are supported automatically. IoCTools ensures all matching registrations are included.

```csharp
public partial class CompositeNotifier : INotifier
{
    [Inject] private readonly IEnumerable<INotifier> _providers; // email, sms, push
}
```

## Background Services

Classes deriving from `BackgroundService` (or implementing `IHostedService`) are always registered via `AddHostedService<T>()`. Any lifetime attribute on the class is ignored for registration; diagnostics guide you to safe dependency lifetimes.

Recommendations

- Prefer singleton-like dependencies or options suited for singletons (`IOptions<T>` or `IOptionsMonitor<T>`)
- Avoid injecting scoped services directly into background services; resolve scopes inside `ExecuteAsync` when needed

```csharp
public partial class EmailProcessor : BackgroundService
{
    [Inject] private readonly ILogger<EmailProcessor> _log;
    protected override async Task ExecuteAsync(CancellationToken token)
    {
        using var scope = _provider.CreateScope(); // when scoped deps are needed
        await Task.Delay(1000, token);
    }
}
```

## Diagnostics (Highlights)

- Missing `partial` on types with `[Inject]`/`[InjectConfiguration]`
- Background service requires `partial`
- Lifetime/dependency mismatches (e.g., singleton depending on scoped)

See diagnostics.md for details and fixes.
