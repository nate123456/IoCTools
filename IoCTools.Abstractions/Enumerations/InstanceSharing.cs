namespace IoCTools.Abstractions.Enumerations;

public enum InstanceSharing
{
    Separate, // Each interface gets its own instance
    Shared // All interfaces resolve to the same instance
}
