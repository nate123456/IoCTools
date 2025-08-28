# Inheritance

IoCTools walks base classes when generating constructors and registrations.

## Basics

- `[Inject]` fields in base classes are included in derived constructors.
- `[DependsOn<T...>]` adds constructor parameters without creating fields. Useful to pass base dependencies through.
- If both `[Inject]` and `[DependsOn<T>]` reference the same type, the `[Inject]` field wins (diagnostics guard this).

```csharp
public abstract partial class BaseProcessor
{
    [Inject] protected readonly ILogger _log;
}

[Scoped]
public partial class OrderProcessor : BaseProcessor, IOrderProcessor
{
    [DependsOn<IClock>] // appear in constructor without a field
}
```

## Tips

- Keep base classes partial if they declare `[Inject]`/`[InjectConfiguration]` fields.
- Prefer `[DependsOn<T>]` for pass-through dependencies when the class does not need a private field.
- Avoid duplicate declarations across levels; diagnostics help catch collisions and duplicates.
