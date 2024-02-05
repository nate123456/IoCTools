using System;

namespace IoCTools.Generator.Annotations;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class InjectAttribute : Attribute
{
}