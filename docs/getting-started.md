# Getting Started

IoCTools removes DI boilerplate by generating constructors and registrations based on small attributes.

## Install

Add package references:

```xml
<PackageReference Include="IoCTools.Abstractions" Version="*" />
<PackageReference Include="IoCTools.Generator" Version="*" PrivateAssets="all" />
```

## First Service

```csharp
[Scoped]
public partial class EmailService : IEmailService
{
    [Inject] private readonly ILogger<EmailService> _log;
    public Task SendAsync(string to, string subject, string body) => Task.CompletedTask;
}
```

IoCTools generates a constructor equivalent to:

```csharp
public EmailService(ILogger<EmailService> log) { _log = log; }
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
    [Inject] private readonly ILogger<CacheService> _log;
}
```

## What To Read Next

- Attributes overview: attributes.md
- Lifetimes and best practices: lifetime-management.md
- Diagnostics and common mistakes: diagnostics.md
