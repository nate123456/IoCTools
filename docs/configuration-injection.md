# Configuration Injection

Inject configuration values, sections, and options directly into services.

## Value Binding

```csharp
public partial class EmailService
{
    [InjectConfiguration("Email:SmtpHost")] private readonly string _host;
    [InjectConfiguration("Email:SmtpPort", DefaultValue = 25)] private readonly int _port;
}
```

## Section Binding

```csharp
public class DatabaseSettings { public string? ConnectionString { get; set; } }

public partial class DataAccess
{
    [InjectConfiguration] private readonly DatabaseSettings _db; // binds section inferred from type name
}
```

Inference rules (no key): `*Settings`/`*Config`/`*Configuration` strip the suffix; otherwise uses type name. For options types (`IOptions<T>`, `IOptionsSnapshot<T>`, `IOptionsMonitor<T>`), the inner type `T` drives the section name.

## Options Pattern

```csharp
public partial class PricingService
{
    [InjectConfiguration] private readonly IOptions<PricingOptions> _options;           // static
    [InjectConfiguration] private readonly IOptionsSnapshot<PricingOptions> _snapshot;  // per-scope reload
    [InjectConfiguration] private readonly IOptionsMonitor<PricingOptions> _monitor;    // change notifications
}
```

## Optional, Defaults, Reloading

- `Required` (default true): throw on missing config; set `false` to allow missing values
- `DefaultValue`: fallback when missing or empty (strings) or conversion fails
- `SupportsReloading`: prefer `IOptionsSnapshot<T>`/`IOptionsMonitor<T>`

```csharp
[InjectConfiguration("Cache:TTL", Required = false, DefaultValue = "00:05:00", SupportsReloading = true)]
private readonly TimeSpan _ttl;
```

## Mixed With DI

You can combine configuration and DI in the same partial class; IoCTools generates a single constructor with both kinds of dependencies.
