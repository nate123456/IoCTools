# IoCTools Diagnostics Reference

Quick reference for all IoCTools diagnostic messages with remediation guidance.

Authoring posture for 1.6.0+: `[Inject]` is deprecated and fires [IOC095](#ioc095) (see the 1.6 primary descriptor, not the 1.5 open-generic fallback of the same ID). `[DependsOn<T>]`, `[DependsOnConfiguration<T>]`, and `[DependsOnOptions<T>]` are canonical for new code.

## Diagnostic Categories

- [Dependency Diagnostics](#dependency-diagnostics) - IOC001-IOC002, IOC006-IOC009, IOC039-IOC055, IOC061-IOC062, IOC076, IOC078-IOC079
- [Lifetime Diagnostics](#lifetime-diagnostics) - IOC012-IOC015, IOC033, IOC059-IOC060, IOC072, IOC075, IOC084, IOC087
- [Configuration Diagnostics](#configuration-diagnostics) - IOC016-IOC019, IOC043-IOC046, IOC056-IOC057, IOC079, IOC088-IOC089
- [Registration Diagnostics](#registration-diagnostics) - IOC004-IOC005, IOC027-IOC038, IOC063-IOC065, IOC069-IOC071, IOC074, IOC081-IOC086, IOC090-IOC094 *(IOC095 moved to Auto-Deps in 1.6 — legacy open-generic fallback descriptor retained under the same ID)*
- [Structural Diagnostics](#structural-diagnostics) - IOC010-IOC011, IOC020-IOC026, IOCO41-IOC042, IOC058, IOC067-IOC068, IOC077, IOC080, IOC093
- [Auto-Deps Diagnostics (1.6.0+)](#auto-deps-diagnostics-160) - IOC095-IOC099, IOC103-IOC108
- [Testing Diagnostics](#testing-diagnostics) - TDIAG01 through TDIAG09
- [FluentValidation Diagnostics](#fluentvalidation-diagnostics) - IOC100-IOC102, IOC111-IOC112

## Severity Legend

- **[!Error](#)** - Blocks compilation; must be fixed
- **[!Warning](#)** - Potential issue; should be reviewed
- **[!Info](#)** - Suggestion; can improve code quality

## Project Scope

IoCTools diagnostics are scoped by `build_property.IsTestProject`.

- **Test-project only:** `TDIAG01`-`TDIAG09`. These help migrate service tests to `[Cover<T>]` and catch generated fixture misuse.
- **Production-project only:** DI graph completeness, manual registration, lifetime-policy, style, redundancy, auto-deps profile, and opportunity diagnostics such as `IOC001`-`IOC003`, `IOC012`, `IOC013`, `IOC027`, `IOC039`, `IOC043`, `IOC047`, `IOC057`, `IOC068`, `IOC079`, `IOC081`-`IOC087`, `IOC090`-`IOC094`, `IOC096`-`IOC099`, `IOC103`-`IOC108`, and `IOC110`.
- **Both production and test projects:** code-generation correctness diagnostics where an invalid attribute shape can break generated code, such as malformed registration/configuration attributes, missing `partial` on code-generated types, invalid dependency sets, manual constructor conflicts, and `[Inject]` deprecation.

Test projects often define fakes, partial harnesses, manual service registrations, and incomplete DI graphs. Production-only diagnostics are intentionally suppressed there so test code sees fixture/testing guidance without production-composition noise.

### Test-support libraries (no `Microsoft.NET.Test.Sdk` reference)

`Microsoft.NET.Test.Sdk` sets `IsTestProject=true` **only on the runner project** (the one that discovers and executes tests). A *test-support* library — fixtures, fake services, or host-builders shared across test projects but with no `Microsoft.NET.Test.Sdk` reference of its own — does **not** receive `IsTestProject` automatically, so IoCTools treats it as a production project and the production-only diagnostics (e.g. `IOC081`/`IOC082`/`IOC086` around manual fixture registration) fire.

This is the deliberate safe default: an unknown project is treated as production so production-composition defects are never silently dropped. IoCTools does **not** infer test context from project names or test-framework package references.

To opt a test-support library into the test scope, set the property explicitly in its `.csproj`:

```xml
<PropertyGroup>
  <IsTestProject>true</IsTestProject>
</PropertyGroup>
```

This mirrors what `Microsoft.NET.Test.Sdk` does for runner projects and routes the support library through the same `build_property.IsTestProject` gate — no per-diagnostic `#pragma warning disable IOC081` / `IOC082` / `IOC086` or blanket `<NoWarn>` needed.

---

## Dependency Diagnostics

Diagnostics related to service dependencies, dependency sets, and dependency hygiene.

### IOC001

**Severity:** [!Error](#) | **Category:** IoCTools.Dependency

**Cause:** No implementation of the depended-upon interface exists in the project.

**Fix:** Create a class implementing the interface with a lifetime attribute, add `[ExternalService]`, or register manually.

**Related:** [IOC002](#ioc002) (implementation exists but not registered), [IOC042](#ioc042) (implementation available but marked external)

---

### IOC002

**Severity:** [!Error](#) | **Category:** IoCTools.Dependency

**Cause:** An implementation of the interface exists but lacks a lifetime attribute and is not registered.

**Fix:** Add `[Scoped]`, `[Singleton]`, or `[Transient]` to the implementation, add `[ExternalService]`, or register manually.

**Related:** [IOC001](#ioc001) (no implementation found), [IOC042](#ioc042) (implementation available but marked external)

---

### IOC003

**Severity:** [!Error](#) | **Category:** IoCTools.Dependency

**Cause:** A circular dependency chain was detected between services.

**Fix:** Break the cycle by using interfaces, introducing a mediator pattern, or refactoring to eliminate the circular reference.

---

### IOC006

**Severity:** [!Warning](#) | **Category:** IoCTools.Dependency

**Cause:** The same type appears in multiple `[DependsOn]` attributes on the same class.

**Fix:** Remove duplicate dependency declarations.

**Related:** [IOC008](#ioc008) (duplicate in single attribute), [IOC040](#ioc040) (duplicate across different attributes)

---

### IOC007

**Severity:** [!Warning](#) | **Category:** IoCTools.Dependency

**Cause:** A type is declared in both `[DependsOn]` and as an `[Inject]` field on the same class.

**Fix:** Remove either the `[DependsOn]` declaration or the `[Inject]` field.

**Related:** [IOC035](#ioc035) (Inject field could be DependsOn)

---

### IOC008

**Severity:** [!Warning](#) | **Category:** IoCTools.Dependency

**Cause:** The same type appears multiple times in a single `[DependsOn]` attribute.

**Fix:** Remove duplicate type declarations from the `[DependsOn]` attribute.

**Related:** [IOC006](#ioc006) (duplicate across multiple attributes), [IOC040](#ioc040) (duplicate across different attributes)

---

### IOC009

**Severity:** [!Warning](#) | **Category:** IoCTools.Dependency

**Cause:** `[SkipRegistration]` specifies an interface not registered by `[RegisterAsAll]`.

**Fix:** Remove the unnecessary `[SkipRegistration]` declaration.

---

### IOC039

**Severity:** [!Warning](#) | **Category:** IoCTools.Dependency

**Cause:** A dependency field is declared via `[Inject]` or `[DependsOn]` but is never referenced in the class.

**Fix:** Remove the unused declaration or reference the generated field in your implementation.

---

### IOC040

**Severity:** [!Warning](#) | **Category:** IoCTools.Dependency

**Cause:** The same dependency type is declared multiple times via different attributes on the same class.

**Fix:** Declare each dependency once. Prefer `[DependsOn]` and remove duplicate declarations.

**Related:** [IOC006](#ioc006) (duplicate across DependsOn), [IOC007](#ioc007) (DependsOn conflicts with Inject)

---

### IOC041

**Severity:** [!Error](#) | **Category:** IoCTools.Dependency

**Cause:** A class has both IoCTools dependency declarations and a manual constructor, which conflict.

**Fix:** Let IoCTools generate the constructor, or remove the IoCTools dependency declarations to use manual constructors.

---

### IOC042

**Severity:** [!Warning](#) | **Category:** IoCTools.Dependency

**Cause:** A dependency is marked `External` but an implementation is already available in the solution.

**Fix:** Remove the `External` flag to let IoCTools manage the dependency normally.

**Related:** [IOC001](#ioc001) (no implementation found), [IOC002](#ioc002) (implementation exists but not registered)

---

### IOC043

**Severity:** [!Warning](#) | **Category:** IoCTools.Dependency

**Cause:** A dependency uses `IOptions<T>` types directly instead of `[DependsOnConfiguration]`.

**Fix:** Use `[DependsOnConfiguration<T>]` instead of depending on `IOptions`/`IOptionsSnapshot`/`IOptionsMonitor` directly.

---

### IOC044

**Severity:** [!Warning](#) | **Category:** IoCTools.Dependency

**Cause:** A dependency type is a primitive, value type, or string, which should use configuration injection instead.

**Fix:** Use `[DependsOnConfiguration<T>]` or `[DependsOnOptions<T>]` for configuration values.

---

### IOC045

**Severity:** [!Warning](#) | **Category:** IoCTools.Dependency

**Cause:** A dependency uses an unsupported collection type.

**Fix:** Use `IReadOnlyCollection<T>` for sets of resolved services.

---

### IOC048

**Severity:** [!Warning](#) | **Category:** IoCTools.Dependency

**Cause:** A dependency is declared as nullable, but dependencies are expected to be required.

**Fix:** Use non-nullable types. Register a no-op implementation if the dependency is optional.

---

### IOC049

**Severity:** [!Error](#) | **Category:** IoCTools.Dependency

**Cause:** A type implementing `IDependencySet` declares members (methods, properties, fields, events, or nested types).

**Fix:** Keep `IDependencySet` types metadata-only. Move members elsewhere.

---

### IOC050

**Severity:** [!Error](#) | **Category:** IoCTools.Dependency

**Cause:** Dependency sets form a cycle (e.g., SetA references SetB which references SetA).

**Fix:** Remove one of the set references to break the cycle.

---

### IOC051

**Severity:** [!Error](#) | **Category:** IoCTools.Dependency

**Cause:** Dependency set expansion produces conflicting member names for the same dependency type.

**Fix:** Align the member names across sets or remove the duplicate dependency.

---

### IOC052

**Severity:** [!Warning](#) | **Category:** IoCTools.Dependency

**Cause:** A type implementing `IDependencySet` is marked for registration via lifetime or registration attributes.

**Fix:** Remove lifetime/registration attributes. Dependency sets are metadata-only and should not be registered.

---

### IOC053

**Severity:** [!Info](#) | **Category:** IoCTools.Dependency

**Cause:** The same set of dependencies repeats across multiple services.

**Fix:** Extract the repeated dependencies into an `IDependencySet` and reference it with `[DependsOn<T>]`.

---

### IOC054

**Severity:** [!Info](#) | **Category:** IoCTools.Dependency

**Cause:** A service already has most members of an existing dependency set.

**Fix:** Adopt the existing dependency set and add the few additional dependencies separately.

---

### IOC055

**Severity:** [!Info](#) | **Category:** IoCTools.Dependency

**Cause:** Services derived from the same base share common dependencies that could be centralized.

**Fix:** Move shared dependencies into a base-oriented `IDependencySet` or the base class.

---

### IOC061

**Severity:** [!Warning](#) | **Category:** IoCTools.Dependency

**Cause:** A derived class repeats a dependency set already applied in a base class.

**Fix:** Remove the redundant `[DependsOn<Set>]` on the derived class.

---

### IOC062

**Severity:** [!Info](#) | **Category:** IoCTools.Dependency

**Cause:** Multiple derived services all reference the same dependency set.

**Fix:** Move `[DependsOn<Set>]` to the shared base class to reduce duplication.

---

### IOC076

**Severity:** [!Warning](#) | **Category:** IoCTools.Dependency

**Cause:** A property trivially wraps an IoCTools dependency field without adding behavior.

**Fix:** Access the injected field directly or move the dependency to the base type.

---

### IOC078

**Severity:** [!Warning](#) | **Category:** IoCTools.Dependency

**Cause:** A MemberNames value collides with an existing field, causing the generated dependency to be skipped.

**Fix:** Remove the existing field or drop MemberNames to let IoCTools generate and wire the dependency.

**Related:** [IOC077](#ioc077) (manual field shadows generated field)

---

## Lifetime Diagnostics

Diagnostics related to service lifetime management and compatibility.

### IOC012

**Severity:** [!Error](#) | **Category:** IoCTools.Lifetime

**Cause:** A Singleton service depends on a Scoped service, which would cause the scoped dependency to be captured for the lifetime of the singleton.

**Fix:** Change the dependency to `[Singleton]`, change this service to `[Scoped]` or `[Transient]`, inject `IServiceProvider` and call `CreateScope()` to resolve on demand, or use a factory delegate.

**Related:** [IOC013](#ioc013) (Singleton depends on Transient), [IOC087](#ioc087) (Transient depends on Scoped)

---

### IOC013

**Severity:** [!Warning](#) | **Category:** IoCTools.Lifetime

**Cause:** A Singleton service depends on a Transient service, capturing only one instance for the singleton's lifetime.

**Fix:** Change the dependency to `[Singleton]` if it should be shared, inject `IServiceProvider` and call `CreateScope()` to resolve on demand, or use a factory delegate `Func<T>`.

**Related:** [IOC012](#ioc012) (Singleton depends on Scoped), [IOC087](#ioc087) (Transient depends on Scoped)

---

### IOC014

**Severity:** [!Error](#) | **Category:** IoCTools.Lifetime

**Cause:** A background service has a non-Singleton lifetime.

**Fix:** Change to `[Singleton]` for optimal background service lifetime, or suppress the warning if the current lifetime is intentional.

**Related:** [IOC010](#ioc010) (deprecated), [IOC072](#ioc072) (hosted service lifetime is implicit)

---

### IOC015

**Severity:** [!Error](#) | **Category:** IoCTools.Lifetime

**Cause:** A service lifetime mismatch exists in the inheritance chain, with the full inheritance path shown in the diagnostic.

**Fix:** Make all services in the chain use the same lifetime, change the consuming service lifetime, or break the inheritance chain.

**Related:** [IOC075](#ioc075) (inconsistent lifetimes across derived services), [IOC084](#ioc084) (redundant lifetime in derived class)

---

### IOC033

**Severity:** [!Warning](#) | **Category:** IoCTools.Lifetime

**Cause:** A class is already implicitly registered as Scoped, making the explicit `[Scoped]` attribute redundant.

**Fix:** Remove the redundant `[Scoped]` attribute or change to a non-default lifetime.

**Related:** [IOC059](#ioc059) (redundant Singleton), [IOC060](#ioc060) (redundant Transient)

---

### IOC059

**Severity:** [!Warning](#) | **Category:** IoCTools.Lifetime

**Cause:** A derived class repeats `[Singleton]` already inherited from a base class.

**Fix:** Remove the redundant `[Singleton]` on the derived class.

**Related:** [IOC033](#ioc033) (redundant Scoped), [IOC060](#ioc060) (redundant Transient), [IOC084](#ioc084) (redundant lifetime in derived)

---

### IOC060

**Severity:** [!Warning](#) | **Category:** IoCTools.Lifetime

**Cause:** A derived class repeats `[Transient]` already inherited from a base class.

**Fix:** Remove the redundant `[Transient]` on the derived class.

**Related:** [IOC033](#ioc033) (redundant Scoped), [IOC059](#ioc059) (redundant Singleton), [IOC084](#ioc084) (redundant lifetime in derived)

---

### IOC072

**Severity:** [!Warning](#) | **Category:** IoCTools.Lifetime

**Cause:** A hosted service declares a lifetime attribute, but hosted services are registered implicitly.

**Fix:** Remove the lifetime attribute unless the class also exposes additional service interfaces.

**Related:** [IOC014](#ioc014) (background service lifetime validation)

---

### IOC075

**Severity:** [!Warning](#) | **Category:** IoCTools.Lifetime

**Cause:** A base class is inherited by IoCTools services with mixed or missing lifetimes.

**Fix:** Place one lifetime attribute on the shared base class so all derived services register consistently.

**Related:** [IOC015](#ioc015) (lifetime mismatch in inheritance chain), [IOC084](#ioc084) (redundant lifetime in derived)

---

### IOC084

**Severity:** [!Warning](#) | **Category:** IoCTools.Lifetime

**Cause:** A derived class declares the same lifetime attribute already inherited from a base class.

**Fix:** Remove the redundant lifetime attribute or change to a different lifetime if intended.

**Related:** [IOC015](#ioc015) (lifetime mismatch in inheritance chain), [IOC059](#ioc059) (redundant Singleton), [IOC060](#ioc060) (redundant Transient)

---

### IOC087

**Severity:** [!Error](#) | **Category:** IoCTools.Lifetime

**Cause:** A Transient service depends on a Scoped service. Transient services resolved from the root scope cannot depend on Scoped services.

**Fix:** Change the dependency to `[Singleton]` or `[Transient]`, change this service to `[Scoped]`, inject `IServiceProvider` and call `CreateScope()`, or use a factory delegate.

**Related:** [IOC012](#ioc012) (Singleton depends on Scoped), [IOC013](#ioc013) (Singleton depends on Transient)

---

## Configuration Diagnostics

Diagnostics related to configuration binding and injection.

### IOC016

**Severity:** [!Error](#) | **Category:** IoCTools.Configuration

**Cause:** A configuration key is invalid (empty, whitespace-only, or contains invalid characters like double colons).

**Fix:** Provide a valid configuration key. Example valid keys: `ConnectionStrings:Default`, `App:Settings:Feature`.

---

### IOC017

**Severity:** [!Warning](#) | **Category:** IoCTools.Configuration

**Cause:** The configuration type cannot be bound from configuration.

**Fix:** Use a supported type: primitives (`string`, `int`, `bool`, `double`), POCOs with parameterless constructors, or collections (`List<T>`, `Dictionary<string, T>`).

---

### IOC018

**Severity:** [!Error](#) | **Category:** IoCTools.Configuration

**Cause:** `[InjectConfiguration]` is used on a class that is not marked as `partial`.

**Fix:** Add `partial` modifier to the class declaration to enable configuration injection constructor generation.

---

### IOC019

**Severity:** [!Warning](#) | **Category:** IoCTools.Configuration

**Cause:** `[InjectConfiguration]` is applied to a static field, which is not supported.

**Fix:** Remove `[InjectConfiguration]` from the static field. Change to an instance field instead.

---

### IOC046

**Severity:** [!Warning](#) | **Category:** IoCTools.Configuration

**Cause:** The same configuration section is bound in multiple ways (e.g., as options and per-field), creating duplicate sources.

**Fix:** Bind each configuration section exactly once. Avoid mixing options bindings with per-field configuration.

**Related:** [IOC056](#ioc056) (mixed binding styles)

---

### IOC056

**Severity:** [!Info](#) | **Category:** IoCTools.Configuration

**Cause:** A configuration section is bound to both an options type and primitive values.

**Fix:** Use a single binding style per section: either the options object or direct primitives, not both.

**Related:** [IOC046](#ioc046) (duplicate bindings)

---

### IOC057

**Severity:** [!Warning](#) | **Category:** IoCTools.Configuration

**Cause:** A configuration section referenced by an options type is not bound in the project.

**Fix:** Add `Configure<T>()`, `AddOptions<T>().Bind...`, or implement `IConfigureOptions<T>`.

---

### IOC079

**Severity:** [!Warning](#) | **Category:** IoCTools.Configuration

**Cause:** A class depends on `IConfiguration` directly instead of using typed configuration.

**Fix:** Use `[DependsOnConfiguration<T>]` or typed options classes instead of raw `IConfiguration`.

---

### IOC088

**Severity:** [!Error](#) | **Category:** IoCTools.Configuration

**Cause:** A configuration type has a circular reference through a property, causing infinite recursion during binding.

**Fix:** Break the cycle by removing the self-referencing property or using a different configuration structure.

---

### IOC089

**Severity:** [!Warning](#) | **Category:** IoCTools.Configuration

**Cause:** `SupportsReloading=true` is used on a primitive type field, but it only works with Options pattern types.

**Fix:** Remove `SupportsReloading=true` from primitive fields. Use `IOptionsSnapshot<T>` with a complex options type for reloadable configuration.

---

## Registration Diagnostics

Diagnostics related to service registration patterns and attributes.

### IOC004

**Severity:** [!Error](#) | **Category:** IoCTools.Registration

**Cause:** `[RegisterAsAll]` is used on a class without a lifetime attribute.

**Fix:** Add `[Scoped]`, `[Singleton]`, or `[Transient]` to the class.

**Related:** [IOC028](#ioc028) (RegisterAs without service indicators), [IOC069](#ioc069) (RegisterAs without lifetime)

---

### IOC005

**Severity:** [!Warning](#) | **Category:** IoCTools.Registration

**Cause:** `[SkipRegistration]` is used without `[RegisterAsAll]`, making it ineffective.

**Fix:** Add `[RegisterAsAll]` to make `[SkipRegistration]` meaningful, or remove the unnecessary `[SkipRegistration]`.

---

### IOC027

**Severity:** [!Info](#) | **Category:** IoCTools.Registration

**Cause:** A service may be registered multiple times due to inheritance or attribute combinations.

**Fix:** Review service registration patterns to ensure no unintended duplicates.

---

### IOC028

**Severity:** [!Error](#) | **Category:** IoCTools.Registration

**Cause:** `[RegisterAs]` is used without service indicators like a lifetime attribute, `[Inject]` fields, or other registration attributes.

**Fix:** Add a lifetime attribute or other service indicators to enable selective interface registration.

**Related:** [IOC004](#ioc004) (RegisterAsAll without lifetime), [IOC069](#ioc069) (RegisterAs without lifetime)

---

### IOC029

**Severity:** [!Error](#) | **Category:** IoCTools.Registration

**Cause:** `[RegisterAs]` specifies an interface that the class does not implement.

**Fix:** Ensure all interfaces specified in `[RegisterAs]` are actually implemented by the class.

---

### IOC030

**Severity:** [!Warning](#) | **Category:** IoCTools.Registration

**Cause:** `[RegisterAs]` contains a duplicate interface specification.

**Fix:** Remove duplicate interface specifications from the `[RegisterAs]` attribute.

---

### IOC031

**Severity:** [!Error](#) | **Category:** IoCTools.Registration

**Cause:** `[RegisterAs]` specifies a non-interface type.

**Fix:** `[RegisterAs]` can only specify interface types. Use concrete class types for direct registration.

---

### IOC032

**Severity:** [!Warning](#) | **Category:** IoCTools.Registration

**Cause:** `[RegisterAs]` is redundant because the class already registers the specified interfaces by default.

**Fix:** Remove the redundant `[RegisterAs]` attribute or reduce the interface list.

---

### IOC034

**Severity:** [!Warning](#) | **Category:** IoCTools.Registration

**Cause:** Both `[RegisterAsAll]` and `[RegisterAs]` are used; `[RegisterAs]` has no effect when `[RegisterAsAll]` is present.

**Fix:** Remove `[RegisterAs]` attributes or drop `[RegisterAsAll]` if selective registration is needed.

---

### IOC035

**Severity:** [!Warning](#) | **Category:** IoCTools.Registration

**Cause:** An `[Inject]` field matches the default naming for `[DependsOn]`, making it unnecessarily verbose.

**Fix:** Replace the `[Inject]` field with `[DependsOn<T>]`. `[Inject]` is deprecated in 1.6.0 (fires [IOC095](#ioc095)); the Roslyn code fix and `ioc-tools migrate-inject` CLI migrate in bulk.

**Related:** [IOC007](#ioc007) (DependsOn conflicts with Inject)

---

### IOC036

**Severity:** [!Warning](#) | **Category:** IoCTools.Registration

**Cause:** A class has multiple lifetime attributes, creating conflicting registrations.

**Fix:** Remove redundant lifetime attributes so only one of `[Scoped]`, `[Singleton]`, or `[Transient]` remains.

---

### IOC037

**Severity:** [!Warning](#) | **Category:** IoCTools.Registration

**Cause:** `[SkipRegistration]` is used alongside other registration attributes, preventing them from taking effect.

**Fix:** Remove redundant registration attributes or drop `[SkipRegistration]`.

---

### IOC038

**Severity:** [!Warning](#) | **Category:** IoCTools.Registration

**Cause:** `[SkipRegistration]` for interfaces has no effect when `[RegisterAsAll]` is set to DirectOnly mode.

**Fix:** Change `[RegisterAsAll]` to `RegistrationMode.All`/`Exclusionary` or remove the ineffective `[SkipRegistration]`.

---

### IOC047

**Severity:** [!Info](#) | **Category:** IoCTools.Registration

**Cause:** An attribute uses a named argument where a params-style constructor argument would be cleaner.

**Fix:** Use params-style constructor arguments for `[DependsOn]` member names and `[DependsOnConfiguration]` keys.

---

### IOC063

**Severity:** [!Warning](#) | **Category:** IoCTools.Registration

**Cause:** A derived class repeats `[RegisterAs]` interfaces already inherited from a base class.

**Fix:** Remove the redundant `[RegisterAs]` on the derived class.

**Related:** [IOC064](#ioc064) (move RegisterAs to base)

---

### IOC064

**Severity:** [!Info](#) | **Category:** IoCTools.Registration

**Cause:** Multiple derived classes repeat the same `[RegisterAs]` interfaces.

**Fix:** Move `[RegisterAs]` to the shared base class to reduce duplication.

**Related:** [IOC063](#ioc063) (redundant RegisterAs in derived)

---

### IOC065

**Severity:** [!Warning](#) | **Category:** IoCTools.Registration

**Cause:** A derived class repeats `[RegisterAsAll]` already inherited from a base class.

**Fix:** Remove the redundant `[RegisterAsAll]` on the derived class.

---

### IOC069

**Severity:** [!Warning](#) | **Category:** IoCTools.Registration

**Cause:** `[RegisterAs]` is used without a lifetime attribute.

**Fix:** Add `[Scoped]`, `[Singleton]`, or `[Transient]` to the class.

**Related:** [IOC004](#ioc004) (RegisterAsAll without lifetime), [IOC028](#ioc028) (RegisterAs without service indicators)

---

### IOC070

**Severity:** [!Warning](#) | **Category:** IoCTools.Registration

**Cause:** `[DependsOn]` or `[Inject]` is used without a lifetime attribute.

**Fix:** Add `[Scoped]`, `[Singleton]`, or `[Transient]` so the class will be registered and validated.

**Related:** [IOC071](#ioc071) (ConditionalService without lifetime)

---

### IOC071

**Severity:** [!Warning](#) | **Category:** IoCTools.Registration

**Cause:** `[ConditionalService]` is used without a lifetime attribute.

**Fix:** Add `[Scoped]`, `[Singleton]`, or `[Transient]` to enable registration.

**Related:** [IOC070](#ioc070) (DependsOn without lifetime)

---

### IOC074

**Severity:** [!Info](#) | **Category:** IoCTools.Registration

**Cause:** A class implements multiple interfaces but only has a lifetime attribute without `[RegisterAsAll]`.

**Fix:** Add `[RegisterAsAll]` to register all interfaces automatically.

---

### IOC081

**Severity:** [!Warning](#) | **Category:** IoCTools.Registration

**Cause:** A service is manually registered with the same lifetime that IoCTools already generates.

**Fix:** Remove the manual registration and rely on IoCTools attributes.

**Related:** [IOC082](#ioc082) (manual registration lifetime differs), [IOC091](#ioc091) (typeof() duplicate registration)

---

### IOC082

**Severity:** [!Error](#) | **Category:** IoCTools.Registration

**Cause:** A service is manually registered with a different lifetime than what IoCTools generates.

**Fix:** Align lifetimes or remove the manual registration.

**Related:** [IOC081](#ioc081) (duplicate registration), [IOC092](#ioc092) (typeof() lifetime mismatch)

---

### IOC083

**Severity:** [!Error](#) | **Category:** IoCTools.Registration

**Cause:** An options type is manually bound via `AddOptions`/`Configure`, but IoCTools already binds it.

**Fix:** Remove the manual binding and rely on generated options registration.

---

### IOC085

**Severity:** [!Warning](#) | **Category:** IoCTools.Registration

**Cause:** A member name matches the generator's default, making the explicit name redundant.

**Fix:** Omit the memberNames value to reduce redundancy.

---

### IOC086

**Severity:** [!Warning](#) | **Category:** IoCTools.Registration

**Cause:** A service is manually registered but the implementation lacks IoCTools attributes.

**Fix:** Add `[Scoped]`/`[Singleton]`/`[Transient]` to the implementation, with `[RegisterAs]` as needed to preserve the service mapping.
Replace the manual registration with the generated `Add<SanitizedAssemblyName>RegisteredServices(...)` extension call before you build the service provider.
Use the extension from the implementation assembly, and pass configuration if its generated signature requires it.
Attributes alone do not register services. Without the extension call, the code can compile while `GetRequiredService<T>()` throws `InvalidOperationException`.

See the [complete migration example](migration.md#ioc086-complete-migration) for the namespace, method name, and call order.

**Related:** [IOC090](#ioc090) (typeof() without attributes)

---

### IOC090

**Severity:** [!Warning](#) | **Category:** IoCTools.Registration

**Cause:** A service is registered via `typeof()` but the implementation lacks IoCTools attributes.

**Fix:** Add `[Scoped]`, `[Singleton]`, or `[Transient]` (and `[RegisterAs]` if needed) instead of using `typeof()`.

**Example:**
```csharp
// Before (manual typeof registration):
services.AddScoped(typeof(IUserService), typeof(UserService));

// After (with IoCTools attributes):
[Scoped]
[RegisterAs<IUserService>]
public partial class UserService : IUserService { }
```

**Related:** [IOC091](#ioc091) (duplicate registration), [IOC092](#ioc092) (lifetime mismatch), [IOC086](#ioc086) (manual registration without attributes)

---

### IOC091

**Severity:** [!Warning](#) | **Category:** IoCTools.Registration

**Cause:** A service is registered via `typeof()` with the same lifetime that IoCTools already generates.

**Fix:** Remove the `typeof()` registration and rely on IoCTools attributes.

**Example:**
```csharp
// Manual registration duplicates IoCTools:
services.AddScoped(typeof(IUserService), typeof(UserService)); // IOC091

// Remove manual registration - IoCTools handles it
```

**Related:** [IOC081](#ioc081) (manual registration duplicates IoCTools), [IOC090](#ioc090) (typeof() without attributes)

---

### IOC092

**Severity:** [!Error](#) | **Category:** IoCTools.Registration

**Cause:** A service is registered via `typeof()` with a different lifetime than what IoCTools generates.

**Fix:** Align lifetimes or remove the `typeof()` registration.

**Example:**
```csharp
// IoCTools registers as Scoped:
[Scoped]
public partial class UserService : IUserService { }

// Manual Singleton registration causes mismatch:
services.AddSingleton(typeof(IUserService), typeof(UserService)); // IOC092

// Fix: Remove manual registration or change lifetime attribute
```

**Related:** [IOC082](#ioc082) (manual registration lifetime differs), [IOC012](#ioc012) (lifetime validation), [IOC090](#ioc090) (typeof() without attributes)

---

### IOC094

**Severity:** [!Info](#) | **Category:** IoCTools.Registration

**Cause:** An open generic is registered via `typeof()` even though the common mapping can be expressed through IoCTools attributes on the generic implementation.

**Fix:** The common open-generic path is natively supported. Prefer expressing the registration through IoCTools attributes on the generic implementation so generated registrations and diagnostics stay aligned. If the manual mapping is intentionally outside current IoCTools intent, this diagnostic remains informational.

**Example:**
```csharp
// Informational - prefer expressing the mapping through IoCTools intent:
services.AddScoped(typeof(IRepository<>), typeof(Repository<>)); // IOC094

// Preferred:
[Scoped]
[RegisterAsAll]
public partial class Repository<T> : IRepository<T> where T : class { }
```

---

### IOC095

> **Note.** The IOC095 ID ships with **two descriptors** in 1.6.0+. The
> originally-assigned open-generic fallback descriptor (below) remains
> registered so existing suppressions continue to apply. The new primary
> meaning — `[Inject]` deprecation — is described in the
> [Auto-Deps Diagnostics](#auto-deps-diagnostics-1-6-0) section below.
> Consumers with pre-1.6 `IOC095` suppressions should review what their
> suppression now covers.

**Severity:** [!Warning](#) | **Category:** IoCTools.Registration

**Cause:** An open generic requests `InstanceSharing.Shared` for interface aliases, but `Microsoft.Extensions.DependencyInjection` does not support open generic implementation factories.

**Fix:** Use `InstanceSharing.Separate` for open generic interface aliases, or wire the shared behavior manually in a container that supports open generic factory aliases. IoCTools falls back to separate registrations so the generated code remains valid.

**Example:**
```csharp
[Scoped]
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)] // IOC095
public partial class Repository<T> : IRepository<T>, ILookup<T> where T : class { }

// Generated registrations fall back to direct open generic mappings:
services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
services.AddScoped(typeof(ILookup<>), typeof(Repository<>));
```

---

## Auto-Deps Diagnostics (1.6.0+)

The auto-deps feature introduced in 1.6.0 ships diagnostics IOC095-IOC099
and IOC103-IOC108. (IOC100-IOC102 remained assigned to
`IoCTools.FluentValidation` from 1.5.1; the three `AutoDepOpen`-validation
diagnostics that were originally planned for those IDs were re-numbered to
IOC106-IOC108 before the 1.6.0 release to avoid a suppression collision.
IOC103/IOC104 are therefore owned by IoCTools.AutoDeps — the FluentValidation
fail-loud infrastructure diagnostics use IOC111/IOC112 to avoid collision.)
For concept documentation see [docs/auto-deps.md](auto-deps.md); every
descriptor's `HelpLinkUri` points at the `#iocXXX` anchor on that page.

<a id="ioc095-inject-deprecated"></a>
### IOC095 (primary, 1.6.0+) — `[Inject]` is deprecated

**Severity:** Warning (1.6) → Error (1.7) → Removed (2.0) | **Category:** IoCTools.AutoDeps

**Cause:** The field uses `[Inject]`, which is deprecated in favor of
`[DependsOn<T>]`. Ships with a Roslyn code fix (IDE) and the
`ioc-tools migrate-inject` CLI (headless) for bulk conversion.

**Fix:** Accept the code-fix light-bulb, run `ioc-tools migrate-inject` at
the solution root, or modulate severity during migration via
`<IoCToolsInjectDeprecationSeverity>Info</IoCToolsInjectDeprecationSeverity>`.
See the [1.5.x → 1.6.x migration guide](migration.md#migrating-from-15x-to-16x).

<a id="ioc096"></a>
### IOC096 — Stale opt-out

**Severity:** Info | **Category:** IoCTools.AutoDeps

**Cause:** `[NoAutoDep<T>]` references a type not in the service's resolved
auto-dep set, or `[NoAutoDepOpen(typeof(T<>))]` references an open-generic
shape with no matching auto-dep derivation for this service.

**Fix:** Remove the stale opt-out, or fix the typo in the referenced type.

<a id="ioc097"></a>
### IOC097 — Profile missing `IAutoDepsProfile` marker

**Severity:** Warning | **Category:** IoCTools.AutoDeps

**Cause:** `AutoDepIn<TProfile, T>`, `AutoDepsApply<TProfile, TBase>`,
`AutoDepsApplyGlob<TProfile>`, or `[AutoDeps<TProfile>]` was given a
`TProfile` that does not implement `IAutoDepsProfile`.

**Fix:** Add the marker interface:

```csharp
public sealed class ControllerDefaults : IAutoDepsProfile { }
```

<a id="ioc098"></a>
### IOC098 — `[DependsOn<T>]` redundant with auto-dep

**Severity:** Info | **Category:** IoCTools.AutoDeps

**Cause:** The service's `[DependsOn<T>]` covers a type also supplied by an
auto-dep (built-in detection, local universal, transitive, or
profile-sourced). The generated constructor is identical either way. The
message names the auto-dep's source (`auto-builtin:ILogger`,
`auto-universal`, `auto-transitive:<AssemblyName>`, or
`auto-profile:<Name>`).

**Fix:** Remove the redundant `[DependsOn<T>]`, or — if you want explicit
customization (`memberName*` or `external: true`) — keep it and ignore the
info (explicit always wins).

<a id="ioc099"></a>
### IOC099 — Profile attachment matches zero services

**Severity:** Info | **Category:** IoCTools.AutoDeps

**Cause:** `AutoDepsApply<TProfile, TBase>` or `AutoDepsApplyGlob<TProfile>`
matches no services in the assembly. Often a stale rule or a typo in the
glob pattern.

**Fix:** Remove the unused rule, fix the glob pattern, or confirm the base
class match still holds.

<a id="ioc106"></a>
### IOC106 — `AutoDepOpen` on multi-arity generic

**Severity:** Error | **Category:** IoCTools.AutoDeps

**Cause:** `AutoDepOpen` was given a multi-arity unbound generic like
`typeof(IFoo<,>)`. No universal "close with self" convention exists for
multi-arity generics.

**Fix:** Use `AutoDep<T>` with an explicitly closed type, or redesign the
dependency so the service-type parameter is a single unbound.

<a id="ioc107"></a>
### IOC107 — `AutoDepOpen` on non-generic

**Severity:** Error | **Category:** IoCTools.AutoDeps

**Cause:** `AutoDepOpen` was given a non-generic type.

**Fix:** Use `AutoDep<T>` for closed types.

<a id="ioc108"></a>
### IOC108 — `AutoDepOpen` closure violates constraints

**Severity:** Error | **Category:** IoCTools.AutoDeps

**Cause:** Closing the unbound generic over a matching service's concrete
type would violate the unbound's type-parameter constraints. The primary
location is the service declaration where codegen would fail; the
secondary location is the `AutoDepOpen` assembly attribute.

**Fix:** Exclude the service from the auto-dep via `[NoAutoDepOpen(...)]`,
or relax the unbound's constraints.

<a id="ioc103"></a>
### IOC103 — Invalid glob pattern

**Severity:** Error | **Category:** IoCTools.AutoDeps

**Cause:** `AutoDepsApplyGlob<TProfile>` was given an invalid pattern —
empty string, unterminated character class, or unsupported metacharacter.
The validator uses the same grammar as `IoCToolsIgnoredTypePatterns` and
`IoCToolsSkipAssignableTypes*`.

**Fix:** Fix the pattern. Simple leading/trailing wildcards (`*.Foo.*`) are
always safe.

<a id="ioc104"></a>
### IOC104 — Profile type is generic

**Severity:** Error | **Category:** IoCTools.AutoDeps

**Cause:** A profile type used as `TProfile` in `AutoDepIn`,
`AutoDepsApply`, `AutoDepsApplyGlob`, or `AutoDeps` is generic. Profiles
must be non-generic in 1.6.

**Fix:** Make the profile class non-generic. If domain-specific typing is
needed, declare per-domain non-generic profiles
(`OrderProfile`, `ReportProfile`, …) rather than one generic profile.

<a id="ioc105"></a>
### IOC105 — Redundant profile attachment

**Severity:** Info | **Category:** IoCTools.AutoDeps

**Cause:** A service is attached to the same profile via more than one path
(e.g., `AutoDepsApplyGlob` match plus explicit `[AutoDeps<TProfile>]` on
the class). The attachment is deduped silently.

**Fix:** Remove one of the redundant attachment declarations so the
attachment is expressed once.

---

## Structural Diagnostics

Diagnostics related to code structure, partial class requirements, and attribute usage patterns.

### IOC010

**Severity:** [!Warning](#) | **Category:** IoCTools.Structural

**Cause:** Background service has a non-Singleton lifetime (deprecated diagnostic).

**Fix:** Use IOC014 instead. This diagnostic has been consolidated into IOC014.

**Related:** [IOC014](#ioc014) (background service lifetime validation)

---

### IOC011

**Severity:** [!Error](#) | **Category:** IoCTools.Structural

**Cause:** A background service class inherits from `BackgroundService` but is not marked as `partial`.

**Fix:** Add `partial` modifier to the class declaration.

**Related:** [IOC080](#ioc080) (partial class requirement)

---

### IOC020

**Severity:** [!Warning](#) | **Category:** IoCTools.Structural

**Cause:** A conditional service has conflicting conditions (overlapping Environment/NotEnvironment or contradictory Equals/NotEquals).

**Fix:** Ensure conditions do not overlap or contradict each other.

---

### IOC021

**Severity:** [!Error](#) | **Category:** IoCTools.Structural

**Cause:** `[ConditionalService]` is used without a lifetime attribute.

**Fix:** Add `[Scoped]`, `[Singleton]`, or `[Transient]` to the class.

**Related:** [IOC071](#ioc071) (ConditionalService without lifetime)

---

### IOC022

**Severity:** [!Warning](#) | **Category:** IoCTools.Structural

**Cause:** `[ConditionalService]` is used without specifying any conditions.

**Fix:** Specify at least one Environment, NotEnvironment, or ConfigValue condition.

---

### IOC023

**Severity:** [!Warning](#) | **Category:** IoCTools.Structural

**Cause:** ConfigValue is specified in `[ConditionalService]` but no Equals or NotEquals comparison is provided.

**Fix:** Add an Equals or NotEquals condition when using ConfigValue.

---

### IOC024

**Severity:** [!Warning](#) | **Category:** IoCTools.Structural

**Cause:** Equals or NotEquals is specified in `[ConditionalService]` but ConfigValue is missing.

**Fix:** Specify the ConfigValue property to define which configuration key to check.

---

### IOC025

**Severity:** [!Warning](#) | **Category:** IoCTools.Structural

**Cause:** ConfigValue in `[ConditionalService]` is empty or whitespace-only.

**Fix:** Provide a valid configuration key path for ConfigValue.

---

### IOC026

**Severity:** [!Warning](#) | **Category:** IoCTools.Structural

**Cause:** Multiple `[ConditionalService]` attributes on the same class may lead to unexpected behavior.

**Fix:** Combine conditions into a single attribute or use separate classes for different conditions.

---

### IOC058

**Severity:** [!Info](#) | **Category:** IoCTools.Structural

**Cause:** Multiple services deriving from the same base class lack lifetime attributes.

**Fix:** Add a single lifetime attribute to the shared base class to register all derived services in one place.

---

### IOC067

**Severity:** [!Warning](#) | **Category:** IoCTools.Structural

**Cause:** A derived class repeats `[ConditionalService]` with the same condition as the base class.

**Fix:** Remove the redundant attribute or change the condition if a different predicate is needed.

---

### IOC068

**Severity:** [!Info](#) | **Category:** IoCTools.Structural

**Cause:** A class has a manual constructor with injectable parameters but no IoCTools attributes.

**Fix:** Add a lifetime attribute and `[DependsOn<T>]` to opt into IoCTools generator support.

---

### IOC093

**Severity:** [!Error](#) | **Category:** IoCTools.Structural

**Cause:** IoCTools could not fully analyze a service type or constructor input and skipped the affected generation path to avoid emitting incomplete output.

**Fix:** Resolve the underlying symbol or semantic-model issue, or report a bug if the source is valid. This diagnostic is emitted when generator analysis fails instead of silently degrading.

**Related:** [IOC092](#ioc092) (manual registration mismatch), [IOC094](#ioc094) (open generic typeof() registration)

---

### IOC077

**Severity:** [!Error](#) | **Category:** IoCTools.Dependency

**Cause:** A manually declared field has the same name as an IoCTools-generated dependency field.

**Fix:** Remove the manual field and rely on `[DependsOn]`/`[DependsOnConfiguration]`. Do not add new `[Inject]` fields as a workaround.

**Related:** [IOC078](#ioc078) (MemberNames collision)

---

### IOC080

**Severity:** [!Error](#) | **Category:** IoCTools.Structural

**Cause:** A class uses IoCTools attributes that require code generation but is not marked as `partial`.

**Fix:** Add `partial` modifier to the class declaration.

**Related:** [IOC011](#ioc011) (background service partial requirement)

---

## Testing Diagnostics

Diagnostics from the IoCTools.Testing package for test fixture generation.

### TDIAG01

**Severity:** [!Info](#) | **Category:** IoCTools.Testing

**Cause:** Test class has a manual `Mock<T>` field that duplicates a generated fixture member for the same dependency type. Only reported when the mocked type matches a constructor parameter of the covered service.

**Fix:** Remove the manual mock field declaration and use the auto-generated `_mockFieldName` field from the fixture.

**Related:** [TDIAG02](#tdiag02) (manual SUT construction), [TDIAG03](#tdiag03) (missing Cover attribute)

---

### TDIAG02

**Severity:** [!Info](#) | **Category:** IoCTools.Testing

**Cause:** Test class manually constructs the service using `new Service(mock1.Object, ...)` but `CreateSut()` is auto-generated.

**Fix:** Replace manual construction with a call to the auto-generated `CreateSut()` method.

**Example:**
```csharp
// Before (manual):
var sut = new UserService(_mockUserRepository.Object, _mockLogger.Object);

// After (use generated):
var sut = CreateSut();
```

**Related:** [TDIAG01](#tdiag01) (manual mock fields), [TDIAG03](#tdiag03) (missing Cover attribute)

---

### TDIAG03

**Severity:** [!Info](#) | **Category:** IoCTools.Testing

**Cause:** Test class has manual `Mock<T>` fields that match a service's constructor dependencies.

**Fix:** Add `[Cover<TService>]` attribute to the test class (must be `partial`) to auto-generate fixture.

**Example:**
```csharp
// Before:
public class UserServiceTests // Not partial
{
    private readonly Mock<IUserRepository> _mockRepo = new();
    private readonly Mock<ILogger<UserService>> _mockLogger = new();
}

// After:
[Cover<UserService>]
public partial class UserServiceTests // Added Cover<T> and partial
{
    // Mocks, CreateSut(), and helpers auto-generated
}
```

**Related:** [TDIAG01](#tdiag01) (manual mock with Cover), [TDIAG05](#tdiag05) (missing partial modifier)

---

### TDIAG04

**Severity:** [!Error](#) | **Category:** IoCTools.Testing

**Cause:** Service referenced in `[Cover<T>]` has no generated constructor (not partial, missing lifetime attribute, or missing dependency declarations).

**Fix:** Mark the service class as `partial`, add a lifetime attribute (`[Scoped]`, `[Singleton]`, `[Transient]`), and add `[DependsOn<T>]` attributes for dependencies.

This diagnostic is emitted at compile time by the TestFixtureAnalyzer.

**Related:** [TDIAG05](#tdiag05) (test class not partial), [IOC080](#ioc080) (partial class requirement)

---

### TDIAG05

**Severity:** [!Error](#) | **Category:** IoCTools.Testing

**Cause:** Test class uses `[Cover<T>]` but is not marked `partial`, preventing fixture generation.

**Fix:** Add the `partial` modifier to the test class declaration.

**Example:**
```csharp
// Before:
[Cover<UserService>]
public class UserServiceTests // Not partial
{
}

// After:
[Cover<UserService>]
public partial class UserServiceTests // Added partial
{
}
```

**Related:** [TDIAG04](#tdiag04) (service not partial), [IOC080](#ioc080) (partial class requirement)

---

### TDIAG06

**Severity:** [!Warning](#) | **Category:** IoCTools.Testing

**Cause:** Generated fixture member names collide or become ambiguous when multiple constructor
parameters share similar type names.

**Fix:** Rename types or add descriptive namespaces to disambiguate the generated fixture members.

**Example:**
```csharp
// Issue: Both parameters resolve to similar field names
public class ReportService(ISettings settings, ISettingsCache settingsCache) { }
// Generated fields: _mockSettings, _mockSettings (collision!)

// Fix: Use more specific type names or namespaces
public class ReportService(ISettingsCache settingsCache, IReportSettings reportSettings) { }
```

**Related:** [TDIAG01](#tdiag01) (manual mock fields with Cover)

---

### TDIAG07

**Severity:** [!Warning](#) | **Category:** IoCTools.Testing

**Cause:** A generated fixture helper (Setup*, Configure*, Use*) is called in a method after the
`Sut` property has been accessed. Since `Sut` triggers lazy construction via `CreateSut()`,
helpers called afterward have no effect on the already-constructed service.

**Fix:** Move fixture helper calls before the first access to the `Sut` property. Arrange-phase
code should configure all mocks before accessing the SUT.

**Example:**
```csharp
// Before (setup after Sut — TDIAG07):
[Fact]
public void Test()
{
    var result = Sut.DoSomething();  // Sut accessed here — lazy CreateSut() fires
    SetupRepository(m => m.Setup(r => r.GetById(1)).Returns(user)); // TOO LATE
}

// After (setup before Sut):
[Fact]
public void Test()
{
    SetupRepository(m => m.Setup(r => r.GetById(1)).Returns(user));
    var result = Sut.DoSomething();
}
```

**Related:** [TDIAG02](#tdiag02) (manual SUT construction), [TDIAG05](#tdiag05) (missing partial)

---

### TDIAG08

**Severity:** [!Info](#) | **Category:** IoCTools.Testing

**Cause:** A test class manually constructs an IoCTools-managed service using `new ServiceType(...)`
but does not use `[Cover<T>]`. The service has source-generator constructor metadata available,
meaning the fixture generator could produce mock fields, `CreateSut()`, and setup helpers.

**Note:** TDIAG08 is advisory — legitimate manual construction exists (e.g. surface tests with bespoke
stubs where `[Cover<T>]` would defeat the test's purpose). Severity is `Info` and will not block
compilation even in projects with `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`. Consumers
who want stricter enforcement can set `<IoCToolsTestingDiagnosticSeverity>Warning</IoCToolsTestingDiagnosticSeverity>`
in their project to escalate TDIAG08 to Warning (and thus to Error when TreatWarningsAsErrors is enabled).

**Fix:** Add `[Cover<ServiceType>]` to the test class (must be `partial`) to auto-generate the fixture
and eliminate manual mock/constructor boilerplate.

**Example:**
```csharp
// Before (manual construction — TDIAG08):
public class UserServiceTests
{
    [Fact]
    public void Test()
    {
        var repo = new Mock<IUserRepository>();
        var sut = new UserService(repo.Object);
        // ...
    }
}

// After (using generated fixture):
[Cover<UserService>]
public partial class UserServiceTests
{
    [Fact]
    public void Test()
    {
        SetupUserRepository(m => m.Setup(r => r.GetById(1)).Returns(user));
        var result = Sut.DoSomething();
    }
}
```

**Related:** [TDIAG03](#tdiag03) (Mock<T> fields without Cover), [TDIAG04](#tdiag04) (Cover<T> service check)

---

### TDIAG09

**Severity:** [!Warning](#) | **Category:** IoCTools.Testing

**Cause:** A test class uses `[Cover<T>(ConcreteHandling = ConcreteHandling.ForceMock)]`, and one of
the service's constructor dependencies is a **concrete class whose public instance methods are all
non-virtual** (the C# default). `ForceMock` suppresses the auto-concrete promotion and emits a
`Mock<TConcrete>`, but Moq can only intercept `virtual`/`abstract` instance methods. `Setup(...)`
against a non-virtual method **compiles but silently no-ops** — the real method body runs against
default backing fields, so the test believes it has stubbed a collaborator when it has not.

**Fix:** Either extract an interface from the concrete dependency and depend on the interface (with
the default `ConcreteHandling.Auto`), or mark the relevant methods `virtual` so Moq can intercept
them. For a pure POCO/record whose surface is properties only, prefer `ConcreteHandling.Auto`, which
constructs a real instance via a `Configure<T>` helper instead of an inert mock.

**Example:**
```csharp
// Before (ForceMock against a sealed-by-default concrete — TDIAG09):
public partial class Collaborator
{
    public int Compute(int x) => x + 1;   // non-virtual: Moq cannot intercept
}

[DependsOn<Collaborator>]
public partial class OrderService { }

[Cover<OrderService>(ConcreteHandling = ConcreteHandling.ForceMock)]
public partial class OrderServiceTests { }   // _mockCollaborator.Setup(...) no-ops

// After (extract an interface, default Auto):
public interface ICollaborator { int Compute(int x); }

public partial class Collaborator : ICollaborator
{
    public int Compute(int x) => x + 1;
}

[DependsOn<ICollaborator>]
public partial class OrderService { }

[Cover<OrderService>]
public partial class OrderServiceTests { }   // _mockCollaborator works
```

**Related:** [TDIAG04](#tdiag04) (Cover<T> service check). See also
[Concrete Handling Modes](testing.md#concrete-handling-modes-concretehandling) in the testing guide.

---

## FluentValidation Diagnostics

Diagnostics for FluentValidation validator composition, lifetime management, and structural requirements.

### IOC100

**Severity:** [!Warning](#) | **Category:** IoCTools.FluentValidation

**Cause:** A validator directly instantiates a DI-managed child validator using `new`, bypassing dependency injection. The child validator's own dependencies won't be resolved.

**Fix:** Inject the child validator via `[DependsOn<AddressValidator>]` and use `OnConstructed()` partial method to wire it with `SetValidator()`.

**Example:**
```csharp
// Before (direct instantiation bypasses DI):
[Scoped]
public partial class OrderValidator : AbstractValidator<Order>
{
    public OrderValidator()
    {
        RuleFor(o => o.Address)
            .SetValidator(new AddressValidator()); // IOC100
    }
}

// After (injected via constructor):
[Scoped]
[DependsOn<AddressValidator>]
public partial class OrderValidator : AbstractValidator<Order>
{
    partial void OnConstructed()
    {
        RuleFor(o => o.Address)
            .SetValidator(_addressValidator);
    }
}
```

**Related:** [IOC101](#ioc101) (lifetime mismatch), [IOC102](#ioc102) (missing partial), [IOC080](#ioc080) (partial class requirement)

---

### IOC101

**Severity:** [!Warning](#) | **Category:** IoCTools.FluentValidation

**Cause:** A validator composition creates a captive dependency — a parent validator with a longer lifetime captures a child validator with a shorter lifetime. For example, a `[Singleton]` parent composing a `[Scoped]` child.

**Fix:** Align lifetimes so the parent's lifetime is equal to or shorter than the child's. Typically, make the parent `[Scoped]` to match.

**Example:**
```csharp
// Before (Singleton captures Scoped child):
[Singleton]
[DependsOn<AddressValidator>]
public partial class OrderValidator : AbstractValidator<Order>
{
}

// After (matching lifetimes):
[Scoped]
[DependsOn<AddressValidator>]
public partial class OrderValidator : AbstractValidator<Order>
{
}
```

**Related:** [IOC100](#ioc100) (direct instantiation), [IOC012](#ioc012) (service lifetime mismatch), [IOC015](#ioc015) (inherited lifetime conflict)

---

### IOC102

**Severity:** [!Error](#) | **Category:** IoCTools.FluentValidation

**Cause:** A validator class extends `AbstractValidator<T>` and has IoCTools attributes (`[Scoped]`, `[DependsOn]`, compatibility-only `[Inject]`, etc.) but is not marked `partial`. Constructor generation requires the `partial` modifier.

**Fix:** Add the `partial` modifier to the validator class declaration.

**Example:**
```csharp
// Before (missing partial):
[Scoped]
[DependsOn<IOrderRepository>]
public class OrderValidator : AbstractValidator<Order> // IOC102
{
}

// After (added partial):
[Scoped]
[DependsOn<IOrderRepository>]
public partial class OrderValidator : AbstractValidator<Order>
{
}
```

**Related:** [IOC080](#ioc080) (partial class requirement for services), [IOC100](#ioc100) (direct instantiation)

---

### IOC111

**Severity:** [!Error](#) | **Category:** IoCTools.FluentValidation

**Cause:** `CompositionGraphBuilder` encountered an unexpected exception while walking a validator's syntax tree to build composition edges. One or more edges may be missing, which can cause false-negative results for [IOC100](#ioc100) and [IOC101](#ioc101).

This is a generator bug — the exception message is included in the diagnostic to aid reporting.

**Note on severity:** Raised as `Error` rather than `Warning` because missing edges silently suppress real IOC100/IOC101 violations. A visible error is preferable to undetected captive-dependency or direct-instantiation bugs.

**Fix:** File an issue at the IoCTools repository with the diagnostic message. As a workaround, suppress `IOC111` on the affected validator and manually verify its composition graph.

**Related:** [IOC100](#ioc100) (direct instantiation), [IOC101](#ioc101) (lifetime mismatch), [IOC112](#ioc112) (pipeline error)

---

### IOC112

**Severity:** [!Error](#) | **Category:** IoCTools.FluentValidation

**Cause:** `ValidatorDiagnosticsPipeline` encountered an unexpected exception while running diagnostic validators against a `ValidatorClassInfo`. All diagnostics for the affected validator were skipped, which can cause false-negative results for [IOC100](#ioc100) and [IOC101](#ioc101).

This is a generator bug — the exception message is included in the diagnostic to aid reporting.

**Note on severity:** Raised as `Error` rather than `Warning` because a dropped validator silently masks real IOC100/IOC101 violations. A visible error is preferable to undetected captive-dependency or direct-instantiation bugs.

**Fix:** File an issue at the IoCTools repository with the diagnostic message. As a workaround, suppress `IOC112` on the affected validator and manually verify it has no direct-instantiation or lifetime-mismatch issues.

**Related:** [IOC100](#ioc100) (direct instantiation), [IOC101](#ioc101) (lifetime mismatch), [IOC111](#ioc111) (graph build error)

---

<a id="ioc113"></a>
### IOC113 — Profile type could not be resolved

**Severity:** Error | **Category:** IoCTools.AutoDeps

**Cause:** The profile type argument in `AutoDepIn`, `AutoDepsApply`, `AutoDepsApplyGlob`, or
`AutoDeps` could not be resolved by Roslyn — the symbol binds as `IErrorTypeSymbol`. This
typically indicates a missing assembly reference or a typo in the type name.

Previously the validator silently passed unresolved symbols (fail-open), which masked
misconfiguration. IOC113 makes the failure visible.

**Fix:** Ensure the assembly containing the profile type is referenced by this project, and
that the type name is spelled correctly.

**Related:** [IOC097](#ioc097) (profile does not implement IAutoDepsProfile),
[IOC104](#ioc104) (profile type is generic)

---

**Back to [main README](../README.md)**
