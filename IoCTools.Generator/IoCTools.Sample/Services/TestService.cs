using IoCTools.Abstractions.Annotations;
using IoCTools.Generator.Sample.Interfaces;

namespace IoCTools.Generator.Sample.Services;

public class TestService
{
    [Inject] private readonly ISomeService _someService;
}