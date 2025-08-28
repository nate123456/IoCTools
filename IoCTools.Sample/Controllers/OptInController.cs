namespace IoCTools.Sample.Controllers;

using Abstractions.Annotations;

using Microsoft.AspNetCore.Mvc;

[Scoped]
[RegisterAsAll]
public partial class OptInController : ControllerBase, IOptInController
{
}

public interface IOptInController
{
}
