# Generics

IoCTools supports generic services, including open generics and constraints.

## Open Generics

```csharp
[Scoped]
public partial class Repository<T> : IRepository<T> where T : IEntity { }
```

Consumers can inject `IRepository<User>`, `IRepository<Order>`, etc. If you also have specific implementations, they will appear alongside in collections.

## Multiple Generic Parameters

```csharp
[Transient]
public partial class Mapper<TIn, TOut> : IMapper<TIn, TOut> { }
```

## Tips

- Put `[Inject]` fields on the generic type if they are common to all closed types.
- Prefer interface registration via `RegisterAs<T...>` if the generic type also implements non-DI interfaces.
- Be mindful of constraints — they affect which closed types can be resolved.
