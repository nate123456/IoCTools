using System;
using IoCTools.Abstractions.Enumerations;

namespace IoCTools.Abstractions.Annotations;

[AttributeUsage(AttributeTargets.Class)]
public sealed class RegisterAsAllAttribute(
    RegistrationMode mode = RegistrationMode.All,
    InstanceSharing instanceSharing = InstanceSharing.Separate) : Attribute
{
    public RegistrationMode Mode { get; } = mode;
    public InstanceSharing InstanceSharing { get; } = instanceSharing;
}