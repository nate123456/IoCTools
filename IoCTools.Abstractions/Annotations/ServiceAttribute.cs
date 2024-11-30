using System;
using IoCTools.Abstractions.Enumerations;

namespace IoCTools.Abstractions.Annotations;

[AttributeUsage(AttributeTargets.Class)]
public sealed class ServiceAttribute(Lifetime lifetime = Lifetime.Scoped) : Attribute
{
    public Lifetime Lifetime { get; set; } = lifetime;
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class UnregisteredServiceAttribute : Attribute
{
}