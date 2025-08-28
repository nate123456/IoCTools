namespace IoCTools.Generator.Models;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/// <summary>
///     Service class information captured for the incremental generator pipeline.
///     Thin record used across discovery/emit/diagnostics stages.
/// </summary>
internal readonly struct ServiceClassInfo
{
    public ServiceClassInfo(INamedTypeSymbol classSymbol,
        TypeDeclarationSyntax? classDeclaration,
        SemanticModel? semanticModel)
    {
        ClassSymbol = classSymbol;
        ClassDeclaration = classDeclaration;
        SemanticModel = semanticModel;
    }

    public INamedTypeSymbol ClassSymbol { get; }
    public TypeDeclarationSyntax? ClassDeclaration { get; }
    public SemanticModel? SemanticModel { get; }
}
