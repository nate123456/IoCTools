using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using IoCTools.Sample.Interfaces;

namespace IoCTools.Sample.Services;

[Service(Lifetime.Singleton)]
public class TestService : ISomeService
{
    [Inject] private readonly ISomeOtherService _someOtherService;
    [Inject] private readonly ISomeService _someService;
}