using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using IoCTools.Sample.Interfaces;

namespace IoCTools.Sample.Services;

[Service(Lifetime.Singleton)]
[DependsOn<ISomeService, ISomeOtherService>]
public partial class TestService : ISomeService
{
    public void Test()
    {
        var thing = _someOtherService.ToString();
    }
}