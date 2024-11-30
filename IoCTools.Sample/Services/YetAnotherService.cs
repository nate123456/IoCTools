using IoCTools.Abstractions.Annotations;
using IoCTools.Sample.Interfaces;

namespace IoCTools.Sample.Services;

[UnregisteredService]
[DependsOn<INewService>]
public partial class YetAnotherService
{
}