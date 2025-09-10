# Getting Started

IoCTools removes DI boilerplate by generating constructors and registrations based on small attributes.

## Install

Add package references:

```xml
<PackageReference Include="IoCTools.Abstractions" Version="*" />
<PackageReference Include="IoCTools.Generator" Version="*" PrivateAssets="all" />
```

## First Service (Preferred: DependsOn)

```csharp
[Scoped]
public partial class EmailService : IEmailService
{
    // Prefer DependsOn: request constructor parameters, no fields created
    [DependsOn<ILogger<EmailService>>]
    public Task SendAsync(string to, string subject, string body) => Task.CompletedTask;
}
```

IoCTools generates a constructor equivalent to:

```csharp
public EmailService(ILogger<EmailService> log) { /* use log inside method scope */ }
```

## Register Generated Services

In your application startup:

```csharp
var builder = WebApplication.CreateBuilder(args);
// The method name is derived from your assembly: Add<YourAssemblyName>RegisteredServices
builder.Services.AddYourAssemblyNameRegisteredServices(builder.Configuration);
```

IoCTools discovers annotated partial classes in your project and registers them with the correct lifetimes and interfaces.

## Configuration Injection (Optional)

```csharp
public partial class CacheService : ICache
{
    [InjectConfiguration("Cache:TTL", DefaultValue = "00:05:00")] private readonly TimeSpan _ttl;
    // When a field is truly needed, use Inject as a last resort
    [Inject] private readonly ILogger<CacheService> _log;
}
```

Note on patterns
- Prefer `[DependsOn<T...>]` for most dependencies (constructor parameters, no fields).
- Use `[Inject]` only when a field is genuinely required or specific field naming is needed.

## What To Read Next

- Attributes overview: attributes.md
- Lifetimes and best practices: lifetime-management.md
- Diagnostics and common mistakes: diagnostics.md
