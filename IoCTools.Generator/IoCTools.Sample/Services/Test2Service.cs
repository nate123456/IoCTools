using System.Collections.Generic;
using IoCTools.Abstractions.Annotations;
using IoCTools.Sample.Interfaces;

namespace IoCTools.Sample.Services;

[Service]
public class Test2Service : ISomeOtherService
{
    [Inject] private readonly IEnumerable<ISomeService> _test;
    [Inject] private readonly IEnumerable<IEnumerable<ISomeService>> _test2;
    [Inject] private readonly ISomeService _test3;
}