# Interface Registration

Control which interfaces a class is registered as, and whether they share the same instance.

## RegisterAs<T...>

Register only the listed interfaces. Use `InstanceSharing.Shared` to map all listed interfaces to the same instance (the concrete type is also registered).

```csharp
[Scoped]
[RegisterAs<IOrderService, IOrderValidator>(InstanceSharing.Shared)]
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

- Collections (`IEnumerable<T>`) include all matching interface registrations automatically.
- Instance sharing works for `RegisterAs` and `RegisterAsAll`.
