# IoCTools Configuration

This document centralizes all IoCTools configuration knobs. It covers build-time (MSBuild/.editorconfig) options and an optional code-based configuration point.

- Generator Style Options (skip-by-assignable with globs and exceptions)
- Diagnostics/validation severity and toggles
- Configuration channels and precedence

## Configuration Channels

- .editorconfig (MSBuild AnalyzerConfig) – recommended for team‑wide policy; works in IDE/CI.
- Optional code-based config: add a single static class in your codebase to keep settings close to code.

Notes:
- Properties appear as `build_property.*` keys in AnalyzerConfig.
- The generator merges .editorconfig and code-based settings. For overlapping keys, strings are merged where applicable (Add/Remove); full overrides replace the set.

---

## Generator Style Options (Skip by Assignable)

IoCTools can skip DI registrations for classes assignable to known framework types (controllers, mediator handlers) while still generating constructors. You can tailor this behavior.

Defaults (when UseDefaults=true):
- Microsoft.AspNetCore.Mvc.ControllerBase
- Mediator.* (Mediator.Abstractions): IRequestHandler`1/`2, INotificationHandler`1, IStreamRequestHandler`2
- MediatR.*: IRequestHandler`1/`2, INotificationHandler`1, IStreamRequestHandler`2

Note: By default only ASP.NET Core controllers are skipped. If you want Mediator/MediatR handlers skipped as well, configure it explicitly (e.g., `SkipAssignableTypesAdd = "Mediator.*;MediatR.*"`). IoCTools still generates constructors for these classes.

Supported properties (.editorconfig):
- build_property.IoCToolsSkipAssignableTypesUseDefaults = true|false
- build_property.IoCToolsSkipAssignableTypes = Type1;Type2    (full override)
- build_property.IoCToolsSkipAssignableTypesAdd = TypeOrGlob;...
- build_property.IoCToolsSkipAssignableTypesRemove = TypeOrGlob;...
- build_property.IoCToolsSkipAssignableExceptions = FullyQualifiedTypeOrGlob;...

Type names:
- Use fully-qualified metadata names. For generics, specify arity: `Mediator.IRequestHandler`2`.
- Globs supported in Add/Remove/Exceptions (e.g., `Mediator.*`, `MyApp.Controllers.*`).
- Separators: semicolon, comma, newline; spaces are ignored around entries.

Examples (.editorconfig):
```ini
is_global = true
build_property.IoCToolsSkipAssignableTypesRemove = Microsoft.AspNetCore.Mvc.ControllerBase
build_property.IoCToolsSkipAssignableExceptions = MyApp.Controllers.AdminController
build_property.IoCToolsSkipAssignableTypesAdd = MyCompany.Framework.BaseComponent; Mediator.*
```

Optional code-based configuration (place in your solution):
```csharp
namespace IoCTools.Generator.Configuration
{
    public static class GeneratorOptions
    {
        public const bool   SkipAssignableTypesUseDefaults = true;           // Optional
        public const string SkipAssignableTypes            = "";              // Optional (full override)
        public const string SkipAssignableTypesAdd         = "Mediator.*";    // Optional
        public const string SkipAssignableTypesRemove      = "Microsoft.AspNetCore.Mvc.ControllerBase"; // Optional
        public const string SkipAssignableExceptions       = "MyApp.Controllers.OptInController";      // Optional
    }
}
```

Recipes:
- Keep defaults and register one controller: set Exceptions = `MyApp.Controllers.AdminController`.
- Register all controllers: set Remove = `Microsoft.AspNetCore.Mvc.ControllerBase`.
- Skip your own base: set Add = `MyCompany.Framework.BaseComponent`.

---

## Diagnostics Configuration

Control diagnostic severities and enable/disable specific validation categories.

Supported properties (.editorconfig / MSBuild):
- build_property.IoCToolsNoImplementationSeverity = Error|Warning|Info|Hidden
- build_property.IoCToolsManualSeverity = Error|Warning|Info|Hidden
- build_property.IoCToolsDisableDiagnostics = true|false
- build_property.IoCToolsLifetimeValidationSeverity = Error|Warning|Info|Hidden
- build_property.IoCToolsDisableLifetimeValidation = true|false

Examples (.editorconfig):
```ini
is_global = true
build_property.IoCToolsNoImplementationSeverity = Error
build_property.IoCToolsManualSeverity = Info
build_property.IoCToolsDisableDiagnostics = false
build_property.IoCToolsLifetimeValidationSeverity = Warning
build_property.IoCToolsDisableLifetimeValidation = false
```

Notes:
- Severity names are case-insensitive.
- Disable flags invert behavior (DisableDiagnostics=true turns everything off; DisableLifetimeValidation=true turns lifetime checks off).

---

## Precedence & Merging

- Full override (SkipAssignableTypes) replaces the entire default/working set.
- Add/Remove merge into the working set after defaults/override.
- Exceptions are checked last and always allow specific concrete classes through.
- Code-based `GeneratorOptions` are merged with .editorconfig; in conflicts, the working set reflects the combination (override + add/remove), then exceptions are applied.

---

## See Also

- Generator Style Options deep-dive: generator-style-options.md
- Diagnostics reference: diagnostics.md
- Getting Started: getting-started.md
