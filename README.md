# IoCTools

This repository provides various useful tools for simplifying and streamlining 
the process of work with Inversion of Control in .NET; 
mainly focusing on working with ASP.NET's Services. This project provides
a source generator which eliminates much of the boilerplate that normally
comes with working with services in .NET.

[![IoC Tools](https://img.shields.io/nuget/v/IoCTools.Abstractions?label=IoCTools.Abstractions)](https://www.nuget.org/packages/IoCTools.Abstractions)
[![IoC Abstractions](https://img.shields.io/nuget/v/IoCTools.Generator?label=IoCTools.Generator)](https://www.nuget.org/packages/IoCTools.Generator)

## Features

Although the project is in alpha and under active development,
some features are already working. 

### Self Describing Services

When it comes to service API design, it is often written with a
specific service lifetime in mind. For example, services that persist values
during the app session are aware implicitly by their design that they
only support a singleton service lifetime.

It therefore makes the most sense to let the service define what lifetime
it expects to be given. Given the following example service that must operate
as a singleton, the before code demonstrates how registering the service
without IoC Tools would go: 

``` c#
// in the service class
namespace SampleProject.Services;

public class SomeService : ISomeService
{
    private readonly int _someValue = 1;
}

// in Program.cs
builder.Services.RegisterSingleton<ISomeService, SomeService>();
```

This example is not ideal as the service does not get to choose its lifetime,
which is its concern as it is designed with one particular service in mind.

With IoC.Tools, you may refactor the above service as follows:

```c#
// in the service class
namespace SampleProject.Services;

[Service(Lifetime.Singleton)]
public partial class SomeService : ISomeService
{
    private readonly int _someValue = 1;
}

// in Program.cs
builder.Services.RegisterSampleProjectServices();
```

This eliminates the need for the setup code in program.cs to have any 
understanding of the designed lifetime of the service, and lets the service
own its own design explicitly. 

### Intuitive Dependency Injection

In the case where your service depends on other services, the following code
is normally required to bring in those dependencies.

In the following code, we see the (recently improved with primary constructor)
syntax required to bring in a non trivial amount of dependencies. 

```c#
// in the service class
namespace SampleProject.Services;

public class SomeService(IOtherService otherService, IAnotherService anotherService, 
    IYetAnotherService yetAnotherService, ISomehowAnotherService somehowAnotherService) 
    : ISomeService
{
    private readonly int _someValue = 1;
    
    public void Test()
    {
        // use services 
    }
}
```

Let's not forget the 'olden days' where it was even worse: 

```c#
// in the service class
namespace SampleProject.Services;

public class SomeService : ISomeService
{
    private readonly IOtherService _otherService;
    private readonly IAnotherService _anotherService;
    private readonly IYetAnotherService _yetAnotherService;
    private readonly ISomehowAnotherService _somehowAnotherService);
        
    public SomeService(IOtherService otherService, IAnotherService anotherService, 
    IYetAnotherService yetAnotherService, ISomehowAnotherService somehowAnotherService)
    {
        _otherService = otherService;
        _anotherService = anotherService;
        _yetAnotherService = yetAnotherService;
        _somehowAnotherService = somehowAnotherService;
    }
    private readonly int _someValue = 1;
    
    public void Test()
    {
        // use services 
    }
}
```

That always seemed like an egregious amount of boilerplate. 

With IoC Tools, you may refactor the above code to the following: 

```c#
// in the service class
namespace SampleProject.Services;

[Service]
public partial class SomeService : ISomeService
{
    [Inject] private readonly IOtherService _otherService;
    [Inject] private readonly IAnotherService _anotherService;
    [Inject] private readonly IYetAnotherService _yetAnotherService;
    [Inject] private readonly ISomehowAnotherService _somehowAnotherService);

    private readonly int _someValue = 1;
    
    public void Test()
    {
        // use services 
    }
}
```

A source generator creates the appropriate constructor behind the scenes,
saving the developer from having to experience bloated constructors.

This is still explicit enough that you can can clearly see even from a git diff
that dependencies are coming in, but doesn't feel repetitive, and still allows 
the user to customize the names of their dependency field as well.

Although this project is still in development, IoC Tools already has support
for injection of dependencies that have any generic depth. 

```c#
// in the service class
namespace SampleProject.Services;

[Service]
public partial class SomeService : ISomeService
{
    [Inject] private readonly IEnumerable<IOtherService> _otherServices;

    private readonly int _someValue = 1;
    
    public void Test()
    {
        // use services 
    }
}
```

This code would work just as well. 

### Streamlined Service Registration

Another major boilerplate concern is the need to individually register each
service in the setup code, or in a separate file. 

It doesn't feel like good code- it is bloated and repetitive.

With IoC Tools, any class decorated with `[Service]` will automatically be
added a new extension method on `IServiceCollection` that enables the user
to simply decorate their services once (which you'd want to do anyway for
resolving dependencies) and have a single method call in the setup code which
adds all the services from the project. 

The method is named after the project folder name due to a potential clash
that would arise from having one project using IoC Tools being referenced by
another project which also uses IoC Tools. 

The project folder name was chosen as source generators have
limitations on determining the root namespace or actual project name.

Given a project in a folder called `SampleProject`, the following would be 
doable in Program.cs:

```c#
// in the service class
// in Program.cs
builder.Services.RegisterSampleProjectServices();
```

As services are added or removed during development, IoC tools will 
automatically generate the appropriate changes in its extension method.

This is also how services can indicate their desired lifetime- using
[Service(Lifetime.Singleton)] or [Service(Lifetime.Transient)] or
[Service(Lifetime.Scoped)]. IoC Tools will automatically use the correct 
registration method.

Registration of generic services is not supported as this time. 
This is due to the fact that there is no way to infer which generic type 
should be supplied for the generic argument(s) without a new way 
to specify them manually.

### Development 

Development is currently active, and while there are public NuGet packages,
they are marked with pre v1 versioning to indicate their instability.

As such, it should be expected that breaking API changes may occur.

Feel free to suggest ideas or bugfixes in the issues!

For more examples of the generator in action, clone down the repo and look 
at the Sample project. 

