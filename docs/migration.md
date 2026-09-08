# Migration Guide

Migrating to IoCTools from manual DI registration or other dependency
injection containers — and migrating between IoCTools versions.

## Migrating from 1.5.x to 1.6.x

IoCTools 1.6.0 deprecates `[Inject]` field injection and introduces the
**auto-deps** system ([docs/auto-deps.md](auto-deps.md)). This section is the
canonical upgrade path.

### What changed

- **`[Inject]` is deprecated.** The attribute is `[Obsolete]` and emits
  `IOC095` (warning severity in 1.6; upgrades to error in 1.7; removed in
  2.0). `[DependsOn<T>]` is the source-of-truth replacement.
- **Auto-deps are new.** `[assembly: AutoDep<T>]` and
  `[assembly: AutoDepOpen(typeof(T<>))]` declare ambient dependencies once per
  assembly. `Microsoft.Extensions.Logging.ILogger<T>` is auto-detected — no
  declaration needed if your project references MEL.
- **IOC095 diagnostic reassigned.** In 1.5.1 IOC095 warned about open-generic
  `InstanceSharing.Shared` fallback. In 1.6 the same ID also covers
  `[Inject]` deprecation; both descriptors ship under IOC095. Consumers with
  existing IOC095 suppressions should review what the suppression now
  applies to.

### Recommended upgrade sequence

1. **Bump the IoCTools packages** to 1.6.0 across `IoCTools.Abstractions`,
   `IoCTools.Generator`, `IoCTools.Generator.Analyzer` (new package in 1.6 —
   ships the Roslyn analyzer and the `[Inject]` → `[DependsOn<T>]` IDE
   code fix; reference it as an analyzer in your csproj to get the
   light-bulb experience), `IoCTools.Testing`, `IoCTools.FluentValidation`,
   and `IoCTools.Tools.Cli`.
2. **(Optional) Silence `IOC095` during the migration window.** For a large
   codebase with many `[Inject]` usages, add a temporary severity override so
   the upgrade build stays readable:

   ```xml
   <PropertyGroup>
     <IoCToolsInjectDeprecationSeverity>Info</IoCToolsInjectDeprecationSeverity>
   </PropertyGroup>
   ```

3. **Run the migration.** Two equivalent paths:

   - **IDE (interactive):** the new `IoCTools.Generator.Analyzer` package
     ships a Roslyn code fix. Hitting Alt+Enter on an `[Inject]` field offers
     "Migrate `[Inject]` to `[DependsOn<T>]`" per field.
   - **CLI (headless, recommended for bulk):**

     ```bash
     ioc-tools migrate-inject --dry-run          # preview
     ioc-tools migrate-inject                    # apply
     ```

     The CLI walks the solution, converts every `[Inject]` field to the
     equivalent `[DependsOn<T>]` surface, and prints a summary (files
     touched, fields deleted because an auto-dep covered them, fields
     converted with `memberName*` preservation, fields split due to
     divergent `external:` flags).

     **Opting fields out of migration.** `migrate-inject` respects two
     forms of `IOC095` suppression:

     - `[SuppressMessage("IoCTools.Usage", "IOC095", ...)]` on the field
       or its enclosing class. A class-level suppression covers every
       `[Inject]` field on the class.
     - `#pragma warning disable IOC095` covering the field
       (paired with `#pragma warning restore IOC095` to bound the region).

     Use this for deliberate `[Inject]` examples — demos, deprecation
     fixtures, or one-off patterns you want to preserve through the
     warning → error → removal lifecycle.

4. **Commit the mechanical conversion as one diff** so code review stays
   manageable.
5. **Audit existing `IOC095` suppressions.** In 1.5.1, `IOC095` flagged an
   open-generic `InstanceSharing.Shared` fallback. In 1.6.0+, `IOC095` is
   primarily the `[Inject]` deprecation warning; the 1.5 fallback
   descriptor is retained under the same ID as a secondary descriptor. A
   `.editorconfig` entry like `dotnet_diagnostic.IOC095.severity = none`
   now silences *both* descriptors — review suppressions carried over from
   1.5.x and scope them appropriately (e.g. move them to the specific
   service files or replace with `IoCToolsInjectDeprecationSeverity`).
6. **Remove the severity override** from the csproj.
7. **(Optional) Promote cross-cutting repeats to auto-deps.** `ILogger<T>`
   is already auto-detected. If your codebase has repeated `TimeProvider`,
   `IMetrics`, or `ITracer` declarations, promote them once:

   ```csharp
   [assembly: AutoDep<TimeProvider>]
   [assembly: AutoDep<IMetrics>]
   ```

### How the code fix decides

For each `[Inject]` field, the migration picks one of three outcomes:

- **Delete entirely** — the field's type is covered by a resolved auto-dep
  for the service and the field carries no customization (no custom name,
  no `[ExternalService]` flag). The auto-dep supplies the dependency
  implicitly; the field is removed.
- **Convert to bare `[DependsOn<T>]`** — the field is not covered by an
  auto-dep and uses the default naming convention.
- **Convert with customization preserved** — custom field names map to
  `memberName1..N`; `[ExternalService]` maps to `external: true`. Fields
  with divergent `external:` flags are split into separate `[DependsOn<T>]`
  attributes because `external:` is attribute-wide on `DependsOn`.

### Migration losses to be aware of

- **Field access modifiers.** `[Inject] protected readonly IFoo _foo;`
  cannot round-trip — `[DependsOn<T>]` emits private fields. Protected /
  internal / public `[Inject]` fields must be hand-migrated to manual
  constructors. The migration tool surfaces these as requiring manual
  review rather than converting them silently.
- **Unusual field ordering.** The generator emits fields in the order
  `[DependsOn<T>]` attributes appear on the class. Multi-slot `DependsOn`
  attributes preserve slot order. Code that depended on interleaved
  `[Inject]` declaration order across multiple fields may need an explicit
  attribute ordering after migration.
- **Kept out of scope:** `[InjectConfiguration]` is unaffected and stays.
  It is not deprecated.

### Opt-out ladder for auto-deps

If auto-deps surprise you after the upgrade, the opt-out ladder goes from
narrow to broad:

| Need | How |
|---|---|
| Suppress the implicit `ILogger<T>` on one service | `[NoAutoDepOpen(typeof(ILogger<>))]` on the class *(rename-safe — does not mention the service type, unlike `[NoAutoDep<ILogger<MyService>>]`)* |
| Suppress one specific auto-dep type on one service | `[NoAutoDep<T>]` on the class |
| Suppress all auto-deps on one service | `[NoAutoDeps]` on the class |
| Disable `ILogger<T>` auto-detection project-wide | `<IoCToolsAutoDetectLogger>false</IoCToolsAutoDetectLogger>` |
| Exclude a namespace of services from auto-deps | `<IoCToolsAutoDepsExcludeGlob>*.Legacy.*</IoCToolsAutoDepsExcludeGlob>` |
| Kill-switch the entire feature | `<IoCToolsAutoDepsDisable>true</IoCToolsAutoDepsDisable>` |

### Solution-wide auto-dep policy

For teams that want one declaration of auto-dep policy across every project
in a solution, the idiomatic pattern is a small `MyCompany.DiConfig` project
containing only assembly attributes with `Scope = AutoDepScope.Transitive`.
Every other project takes a `<ProjectReference>` to it and inherits the
policy:

```csharp
// MyCompany.DiConfig/AssemblyInfo.cs
[assembly: AutoDepOpen(typeof(ILogger<>), Scope = AutoDepScope.Transitive)]
[assembly: AutoDep<TimeProvider>(Scope = AutoDepScope.Transitive)]
[assembly: AutoDepIn<ControllerDefaults, IMediator>(Scope = AutoDepScope.Transitive)]
[assembly: AutoDepsApply<ControllerDefaults, ControllerBase>(Scope = AutoDepScope.Transitive)]
```

This is the same pattern teams already use for
`[assembly: InternalsVisibleTo]`. MSBuild is **not** used for declaring
auto-deps — it remains override-only.

### Deprecation timeline

| Version | `[Inject]` posture |
|---|---|
| **1.6.x** | Deprecated. `IOC095` warning. Code fix + `migrate-inject` CLI ship. Severity tunable via `IoCToolsInjectDeprecationSeverity`. |
| **1.7.0** | `[Obsolete(error: true)]`. `IOC095` defaults to error. Severity override permits raising but not lowering. |
| **2.0.0** | `InjectAttribute` removed from `IoCTools.Abstractions`; related analyzers deleted. |

See [docs/auto-deps.md](auto-deps.md) for the full auto-deps reference.

---

## From Manual DI

### Step 1: Add IoCTools packages

```bash
dotnet add package IoCTools.Abstractions
dotnet add package IoCTools.Generator
```

### Step 2: Annotate services

Convert manual constructor injection to IoCTools attributes:

**Before:**
```csharp
public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UserService> _logger;

    public UserService(IUserRepository userRepository, ILogger<UserService> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }
}

// Registration
services.AddScoped<IUserService, UserService>();
```

**After:**
```csharp
[Scoped]
[DependsOn<IUserRepository, ILogger<UserService>>]
public partial class UserService : IUserService
{
    // Constructor auto-generated, fields auto-generated
}

// One line replaces all registrations
builder.Services.AddYourAssemblyRegisteredServices(configuration);
```

### Step 3: Remove manual registrations

Call the generated extension before you build the service provider. Attributes alone do not add services to `IServiceCollection`.

Delete lines like:
```csharp
// DELETE these
services.AddScoped<IUserService, UserService>();
services.AddScoped<IUserRepository, UserRepository>();
services.AddSingleton<ICacheService, CacheService>();
// ... dozens of lines
```

Replace with:
```csharp
// ADD this
builder.Services.AddYourAssemblyRegisteredServices(configuration);
```

#### IOC086 complete migration

For an assembly named `Migration.Probe`, replace `services.AddSingleton<Probe>()` with the following code:

```csharp
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Migration.Probe.Extensions.Generated;

var services = new ServiceCollection();
services.AddMigrationProbeRegisteredServices();
using var provider = services.BuildServiceProvider();
var first = provider.GetRequiredService<Probe>();
var second = provider.GetRequiredService<Probe>();
System.Diagnostics.Debug.Assert(ReferenceEquals(first, second));

[Singleton]
public sealed class Probe { }
```

The generator uses the implementation assembly name, not the class namespace.
It replaces hyphens and spaces with underscores for the generated namespace, then adds `.Extensions.Generated`.
For the method name, it also removes periods, then adds the `Add` prefix and `RegisteredServices` suffix.
Pass configuration when the generated signature requires it. Check the generated output for referenced assemblies whose generation settings you cannot inspect.

If you omit `AddMigrationProbeRegisteredServices()`, this example still compiles, but `GetRequiredService<Probe>()` throws `InvalidOperationException`.
Preserve interface mappings with `[RegisterAs]` as needed. Call each required implementation assembly's generated extension before you build the provider.

### Step 4: Handle configuration

**Before:**
```csharp
// Options class
public class EmailOptions { public string FromAddress { get; set; } }

// Registration
services.Configure<EmailOptions>(configuration.GetSection("Email"));

// Usage
public class EmailService
{
    private readonly EmailOptions _options;
    public EmailService(IOptionsMonitor<EmailOptions> options)
    {
        _options = options.CurrentValue;
    }
}
```

**After:**
```csharp
// No options class needed
[DependsOnConfiguration<string>("Email:FromAddress")]
public partial class EmailService
{
    // _emailFromAddress auto-generated and injected
}
```

### Common Migration Patterns

In 1.6.0+, `[Inject]` is deprecated and emits [IOC095](diagnostics.md#ioc095). Use `[DependsOn]`, `[DependsOnConfiguration]`, and `[DependsOnOptions]` directly in new code. `InjectConfiguration` remains supported but is not preferred.

| Manual DI | IoCTools |
|-----------|----------|
| `services.AddScoped<T, Impl>()` | `[Scoped] public partial class Impl : T` |
| `services.AddSingleton<T>()` | `[Singleton] public partial class T` |
| `IOptionsMonitor<T>` | `[DependsOnConfiguration<T>]` |
| `IConfiguration["key"]` | `[DependsOnConfiguration<string>("key")]` |
| Manual constructor | `[DependsOn<T1, T2>]` |
| `services.AddScoped(typeof(IRepository<>), typeof(Repository<>))` | `[Scoped] [RegisterAsAll] public partial class Repository<T> : IRepository<T> where T : class` |

---

## From Autofac

### Module registration

**Autofac:**
```csharp
public class ServicesModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<UserService>()
            .As<IUserService>()
            .InstancePerLifetimeScope();

        builder.RegisterType<EmailService>()
            .As<IEmailService>()
            .InstancePerLifetimeScope();
    }
}
```

**IoCTools:**
```csharp
[Scoped]
public partial class UserService : IUserService { }

[Scoped]
public partial class EmailService : IEmailService { }

// No modules needed
builder.Services.AddYourAssemblyRegisteredServices(configuration);
```

### Property injection

Autofac's property injection has no direct equivalent in IoCTools. Use constructor injection:

**Autofac:**
```csharp
builder.RegisterType<UserService>()
    .PropertiesAutowired(PropertyWiringOptions.AllowCircularDependencies);
```

**IoCTools:**
```csharp
[Scoped]
[DependsOn<ILogger<UserService>, IUserRepository>]
public partial class UserService : IUserService
{
    // All dependencies via constructor (auto-generated)
}
```

---

## From StructureMap

### Registry DSL

**StructureMap:**
```csharp
public class ServiceRegistry : Registry
{
    public ServiceRegistry()
    {
        For<IUserService>().Use<UserService>().Scoped();
        For<IEmailService>().Use<EmailService>().Scoped();
    }
}
```

**IoCTools:**
```csharp
[Scoped]
public partial class UserService : IUserService { }

[Scoped]
public partial class EmailService : IEmailService { }

builder.Services.AddYourAssemblyRegisteredServices(configuration);
```

### Named Instances

StructureMap named instances use `[RegisterAs]` with multiple interfaces:

**StructureMap:**
```csharp
For<ILogger>().Use<ConsoleLogger>().Named("Console");
For<ILogger>().Use<FileLogger>().Named("File");
```

**IoCTools:**
```csharp
[RegisterAs<ILogger, IConsoleLogger>]
public partial class ConsoleLogger : ILogger, IConsoleLogger { }

[RegisterAs<ILogger, IFileLogger>]
public partial class FileLogger : ILogger, IFileLogger { }
```

---

## From Microsoft.Extensions.DependencyInjection (Manual)

The transition is straightforward since IoCTools uses the same container:

**Remove:**
```csharp
services.AddScoped<IUserService, UserService>();
services.AddScoped<IUserRepository, UserRepository>();
services.AddSingleton<ICacheService, CacheService>();
services.AddHttpClient();
services.Configure<EmailOptions>(configuration.GetSection("Email"));
```

**Add:**
```csharp
builder.Services.AddYourAssemblyRegisteredServices(configuration);
```

Add attributes to services, and registration happens automatically.

---

## From DryIoc

### Container setup

**DryIoc:**
```csharp
var container = new Container();
container.Register<IUserService, UserService>(Reuse.Scoped);
container.Register<IEmailService, EmailService>(Reuse.Scoped);
```

**IoCTools:**
```csharp
[Scoped]
public partial class UserService : IUserService { }

[Scoped]
public partial class EmailService : IEmailService { }

builder.Services.AddYourAssemblyRegisteredServices(configuration);
```

### Resolvable factories

DryIoc's `Func<T>` factories work with Microsoft.Extensions.DependencyInjection:

**DryIoc:**
```csharp
container.RegisterDelegate<IUserFactory>(c => () => c.Resolve<UserService>());
```

**IoCTools:**
```csharp
// Use Microsoft DI's built-in factory support
services.AddScoped<Func<IUserService>>(sp => () => sp.GetRequiredService<IUserService>());
```

---

## Migration Checklist

- [ ] Add IoCTools packages to all service projects
- [ ] Add `partial` modifier to service classes
- [ ] Add lifetime attributes (`[Scoped]`, `[Singleton]`, `[Transient]`)
- [ ] Replace manual constructors with `[DependsOn<>]`
- [ ] Replace `IOptions<T>` with `[DependsOnConfiguration<>]`
- [ ] Remove new `[Inject]` / `InjectConfiguration` usage from migrated code
- [ ] Remove manual service registrations from startup
- [ ] Add `AddYourAssemblyRegisteredServices()` call
- [ ] Build and verify no IOC001/IOC002 diagnostics
- [ ] Run tests to ensure behavior unchanged
- [ ] Commit and deploy

---

## Troubleshooting Migration

### IOC001: No implementation found

**Cause:** Interface has no implementation or implementation lacks lifetime attribute.

**Fix:** Add `[Scoped]`/`[Singleton]`/`[Transient]` to implementation.

### IOC002: Implementation not registered

**Cause:** Implementation exists but no lifetime attribute.

**Fix:** Add lifetime attribute or `[ExternalService]` for manually registered services.

### IOC012: Singleton depends on Scoped

**Cause:** Lifetime mismatch in dependencies.

**Fix:** Change dependency to Singleton or use `IServiceProvider.CreateScope()`.

### IOC080: Service must be partial

**Cause:** Class uses IoCTools attributes but isn't `partial`.

**Fix:** Add `partial` modifier to class declaration.

## Related

- [Getting Started](getting-started.md) — IoCTools introduction
- [Attribute Reference](attributes.md) — All IoCTools attributes
- [Diagnostics Reference](diagnostics.md) — All diagnostic codes with fix guidance

---

**Back to [main README](../README.md)**
