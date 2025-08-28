# IoCTools Docs

This documentation is concise by design: each page covers a single topic with focused examples and no overlap. If you’re new, start with Getting Started, then browse topics as needed.

## Table of Contents

- [Getting Started](getting-started.md)
- [Attributes & Usage](attributes.md)
- [Lifetime Management](lifetime-management.md)
- [Configuration Injection](configuration-injection.md)
- [Conditional Services](conditional-services.md)
- [Background Services](background-services.md)
- [Interface Registration](multi-interface-registration.md)
- [Generics](generics.md)
- [Inheritance](inheritance.md)
- [Diagnostics](diagnostics.md)
- [Generator Style Options](generator-style-options.md)
- [Recipes](recipes.md)
- [FAQ](faq.md)

## What IoCTools Generates

- Constructors for partial classes that use `[Inject]` or `[InjectConfiguration]`
- DI registrations for services and interfaces (including RegisterAs/All)
- Hosted-service registration for classes deriving from `BackgroundService` or implementing `IHostedService`
- Diagnostics that catch common misconfigurations at build time

See IoCTools.Sample for end-to-end examples.
