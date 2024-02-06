using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace IoCTools.Generator;

[Generator]
public class DependencyInjectionGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
    }

    public void Execute(GeneratorExecutionContext context)
    {
        var servicesToRegister = new List<ServiceRegistration>();

        context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.projectdir", out var projectDir);

        // turn Test.Api into TestApi as a useful name to differentiate between service registration methods
        var extNameSpace = GetLastFolderName(projectDir!).Replace(".", "");

        // Process all syntax trees in the compilation.
        foreach (var syntaxTree in context.Compilation.SyntaxTrees)
        {
            var semanticModel = context.Compilation.GetSemanticModel(syntaxTree);
            var root = syntaxTree.GetRoot();

            // Find all class declarations.
            var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

            foreach (var classDeclaration in classDeclarations)
            {
                // Check if the class is marked as partial.
                var isPartial = classDeclaration.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PartialKeyword));

                var hasServiceAttribute = classDeclaration.AttributeLists
                    .SelectMany(a => a.Attributes)
                    .Any(attr =>
                        IsAttributeOfType(attr, semanticModel, "IoCTools.Abstractions.Annotations.ServiceAttribute"));

                var implementsInterface = classDeclaration.BaseList?.Types
                    .Any(baseType =>
                        semanticModel.GetSymbolInfo(baseType.Type).Symbol is ITypeSymbol
                        {
                            TypeKind: TypeKind.Interface
                        }) ?? false;

                var injectedFields = GetInjectedFields(classDeclaration, semanticModel);

                if (hasServiceAttribute && !implementsInterface)
                {
                    var diagnostic = Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "DI004",
                            "Service Attribute Present on Unlikely Class",
                            "Class '{0}' is marked with the [Service] attribute but does not implement any interfaces",
                            "DependencyInjection",
                            DiagnosticSeverity.Warning,
                            true),
                        classDeclaration.GetLocation(),
                        classDeclaration.Identifier.Text);

                    context.ReportDiagnostic(diagnostic);
                }

                switch (injectedFields.Count)
                {
                    case > 0 when !hasServiceAttribute:
                    {
                        var diagnostic = Diagnostic.Create(
                            new DiagnosticDescriptor(
                                "DI003",
                                "Missing Service Attribute",
                                "Class '{0}' uses dependency injection but is not marked with the [Service] attribute",
                                "DependencyInjection",
                                DiagnosticSeverity.Warning,
                                true),
                            classDeclaration.GetLocation(),
                            classDeclaration.Identifier.Text);

                        context.ReportDiagnostic(diagnostic);
                        break;
                    }
                    case > 0 when !isPartial:
                    {
                        var diagnostic = Diagnostic.Create(
                            new DiagnosticDescriptor(
                                "DI001",
                                "Non-partial class with [Inject]",
                                "Class '{0}' contains fields with the [Inject] attribute but is not marked as partial",
                                "DependencyInjection",
                                DiagnosticSeverity.Error,
                                true),
                            classDeclaration.GetLocation(),
                            classDeclaration.Identifier.Text);

                        context.ReportDiagnostic(diagnostic);
                        continue;
                    }
                }

                if (!hasServiceAttribute) continue;
                if (!injectedFields.Any()) continue;

                var constructorCode = GenerateConstructorCode(classDeclaration, injectedFields);
                context.AddSource($"{classDeclaration.Identifier.Text}_DI_ctor.g.cs", constructorCode);
            }

            servicesToRegister.AddRange(GetServicesToRegister(semanticModel, root));
        }

        var registrationCode = GenerateRegistrationExtensionMethod(servicesToRegister, extNameSpace);
        context.AddSource("ServiceCollectionExtensions.g.cs", registrationCode);
    }

    private static bool IsInjectAttribute(SyntaxNode attribute, SemanticModel semanticModel, string? attributeName)
    {
        var typeInfo = semanticModel.GetTypeInfo(attribute);
        return typeInfo.Type != null && typeInfo.Type.ToDisplayString() == attributeName;
    }

    private static bool IsAttributeOfType(SyntaxNode attribute, SemanticModel semanticModel, string? attributeFullName)
    {
        if (semanticModel.GetSymbolInfo(attribute).Symbol is IMethodSymbol attributeConstructor)
            return attributeConstructor.ContainingType.ToDisplayString() == attributeFullName;

        return false;
    }

    private static IEnumerable<ServiceRegistration> GetServicesToRegister(SemanticModel semanticModel, SyntaxNode root)
    {
        var serviceRegistrations = new List<ServiceRegistration>();

        foreach (var classDeclaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);
            if (classSymbol == null || !classSymbol.Interfaces.Any()) continue;

            var serviceAttribute = classSymbol.GetAttributes().FirstOrDefault(attr =>
                attr.AttributeClass?.ToDisplayString() == "IoCTools.Abstractions.Annotations.ServiceAttribute");

            if (serviceAttribute == null || classSymbol.Interfaces.Any() != true) continue;

            var lifetime = ExtractLifetime(serviceAttribute);
            var shouldRegister = ExtractShouldRegister(serviceAttribute);

            if (!shouldRegister) continue;

            serviceRegistrations.AddRange(
                classSymbol.Interfaces.Select(interfaceSymbol =>
                    new ServiceRegistration(classSymbol, interfaceSymbol, lifetime)));
        }

        return serviceRegistrations;
    }

    private static string ExtractLifetime(AttributeData serviceAttribute)
    {
        // Check constructor arguments first
        var constructorArgLifetime = serviceAttribute.ConstructorArguments.FirstOrDefault().Value;
        if (constructorArgLifetime != null)
        {
            // Assuming constructorArgLifetime is an enum represented as an int, map it to the string.
            // This example uses a switch expression for mapping.
            var lifetimeStr = constructorArgLifetime switch
            {
                0 => "Scoped",
                1 => "Transient",
                2 => "Singleton",
                _ => throw new Exception(
                    $"Couldn't parse lifetime value from constructor arguments: {constructorArgLifetime}")
            };
            return lifetimeStr;
        }

        // Then check named arguments if constructor argument wasn't used or didn't provide a valid value
        var lifetimeArg = serviceAttribute.NamedArguments.FirstOrDefault(arg => arg.Key == "Lifetime");
        if (lifetimeArg.Key != null)
        {
            var lifetimeValue = lifetimeArg.Value.Value?.ToString();
            if (lifetimeValue is "Scoped" or "Transient" or "Singleton")
                return lifetimeValue;
            if (lifetimeValue != null)
                throw new Exception("Couldn't parse lifetime value from named arguments: " + lifetimeValue);
        }

        // Default to "Scoped" if neither constructor nor named arguments specified a lifetime
        return "Scoped";
    }

    private static bool ExtractShouldRegister(AttributeData serviceAttribute)
    {
        var shouldRegisterArg =
            serviceAttribute.NamedArguments.FirstOrDefault(arg => arg.Key == "Register");
        return shouldRegisterArg.Key == null || (bool)shouldRegisterArg.Value.Value!;
    }

    private static string GenerateRegistrationExtensionMethod(IEnumerable<ServiceRegistration> services,
        string? extNameSpace)
    {
        // support for services with generic args is not yet in
        var finalServices = services.Where(s => s.ClassSymbol.TypeParameters.Length == 0).ToList();

        var uniqueNamespaces = new HashSet<string>();

        foreach (var service in finalServices)
        {
            uniqueNamespaces.Add(service.ClassSymbol.ContainingNamespace.ToDisplayString());

            var interfaces = service.ClassSymbol.Interfaces;

            foreach (var ns in interfaces.Select(i => i.ContainingNamespace.ToDisplayString()))
                uniqueNamespaces.Add(ns);
        }

        var usings = new StringBuilder();
        foreach (var ns in uniqueNamespaces) usings.AppendLine($"using {ns};");

        var registrations = new StringBuilder();

        foreach (var service in finalServices)
        {
            var interfaceType = service.ClassSymbol.Interfaces.FirstOrDefault()?.Name ?? service.ClassSymbol.Name;
            var classType = service.ClassSymbol.Name;
            var lifetime = service.Lifetime;

            registrations.AppendLine($"         services.Add{lifetime}<{interfaceType}, {classType}>();");
        }

        var code = $$"""
                     using Microsoft.Extensions.DependencyInjection;
                     {{usings.ToString().Trim()}}

                     namespace IoCTools.Extensions;

                     public static class ServiceCollectionExtensions
                     {
                         public static IServiceCollection Add{{extNameSpace}}RegisteredServices(this IServiceCollection services)
                         {
                              {{registrations.ToString().Trim()}}
                              return services;
                         }
                     }

                     """;
        return code;
    }


    private static List<(ITypeSymbol ServiceType, string FieldName)> GetInjectedFields(
        SyntaxNode classDeclaration, SemanticModel semanticModel)
    {
        var injectedFields = new List<(ITypeSymbol ServiceType, string FieldName)>();

        foreach (var fieldDeclaration in classDeclaration.DescendantNodes().OfType<FieldDeclarationSyntax>())
        foreach (var variable in fieldDeclaration.Declaration.Variables)
        {
            var hasInjectAttribute = fieldDeclaration.AttributeLists
                .SelectMany(a => a.Attributes)
                .Any(attr =>
                    IsInjectAttribute(attr, semanticModel, "IoCTools.Abstractions.Annotations.InjectAttribute"));

            if (!hasInjectAttribute) continue;
            var fieldType = semanticModel.GetTypeInfo(fieldDeclaration.Declaration.Type).Type;
            if (fieldType != null) injectedFields.Add((fieldType, variable.Identifier.Text));
        }

        return injectedFields;
    }

    private static string GenerateConstructorCode(TypeDeclarationSyntax classDeclaration,
        List<(ITypeSymbol ServiceType, string FieldName)> injectedFields)
    {
        var uniqueNamespaces = new HashSet<string>();

        foreach (var (serviceType, _) in injectedFields) CollectNamespaces(serviceType, uniqueNamespaces);

        var usings = new StringBuilder();
        foreach (var ns in uniqueNamespaces) usings.AppendLine($"using {ns};");

        var namespaceName = GetClassNamespace(classDeclaration);

        var fullClassName = classDeclaration.Identifier.Text;

        // Check if the class is generic
        if (classDeclaration.TypeParameterList != null)
        {
            var typeParameters = classDeclaration.TypeParameterList.Parameters
                .Select(param => param.Identifier.Text);
            fullClassName += $"<{string.Join(", ", typeParameters)}>";
        }

        var fieldsStrings = injectedFields.Select((f, index) =>
        {
            var removedNsType = uniqueNamespaces.Aggregate(f.ServiceType.ToDisplayString(),
                (current, ns) => current.Replace(ns, string.Empty));

            return $"{removedNsType.Replace(".", "")} d{index + 1}";
        });

        var parameters = string.Join(",\n        ", fieldsStrings);
        var assignments = string.Join("\n        ",
            injectedFields.Select((f, index) => $"this.{f.FieldName} = d{index + 1};"));

        var constructorCode = $$"""
                                {{usings}}
                                namespace {{namespaceName}};

                                public partial class {{fullClassName}}
                                {
                                    public {{classDeclaration.Identifier.Text}}({{parameters}})
                                    {
                                        {{assignments}}
                                    }
                                }
                                """.Trim();

        return constructorCode;
    }

    private static void CollectNamespaces(ISymbol typeSymbol, ISet<string> namespaces)
    {
        // Add the namespace of the current type
        var ns = typeSymbol.ContainingNamespace.ToDisplayString();
        if (!string.IsNullOrEmpty(ns) && ns != "<global namespace>") namespaces.Add(ns);

        if (typeSymbol is not INamedTypeSymbol { IsGenericType: true } namedTypeSymbol) return;

        // If the type is a generic type, recursively collect namespaces from its type arguments
        foreach (var typeArgument in namedTypeSymbol.TypeArguments) CollectNamespaces(typeArgument, namespaces);
    }

    // shamefully stolen from https://andrewlock.net/creating-a-source-generator-part-5-finding-a-type-declarations-namespace-and-type-hierarchy/
    private static string GetClassNamespace(SyntaxNode classDecl)
    {
        // If we don't have a namespace at all we'll return an empty string
        // This accounts for the "default namespace" case
        var nameSpace = string.Empty;

        // Get the containing syntax node for the type declaration
        // (could be a nested type, for example)
        var potentialNamespaceParent = classDecl.Parent;

        // Keep moving "out" of nested classes etc until we get to a namespace
        // or until we run out of parents
        while (potentialNamespaceParent != null &&
               potentialNamespaceParent is not NamespaceDeclarationSyntax
               && potentialNamespaceParent is not FileScopedNamespaceDeclarationSyntax)
            potentialNamespaceParent = potentialNamespaceParent.Parent;

        // Build up the final namespace by looping until we no longer have a namespace declaration
        if (potentialNamespaceParent is not BaseNamespaceDeclarationSyntax namespaceParent) return nameSpace;

        // We have a namespace. Use that as the type
        nameSpace = namespaceParent.Name.ToString();

        // Keep moving "out" of the namespace declarations until we 
        // run out of nested namespace declarations
        while (true)
        {
            if (namespaceParent.Parent is not NamespaceDeclarationSyntax parent) break;

            // Add the outer namespace as a prefix to the final namespace
            nameSpace = $"{namespaceParent.Name}.{nameSpace}";
            namespaceParent = parent;
        }

        // return the final namespace
        return nameSpace;
    }

    private static string GetLastFolderName(string folderPath)
    {
        // Ensure the path ends with a directory separator to correctly handle paths that end with a folder name.
        if (!folderPath.EndsWith(Path.DirectorySeparatorChar.ToString())) folderPath += Path.DirectorySeparatorChar;

        var directoryInfo = new DirectoryInfo(folderPath);
        return directoryInfo.Name;
    }
}

internal class ServiceRegistration(INamedTypeSymbol classSymbol, INamedTypeSymbol interfaceSymbol, string lifetime)
{
    public INamedTypeSymbol ClassSymbol { get; } = classSymbol;
    public INamedTypeSymbol InterfaceSymbol { get; } = interfaceSymbol;
    public string Lifetime { get; } = lifetime;
}