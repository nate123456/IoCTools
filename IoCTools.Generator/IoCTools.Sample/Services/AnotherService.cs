using IoCTools.Generator.Annotations;
using IoCTools.Sample.Interfaces;

namespace IoCTools.Sample.Services;

[Service]
public partial class AnotherService<T> : IAnotherService
{
    [Inject] private readonly ISomeService _someService;
}

public interface IAnotherService
{
}