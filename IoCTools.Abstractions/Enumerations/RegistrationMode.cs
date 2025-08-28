namespace IoCTools.Abstractions.Enumerations;

public enum RegistrationMode
{
    DirectOnly, // Register only the concrete type (no interfaces)
    All, // Register concrete type and all interfaces including inherited ones  
    Exclusionary // Register only interfaces (including inherited) except explicitly excluded ones (no concrete type)
}
