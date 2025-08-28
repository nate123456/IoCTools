namespace IoCTools.Generator.Utilities;

using System.Collections.Generic;

internal static class FrameworkTypes
{
    // Framework types that are commonly registered manually or by the framework
    public static readonly HashSet<string> KnownTypes = new()
    {
        "Microsoft.Extensions.Logging.ILogger",
        "Microsoft.Extensions.Logging.ILogger<>",
        "Microsoft.Extensions.Configuration.IConfiguration",
        "Microsoft.Extensions.Configuration.IConfigurationRoot",
        "Microsoft.Extensions.Configuration.IConfigurationSection",
        "System.Net.Http.HttpClient",
        "Microsoft.Extensions.DependencyInjection.IServiceProvider",
        "Microsoft.Extensions.Options.IOptions<>",
        "Microsoft.Extensions.Options.IOptionsMonitor<>",
        "Microsoft.Extensions.Options.IOptionsSnapshot<>",
        "Microsoft.Extensions.Hosting.IHostEnvironment",
        "Microsoft.Extensions.Hosting.IHostApplicationLifetime",
        "Microsoft.AspNetCore.Http.IHttpContextAccessor",
        "System.IServiceProvider",
        "Mediator.IMediator",
        "Mediator.ISender",
        "Mediator.IPublisher"
    };
}
