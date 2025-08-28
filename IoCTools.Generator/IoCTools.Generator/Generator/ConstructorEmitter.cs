namespace IoCTools.Generator.Generator;

using System;
using System.Linq;
using System.Text;

using Analysis;

using CodeGeneration;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using Models;

using Utilities;

internal static class ConstructorEmitter
{
    public static void EmitSingleConstructor(SourceProductionContext context,
        ServiceClassInfo serviceInfo)
    {
        try
        {
            if (serviceInfo.SemanticModel == null || serviceInfo.ClassDeclaration == null)
                return;

            var hasRegisterAsOnly = serviceInfo.ClassSymbol.GetAttributes()
                .Any(attr => attr.AttributeClass?.Name?.StartsWith("RegisterAsAttribute") == true &&
                             attr.AttributeClass?.IsGenericType == true);

            var hasInjectFields = ServiceDiscovery.HasInjectFieldsAcrossPartialClasses(serviceInfo.ClassSymbol);
            var hasInjectConfigurationFields =
                ServiceDiscovery.HasInjectConfigurationFieldsAcrossPartialClasses(serviceInfo.ClassSymbol);
            var hasDependsOnAttribute = serviceInfo.ClassSymbol.GetAttributes()
                .Any(attr => attr.AttributeClass?.Name?.StartsWith("DependsOn") == true);
            var hasConditionalServiceAttribute = serviceInfo.ClassSymbol.GetAttributes()
                .Any(attr =>
                    attr.AttributeClass?.ToDisplayString() ==
                    "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute");
            var hasRegisterAsAllAttribute = serviceInfo.ClassSymbol.GetAttributes()
                .Any(attr => attr.AttributeClass?.Name == "RegisterAsAllAttribute");
            var (hasLifetimeAttribute, _, _, _) = ServiceDiscovery.GetLifetimeAttributes(serviceInfo.ClassSymbol);
            var isHostedService = TypeAnalyzer.IsAssignableFromIHostedService(serviceInfo.ClassSymbol);

            if (hasRegisterAsOnly && !hasInjectFields && !hasInjectConfigurationFields && !hasDependsOnAttribute &&
                !hasConditionalServiceAttribute && !hasRegisterAsAllAttribute && !hasLifetimeAttribute &&
                !isHostedService)
                return;

            var hierarchyDependencies = DependencyAnalyzer.GetConstructorDependencies(
                serviceInfo.ClassSymbol, serviceInfo.SemanticModel);

            var constructorCode = GenerateInheritanceAwareConstructorCodeWithContext(
                serviceInfo.ClassDeclaration, hierarchyDependencies, serviceInfo.SemanticModel, context);

            if (!string.IsNullOrEmpty(constructorCode))
            {
                var canonicalKey = serviceInfo.ClassSymbol.ToDisplayString();
                var sanitizedTypeName = canonicalKey.Replace("<", "_").Replace(">", "_")
                    .Replace(".", "_").Replace(",", "_").Replace(" ", "_");
                var fileName = $"{sanitizedTypeName}_Constructor.g.cs";

                context.AddSource(fileName, SourceText.From(constructorCode, Encoding.UTF8));
            }
        }
        catch (Exception ex)
        {
            var typeName = serviceInfo.ClassSymbol.ToDisplayString();
            GeneratorDiagnostics.Report(context, "IOC995", "Constructor generation error",
                $"Failed to generate constructor for {typeName}: {ex.Message}");
        }
    }

    private static string GenerateInheritanceAwareConstructorCodeWithContext(
        TypeDeclarationSyntax classDeclaration,
        InheritanceHierarchyDependencies hierarchyDependencies,
        SemanticModel semanticModel,
        SourceProductionContext context)
    {
        try
        {
            return ConstructorGenerator.GenerateInheritanceAwareConstructorCodeWithContext(
                classDeclaration, hierarchyDependencies, semanticModel, context);
        }
        catch (Exception ex)
        {
            GeneratorDiagnostics.Report(context, "IOC992", "Constructor generation adapter error", ex.Message);
            return string.Empty;
        }
    }
}
