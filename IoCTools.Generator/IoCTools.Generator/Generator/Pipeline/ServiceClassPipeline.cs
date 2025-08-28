namespace IoCTools.Generator.Generator.Pipeline;

using System.Collections.Immutable;
using System.Linq;

using Analysis;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Models;

internal static class ServiceClassPipeline
{
    internal static IncrementalValuesProvider<ServiceClassInfo> Build(
        IncrementalGeneratorInitializationContext context)
    {
        return context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node,
                    _) => node is TypeDeclarationSyntax,
                static (ctx,
                    _) =>
                {
                    var typeDecl = (TypeDeclarationSyntax)ctx.Node;
                    var symbol = ctx.SemanticModel.GetDeclaredSymbol(typeDecl);
                    if (symbol == null || symbol.IsStatic || symbol.TypeKind != TypeKind.Class) return null;

                    var hasInject = ServiceDiscovery.HasInjectFieldsAcrossPartialClasses(symbol);
                    var hasInjectConfig = ServiceDiscovery.HasInjectConfigurationFieldsAcrossPartialClasses(symbol);
                    var hasDependsOn = symbol.GetAttributes()
                        .Any(a => a.AttributeClass?.Name?.StartsWith("DependsOn") == true);
                    var hasRegAll = symbol.GetAttributes().Any(a => a.AttributeClass?.Name == "RegisterAsAllAttribute");
                    var hasRegAs = symbol.GetAttributes().Any(a =>
                        a.AttributeClass?.Name?.StartsWith("RegisterAsAttribute") == true &&
                        a.AttributeClass?.IsGenericType == true);
                    var hasConditional = symbol.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() ==
                                                                         "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute");
                    var (hasLifetime, _, _, _) = ServiceDiscovery.GetLifetimeAttributes(symbol);
                    var isHosted = TypeAnalyzer.IsAssignableFromIHostedService(symbol);
                    var isPartialWithInterfaces = typeDecl.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)) &&
                                                  symbol.Interfaces.Any();

                    var hasServiceIntent = hasInject || hasDependsOn || hasConditional || hasRegAll || hasRegAs ||
                                           hasLifetime || isHosted || isPartialWithInterfaces || hasInjectConfig;
                    return hasServiceIntent
                        ? new ServiceClassInfo(symbol, typeDecl, ctx.SemanticModel)
                        : (ServiceClassInfo?)null;
                })
            .Where(static x => x.HasValue)
            .Select(static (x,
                _) => x!.Value)
            .Collect()
            .SelectMany(static (services,
                    _) =>
                services
                    .GroupBy(s => s.ClassSymbol, SymbolEqualityComparer.Default)
                    .Select(g => g.First())
                    .ToImmutableArray());
    }
}
