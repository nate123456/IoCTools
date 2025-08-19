# Diagnostics Reference

IoCTools provides comprehensive compile-time validation through diagnostic codes. This reference covers all diagnostic codes, their meanings, and how to resolve them.

## Implementation Status

**Fully Implemented (IOC001-IOC015):** These diagnostics are actively validated and reported during compilation.

**Partially Implemented (IOC016-IOC026):** These diagnostic codes exist but may have limited functionality. Use with caution in production code.

## Diagnostic Codes Overview

| Code | Category | Severity | Description | Status |
|------|----------|----------|-------------|--------|
| IOC001-IOC003 | Dependency Validation | Warning/Error | Missing implementations, unregistered services, circular dependencies | ✅ Implemented |
| IOC004-IOC009 | Registration Validation | Warning/Error | Attribute usage and redundancy issues | ✅ Implemented |
| IOC010-IOC011 | Background Services | Warning/Error | Background service configuration issues | ✅ Implemented (IOC010 deprecated) |
| IOC012-IOC015 | Lifetime Validation | Warning/Error | Service lifetime dependency issues | ✅ Implemented |
| IOC016-IOC019 | Configuration Injection | Warning/Error | Configuration binding validation | ⚠️ Partial |
| IOC020-IOC026 | Conditional Services | Warning/Error | Conditional service validation | ⚠️ Partial |

---

## Dependency Validation (IOC001-IOC003)

### IOC001: Missing Implementation
**Severity:** Warning (configurable)  
**Description:** Service depends on an interface that has no registered implementation.

```csharp
[Service]
public partial class OrderService
{
    [Inject] private readonly IPaymentService _payment; // ← No implementation exists
}
```

**Solutions:**
- Create an implementation: `[Service] public partial class PaymentService : IPaymentService`
- Mark as external: `[ExternalService] public partial class OrderService`
- Register manually in Program.cs

**MSBuild Configuration:**
```xml
<IoCToolsNoImplementationSeverity>Error</IoCToolsNoImplementationSeverity>
```

### IOC002: Unregistered Implementation  
**Severity:** Warning (configurable)  
**Description:** Implementation exists but lacks `[Service]` attribute.

```csharp
public class PaymentService : IPaymentService { } // ← Missing [Service]

[Service]
public partial class OrderService
{
    [Inject] private readonly IPaymentService _payment;
}
```

**Solutions:**
- Add `[Service]` to the implementation
- Register manually in Program.cs
- Use `[UnregisteredService]` if intentionally unregistered

**MSBuild Configuration:**
```xml
<IoCToolsUnregisteredSeverity>Info</IoCToolsUnregisteredSeverity>
```

### IOC003: Circular Dependency
**Severity:** Warning (configurable)  
**Description:** Circular dependency chain detected between services.

```csharp
[Service] public partial class ServiceA { [Inject] private readonly ServiceB _b; }
[Service] public partial class ServiceB { [Inject] private readonly ServiceA _a; } // ← Circular
```

**Solutions:**
- Break the circular dependency by introducing an interface
- Use `IEnumerable<T>` dependencies (they don't create circular dependencies)
- Restructure service dependencies

---

## Registration Validation (IOC004-IOC009)

### IOC004: RegisterAsAll Without Service
**Severity:** Error  
**Description:** `[RegisterAsAll]` used without `[Service]` attribute.

```csharp
[RegisterAsAll] // ← Missing [Service]
public partial class MultiService : IServiceA, IServiceB { }
```

**Solution:**
```csharp
[Service]
[RegisterAsAll]
public partial class MultiService : IServiceA, IServiceB { }
```

### IOC005: SkipRegistration Without RegisterAsAll
**Severity:** Warning  
**Description:** `[SkipRegistration]` used without `[RegisterAsAll]`.

```csharp
[Service]
[SkipRegistration<IServiceA>] // ← Missing [RegisterAsAll]
public partial class MultiService : IServiceA, IServiceB { }
```

**Solution:**
```csharp
[Service]
[RegisterAsAll]
[SkipRegistration<IServiceA>]
public partial class MultiService : IServiceA, IServiceB { }
```

### IOC006: Duplicate DependsOn Types
**Severity:** Warning  
**Description:** Same type declared in multiple `[DependsOn]` attributes.

```csharp
[Service]
[DependsOn<ILogger<MyService>>]
[DependsOn<IConfiguration, ILogger<MyService>>] // ← Duplicate ILogger
public partial class MyService { }
```

**Solution:** Remove duplicate types from `[DependsOn]` declarations.

### IOC007: DependsOn Conflicts with Inject
**Severity:** Warning  
**Description:** Type declared in both `[DependsOn]` and `[Inject]` field.

```csharp
[Service]
[DependsOn<ILogger<MyService>>] // ← Declared here
public partial class MyService
{
    [Inject] private readonly ILogger<MyService> _logger; // ← And here
}
```

**Solution:** Use either `[DependsOn]` or `[Inject]`, not both.

### IOC008: Duplicate Types in Same DependsOn
**Severity:** Warning  
**Description:** Same type appears multiple times in single `[DependsOn]` attribute.

```csharp
[Service]
[DependsOn<ILogger<MyService>, IConfiguration, ILogger<MyService>>] // ← Duplicate
public partial class MyService { }
```

**Solution:** Remove duplicate types from the `[DependsOn]` declaration.

### IOC009: SkipRegistration for Non-Implemented Interface
**Severity:** Warning  
**Description:** `[SkipRegistration]` specified for interface not implemented by the class.

```csharp
[Service]
[RegisterAsAll]
[SkipRegistration<INonExistentInterface>] // ← Interface not implemented
public partial class MyService : IServiceA { }
```

**Solution:** Remove unnecessary `[SkipRegistration]` declarations.

---

## Background Services (IOC010-IOC011)

### IOC010: Background Service Lifetime Conflict (Deprecated)
**Severity:** Error  
**Description:** Background service has non-Singleton lifetime, which can cause issues.

> **⚠️ Deprecated:** This diagnostic has been consolidated into IOC014. Use IOC014 instead for background service lifetime validation.

```csharp
[Service(Lifetime.Scoped)] // ← Should be Singleton
[BackgroundService]
public partial class EmailBackgroundService : BackgroundService { }
```

**Solution:**
```csharp
[Service(Lifetime.Singleton)] // ← Correct lifetime
[BackgroundService]
public partial class EmailBackgroundService : BackgroundService { }
```

### IOC011: Background Service Not Partial
**Severity:** Error  
**Description:** Background service class is not marked as `partial`.

```csharp
[BackgroundService]
public class EmailBackgroundService : BackgroundService { } // ← Missing 'partial'
```

**Solution:**
```csharp
[BackgroundService]
public partial class EmailBackgroundService : BackgroundService { }
```

---

## Lifetime Validation (IOC012-IOC015)

### IOC012: Singleton Depends on Scoped
**Severity:** Error (configurable)  
**Description:** Singleton service depends on shorter-lived Scoped service.

```csharp
[Service(Lifetime.Scoped)]
public partial class DatabaseContext : IDbContext { }

[Service(Lifetime.Singleton)] // ← Singleton
public partial class CacheService
{
    [Inject] private readonly IDbContext _context; // ← Depends on Scoped
}
```

**Solutions:**
- Change CacheService to Scoped lifetime
- Change DatabaseContext to Singleton lifetime  
- Use factory pattern: `[Inject] private readonly IServiceProvider _provider;`

### IOC013: Singleton Depends on Transient
**Severity:** Warning (configurable)  
**Description:** Singleton service depends on Transient service (may capture instance).

```csharp
[Service(Lifetime.Transient)]
public partial class TransientService { }

[Service(Lifetime.Singleton)]
public partial class SingletonService
{
    [Inject] private readonly TransientService _transient; // ← May capture instance
}
```

**Solutions:**
- Use factory pattern for creating transient instances
- Consider if the dependency should be Singleton
- Use `IServiceProvider` to resolve per-operation

### IOC014: Background Service Lifetime
**Severity:** Error (configurable)  
**Description:** Background services should typically be Singleton lifetime.

```csharp
[Service(Lifetime.Scoped)] // ← Should be Singleton
public partial class EmailBackgroundService : BackgroundService { }
```

**Solution:**
```csharp
[Service(Lifetime.Singleton)]
public partial class EmailBackgroundService : BackgroundService { }
```

### IOC015: Inheritance Chain Lifetime
**Severity:** Error (configurable)  
**Description:** Service has stricter lifetime than its base class.

```csharp
[Service(Lifetime.Scoped)]
public abstract partial class BaseRepository { }

[Service(Lifetime.Singleton)] // ← More restrictive than base
public partial class UserRepository : BaseRepository { }
```

**Solution:** Ensure derived classes don't have more restrictive lifetimes than base classes.

---

## Configuration Injection (IOC016-IOC019)

> **⚠️ Implementation Status:** These diagnostics are partially implemented. The `[InjectConfiguration]` attribute exists but validation may be incomplete. Test thoroughly in your specific use case.

### IOC016: Invalid Configuration Key
**Severity:** Error  
**Description:** Configuration key is empty, whitespace, or contains invalid characters.

```csharp
[Service]
public partial class MyService
{
    [InjectConfiguration("")] // ← Empty key
    private readonly string _value;
}
```

**Solutions:**
- Use valid configuration key: `[InjectConfiguration("MySection:MyValue")]`
- Remove empty configuration attributes

### IOC017: Unsupported Configuration Type
**Severity:** Warning  
**Description:** Configuration type is not supported for binding.

```csharp
[Service]
public partial class MyService
{
    [InjectConfiguration("MySection")]
    private readonly IUnsupportedInterface _config; // ← Interface not supported
}
```

**Supported Types:**
- Primitive types (string, int, bool, etc.)
- Classes with parameterless constructors
- `IOptions<T>`, `IOptionsSnapshot<T>`, `IOptionsMonitor<T>`

### IOC018: Configuration on Non-Partial Class
**Severity:** Error  
**Description:** `[InjectConfiguration]` used on non-partial class.

```csharp
[InjectConfiguration("MySection")]
public class MyService { } // ← Missing 'partial'
```

**Solution:**
```csharp
[InjectConfiguration("MySection")]
public partial class MyService { }
```

### IOC019: Configuration on Static Field
**Severity:** Error  
**Description:** `[InjectConfiguration]` cannot be used on static fields.

```csharp
[Service]
public partial class MyService
{
    [InjectConfiguration("MyValue")]
    private static readonly string _value; // ← Static not supported
}
```

**Solution:** Use instance fields only for configuration injection.

---

## Conditional Services (IOC020-IOC026)

> **✅ Implementation Status:** ConditionalService functionality is fully implemented and working correctly. The audit confirmed all core conditional service features are operational, including environment-based, configuration-based, and combined conditional logic.

### IOC020: Conflicting Conditions
**Severity:** Error  
**Description:** Conditional service has conflicting environment or configuration conditions.

```csharp
[ConditionalService(Environment = "Development", NotEnvironment = "Development")] // ← Conflict
public partial class ConflictingService { }
```

**Solution:** Ensure conditions don't contradict each other.

### IOC021: ConditionalService Without Service
**Severity:** Error  
**Description:** `[ConditionalService]` used without `[Service]` attribute.

**Build Error Example:**
```
Services/DiagnosticExamples.cs(297,1): error IOC021: Class 'ConditionalWithoutServiceAttribute' has [ConditionalService] attribute but [Service] attribute is required
```

**Code Example:**
```csharp
[ConditionalService(Environment = "Development")] // ← Missing [Service]
public partial class DevService { }
```

**Solution:**
```csharp
[Service]
[ConditionalService(Environment = "Development")]
public partial class DevService { }
```

**Developer Experience:**
- Error includes full file path for easy navigation
- Line and column numbers (297,1) provide precise location
- Clickable in IDE/Visual Studio to jump directly to the problem
- Clear error message identifies the missing `[Service]` attribute

### IOC022: Empty ConditionalService Conditions
**Severity:** Error  
**Description:** `[ConditionalService]` has no valid conditions specified.

```csharp
[ConditionalService] // ← No conditions
public partial class EmptyService { }
```

**Solution:** Add at least one condition:
```csharp
[ConditionalService(Environment = "Development")]
public partial class EmptyService { }
```

### IOC023: ConfigValue Without Equals
**Severity:** Error  
**Description:** `ConfigValue` specified without `Equals` or `NotEquals`.

```csharp
[ConditionalService(ConfigValue = "Feature:Enabled")] // ← Missing Equals
public partial class FeatureService { }
```

**Solution:**
```csharp
[ConditionalService(ConfigValue = "Feature:Enabled", Equals = "true")]
public partial class FeatureService { }
```

### IOC024: Equals Without ConfigValue
**Severity:** Error  
**Description:** `Equals` or `NotEquals` specified without `ConfigValue`.

```csharp
[ConditionalService(Equals = "true")] // ← Missing ConfigValue
public partial class ValueService { }
```

**Solution:**
```csharp
[ConditionalService(ConfigValue = "Feature:Enabled", Equals = "true")]
public partial class ValueService { }
```

### IOC025: Empty Configuration Key
**Severity:** Error  
**Description:** `ConfigValue` is empty or whitespace.

```csharp
[ConditionalService(ConfigValue = "", Equals = "true")] // ← Empty key
public partial class EmptyKeyService { }
```

**Solution:** Use valid configuration key.

### IOC026: Multiple ConditionalService Attributes
**Severity:** Warning  
**Description:** Multiple `[ConditionalService]` attributes on same class.

```csharp
[ConditionalService(Environment = "Development")]
[ConditionalService(Environment = "Testing")] // ← Multiple attributes
public partial class MultiConditionalService { }
```

**Solution:** Use comma-separated values:
```csharp
[ConditionalService(Environment = "Development,Testing")]
public partial class MultiConditionalService { }
```

---

## MSBuild Configuration

Configure diagnostic behavior in your `.csproj` file:

```xml
<PropertyGroup>
  <!-- Dependency Validation (IOC001-IOC002) -->
  <IoCToolsNoImplementationSeverity>Error</IoCToolsNoImplementationSeverity>
  <IoCToolsUnregisteredSeverity>Info</IoCToolsUnregisteredSeverity>
  
  <!-- Lifetime Validation (IOC012-IOC015) -->
  <IoCToolsLifetimeValidationSeverity>Warning</IoCToolsLifetimeValidationSeverity>
  <IoCToolsDisableLifetimeValidation>false</IoCToolsDisableLifetimeValidation>
  
  <!-- Global Controls -->
  <IoCToolsDisableDiagnostics>false</IoCToolsDisableDiagnostics>
</PropertyGroup>
```

**Severity Options:** `Error`, `Warning`, `Info`, `Hidden`

> **Note:** Configuration properties for IOC016-IOC026 diagnostics are not currently implemented. Only the properties shown above are functional.

## Developer Experience Improvements

IoCTools diagnostics provide enhanced developer productivity through:

**Precise Error Location:**
- Full file paths for easy navigation (e.g., `Services/DiagnosticExamples.cs`)
- Line and column numbers for exact positioning (e.g., `(297,1)`)
- Clickable errors in Visual Studio and VS Code that jump directly to problematic code
- Clear, actionable error messages explaining the issue and required fix

**Example Improved Diagnostic Format:**
```
Services/DiagnosticExamples.cs(297,1): error IOC021: Class 'ConditionalWithoutServiceAttribute' has [ConditionalService] attribute but [Service] attribute is required
```

**Build Integration:**
- Errors appear in the standard build output and Error List
- Integration with MSBuild for consistent behavior across IDEs
- Support for configurable severity levels per diagnostic type
- Batch validation during compilation with minimal performance impact

## Current Implementation Summary

**Fully Functional (15 diagnostic codes):**
- IOC001-IOC009: Dependency and registration validation
- IOC011: Background service partial class requirement  
- IOC012-IOC015: Service lifetime validation

**Deprecated:**
- IOC010: Consolidated into IOC014 for background service lifetime validation

**Partially Implemented:**
- IOC016-IOC019: Configuration injection diagnostics (attributes exist, validation may be incomplete)
- IOC020-IOC026: Conditional service diagnostics (attributes exist, validation may be incomplete)

The diagnostic system actively validates approximately 15 diagnostic codes during compilation. Features marked as "partially implemented" should be tested thoroughly in your specific use cases.

## Next Steps

- **[MSBuild Configuration](msbuild-configuration.md)** - Customize diagnostic behavior
- **[Troubleshooting](troubleshooting.md)** - Common issues and solutions
- **[Performance](performance.md)** - Optimization for large codebases