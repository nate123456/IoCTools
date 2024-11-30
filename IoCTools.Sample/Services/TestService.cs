using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using IoCTools.Sample.Interfaces;
using Mediator;

namespace IoCTools.Sample.Services;

[Service(Lifetime.Singleton)]
[DependsOn<ISomeService, ISomeOtherService, IMediator>]
public partial class TestService : ISomeService
{
    public async Task Test()
    {
        var thing = _someOtherService.ToString();
        await _mediator.Send(thing);
    }
}