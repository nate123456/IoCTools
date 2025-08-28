namespace IoCTools.Abstractions.Annotations;

using System;

/// <summary>
///     Marks a field or DependsOn attribute as depending on an externally registered service.
///     Use this for services registered manually in Program.cs or provided by frameworks.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Class | AttributeTargets.Interface)]
public sealed class ExternalServiceAttribute : Attribute
{
}
