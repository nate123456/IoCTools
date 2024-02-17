using System;
using IoCTools.Abstractions.Enumerations;

namespace IoCTools.Abstractions.Annotations;

[AttributeUsage(AttributeTargets.Class)]
public sealed class ServiceAttribute(Lifetime lifetime = Lifetime.Scoped, bool register = true) : Attribute
{
    public Lifetime Lifetime { get; set; } = lifetime;
    public bool Register { get; set; } = register;
}