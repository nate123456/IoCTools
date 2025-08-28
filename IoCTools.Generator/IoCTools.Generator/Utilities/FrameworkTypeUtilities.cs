namespace IoCTools.Generator.Utilities;

using System;
using System.Collections.Generic;

internal static class FrameworkTypeUtilities
{
    private static readonly HashSet<string> Exact = new(StringComparer.Ordinal)
    {
        "Microsoft.Extensions.Configuration.IConfiguration",
        "Microsoft.Extensions.Configuration.IConfigurationRoot",
        "Microsoft.Extensions.Configuration.IConfigurationSection",
        "System.Net.Http.HttpClient",
        "Microsoft.Extensions.DependencyInjection.IServiceProvider",
        "Microsoft.Extensions.DependencyInjection.IServiceScopeFactory",
        "Microsoft.Extensions.DependencyInjection.IServiceScope",
        "Microsoft.Extensions.Hosting.IHostEnvironment",
        "Microsoft.Extensions.Hosting.IHostApplicationLifetime",
        "Microsoft.AspNetCore.Http.IHttpContextAccessor",
        "Microsoft.Extensions.Caching.Memory.IMemoryCache",
        "Microsoft.Extensions.Caching.Distributed.IDistributedCache",
        "Mediator.IMediator",
        "Mediator.ISender",
        "Mediator.IPublisher",
        "Microsoft.Extensions.Hosting.IHostedService",
        "Microsoft.Extensions.FileProviders.IFileProvider",
        "Microsoft.Extensions.Primitives.IChangeToken",
        "System.ComponentModel.INotifyPropertyChanged",
        "System.IDisposable",
        "System.IAsyncDisposable"
    };

    private static readonly string[] GenericPrefixes =
    {
        "Microsoft.Extensions.Logging.ILogger<", "Microsoft.Extensions.Options.IOptions<",
        "Microsoft.Extensions.Options.IOptionsMonitor<", "Microsoft.Extensions.Options.IOptionsSnapshot<"
    };

    private static readonly string[] ExactPrefixes = { "Microsoft.Extensions.Logging.ILogger" };

    internal static bool IsFrameworkType(string typeName)
    {
        if (Exact.Contains(typeName)) return true;
        foreach (var p in ExactPrefixes)
            if (string.Equals(typeName, p, StringComparison.Ordinal))
                return true;
        foreach (var gp in GenericPrefixes)
            if (typeName.StartsWith(gp, StringComparison.Ordinal))
                return true;
        return false;
    }
}
