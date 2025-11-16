# IoCTools

[![NuGet](https://img.shields.io/nuget/v/IoCTools.Abstractions?label=IoCTools.Abstractions)](https://www.nuget.org/packages/IoCTools.Abstractions)
[![NuGet](https://img.shields.io/nuget/v/IoCTools.Generator?label=IoCTools.Generator)](https://www.nuget.org/packages/IoCTools.Generator)

> A Roslyn source generator that lets each service declare its own lifetime, dependencies, and registration intent with small attributes. IoCTools emits constructors, service registrations, and analyzers at build time—no reflection, no runtime scanning.

## Highlights

- **Self-describing services** – `[Scoped]`, `[DependsOn<T>]`, `[RegisterAs<…>]`, `[ConditionalService]`, and `[InjectConfiguration]` live on the class, so intent never leaves the type.
- **Accurate registrations** – The generator produces `Add<YourAssembly>RegisteredServices()` extensions that register concrete types, interfaces, options bindings, conditional services, and background workers.
- **Analyzer coverage** – 30+ diagnostics (IOC001–IOC040) keep registrations honest: missing lifetimes, redundant `RegisterAs`, conflicting `[SkipRegistration]`, invalid config keys, singleton/ scoped mismatches, etc.
- **Zero reflection** – Everything happens at compile time. Startup cost stays flat, and generated code is plain C# you can inspect.

IoCTools treats each service class as the single source of truth for its registration story. Lifetimes, interface exposure, configuration needs, and conditional flags live beside the implementation, so setup code isn’t forced to guess or duplicate those concerns. This separation keeps startup/bootstrap files lean while ensuring services are designed with the lifetime/registration model they actually require.

## Installation

```bash
dotnet add package IoCTools.Abstractions
dotnet add package IoCTools.Generator --prerelease
```

Or directly in your project file:

```xml
<ItemGroup>
  <PackageReference Include="IoCTools.Abstractions" Version="*" />
  <PackageReference Include="IoCTools.Generator" Version="*" PrivateAssets="all" />
</ItemGroup>
```

## Getting Started in Three Steps

1. **Annotate a partial service**

   ```csharp
   [DependsOn<ILogger<EmailService>>] // Scoped implied when DependsOn/Inject/etc. are present (IOC033 otherwise)
   public partial class EmailService : IEmailService
   {
       public Task SendAsync(string to, string subject, string body) => Task.CompletedTask;
   }
   ```

   _Tip:_ Any partial class that implements at least one interface is treated as a scoped service even without `[Scoped]`. Add `[Singleton]`/`[Transient]` (or `[Scoped]` when you truly need to override diagnostics) only when you want to change that default. You can change the implicit lifetime globally via `build_property.IoCToolsDefaultServiceLifetime`, and both the generated registrations and IOC012/IOC013 diagnostics honor whatever value you pick.

2. **Build** – IoCTools emits `Add<YourAssembly>RegisteredServices()` into `<AssemblyName>.Extensions.Generated`.

3. **Call the extension during startup**

   ```csharp
   using YourAssembly.Extensions.Generated;

   var builder = WebApplication.CreateBuilder(args);
   builder.Services.AddYourAssemblyRegisteredServices(builder.Configuration);
   ```

## Before & After: Replacing DI Smells

### Legacy `BillingService` (manual, brittle)

```csharp
public class LegacyBillingService : IBillingService, ILegacyDiagnostics
{
    private readonly ILogger<LegacyBillingService> _logger;
    private readonly IHttpClientFactory _httpClients;
    private readonly BillingOptions _options;
    private readonly IConfiguration _config;

    public LegacyBillingService(
        ILogger<LegacyBillingService> logger,
        IHttpClientFactory httpClients,
        IOptionsMonitor<BillingOptions> options,
        IConfiguration config)
    {
        _logger = logger;
        _httpClients = httpClients;
        _options = options.CurrentValue;
        _config = config;
    }

    public async Task ChargeAsync(BillingRequest request)
    {
        var baseUrl = _config["Billing:BaseUrl"] ?? throw new InvalidOperationException("Billing options missing");
        // ... manual retry logic based on _options.RetryCount ...
    }
}

services.AddHttpClient();
services.AddSingleton<IOptionsMonitor<BillingOptions>, BillingOptionsMonitor>();
services.Configure<BillingOptions>(configuration.GetSection("Billing"));
services.AddScoped<IBillingService, LegacyBillingService>();
services.AddScoped<ILegacyDiagnostics, LegacyBillingService>();
```

Problems: duplicated registrations, runtime config lookups every call, manual interface wiring, no analyzer guardrails.

### IoCTools `BillingService` (attributes, analyzers, generated DI)

```csharp
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

[Scoped]
[RegisterAs<IBillingService, IBillingDiagnostics>(InstanceSharing.Shared)]
[ConditionalService(Environment = "Production,Staging")]
[DependsOn<ILogger<BillingService>, IHttpClientFactory, IClock>]
[SkipRegistration<ILegacyDiagnostics>] // Keep internal interface private to DI
public partial class BillingService : IBillingService, IBillingDiagnostics, ILegacyDiagnostics
{
    [InjectConfiguration("Billing:BaseUrl", Required = true)] private readonly string _baseUrl;
    [InjectConfiguration("Billing:RetryCount", DefaultValue = "3")] private readonly int _retryCount;
    [Inject] private readonly IMeter<BillingService> _meter; // field genuinely reused across methods

    public async Task ChargeAsync(BillingRequest request)
    {
        using var client = _httpClientFactory.CreateClient("billing");
        // Generated constructor supplies _logger, _httpClientFactory, _clock, and configuration fields.
    }
}
```

Generated code now:

- Creates a constructor that accepts `ILogger<BillingService> logger`, `IHttpClientFactory httpClientFactory`, and `IClock clock` (from `[DependsOn<…>]`).
- Injects `_meter`, `_baseUrl`, and `_retryCount` per attribute metadata.
- Registers the service once via `builder.Services.AddYourAssemblyRegisteredServices(configuration);` – IoCTools emits `services.AddScoped<IBillingService, BillingService>()` plus shared-instance factory wiring for `IBillingDiagnostics`.
- Emits diagnostics if `[Scoped]` becomes redundant (IOC033), if `[SkipRegistration<ILegacyDiagnostics>]` can never trigger (IOC009/IOC038), or if configuration keys are invalid (IOC016–IOC017).

## Naming & Generated Surface

### Dependency name derivation

IoCTools strips a leading `I` from interface names, applies the configured naming style, and prefixes fields with `_` by default. When SnakeCase is enabled it uses:

```csharp
Regex.Replace(fieldBaseName, "(?<!^)([A-Z])", "_$1").ToLowerInvariant();
```

So `IEmailService` → `_emailService`, `IDetailedInvoiceAuditor` → `_detailedInvoiceAuditor`, and with snake_case `_detailed_invoice_auditor`. Diagnostics IOC035 fire when an `[Inject]` field matches this auto-generated pattern, nudging you back to `[DependsOn<…>]` when a field isn’t required.

### Generated registration extension

For assembly `IoCTools.Sample` the generator emits:

```csharp
namespace IoCTools.Sample.Extensions.Generated;

public static class GeneratedServiceCollectionExtensions
{
    public static IServiceCollection AddIoCToolsSampleRegisteredServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // registrations, configuration bindings, AddHostedService calls, etc.
        return services;
    }
}
```

- **Namespace**: `<AssemblyNameWithoutInvalidChars>.Extensions.Generated`.
- **Method name**: `Add<SafeAssemblyName>RegisteredServices` (periods removed, hyphens/spaces replaced with `_`).
- Bring the namespace into scope (`using YourAssembly.Extensions.Generated;`) and call the method from `Program.cs`. Background services, conditional registrations, and configuration bindings flow through that single call.
- The generator adds the `IConfiguration` parameter only when a project actually uses `[InjectConfiguration]` or `[ConditionalService]`—otherwise the extension is `AddYourAssemblyRegisteredServices(this IServiceCollection services)`.

## Attribute Reference

| Attribute | Category | When to use | Notes |
|-----------|----------|-------------|-------|
| `[Scoped]`, `[Singleton]`, `[Transient]` | Lifetime | Declare how the service is registered. | Services own their lifetimes so startup code doesn’t. Use **one** per class; IOC036 warns otherwise. Scoped is implicit for partial classes that implement interfaces or when other service indicators exist. |
| `[DependsOn<T1, T2, …>]` | Dependencies | Request constructor parameters without fields. | Preferred approach—constructor is generated with parameters for each type. Apply the attribute multiple times (e.g., three `[DependsOn<…>]` blocks of five types each for 15 dependencies) when you need more than the generic arity allows. |
| `[Inject]` | Dependencies | Last resort when a field must exist (custom naming, mutability). | IOC035 tells you when the default naming regex already covers the dependency. |
| `[InjectConfiguration("Key", DefaultValue = "…", Required = bool, SupportsReloading = bool)]` | Configuration | Bind simple values or options straight into fields. | When no key is specified the section is inferred from the field type (e.g., `CacheOptions` → `Cache`). |
| `[RegisterAs<T1, …>(InstanceSharing.Shared\|Separate)]` | Interface control | Register only selected interfaces, optionally sharing instances. | Shared mode emits factory registrations so all listed interfaces reuse one instance. |
| `[RegisterAsAll(RegistrationMode.All\|Exclusionary\|DirectOnly, InstanceSharing)]` | Interface control | Register every implemented interface (or concrete only). | Combine with `[SkipRegistration<T>]` to prune specific interfaces. |
| `[SkipRegistration]` / `[SkipRegistration<T1, …>]` | Interface control | Disable registration completely or exclude individual interfaces. | IOC005/IOC037/IOC038 guard invalid combinations. |
| `[ConditionalService(Environment = "Prod", ConfigValue = "Feature:X", Equals = "true")]` | Conditional | Register only when env/config matches. | Requires a lifetime attribute (IOC021). |
| `[ManualService]` / `[ExternalService]` | Advanced | Mark services or dependencies that are registered manually. | Keeps analyzers quiet when DI is handled elsewhere. |
| `[InjectConfigurationOptions]` / `[DependsOnOptions]` | Options | Let IoCTools bind strongly typed options classes once and reuse them. | Automatically wires `IOptionsMonitor<T>` behind the scenes. |

## Analyzer (Diagnostic) Reference

| Rule | Severity | Summary |
|------|----------|---------|
| IOC001 | Warning | Service depends on an interface with no implementation in the project. |
| IOC002 | Warning | Implementation exists but is missing a lifetime attribute, so it never registers. |
| IOC003 | Warning | Circular dependency detected (message lists the cycle). |
| IOC004 | Error | `[RegisterAsAll]` requires a lifetime attribute because it defines a service. |
| IOC005 | Warning | `[SkipRegistration]` without `[RegisterAsAll]` has no effect. |
| IOC006 | Warning | Duplicate dependency types across multiple `[DependsOn]` attributes. |
| IOC007 | Warning | Deprecated – replaced by IOC040 redundant dependency warnings. |
| IOC008 | Warning | Duplicate type listed inside a single `[DependsOn]` attribute. |
| IOC009 | Warning | `[SkipRegistration<T>]` targets an interface that would never be registered. |
| IOC010 | Warning | Deprecated (background-service lifetime warnings are handled by IOC014). |
| IOC011 | Error | Background services must be declared `partial`. |
| IOC012 | Error | Singleton service depends on a scoped service. |
| IOC013 | Warning | Singleton service depends on a transient service. |
| IOC014 | Error | Background service uses a non-singleton lifetime. |
| IOC015 | Error | Lifetime mismatch across an inheritance chain. |
| IOC016 | Error | `[InjectConfiguration]` uses an invalid configuration key. |
| IOC017 | Warning | `[InjectConfiguration]` targets an unsupported type. |
| IOC018 | Error | `[InjectConfiguration]` applied to a non-partial class. |
| IOC019 | Warning | `[InjectConfiguration]` cannot target static fields. |
| IOC020 | Warning | `[ConditionalService]` contains conflicting conditions. |
| IOC021 | Error | `[ConditionalService]` requires a lifetime attribute. |
| IOC022 | Warning | `[ConditionalService]` declared with no conditions. |
| IOC023 | Warning | `ConfigValue` set without `Equals` / `NotEquals`. |
| IOC024 | Warning | `Equals` / `NotEquals` provided without a `ConfigValue`. |
| IOC025 | Warning | `ConfigValue` is empty or whitespace. |
| IOC026 | Warning | Multiple `[ConditionalService]` attributes on the same class. |
| IOC027 | Info | Potential duplicate service registrations detected. |
| IOC028 | Error | `[RegisterAs]` used without any service indicators/lifetime metadata. |
| IOC029 | Error | `[RegisterAs]` lists an interface the class does not implement. |
| IOC030 | Warning | Duplicate interface listed inside `[RegisterAs]`. |
| IOC031 | Error | `[RegisterAs]` references a non-interface type. |
| IOC032 | Warning | `[RegisterAs]` duplicates what the generator already infers. |
| IOC033 | Warning | `[Scoped]` attribute is redundant because the service is implicitly scoped. |
| IOC034 | Warning | `[RegisterAsAll]` combined with `[RegisterAs]` is redundant. |
| IOC035 | Warning | `[Inject]` field matches the default `[DependsOn]` naming pattern. |
| IOC036 | Warning | Multiple lifetime attributes are applied to the same class. |
| IOC037 | Warning | `[SkipRegistration]` overrides other registration attributes on the same class. |
| IOC038 | Warning | `[SkipRegistration<T>]` does nothing when `[RegisterAsAll(RegistrationMode.DirectOnly)]` is used. |
| IOC039 | Warning | Dependency declared via `[Inject]`/`[DependsOn]` is never referenced. |
| IOC040 | Warning | A dependency type is declared multiple times via `[Inject]` fields and/or `[DependsOn]` attributes. |

Each diagnostic includes a remediation tip in Visual Studio / Rider / CLI build output. Treat them as code reviews from the generator.

## Key Workflows

- **Dependency hygiene** – IOC039 warns when `[Inject]` or `[DependsOn]` declarations never get referenced, and IOC040 catches redundant combinations of `[Inject]` fields and `[DependsOn]` attributes before they reach generated constructors.
- **Configuration injection**: `[InjectConfiguration]` supports complex objects, primitives, and arrays. Pair with diagnostics IOC016–IOC019 to stay honest.
- **Conditional services**: Use `Environment`/`NotEnvironment` for environment-specific registrations and `ConfigValue` + `Equals`/`NotEquals` for feature toggles.
- **Background workers**: Any partial `BackgroundService` is registered through `AddHostedService<T>()`; analyzers enforce singleton lifetimes.
- **Lifetime validation**: IOC012/IOC013 warn when a singleton captures scoped or transient services; IOC015 watches inheritance chains so longer-lived services never depend on shorter-lived implementations.
- **Inheritance chains**: Partial base/derived services share the same constructor graph. The generator walks the hierarchy so lifetimes stay consistent (IOC015 protects you) and dependencies from base classes are included automatically.
- **Manual/External services**: Mark services you register yourself with `[ManualService]` or `[ExternalService]` to satisfy the analyzers without disabling them globally.
- **Collections**: The generator produces `IEnumerable<T>`/`IReadOnlyList<T>` wrappers when multiple implementations exist—no manual `services.AddSingleton<IEnumerable<T>>` needed.

## Future Ideas

The current roadmap builds on IOC039/IOC040 by surfacing more of the generator’s work directly in source, so developers rarely need to open `.g.cs` files:

- **IDE quick fixes** – ship Roslyn light-bulb actions for IOC039/IOC040 so you can convert `[Inject]` → `[DependsOn]`, drop redundant declarations, or remove dead dependencies with a click.
- **CLI parity for fixes** – pair the IDE actions with a `dotnet ioc fix/report/describe` tool so console and CI workflows can apply the same quick fixes, view redundancy reports, and inspect generated members without opening `.g.cs`.
- **Structured warning aggregates** – add a low-severity diagnostic summarizing the dependency hygiene status per class (e.g., “2 unused dependencies, 1 redundant”) to make large refactors easier to triage.
- **Fine-grained analyzer knobs** – expose MSBuild properties (e.g., `IoCToolsUnusedDependencySeverity`, `IoCToolsRedundantDependencyScope`) so teams can tune these warnings per project or per configuration.
- **Rich XML documentation & metadata** – have generated fields/constructors emit `<summary>`/`<param>` details noting which attribute produced them, and optionally tag them with `[GeneratedDependency(Attribute = …, DeclaredAt = …)]` so Go-To-Definition jumps back to the originating partial.
- **Analyzer-assisted navigation** – provide info diagnostics/code actions that list generated constructor signatures inline or open a preview window, eliminating the need to browse generated files.
- **Partial-class alignment hints** – warn (info) when another partial already defines a conflicting field or when `[DependsOn]` output is unused, including the generated signature in the message for quick fixes.
- **Debugger-friendly instrumentation** – optionally register a lightweight inspector in DEBUG builds so you can inspect the generated dependency graph at runtime without touching the `.g.cs` output.
- **Service graph dumps** – behind an MSBuild flag (e.g., `IoCToolsDumpServiceGraph=true`), emit a compact JSON summary of each service, its generated fields, and registrations to simplify reviews and CI audits.
- **Partial-type mapping guidance** – extend analyzers to suggest relocating `[DependsOn]` declarations to the partial that actually consumes the dependency, preventing future IOC039 hits.

## Configuration

IoCTools reads configuration from MSBuild properties/`.editorconfig` and from an optional `IoCTools.Generator.Configuration.GeneratorOptions` class. Common knobs:

| Property / API | Purpose | Example |
|----------------|---------|---------|
| `build_property.IoCToolsNoImplementationSeverity`, `IoCToolsManualSeverity`, `IoCToolsLifetimeValidationSeverity` | Override analyzer severity per category. | `.editorconfig`: `build_property.IoCToolsNoImplementationSeverity = error` |
| `build_property.IoCToolsDisableDiagnostics` | Disable all IoCTools diagnostics (not recommended except in migration). | `true` |
| `build_property.IoCToolsDisableLifetimeValidation` | Turn off lifetime-specific analyzers (IOC012–IOC015). | `true` |
| `build_property.IoCToolsSkipAssignableTypesUseDefaults` / `IoCToolsSkipAssignableTypes` / `…Add` / `…Remove` | Control “skip-by-assignable” generator style filters (exclude categories of services from registration). | `IoCToolsSkipAssignableTypesAdd = Namespace.*;Legacy.*` |
| `build_property.IoCToolsSkipAssignableExceptions` | Carve exceptions back in when using skip lists. | `IoCToolsSkipAssignableExceptions = Namespace.ImportantService` |
| `build_property.IoCToolsDefaultServiceLifetime` | Sets the implicit lifetime applied when a service has intent but no explicit `[Scoped]/[Singleton]/[Transient]`; generator output and IOC012/IOC013 both use the configured value. | `IoCToolsDefaultServiceLifetime = Singleton` (values: `Scoped`, `Singleton`, `Transient`). |
| `IoCTools.Generator.Configuration.GeneratorOptions` class | Configure the same skip/exceptions options via code when MSBuild isn’t convenient. | Define a static class with `public static GeneratorStyleOptions Current => new(...);` |

All properties can live in `Directory.Build.props`, `.editorconfig`, or project files. The generator merges “base list + add/remove + exceptions” so you can set organization-wide defaults then fine-tune per project.

## Samples & License

- `IoCTools.Sample` demonstrates every attribute, diagnostic, and configuration scenario (background services, RegisterAs vs RegisterAsAll, shared instances, options binding, etc.).
- Licensed under MIT. See `LICENSE`.
