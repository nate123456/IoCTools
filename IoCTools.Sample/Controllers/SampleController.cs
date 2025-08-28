namespace IoCTools.Sample.Controllers;

using Abstractions.Annotations;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

// Demonstrates that ASP.NET Core controllers are skipped by default for registration
[Scoped]
public partial class SampleController : ControllerBase
{
    [Inject] private readonly ILogger<SampleController> _logger;
}
