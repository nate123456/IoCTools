# FAQ

## Do I have to mark classes partial?
Yes — any class that uses `[Inject]` or `[InjectConfiguration]`, or that is a background service, must be `partial` so IoCTools can generate a constructor.

## How do I register services?
Call the generated extension (e.g., `AddIoCToolsSampleRegisteredServices()`) in your startup. The name is derived from your assembly.

## Can I use constructor parameters instead of fields?
Use `[DependsOn<T...>]` to request parameters without creating fields. This is useful for pass-through dependencies.

## How do I opt out of registration?
Use `[SkipRegistration]` (entire type) or `[SkipRegistration<TInterface>]` (specific interface).

## What about ASP.NET controllers or mediator handlers?
Use generator style options to skip assignable types by default and add exceptions. See generator-style-options.md.

## Is `IEnumerable<T>` injection supported?
Yes. All matching registrations are included automatically.

## How do conditional services work at runtime?
`[ConditionalService]` emits registration guarded by checks for environment and/or configuration values. Only matching services are registered.
