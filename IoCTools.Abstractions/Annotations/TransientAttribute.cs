namespace IoCTools.Abstractions.Annotations;

using System;

/// <summary>
///     Marks a class as a transient service to be registered with the dependency injection container.
///     Transient services are created each time they are requested from the service container.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class TransientAttribute : Attribute
{
}
