# IoCTools

This repository provides various useful tools for simplifying and streamlining the process of working with Inversion of
Control (IoC) in .NET, focusing on ASP.NET services. This project includes a source generator that eliminates much of
the boilerplate associated with service registration and dependency injection in .NET.

[![IoC Tools](https://img.shields.io/nuget/v/IoCTools.Abstractions?label=IoCTools.Abstractions)](https://www.nuget.org/packages/IoCTools.Abstractions)
[![IoC Abstractions](https://img.shields.io/nuget/v/IoCTools.Generator?label=IoCTools.Generator)](https://www.nuget.org/packages/IoCTools.Generator)

---

## Features

While this project is in alpha and under active development, many features are already functional.

---

### Self-Describing Services

Services often have specific lifetimes in mind. For example, singleton services persist values during the app session.
IoCTools lets the service itself define its lifetime, removing the need for explicit registration in `Program.cs`.

#### Example:

**Before (without IoCTools):**

```csharp
// in the service class
namespace SampleProject.Services;

public class SomeService : ISomeService
{
    private readonly int _someValue = 1;
}

// in Program.cs
builder.Services.AddSingleton<ISomeService, SomeService>();
```

**After (with IoCTools):**

```csharp
// in the service class
namespace SampleProject.Services;

[Service(Lifetime.Singleton)]
public partial class SomeService : ISomeService
{
    private readonly int _someValue = 1;
}

// in Program.cs
builder.Services.RegisterSampleProjectServices();
```

The service now defines its own lifetime, and registration is streamlined with a single method.

---

### Intuitive Dependency Injection

Dependency injection no longer needs verbose constructors or repetitive field assignments.

#### Example:

**Before (traditional DI):**

```csharp
namespace SampleProject.Services;

public class SomeService(IOtherService otherService, IAnotherService anotherService, 
    IYetAnotherService yetAnotherService, ISomehowAnotherService somehowAnotherService) 
    : ISomeService
{
    private readonly IOtherService _otherService = otherService;
    private readonly IAnotherService _anotherService = anotherService;
    private readonly IYetAnotherService _yetAnotherService = yetAnotherService;
    private readonly ISomehowAnotherService _somehowAnotherService = somehowAnotherService;

    private readonly int _someValue = 1;
}
```

**After (with IoCTools):**

```csharp
namespace SampleProject.Services;

[Service]
public partial class SomeService : ISomeService
{
    [Inject] private readonly IOtherService _otherService;
    [Inject] private readonly IAnotherService _anotherService;
    [Inject] private readonly IYetAnotherService _yetAnotherService;
    [Inject] private readonly ISomehowAnotherService _somehowAnotherService;

    private readonly int _someValue = 1;
}
```

IoCTools generates the constructor, simplifying DI management. It supports dependencies of any generic depth, such as
`IEnumerable<T>`.

---

### Streamlined Service Registration

IoCTools eliminates repetitive service registration by generating an extension method to register all `[Service]`
-annotated classes automatically.

**Before:**

```csharp
builder.Services.AddSingleton<ISomeService, SomeService>();
builder.Services.AddScoped<IAnotherService, AnotherService>();
builder.Services.AddTransient<IOtherService, OtherService>();
```

**After:**

```csharp
builder.Services.RegisterSampleProjectServices();
```

This method is project-specific, avoiding naming conflicts when multiple projects use IoCTools.

---

### Generic Service Support

IoCTools fully supports the registration of generic services.

#### Example:

```csharp
namespace SampleProject.Services;

[Service]
public class GenericService<T> : IGenericService<T>
{
}
```

**Generated Code:**

```csharp
services.AddScoped(typeof(IGenericService<>), typeof(GenericService<>));
```

---

### Excluding Services from Registration

For cases where a service shouldn’t be registered (e.g., controllers or handlers), use `[UnregisteredService]` to
exclude it explicitly.

```csharp
namespace SampleProject.Controllers;

[UnregisteredService]
public class MyController : ControllerBase
{
}
```

---

### `DependsOn`: Declarative Dependency Injection

`DependsOn` offers a concise way to define dependencies with default naming conventions.

#### Example:

```csharp
namespace SampleProject.Services;

[Service]
[DependsOn<IOtherService, IAnotherService, IYetAnotherService, ISomehowAnotherService>]
public partial class SomeService : ISomeService
{
    private readonly int _someValue = 1;
}
```

IoCTools generates fields for these dependencies, allowing the service to use them directly.

You can customize field naming with the following options:

- **`namingConvention`**: Use `CamelCase` (default), `PascalCase`, or `SnakeCase`.
- **`stripI`**: Removes the leading `I` in interface names (default: `true`).
- **`prefix`**: Adds a prefix to field names (default: `_`).

#### Example with Customization:

```csharp
[DependsOn<IOtherService, IAnotherService>(namingConvention: NamingConvention.PascalCase, prefix: "field_")]
[DependsOn<IYetAnotherService, ISomehowAnotherService>]
public partial class SomeService : ISomeService
{
}
```

---

### Development Status

IoCTools is in active development, with public pre-release NuGet packages available. Expect breaking changes as the
project evolves.

Feel free to contribute or report issues!

---
