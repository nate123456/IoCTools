using System;

namespace IoCTools.Abstractions.Annotations;

/// <summary>
///     Marks a service class to skip registration for all interfaces when used with RegisterAsAll.
///     For selective interface skipping, use the generic versions.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class SkipRegistrationAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class SkipRegistrationAttribute<TInterface> : Attribute
{
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class SkipRegistrationAttribute<TInterface1, TInterface2> : Attribute
{
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class SkipRegistrationAttribute<TInterface1, TInterface2, TInterface3> : Attribute
{
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class SkipRegistrationAttribute<TInterface1, TInterface2, TInterface3, TInterface4> : Attribute
{
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class
    SkipRegistrationAttribute<TInterface1, TInterface2, TInterface3, TInterface4, TInterface5> : Attribute
{
}