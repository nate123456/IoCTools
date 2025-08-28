# Diagnostics

IoCTools emits build-time diagnostics to catch issues early. Below are common categories and how to fix them.

## Partial and Generation

- Class with `[Inject]` or `[InjectConfiguration]` is not `partial` → add `partial`.
- Background service is not `partial` → add `partial`.

```csharp
public partial class EmailService { [Inject] private readonly ILogger<EmailService> _log; }
```

## Lifetime and Dependencies

- Singleton depending on scoped service → make the dependency singleton or resolve via scope factory.
- Background service using scoped dependency directly → resolve a scope inside `ExecuteAsync`.

## Conditional Services

- `ConditionalService` with no conditions → add `Environment` or `ConfigValue` with `Equals`/`NotEquals`.
- Conflicting or duplicate conditions → consolidate into one attribute.

## Interface Registration

- `RegisterAs<T...>` includes a type not implemented → ensure the class implements the interface.
- Duplicate interfaces in `RegisterAs<T...>` → remove duplicates.
- `SkipRegistration<T>` for types that wouldn’t be registered → remove unnecessary skip.

## Configuration Injection

- Static fields with `[InjectConfiguration]` → not supported; use instance fields.
- Missing configuration value with `Required = true` → provide value, set `Required = false`, or add `DefaultValue`.

If a diagnostic message is unclear, check the sample project for a minimal reproduction pattern.
