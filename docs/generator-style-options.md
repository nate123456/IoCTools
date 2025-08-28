# Generator Style Options (Skip by Assignable + Exceptions)

Note: This is part of the IoCTools configuration surface. For a complete overview (including diagnostics configuration and precedence rules), see configuration.md.

IoCTools can automatically skip registration for classes assignable to common framework types (e.g., ASP.NET controllers, mediator handlers). You can fine‑tune this behavior via .editorconfig/MSBuild properties or via a single optional code-based configuration class. Globs are supported.

## Defaults

By default, IoCTools skips types assignable to:

- Microsoft.AspNetCore.Mvc.ControllerBase
- Mediator.* (Mediator.Abstractions): IRequestHandler`1, IRequestHandler`2, INotificationHandler`1, IStreamRequestHandler`2
- MediatR.*: IRequestHandler`1, IRequestHandler`2, INotificationHandler`1, IStreamRequestHandler`2

Constructor generation still runs; only DI registrations are skipped.

Note
- By default, only ASP.NET Core controllers are skipped. If you want Mediator/MediatR handlers skipped as well, add them via configuration (e.g., SkipAssignableTypesAdd = "Mediator.*;MediatR.*"). This keeps responsibility for handler registration with the mediator library and avoids duplicate registrations.

## .editorconfig (MSBuild) options

Place a .editorconfig in your project (or repo root) with these properties:

- build_property.IoCToolsSkipAssignableTypesUseDefaults = true|false
- build_property.IoCToolsSkipAssignableTypes = Type1;Type2  (full override)
- build_property.IoCToolsSkipAssignableTypesAdd = TypeOrGlob;...
- build_property.IoCToolsSkipAssignableTypesRemove = TypeOrGlob;...
- build_property.IoCToolsSkipAssignableExceptions = FullyQualifiedTypeOrGlob;...

Notes:
- Use fully-qualified metadata names. For generics, specify arity (e.g., Mediator.IRequestHandler`2).
- Globs are supported in Add/Remove/Exceptions (e.g., Mediator.* or MyApp.Controllers.*).
- Exceptions apply to the concrete class name (class is always registered even if a base/interface is skipped).

Example:

```ini
is_global = true
build_property.IoCToolsSkipAssignableExceptions = MyApp.Controllers.AdminController
build_property.IoCToolsSkipAssignableTypesAdd = MyCompany.Framework.BaseComponent
```

## Code-based configuration (optional, single place)

Alternatively, add one class to your codebase. The generator will pick it up automatically:

```csharp
namespace IoCTools.Generator.Configuration
{
    public static class GeneratorOptions
    {
        public const bool   SkipAssignableTypesUseDefaults = true;
        public const string SkipAssignableTypes            = ""; // full override (optional)
        public const string SkipAssignableTypesAdd         = "Mediator.*;MyCompany.Framework.BaseComponent";
        public const string SkipAssignableTypesRemove      = "Microsoft.AspNetCore.Mvc.ControllerBase";
        public const string SkipAssignableExceptions       = "MyApp.Controllers.OptInController;MyApp.Controllers.*Admin*";
    }
}
```

Semantics match the .editorconfig options and also support globs.

## Practical recipes

- Keep defaults, and register one controller: set Exceptions = MyApp.Controllers.OptInController
- Skip your own framework base class: set TypesAdd = MyCompany.Framework.BaseComponent
- Register all controllers while keeping mediator skipped: set TypesRemove = Microsoft.AspNetCore.Mvc.ControllerBase

## Sample app

The sample app includes an .editorconfig demonstrating:

- IoCToolsSkipAssignableExceptions = IoCTools.Sample.Controllers.OptInController
- IoCToolsSkipAssignableTypesAdd   = IoCTools.Sample.Framework.FrameworkBase

At runtime, you’ll see:

- OptInController registered (exception)
- FrameworkDerivedService skipped (assignable rule)
