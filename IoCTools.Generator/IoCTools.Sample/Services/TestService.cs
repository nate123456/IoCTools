using IoCTools.Generator.Annotations;
using IoCTools.Generator.Enumerations;
using IoCTools.Sample.Interfaces;

namespace IoCTools.Sample.Services;

[Service(Lifetime.Singleton)]
public partial class TestService : ISomeService
{
    [Inject] private readonly ISomeOtherService _someOtherService;
    [Inject] private readonly ISomeService _someService;
}