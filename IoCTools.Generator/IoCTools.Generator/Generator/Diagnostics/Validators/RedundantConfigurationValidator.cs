namespace IoCTools.Generator.Generator.Diagnostics.Validators;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Analysis;

using Intent;

using IoCTools.Generator.Diagnostics;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Utilities;

internal static class RedundantConfigurationValidator
{
    internal static void Validate(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        ValidateRegisterAsMatchesImplementedInterfaces(context, classDeclaration, classSymbol);
        ValidateScopedLifetimeRedundancy(context, classDeclaration, classSymbol);
        ValidateRegisterAsCombinedWithRegisterAsAll(context, classDeclaration, classSymbol);
        ValidateConflictingLifetimeAttributes(context, classDeclaration, classSymbol);
        ValidateSkipRegistrationOverridesIntent(context, classDeclaration, classSymbol);
        ValidateSkipRegistrationIneffectiveMode(context, classDeclaration, classSymbol);
    }

    private static void ValidateRegisterAsMatchesImplementedInterfaces(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        var registerAsAttributes = GetRegisterAsAttributes(classSymbol);
        if (registerAsAttributes.Count == 0) return;

        var implementedInterfaces = InterfaceDiscovery.GetAllInterfacesForService(classSymbol);
        if (implementedInterfaces.Count == 0) return;

        var declaredInterfaceSet = new HashSet<INamedTypeSymbol>(implementedInterfaces, SymbolEqualityComparer.Default);
        if (declaredInterfaceSet.Count == 0) return;

        var registerAsInterfaces = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var symbol in registerAsAttributes
                     .SelectMany(attr => attr.AttributeClass?.TypeArguments ?? ImmutableArray<ITypeSymbol>.Empty)
                     .OfType<INamedTypeSymbol>())
            if (symbol.TypeKind == TypeKind.Interface)
                registerAsInterfaces.Add(symbol);

        if (registerAsInterfaces.Count == 0) return;
        if (!registerAsInterfaces.SetEquals(declaredInterfaceSet)) return;

        var formattedInterfaces = string.Join(
            ", ",
            registerAsInterfaces
                .Select(symbol => symbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat))
                .OrderBy(name => name, StringComparer.Ordinal));

        foreach (var attribute in registerAsAttributes)
        {
            var location = attribute.ApplicationSyntaxReference?.GetSyntax()?.GetLocation() ??
                           classDeclaration.GetLocation();
            var diagnostic = Diagnostic.Create(DiagnosticDescriptors.RedundantRegisterAsAttribute,
                location,
                classSymbol.Name,
                formattedInterfaces);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void ValidateScopedLifetimeRedundancy(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        var (hasLifetimeAttribute, isScoped, _, _) = ServiceDiscovery.GetLifetimeAttributes(classSymbol);
        if (!hasLifetimeAttribute || !isScoped) return;

        var attributes = classSymbol.GetAttributes();
        if (attributes.Any(IsRegisterAsAllAttribute)) return; // Required lifetime attribute
        if (attributes.Any(IsConditionalServiceAttribute)) return; // Conditional services require explicit lifetime

        var hasRegisterAs = attributes.Any(IsRegisterAsAttribute);
        var hasDependsOn = attributes.Any(IsDependsOnAttribute);
        var hasInjectFields = ServiceDiscovery.HasInjectFieldsAcrossPartialClasses(classSymbol);
        var hasInjectConfigurationFields =
            ServiceDiscovery.HasInjectConfigurationFieldsAcrossPartialClasses(classSymbol);
        var isHostedService = TypeAnalyzer.IsAssignableFromIHostedService(classSymbol);
        var isPartialWithInterfaces = classDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)) &&
                                      classSymbol.Interfaces.Any();

        var hasImplicitIntent = ServiceIntentEvaluator.HasExplicitServiceIntent(
            classSymbol,
            hasInjectFields,
            hasInjectConfigurationFields,
            hasDependsOn,
            attributes.Any(IsConditionalServiceAttribute),
            attributes.Any(IsRegisterAsAllAttribute),
            hasRegisterAs,
            false, // Evaluate intent assuming no lifetime attribute
            isHostedService,
            isPartialWithInterfaces);

        if (!hasImplicitIntent) return;

        var reasons = BuildScopedRedundancyReasons(hasDependsOn, hasRegisterAs, hasInjectFields,
            hasInjectConfigurationFields, isHostedService, isPartialWithInterfaces);
        var reasonText = reasons.Count > 0
            ? string.Join(", ", reasons.Distinct(StringComparer.Ordinal))
            : "existing service intent";

        var scopedAttribute = attributes.FirstOrDefault(IsScopedAttribute);
        var location = scopedAttribute?.ApplicationSyntaxReference?.GetSyntax()?.GetLocation() ??
                       classDeclaration.GetLocation();

        var diagnostic = Diagnostic.Create(DiagnosticDescriptors.RedundantScopedLifetimeAttribute,
            location,
            classSymbol.Name,
            reasonText);
        context.ReportDiagnostic(diagnostic);
    }

    private static void ValidateRegisterAsCombinedWithRegisterAsAll(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        var registerAsAttributes = GetRegisterAsAttributes(classSymbol);
        if (registerAsAttributes.Count == 0) return;

        var hasRegisterAsAll = classSymbol.GetAttributes().Any(IsRegisterAsAllAttribute);
        if (!hasRegisterAsAll) return;

        foreach (var attribute in registerAsAttributes)
        {
            var location = attribute.ApplicationSyntaxReference?.GetSyntax()?.GetLocation() ??
                           classDeclaration.GetLocation();
            var diagnostic = Diagnostic.Create(DiagnosticDescriptors.RedundantRegisterAsWithRegisterAsAll,
                location,
                classSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void ValidateConflictingLifetimeAttributes(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        var lifetimeAttributes = classSymbol.GetAttributes()
            .Where(attr => IsScopedAttribute(attr) || IsSingletonAttribute(attr) || IsTransientAttribute(attr))
            .ToList();
        if (lifetimeAttributes.Count <= 1) return;

        var formattedNames = lifetimeAttributes
            .Select(attr => attr.AttributeClass?.Name?.Replace("Attribute", string.Empty) ?? "Lifetime")
            .Distinct(StringComparer.Ordinal)
            .Select(name => $"[{name}]")
            .ToList();

        var location = lifetimeAttributes[1].ApplicationSyntaxReference?.GetSyntax()?.GetLocation() ??
                       classDeclaration.GetLocation();

        var diagnostic = Diagnostic.Create(DiagnosticDescriptors.MultipleLifetimeAttributes,
            location,
            classSymbol.Name,
            string.Join(", ", formattedNames));
        context.ReportDiagnostic(diagnostic);
    }

    private static void ValidateSkipRegistrationOverridesIntent(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        var skipAllAttribute = classSymbol.GetAttributes()
            .FirstOrDefault(IsNonGenericSkipRegistrationAttribute);
        if (skipAllAttribute == null) return;

        var conflicts = new List<string>();

        var lifetimeAttributes = classSymbol.GetAttributes()
            .Where(attr => IsScopedAttribute(attr) || IsSingletonAttribute(attr) || IsTransientAttribute(attr))
            .Select(attr => attr.AttributeClass?.Name?.Replace("Attribute", string.Empty) ?? "Lifetime")
            .ToList();
        if (lifetimeAttributes.Any())
            conflicts.AddRange(lifetimeAttributes.Select(name => $"[{name}]"));

        if (classSymbol.GetAttributes().Any(IsRegisterAsAllAttribute)) conflicts.Add("[RegisterAsAll]");
        if (GetRegisterAsAttributes(classSymbol).Any()) conflicts.Add("[RegisterAs]");
        if (classSymbol.GetAttributes().Any(IsConditionalServiceAttribute)) conflicts.Add("[ConditionalService]");

        if (!conflicts.Any()) return;

        var location = skipAllAttribute.ApplicationSyntaxReference?.GetSyntax()?.GetLocation() ??
                       classDeclaration.GetLocation();
        var diagnostic = Diagnostic.Create(DiagnosticDescriptors.SkipRegistrationOverridesOtherAttributes,
            location,
            classSymbol.Name,
            string.Join(", ", conflicts.Distinct(StringComparer.Ordinal)));
        context.ReportDiagnostic(diagnostic);
    }

    private static void ValidateSkipRegistrationIneffectiveMode(SourceProductionContext context,
        TypeDeclarationSyntax classDeclaration,
        INamedTypeSymbol classSymbol)
    {
        var registerAsAllAttribute = classSymbol.GetAttributes().FirstOrDefault(IsRegisterAsAllAttribute);
        if (registerAsAllAttribute == null) return;

        var registrationMode = AttributeParser.GetRegistrationMode(registerAsAllAttribute);
        if (!string.Equals(registrationMode, "DirectOnly", StringComparison.Ordinal)) return;

        var genericSkipAttributes = classSymbol.GetAttributes()
            .Where(IsGenericSkipRegistrationAttribute)
            .ToList();
        if (!genericSkipAttributes.Any()) return;

        foreach (var attribute in genericSkipAttributes)
        {
            var location = attribute.ApplicationSyntaxReference?.GetSyntax()?.GetLocation() ??
                           classDeclaration.GetLocation();
            var diagnostic = Diagnostic.Create(DiagnosticDescriptors.SkipRegistrationIneffectiveInDirectMode,
                location,
                classSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static List<string> BuildScopedRedundancyReasons(bool hasDependsOn,
        bool hasRegisterAs,
        bool hasInjectFields,
        bool hasInjectConfigurationFields,
        bool isHostedService,
        bool isPartialWithInterfaces)
    {
        var reasons = new List<string>();
        if (hasDependsOn) reasons.Add("[DependsOn]");
        if (hasRegisterAs) reasons.Add("[RegisterAs]");
        if (hasInjectFields && !hasInjectConfigurationFields) reasons.Add("[Inject]");
        if (isHostedService) reasons.Add("BackgroundService inheritance");
        if (!reasons.Any() && isPartialWithInterfaces) reasons.Add("partial interface type");
        return reasons;
    }

    private static List<AttributeData> GetRegisterAsAttributes(INamedTypeSymbol classSymbol)
    {
        return classSymbol.GetAttributes()
            .Where(IsRegisterAsAttribute)
            .ToList();
    }

    private static bool IsRegisterAsAttribute(AttributeData attribute)
    {
        var display = attribute.AttributeClass?.ToDisplayString();
        return attribute.AttributeClass?.IsGenericType == true &&
               display != null &&
               display.StartsWith("IoCTools.Abstractions.Annotations.RegisterAsAttribute", StringComparison.Ordinal);
    }

    private static bool IsRegisterAsAllAttribute(AttributeData attribute)
        => attribute.AttributeClass?.ToDisplayString() ==
           "IoCTools.Abstractions.Annotations.RegisterAsAllAttribute";

    private static bool IsConditionalServiceAttribute(AttributeData attribute)
        => attribute.AttributeClass?.ToDisplayString() ==
           "IoCTools.Abstractions.Annotations.ConditionalServiceAttribute";

    private static bool IsDependsOnAttribute(AttributeData attribute)
        => attribute.AttributeClass?.Name?.StartsWith("DependsOn", StringComparison.Ordinal) == true;

    private static bool IsScopedAttribute(AttributeData attribute)
        => attribute.AttributeClass?.ToDisplayString() ==
           "IoCTools.Abstractions.Annotations.ScopedAttribute";

    private static bool IsSingletonAttribute(AttributeData attribute)
        => attribute.AttributeClass?.ToDisplayString() ==
           "IoCTools.Abstractions.Annotations.SingletonAttribute";

    private static bool IsTransientAttribute(AttributeData attribute)
        => attribute.AttributeClass?.ToDisplayString() ==
           "IoCTools.Abstractions.Annotations.TransientAttribute";

    private static bool IsNonGenericSkipRegistrationAttribute(AttributeData attribute)
        => attribute.AttributeClass?.ToDisplayString() ==
           "IoCTools.Abstractions.Annotations.SkipRegistrationAttribute";

    private static bool IsGenericSkipRegistrationAttribute(AttributeData attribute)
        => attribute.AttributeClass?.ToDisplayString()
                .StartsWith("IoCTools.Abstractions.Annotations.SkipRegistrationAttribute", StringComparison.Ordinal) ==
            true && attribute.AttributeClass?.IsGenericType == true;
}
