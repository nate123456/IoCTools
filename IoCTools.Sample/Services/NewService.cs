using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using IoCTools.Sample.Interfaces;

namespace IoCTools.Sample.Services;

[Service(Lifetime.Singleton)]
[DependsOn<IAnotherService, ISomehowAnotherService, ISomeOtherService>]
public partial class NewService : INewService
{
    public void Test()
    {
        _someOtherService.ToString();
        _somehowAnotherService.ToString();
        _anotherService.ToString();
    }
}