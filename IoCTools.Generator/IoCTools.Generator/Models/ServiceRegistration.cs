using Microsoft.CodeAnalysis;

namespace IoCTools.Generator.Models;

internal class ServiceRegistration(
    INamedTypeSymbol classSymbol,
    INamedTypeSymbol interfaceSymbol,
    string lifetime,
    bool useSharedInstance = false,
    bool hasConfigurationInjection = false)
{
    public INamedTypeSymbol ClassSymbol { get; } = classSymbol;
    public INamedTypeSymbol InterfaceSymbol { get; } = interfaceSymbol;
    public string Lifetime { get; } = lifetime;
    public bool UseSharedInstance { get; } = useSharedInstance;
    public bool HasConfigurationInjection { get; } = hasConfigurationInjection;
}