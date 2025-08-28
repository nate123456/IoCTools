namespace IoCTools.Abstractions.Annotations;

using System;

/// <summary>
///     Marks a class as a scoped service to be registered with the dependency injection container.
///     Scoped services are created once per scope (e.g., per HTTP request in web applications).
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class ScopedAttribute : Attribute
{
}
