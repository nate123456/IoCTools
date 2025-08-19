using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace IoCTools.Generator.Analysis;

internal static class TypeAnalyzer
{
    /// <summary>
    ///     Collects all types from compilation and builds implementation maps for validation
    /// </summary>
    public static (Dictionary<string, List<INamedTypeSymbol>> AllImplementations,
        HashSet<string> AllRegisteredServices,
        Dictionary<string, string> ServiceLifetimes,
        List<INamedTypeSymbol> ServicesWithAttributes)
        CollectTypesAndBuildMaps(GeneratorExecutionContext context)
    {
        var allImplementations = new Dictionary<string, List<INamedTypeSymbol>>();
        var allRegisteredServices = new HashSet<string>();
        var serviceLifetimes = new Dictionary<string, string>();
        var servicesWithAttributes = new List<INamedTypeSymbol>();

        // Collect all types and build maps
        foreach (var syntaxTree in context.Compilation.SyntaxTrees)
        {
            var semanticModel = context.Compilation.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();
            var classDeclarations = root.DescendantNodes().OfType<TypeDeclarationSyntax>();

            foreach (var classDeclaration in classDeclarations)
            {
                var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration) as INamedTypeSymbol;
                if (classSymbol == null) continue;

                var hasServiceAttribute = classSymbol.GetAttributes()
                    .Any(attr =>
                        attr.AttributeClass?.ToDisplayString() == "IoCTools.Abstractions.Annotations.ServiceAttribute");

                // Collect services with attributes for processing
                if (HasRelevantAttributes(classSymbol)) servicesWithAttributes.Add(classSymbol);

                // Get service lifetime if it has [Service] attribute
                string? serviceLifetime = null;
                // Check for ExternalService attribute - external services should be treated as valid dependencies
                var hasExternalServiceAttribute = classSymbol.GetAttributes()
                    .Any(attr =>
                        attr.AttributeClass?.ToDisplayString() ==
                        "IoCTools.Abstractions.Annotations.ExternalServiceAttribute");
                if (hasServiceAttribute || hasExternalServiceAttribute)
                    serviceLifetime = GetServiceLifetime(classSymbol);

                // Check for ExternalService attribute - external services should be treated as valid dependencies

                // Collect all implementations for validation
                foreach (var interfaceSymbol in classSymbol.AllInterfaces)
                {
                    var interfaceDisplayString = interfaceSymbol.ToDisplayString();
                    if (!allImplementations.ContainsKey(interfaceDisplayString))
                        allImplementations[interfaceDisplayString] = new List<INamedTypeSymbol>();
                    allImplementations[interfaceDisplayString].Add(classSymbol);

                    if (hasServiceAttribute || hasExternalServiceAttribute)
                    {
                        allRegisteredServices.Add(interfaceDisplayString);
                        if (serviceLifetime != null) serviceLifetimes[interfaceDisplayString] = serviceLifetime;

                        // For generic interfaces, also store the open generic version
                        if (interfaceSymbol is INamedTypeSymbol namedInterface && namedInterface.IsGenericType)
                        {
                            string openGenericInterface;
                            if (namedInterface.IsUnboundGenericType)
                                // This is already an open generic type definition (e.g., IRepository<T>)
                                openGenericInterface = namedInterface.ToDisplayString();
                            else
                                // This is a constructed generic type, get the open generic definition
                                openGenericInterface = namedInterface.ConstructedFrom.ToDisplayString();

                            allRegisteredServices.Add(openGenericInterface);
                            if (serviceLifetime != null) serviceLifetimes[openGenericInterface] = serviceLifetime;
                        }
                    }

                    // ALSO add to allImplementations for generic interfaces (both open and constructed)
                    if (interfaceSymbol is INamedTypeSymbol namedInterfaceImpl && namedInterfaceImpl.IsGenericType)
                    {
                        string openGenericInterface;
                        if (namedInterfaceImpl.IsUnboundGenericType)
                            // This is already an open generic type definition (e.g., IRepository<T>)
                            openGenericInterface = namedInterfaceImpl.ToDisplayString();
                        else
                            // This is a constructed generic type, get the open generic definition
                            openGenericInterface = namedInterfaceImpl.ConstructedFrom.ToDisplayString();

                        // Add the open generic to implementations map
                        if (!allImplementations.ContainsKey(openGenericInterface))
                            allImplementations[openGenericInterface] = new List<INamedTypeSymbol>();
                        allImplementations[openGenericInterface].Add(classSymbol);
                    }
                }

                // Also track the class itself if it has [Service] or [ExternalService] attribute
                if (hasServiceAttribute || hasExternalServiceAttribute)
                {
                    var classDisplayString = classSymbol.ToDisplayString();
                    allRegisteredServices.Add(classDisplayString);
                    if (serviceLifetime != null) serviceLifetimes[classDisplayString] = serviceLifetime;

                    // For generic classes, also store the open generic version
                    if (classSymbol.IsGenericType)
                    {
                        string openGenericClass;
                        if (classSymbol.IsUnboundGenericType)
                            // This is already an open generic type definition (e.g., Repository<T>)
                            openGenericClass = classSymbol.ToDisplayString();
                        else
                            // This is a constructed generic type, get the open generic definition
                            openGenericClass = classSymbol.ConstructedFrom.ToDisplayString();

                        allRegisteredServices.Add(openGenericClass);
                        if (serviceLifetime != null) serviceLifetimes[openGenericClass] = serviceLifetime;
                    }
                }
            }
        }

        return (allImplementations, allRegisteredServices, serviceLifetimes, servicesWithAttributes);
    }

    /// <summary>
    ///     Gets the service lifetime for a class symbol
    /// </summary>
    private static string GetServiceLifetime(INamedTypeSymbol classSymbol)
    {
        var serviceAttribute = classSymbol.GetAttributes()
            .FirstOrDefault(attr => attr.AttributeClass?.ToDisplayString() ==
                                    "IoCTools.Abstractions.Annotations.ServiceAttribute");

        if (serviceAttribute == null) return "Scoped"; // Default

        // Check constructor arguments for lifetime
        if (serviceAttribute.ConstructorArguments.Length > 0)
        {
            var lifetimeValue = serviceAttribute.ConstructorArguments[0].Value;
            if (lifetimeValue != null)
            {
                // Convert enum value to string name
                var lifetimeInt = (int)lifetimeValue;
                return lifetimeInt switch
                {
                    0 => "Scoped",
                    1 => "Transient",
                    2 => "Singleton",
                    _ => "Scoped" // Default fallback
                };
            }
        }

        // Default lifetime is Scoped
        return "Scoped";
    }

    /// <summary>
    ///     Determines if a type has attributes relevant to the IoC generator
    /// </summary>
    public static bool HasRelevantAttributes(INamedTypeSymbol type)
    {
        // Static classes should be ignored regardless of attributes
        if (type.IsStatic) return false;

        // Check for explicit attributes first
        var hasAttributes = type.GetAttributes().Any(attr =>
            attr.AttributeClass?.Name == "ServiceAttribute" ||
            attr.AttributeClass?.Name == "InjectAttribute" ||
            attr.AttributeClass?.Name == "InjectConfigurationAttribute" ||
            attr.AttributeClass?.Name == "DependsOnAttribute" ||
            attr.AttributeClass?.Name == "UnregisteredServiceAttribute" ||
            attr.AttributeClass?.Name == "BackgroundServiceAttribute" ||
            attr.AttributeClass?.Name == "RegisterAsAllAttribute" ||
            attr.AttributeClass?.Name == "ExternalServiceAttribute" ||
            attr.AttributeClass?.Name == "ConditionalServiceAttribute" ||
            attr.AttributeClass?.Name?.StartsWith("SkipRegistrationAttribute") == true ||
            (attr.AttributeClass?.IsGenericType == true &&
             attr.AttributeClass.Name == "DependsOn"));

        if (hasAttributes) return true;

        // Also check for field-level attributes like [Inject] and [InjectConfiguration]
        // Use syntax-based detection for better reliability with field attributes
        foreach (var syntaxRef in type.DeclaringSyntaxReferences)
            if (syntaxRef.GetSyntax() is TypeDeclarationSyntax typeDeclaration)
                foreach (var fieldDeclaration in typeDeclaration.DescendantNodes().OfType<FieldDeclarationSyntax>())
                foreach (var attributeList in fieldDeclaration.AttributeLists)
                foreach (var attribute in attributeList.Attributes)
                {
                    var attributeText = attribute.Name.ToString();
                    if (attributeText == "Inject" || attributeText == "InjectAttribute" ||
                        attributeText.EndsWith("Inject") || attributeText.EndsWith("InjectAttribute") ||
                        attributeText == "InjectConfiguration" || attributeText == "InjectConfigurationAttribute" ||
                        attributeText.EndsWith("InjectConfiguration") ||
                        attributeText.EndsWith("InjectConfigurationAttribute"))
                        return true;
                }

        // Also consider BackgroundService inheritance as relevant for service registration
        var currentServiceType = type;
        while (currentServiceType != null)
        {
            if (currentServiceType.ToDisplayString() == "Microsoft.Extensions.Hosting.BackgroundService") return true;
            currentServiceType = currentServiceType.BaseType;
        }

        return false;
    }
}