namespace IoCTools.Generator.CodeGeneration;

using System.Collections.Generic;
using System.Linq;

using Generator;

using Microsoft.CodeAnalysis;

using Models;

using Utilities;

internal static partial class ServiceRegistrationGenerator
{
    internal static IEnumerable<ServiceRegistration> GetMultiInterfaceRegistrations(INamedTypeSymbol classSymbol,
        AttributeData registerAsAllAttribute,
        string lifetime)
    {
        var registrationMode = ExtractRegistrationMode(registerAsAllAttribute);
        var instanceSharing = ExtractInstanceSharing(registerAsAllAttribute, lifetime);
        var skipConcreteRegistration = classSymbol.IsAbstract;
        return GenerateMultiInterfaceRegistrationsInternal(classSymbol, registerAsAllAttribute, lifetime,
            registrationMode, instanceSharing, skipConcreteRegistration);
    }

    internal static IEnumerable<ServiceRegistration> GetMultiInterfaceRegistrationsForConditionalServices(
        INamedTypeSymbol classSymbol,
        AttributeData registerAsAllAttribute,
        string lifetime)
    {
        var registrationMode = ExtractRegistrationMode(registerAsAllAttribute);
        var instanceSharing = ExtractInstanceSharing(registerAsAllAttribute, lifetime);

        if (registrationMode == "All" && instanceSharing == "Separate")
        {
            var interfaces = GetInterfacesToRegister(classSymbol, registrationMode, GetSkippedInterfaces(classSymbol));
            if (interfaces.Any()) instanceSharing = "Shared";
        }

        var skipConcreteRegistration = classSymbol.IsAbstract;
        return GenerateMultiInterfaceRegistrationsInternal(classSymbol, registerAsAllAttribute, lifetime,
            registrationMode, instanceSharing, skipConcreteRegistration);
    }

    private static IEnumerable<ServiceRegistration> GenerateMultiInterfaceRegistrationsInternal(
        INamedTypeSymbol classSymbol,
        AttributeData registerAsAllAttribute,
        string lifetime,
        string registrationMode,
        string instanceSharing,
        bool skipConcreteRegistration = false)
    {
        var registrations = new List<ServiceRegistration>();
        var hasConfigurationInjection = ServiceDiscovery.HasInjectConfigurationFieldsAcrossPartialClasses(classSymbol);
        var interfacesToRegister =
            GetInterfacesToRegister(classSymbol, registrationMode, GetSkippedInterfaces(classSymbol));

        var skippedInterfaces = GetSkippedInterfaces(classSymbol);

        if (registrationMode == "DirectOnly")
        {
            if (!skipConcreteRegistration)
            {
                var (hasLifetimeAttribute, _, _, _) = ServiceDiscovery.GetLifetimeAttributes(classSymbol);
                var useSharedInstanceForConcrete = hasLifetimeAttribute;
                registrations.Add(new ServiceRegistration(classSymbol, classSymbol, lifetime,
                    useSharedInstanceForConcrete, hasConfigurationInjection));
            }
        }
        else if (registrationMode == "All")
        {
            var useFactoryPattern =
                (instanceSharing == "Shared" || lifetime == "Singleton") && interfacesToRegister.Any();

            if (useFactoryPattern)
            {
                if (!skipConcreteRegistration)
                {
                    var (hasLifetimeAttribute, _, _, _) = ServiceDiscovery.GetLifetimeAttributes(classSymbol);
                    var useSharedInstanceForConcrete = hasLifetimeAttribute ||
                                                       (classSymbol.TypeParameters.Length > 0 &&
                                                        instanceSharing == "Shared");
                    registrations.Add(new ServiceRegistration(classSymbol, classSymbol, lifetime,
                        useSharedInstanceForConcrete, hasConfigurationInjection));
                }

                foreach (var interfaceSymbol in interfacesToRegister)
                    registrations.Add(new ServiceRegistration(classSymbol, interfaceSymbol, lifetime, true,
                        hasConfigurationInjection));
            }
            else
            {
                if (!skipConcreteRegistration)
                {
                    var (hasLifetimeAttribute, _, _, _) = ServiceDiscovery.GetLifetimeAttributes(classSymbol);
                    var useSharedInstanceForConcrete = hasLifetimeAttribute;
                    registrations.Add(new ServiceRegistration(classSymbol, classSymbol, lifetime,
                        useSharedInstanceForConcrete, hasConfigurationInjection));
                }

                foreach (var interfaceSymbol in interfacesToRegister)
                    registrations.Add(new ServiceRegistration(classSymbol, interfaceSymbol, lifetime, false,
                        hasConfigurationInjection));
            }
        }
        else if (registrationMode == "Exclusionary")
        {
            var useFactoryPatternExclusionary =
                (instanceSharing == "Shared" || lifetime == "Singleton") && interfacesToRegister.Any();

            if (useFactoryPatternExclusionary)
            {
                var (hasLifetimeAttribute, _, _, _) = ServiceDiscovery.GetLifetimeAttributes(classSymbol);
                registrations.Add(new ServiceRegistration(classSymbol, classSymbol, lifetime, hasLifetimeAttribute,
                    hasConfigurationInjection));
                foreach (var interfaceSymbol in interfacesToRegister)
                    registrations.Add(new ServiceRegistration(classSymbol, interfaceSymbol, lifetime, true,
                        hasConfigurationInjection));
            }
            else
            {
                foreach (var interfaceSymbol in interfacesToRegister)
                    registrations.Add(new ServiceRegistration(classSymbol, interfaceSymbol, lifetime, false,
                        hasConfigurationInjection));
            }
        }

        return registrations;
    }

    private static HashSet<string> GetSkippedInterfaces(INamedTypeSymbol classSymbol)
    {
        var skipped = new HashSet<string>();
        var skipAttributes = classSymbol.GetAttributes()
            .Where(attr => attr.AttributeClass?.Name.StartsWith("SkipRegistrationAttribute") == true);
        foreach (var skipAttribute in skipAttributes)
            if (skipAttribute.AttributeClass?.TypeArguments != null)
                foreach (var typeArg in skipAttribute.AttributeClass.TypeArguments)
                    skipped.Add(typeArg.ToDisplayString());
        return skipped;
    }

    private static List<INamedTypeSymbol> GetInterfacesToRegister(INamedTypeSymbol classSymbol,
        string registrationMode,
        HashSet<string> skippedInterfaces)
    {
        var interfacesToRegister = new List<INamedTypeSymbol>();
        switch (registrationMode)
        {
            case "DirectOnly":
                break;
            case "All":
            {
                var allInterfaces = GetAllInterfaces(classSymbol);
                foreach (var iface in allInterfaces)
                {
                    var name = iface.ToDisplayString();
                    if (skippedInterfaces.Contains(name)) continue;
                    if (classSymbol.TypeParameters.Length > 0 && iface.TypeParameters.Length == 0) continue;
                    interfacesToRegister.Add(iface);
                }

                break;
            }
            case "Exclusionary":
            {
                var allInterfaces = GetAllInterfaces(classSymbol);
                foreach (var iface in allInterfaces)
                {
                    var name = iface.ToDisplayString();
                    if (skippedInterfaces.Contains(name)) continue;
                    if (classSymbol.TypeParameters.Length > 0 && iface.TypeParameters.Length == 0) continue;
                    interfacesToRegister.Add(iface);
                }

                break;
            }
        }

        return interfacesToRegister;
    }

    private static List<INamedTypeSymbol> GetAllInterfaces(INamedTypeSymbol typeSymbol)
    {
        var all = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        CollectAllInterfaces(typeSymbol, all);
        return all.ToList();
    }

    private static void CollectAllInterfaces(INamedTypeSymbol typeSymbol,
        HashSet<INamedTypeSymbol> all)
    {
        foreach (var interfaceSymbol in typeSymbol.Interfaces)
            if (all.Add(interfaceSymbol))
                CollectAllInterfaces(interfaceSymbol, all);

        var baseType = typeSymbol.BaseType;
        if (baseType != null && baseType.SpecialType != SpecialType.System_Object) CollectAllInterfaces(baseType, all);

        foreach (var interfaceSymbol in typeSymbol.AllInterfaces) all.Add(interfaceSymbol);
    }

    private static string ExtractRegistrationMode(AttributeData registerAsAllAttribute)
        => AttributeParser.GetRegistrationMode(registerAsAllAttribute);

    private static string ExtractInstanceSharing(AttributeData registerAsAllAttribute,
        string lifetime)
    {
        var sharingArg = registerAsAllAttribute.NamedArguments
            .FirstOrDefault(arg => arg.Key == "InstanceSharing" || arg.Key == "instanceSharing");
        if (sharingArg.Key != null)
        {
            var sharingValue = sharingArg.Value.Value?.ToString();
            if (sharingValue is "Separate" or "Shared") return sharingValue;
        }

        if (registerAsAllAttribute.ConstructorArguments.Length > 1)
        {
            var ctor = registerAsAllAttribute.ConstructorArguments[1].Value;
            var explicitSharing = ctor switch { 0 => "Separate", 1 => "Shared", _ => null };
            if (explicitSharing != null) return explicitSharing;
        }

        if (lifetime == "Singleton") return "Shared";
        return GetExpectedInstanceSharingDefault(ExtractRegistrationMode(registerAsAllAttribute));
    }

    private static string GetExpectedInstanceSharingDefault(string registrationMode)
        => registrationMode switch
        {
            "All" => "Separate",
            "Exclusionary" => "Shared",
            "DirectOnly" => "Separate",
            _ => "Separate"
        };
}
