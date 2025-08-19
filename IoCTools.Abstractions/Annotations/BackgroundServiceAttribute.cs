using System;

namespace IoCTools.Abstractions.Annotations;

/// <summary>
///     Marks a class as a background service that should be registered as IHostedService.
///     Classes inheriting from BackgroundService are automatically detected and registered,
///     but this attribute allows for explicit configuration.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class BackgroundServiceAttribute : Attribute
{
    public BackgroundServiceAttribute()
    {
    }

    public BackgroundServiceAttribute(bool autoRegister)
    {
        AutoRegister = autoRegister;
    }

    /// <summary>
    ///     Gets or sets whether the background service should be automatically registered as IHostedService.
    ///     Default is true for classes inheriting from BackgroundService.
    /// </summary>
    public bool AutoRegister { get; set; } = true;

    /// <summary>
    ///     Gets or sets the service name for diagnostic purposes.
    ///     If not specified, uses the class name.
    /// </summary>
    public string? ServiceName { get; set; }

    /// <summary>
    ///     Gets or sets whether to suppress diagnostic warnings about lifetime conflicts.
    ///     Default is false - warnings will be shown if Service attribute has non-Singleton lifetime.
    /// </summary>
    public bool SuppressLifetimeWarnings { get; set; } = false;
}