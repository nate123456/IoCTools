# Interface Registration

Control which interfaces a class is registered as, and whether they share the same instance.

## RegisterAs<T...>

Register only the listed interfaces. Use `InstanceSharing.Shared` to map all listed interfaces to the same instance (the concrete type is also registered).

```csharp
[RegisterAs<IOrderService, IOrderValidator>(InstanceSharing.Shared)] // Scoped by default
public partial class OrderService : IOrderService, IOrderValidator, IInternalOnly { }
```

## RegisterAsAll

Register all interfaces, optionally without the concrete type.

```csharp
[Singleton]
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Separate)]
public partial class Cache : ICache, IKeyedCache { }

[RegisterAsAll(RegistrationMode.Exclusionary)] // register interfaces only
public partial class Processor : IProcessor, IHandler { }
```

## SkipRegistration

Opt out for the whole service or specific interfaces.

```csharp
[SkipRegistration] // do not register this type at all
public partial class ManualService : IManual { }

[RegisterAsAll(RegistrationMode.All)]
[SkipRegistration<IHandler>] // register everything except IHandler
public partial class Worker : IProcessor, IHandler { }
```

Notes

- `[SkipRegistration]` without `[RegisterAsAll]` triggers IOC005 because there are no generated registrations to skip. Either add `[RegisterAsAll]` or remove the skip attribute.
- `[SkipRegistration<I…>]` only matters when interfaces would be registered. If `[RegisterAsAll(RegistrationMode.DirectOnly)]` is used, IOC038 reminds you the skip list is ignored because DirectOnly registers only the concrete type.
- `[SkipRegistration]` suppresses all registrations—including lifetimes, `RegisterAs`, `RegisterAsAll`, and `ConditionalService`. IOC037 warns when you combine those attributes since SkipRegistration wins.
- `[RegisterAs]` and `[RegisterAsAll]` already imply a scoped lifetime. Adding `[Scoped]` just to “be safe” triggers IOC033.
- Collections (`IEnumerable<T>`) include all matching interface registrations automatically.
- Instance sharing works for `RegisterAs` and `RegisterAsAll`.
