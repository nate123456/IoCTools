# Attributes & Usage

Each attribute has a single responsibility. Combine them for the behavior you want.

## Lifetimes

- `Scoped`: Registers the concrete type (and selected interfaces) with scoped lifetime.
- `Singleton`: Registers with singleton lifetime.
- `Transient`: Registers with transient lifetime.

Example

```csharp
[Scoped]
public partial class UserService : IUserService
{
    [Inject] private readonly ILogger<UserService> _log;
}
```

## Dependency Injection

- Recommended: `DependsOn<T...>` — declare constructor parameters without creating fields. Use this for most dependencies to keep classes minimal and avoid unnecessary state.
- Last resort: `Inject` — only when you truly need a field (e.g., you read it across methods, you need specific field naming, or you must store the instance).

Examples

```csharp
// Preferred: constructor-only dependency, no field created
[Scoped]
public partial class ReportGenerator
{
    [DependsOn<IEmailService, ILogger<ReportGenerator>>]
    public Task GenerateAsync() => Task.CompletedTask;
}

// Last resort: field is genuinely needed (e.g., reused across methods)
[Scoped]
public partial class StatefulProcessor
{
    [Inject] private readonly ILogger<StatefulProcessor> _log; // field required by design
}
```

## Configuration Injection

- `InjectConfiguration` (no key): Binds a section inferred from the type name (e.g., `DatabaseSettings` → `"Database"`).
- `InjectConfiguration("Key:Path")`: Binds a single value or a section by key.
- Options: `DefaultValue`, `Required` (default true), `SupportsReloading`.

```csharp
public partial class CacheService
{
    [InjectConfiguration("Cache:TTL", DefaultValue = "00:05:00")] private readonly TimeSpan _ttl;
    [InjectConfiguration] private readonly DatabaseSettings _db; // section inferred from type
}
```

## Interface Registration

- `RegisterAs<T1, T2, ...>`: Register only the listed interfaces. Control instance mapping with `InstanceSharing`:
  - `Separate`: each interface resolves to its own instance
  - `Shared`: all listed interfaces resolve to one instance
- `RegisterAsAll(mode, instanceSharing)`: Register concrete + all interfaces (`All`), just interfaces (`Exclusionary`), or just concrete (`DirectOnly`).
- `SkipRegistration` / `SkipRegistration<T...>`: Opt-out globally or for specific interfaces.

```csharp
[Scoped]
[RegisterAs<IOrderService, IOrderValidator>(InstanceSharing.Shared)]
public partial class OrderService : IOrderService, IOrderValidator, IInternalOnly
{
    [Inject] private readonly IPaymentGateway _payments;
}
// IInternalOnly is not registered (not listed)
```

## Conditional Registration

- `ConditionalService`: Register only for specific environments or when a config value matches.

Keys

- `Environment` / `NotEnvironment`: e.g., "Development", "Production", or comma-separated list
- `ConfigValue` with `Equals` or `NotEquals`: e.g., `ConfigValue = "Features:UseRedis", Equals = "true"`

```csharp
[Singleton]
[ConditionalService(Environment = "Production")] 
public partial class MetricsCollector : IMetricsCollector { }

[Scoped]
[ConditionalService(ConfigValue = "Features:Premium", Equals = "true")]
public partial class PremiumService : IPremiumFeature { }
```

## Dependency Shape

- `DependsOn<T1, ...>`: Include constructor parameters for services you do not want as fields. Useful for base classes or when fields aren’t needed.
- `ExternalService`: Mark a field or dependency as externally registered (e.g., framework or manual Program.cs registration). Used for diagnostics clarity.

```csharp
[Scoped]
[DependsOn<IClock, IGuidFactory>]
public partial class SessionService : ISessionService
{
    [Inject] private readonly ILogger<SessionService> _log; // plus clock + guid via DependsOn
}
```

## Background Services

- Any partial class deriving from `BackgroundService` or implementing `IHostedService` is auto-registered via `AddHostedService<T>()`.
- Lifetimes on background services are ignored for registration (they always register as hosted services). Diagnostics guide best practices.

```csharp
public partial class EmailProcessor : BackgroundService
{
    [Inject] private readonly ILogger<EmailProcessor> _log;
    protected override Task ExecuteAsync(CancellationToken token) => Task.CompletedTask;
}
```
