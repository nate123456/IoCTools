# Diagnostics

IoCTools emits build-time diagnostics to catch issues early. Below are common categories and how to fix them.

## Diagnostic Catalog

| Rule | Severity | Summary |
|------|----------|---------|
| IOC001 | Warning | Service depends on an interface with no implementation in the project. |
| IOC002 | Warning | Implementation exists but is missing a lifetime attribute, so it never registers. |
| IOC003 | Warning | Circular dependency detected (the message lists the cycle). |
| IOC004 | Error   | `[RegisterAsAll]` requires a lifetime attribute because it defines a service. |
| IOC005 | Warning | `[SkipRegistration]` without `[RegisterAsAll]` has no effect. |
| IOC006 | Warning | Duplicate dependency types across multiple `[DependsOn]` attributes. |
| IOC007 | Warning | `[DependsOn]` entry conflicts with an `[Inject]` field for the same type. |
| IOC008 | Warning | Duplicate type listed inside a single `[DependsOn]` attribute. |
| IOC009 | Warning | `[SkipRegistration<T>]` targets an interface that would never be registered. |
| IOC010 | Warning | (Deprecated) Background-service lifetime conflicts; superseded by IOC014. |
| IOC011 | Error   | Background services must be declared `partial` so constructors can be generated. |
| IOC012 | Error   | Singleton service depends on a scoped service. |
| IOC013 | Warning | Singleton service depends on a transient service. |
| IOC014 | Error   | Background services should be singletons; other lifetimes trigger this diagnostic. |
| IOC015 | Error   | Lifetime mismatch across an inheritance chain (e.g., singleton inheriting scoped dependencies). |
| IOC016 | Error   | `[InjectConfiguration]` uses an invalid configuration key (empty, whitespace, malformed). |
| IOC017 | Warning | `[InjectConfiguration]` targets an unsupported type (no binder available). |
| IOC018 | Error   | `[InjectConfiguration]` applied to a non-partial class or record. |
| IOC019 | Warning | `[InjectConfiguration]` cannot target static fields. |
| IOC020 | Warning | `[ConditionalService]` contains conflicting conditions (e.g., mutually exclusive environments). |
| IOC021 | Error   | `[ConditionalService]` requires a lifetime attribute (`[Scoped]`, `[Singleton]`, `[Transient]`). |
| IOC022 | Warning | `[ConditionalService]` declared with no conditions. |
| IOC023 | Warning | `ConfigValue` set without `Equals` / `NotEquals`. |
| IOC024 | Warning | `Equals` / `NotEquals` provided without a `ConfigValue`. |
| IOC025 | Warning | `ConfigValue` is empty or whitespace. |
| IOC026 | Warning | Multiple `[ConditionalService]` attributes on the same class. |
| IOC027 | Info    | Potential duplicate service registrations detected. |
| IOC028 | Error   | `[RegisterAs]` used without any service indicators/lifetime metadata. |
| IOC029 | Error   | `[RegisterAs]` lists an interface the class does not implement. |
| IOC030 | Warning | Duplicate interface listed inside `[RegisterAs]`. |
| IOC031 | Error   | `[RegisterAs]` references a non-interface type. |
| IOC032 | Warning | `[RegisterAs]` duplicates what the generator already infers. |
| IOC033 | Warning | `[Scoped]` attribute is redundant because the service is implicitly scoped. |
| IOC034 | Warning | `[RegisterAsAll]` combined with `[RegisterAs]` is redundant. |
| IOC035 | Warning | `[Inject]` field matches the default `[DependsOn]` pattern—prefer `[DependsOn]`. |
| IOC036 | Warning | Multiple lifetime attributes are applied to the same class. |
| IOC037 | Warning | `[SkipRegistration]` overrides other registration attributes on the same class. |
| IOC038 | Warning | `[SkipRegistration<T>]` does nothing when `[RegisterAsAll(RegistrationMode.DirectOnly)]` is used. |

Use the catalog as a quick reference; the sections below dive deeper into common categories and remediation steps.

## Partial and Generation

- Class with `[Inject]` or `[InjectConfiguration]` is not `partial` → add `partial`.
- Background service is not `partial` → add `partial`.

```csharp
public partial class EmailService { [Inject] private readonly ILogger<EmailService> _log; }
```

## Lifetime and Dependencies

- Singleton depending on scoped service → make the dependency singleton or resolve via scope factory.
- Background service using scoped dependency directly → resolve a scope inside `ExecuteAsync`.
- Prefer `[DependsOn<T...>]` to avoid unnecessary fields; use `[Inject]` as last resort when a field is required. Excess fields can trigger naming or duplication issues in complex hierarchies.
- IOC035 warns when `[Inject]` fields match the default `_dependency` naming the generator already produces. Remove the field and add `[DependsOn<TDependency>]` unless you need a bespoke field name or mutability.
- IOC036 reports when you stack multiple lifetime attributes (e.g., `[Scoped]` plus `[Singleton]`). Keep a single lifetime attribute per class.

## Conditional Services

- `ConditionalService` with no conditions → add `Environment` or `ConfigValue` with `Equals`/`NotEquals`.
- Conflicting or duplicate conditions → consolidate into one attribute.

## Interface Registration

- `RegisterAs<T...>` includes a type not implemented → ensure the class implements the interface.
- Duplicate interfaces in `RegisterAs<T...>` → remove duplicates.
- `SkipRegistration<T>` for types that wouldn’t be registered → remove unnecessary skip.
- IOC005 reminds you that `[SkipRegistration]` needs `[RegisterAsAll]`; otherwise it has no effect.
- `[RegisterAs<T...>]` listing the exact same set of interfaces the class already implements (IOC032) → remove the attribute or limit it to the interfaces you really want.
- `[Scoped]` combined with other service indicators such as `[DependsOn]`, `[Inject]`, or `[RegisterAs]` (IOC033) → the generator already registers the service as scoped, so drop `[Scoped]` unless you truly need to override to Singleton/Transient.
- `[RegisterAsAll]` plus `[RegisterAs<T...>]` on the same class (IOC034) → keep one approach; `[RegisterAsAll]` already covers every implemented interface.
- IOC037 warns when `[SkipRegistration]` is combined with other registration attributes (lifetimes, `RegisterAs`, `RegisterAsAll`, `ConditionalService`) because SkipRegistration suppresses them.
- IOC038 warns when `[SkipRegistration<…>]` is used while `[RegisterAsAll]` is set to `RegistrationMode.DirectOnly`. DirectOnly never registers interfaces, so those skip declarations do nothing.

## Configuration Injection

- Static fields with `[InjectConfiguration]` → not supported; use instance fields.
- Missing configuration value with `Required = true` → provide value, set `Required = false`, or add `DefaultValue`.

If a diagnostic message is unclear, check the sample project for a minimal reproduction pattern.
