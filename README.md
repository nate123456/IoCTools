# IoCTools

IoCTools is a .NET source generator that lets services declare their own dependencies and lifetimes using small, focused attributes. It generates constructors and service registrations at build time — no runtime reflection, minimal boilerplate.

[![NuGet](https://img.shields.io/nuget/v/IoCTools.Abstractions?label=IoCTools.Abstractions)](https://www.nuget.org/packages/IoCTools.Abstractions)
[![NuGet](https://img.shields.io/nuget/v/IoCTools.Generator?label=IoCTools.Generator)](https://www.nuget.org/packages/IoCTools.Generator)

## Quick Start

1) Install

```xml
<PackageReference Include="IoCTools.Abstractions" Version="*" />
<PackageReference Include="IoCTools.Generator" Version="*" PrivateAssets="all" />
```

2) Annotate a service

```csharp
[Scoped]
public partial class EmailService : IEmailService
{
    [Inject] private readonly ILogger<EmailService> _logger;
    public Task SendAsync(string to, string subject, string body)
        => Task.CompletedTask;
}
```

3) Register generated services

```csharp
var builder = WebApplication.CreateBuilder(args);
// The method name is derived from your assembly: Add<YourAssemblyName>RegisteredServices
builder.Services.AddYourAssemblyNameRegisteredServices(builder.Configuration);
```

That’s it: IoCTools generates the constructor and service registrations.

## Core Features

- Modern lifetimes: `[Scoped]`, `[Singleton]`, `[Transient]`
- Field injection: `[Inject]` for DI services
- Configuration injection: `[InjectConfiguration]` (values, sections, options)
- Interface control: `RegisterAs<T...>`, `RegisterAsAll`, `SkipRegistration`
- Conditional registration: `[ConditionalService]` by environment/config
- Background services: auto-detect `BackgroundService`/`IHostedService`
- Diagnostics: actionable build-time checks

## Docs

Start here: [docs/index.md](docs/index.md)

- Getting Started: [docs/getting-started.md](docs/getting-started.md)
- Attributes & Usage: [docs/attributes.md](docs/attributes.md)
- Lifetime Management: [docs/lifetime-management.md](docs/lifetime-management.md)
- Configuration Injection: [docs/configuration-injection.md](docs/configuration-injection.md)
- Conditional Services: [docs/conditional-services.md](docs/conditional-services.md)
- Background Services: [docs/background-services.md](docs/background-services.md)
- Interface Registration: [docs/multi-interface-registration.md](docs/multi-interface-registration.md)
- Generics: [docs/generics.md](docs/generics.md)
- Inheritance: [docs/inheritance.md](docs/inheritance.md)
- Diagnostics: [docs/diagnostics.md](docs/diagnostics.md)
- Generator Style Options: [docs/generator-style-options.md](docs/generator-style-options.md)
- Recipes: [docs/recipes.md](docs/recipes.md)
- FAQ: [docs/faq.md](docs/faq.md)

## Sample

Browse IoCTools.Sample for end-to-end scenarios, including advanced patterns and background services.

## License

MIT — see LICENSE.
