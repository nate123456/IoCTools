namespace IoCTools.Abstractions.Annotations;

using System;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class InjectAttribute : Attribute
{
}
