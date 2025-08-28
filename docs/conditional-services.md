# Conditional Services

Register a service only in certain environments or when a configuration value matches.

## By Environment

```csharp
[Scoped]
[ConditionalService(Environment = "Production")] // or "Development,Staging"
public partial class MetricsCollector : IMetricsCollector { }
```

## By Configuration

```csharp
[Singleton]
[ConditionalService(ConfigValue = "Features:UseRedis", Equals = "true")]
public partial class RedisCache : ICache { }

[Scoped]
[ConditionalService(ConfigValue = "Database:Type", NotEquals = "InMemory")]
public partial class SqlRepository : IRepository { }
```

Notes

- `Equals`/`NotEquals` compare raw configuration strings.
- You can combine `Environment` and `ConfigValue` in one attribute.
- At least one condition is required; diagnostics will flag missing or conflicting settings.

## Interactions

- Works with any lifetime attribute.
- Works with interface controls (`RegisterAs`, `RegisterAsAll`, `SkipRegistration`).
- Background services with conditions still register via `AddHostedService<T>()`.
