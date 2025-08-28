namespace IoCTools.Abstractions.Annotations;

using System;

/// <summary>
///     Marks a class as a singleton service to be registered with the dependency injection container.
///     Singleton services are created once for the entire application lifetime.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class SingletonAttribute : Attribute
{
}
