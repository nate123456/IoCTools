namespace IoCTools.Abstractions.Annotations;

using System;

using Enumerations;

/// <summary>
///     Specifies that a service should be registered as specific interface types in the dependency injection container.
///     This provides selective interface registration compared to RegisterAsAll which registers all implemented
///     interfaces.
/// </summary>
/// <typeparam name="T1">The first interface type to register the service as</typeparam>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RegisterAsAttribute<T1>(InstanceSharing instanceSharing = InstanceSharing.Separate) : Attribute
{
    public InstanceSharing InstanceSharing { get; } = instanceSharing;
}

/// <summary>
///     Specifies that a service should be registered as specific interface types in the dependency injection container.
/// </summary>
/// <typeparam name="T1">The first interface type to register the service as</typeparam>
/// <typeparam name="T2">The second interface type to register the service as</typeparam>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RegisterAsAttribute<T1, T2>(InstanceSharing instanceSharing = InstanceSharing.Separate) : Attribute
{
    public InstanceSharing InstanceSharing { get; } = instanceSharing;
}

/// <summary>
///     Specifies that a service should be registered as specific interface types in the dependency injection container.
/// </summary>
/// <typeparam name="T1">The first interface type to register the service as</typeparam>
/// <typeparam name="T2">The second interface type to register the service as</typeparam>
/// <typeparam name="T3">The third interface type to register the service as</typeparam>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RegisterAsAttribute<T1, T2, T3>(InstanceSharing instanceSharing = InstanceSharing.Separate)
    : Attribute
{
    public InstanceSharing InstanceSharing { get; } = instanceSharing;
}

/// <summary>
///     Specifies that a service should be registered as specific interface types in the dependency injection container.
/// </summary>
/// <typeparam name="T1">The first interface type to register the service as</typeparam>
/// <typeparam name="T2">The second interface type to register the service as</typeparam>
/// <typeparam name="T3">The third interface type to register the service as</typeparam>
/// <typeparam name="T4">The fourth interface type to register the service as</typeparam>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RegisterAsAttribute<T1, T2, T3, T4>(InstanceSharing instanceSharing = InstanceSharing.Separate)
    : Attribute
{
    public InstanceSharing InstanceSharing { get; } = instanceSharing;
}

/// <summary>
///     Specifies that a service should be registered as specific interface types in the dependency injection container.
/// </summary>
/// <typeparam name="T1">The first interface type to register the service as</typeparam>
/// <typeparam name="T2">The second interface type to register the service as</typeparam>
/// <typeparam name="T3">The third interface type to register the service as</typeparam>
/// <typeparam name="T4">The fourth interface type to register the service as</typeparam>
/// <typeparam name="T5">The fifth interface type to register the service as</typeparam>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RegisterAsAttribute<T1, T2, T3, T4, T5>(InstanceSharing instanceSharing = InstanceSharing.Separate)
    : Attribute
{
    public InstanceSharing InstanceSharing { get; } = instanceSharing;
}

/// <summary>
///     Specifies that a service should be registered as specific interface types in the dependency injection container.
/// </summary>
/// <typeparam name="T1">The first interface type to register the service as</typeparam>
/// <typeparam name="T2">The second interface type to register the service as</typeparam>
/// <typeparam name="T3">The third interface type to register the service as</typeparam>
/// <typeparam name="T4">The fourth interface type to register the service as</typeparam>
/// <typeparam name="T5">The fifth interface type to register the service as</typeparam>
/// <typeparam name="T6">The sixth interface type to register the service as</typeparam>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RegisterAsAttribute<T1, T2, T3, T4, T5, T6>(
    InstanceSharing instanceSharing = InstanceSharing.Separate) : Attribute
{
    public InstanceSharing InstanceSharing { get; } = instanceSharing;
}

/// <summary>
///     Specifies that a service should be registered as specific interface types in the dependency injection container.
/// </summary>
/// <typeparam name="T1">The first interface type to register the service as</typeparam>
/// <typeparam name="T2">The second interface type to register the service as</typeparam>
/// <typeparam name="T3">The third interface type to register the service as</typeparam>
/// <typeparam name="T4">The fourth interface type to register the service as</typeparam>
/// <typeparam name="T5">The fifth interface type to register the service as</typeparam>
/// <typeparam name="T6">The sixth interface type to register the service as</typeparam>
/// <typeparam name="T7">The seventh interface type to register the service as</typeparam>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RegisterAsAttribute<T1, T2, T3, T4, T5, T6, T7>(
    InstanceSharing instanceSharing = InstanceSharing.Separate) : Attribute
{
    public InstanceSharing InstanceSharing { get; } = instanceSharing;
}

/// <summary>
///     Specifies that a service should be registered as specific interface types in the dependency injection container.
/// </summary>
/// <typeparam name="T1">The first interface type to register the service as</typeparam>
/// <typeparam name="T2">The second interface type to register the service as</typeparam>
/// <typeparam name="T3">The third interface type to register the service as</typeparam>
/// <typeparam name="T4">The fourth interface type to register the service as</typeparam>
/// <typeparam name="T5">The fifth interface type to register the service as</typeparam>
/// <typeparam name="T6">The sixth interface type to register the service as</typeparam>
/// <typeparam name="T7">The seventh interface type to register the service as</typeparam>
/// <typeparam name="T8">The eighth interface type to register the service as</typeparam>
[AttributeUsage(AttributeTargets.Class)]
public sealed class RegisterAsAttribute<T1, T2, T3, T4, T5, T6, T7, T8>(
    InstanceSharing instanceSharing = InstanceSharing.Separate) : Attribute
{
    public InstanceSharing InstanceSharing { get; } = instanceSharing;
}
