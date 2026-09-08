namespace IoCTools.Generator.Generator.Diagnostics.Validators;

using System;
using System.Collections.Generic;
using System.Linq;

using IoCTools.Generator.Diagnostics;
using IoCTools.Generator.Models;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

internal static class ManualRegistrationValidator
{
    private static bool IsInsideServicesReplaceCall(InvocationExpressionSyntax invocation, SemanticModel semanticModel)
    {
        // Walk up: ArgumentSyntax → ArgumentListSyntax → InvocationExpressionSyntax (the wrapper).
        // If the wrapper invocation is `IServiceCollection.Replace(...)`, treat the inner
        // ServiceDescriptor.X<T>() / AddX<T>() as an override, not a duplicate.
        for (SyntaxNode? node = invocation.Parent; node is not null; node = node.Parent)
        {
            if (node is InvocationExpressionSyntax outer)
            {
                var outerSymbol = semanticModel.GetSymbolInfo(outer).Symbol as IMethodSymbol
                    ?? semanticModel.GetSymbolInfo(outer).CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
                if (outerSymbol is null) return false;
                if (outerSymbol.Name != "Replace") return false;
                var containingNamespace = outerSymbol.ContainingType?.ContainingNamespace?.ToDisplayString() ?? string.Empty;
                return containingNamespace.StartsWith("Microsoft.Extensions.DependencyInjection", System.StringComparison.Ordinal);
            }
        }

        return false;
    }

    /// <summary>
    ///     Detects the canonical IHostedService companion-interface bridge:
    ///     <c>services.AddSingleton&lt;IHostedService&gt;(sp =&gt; sp.GetRequiredService&lt;TImpl&gt;())</c>
    ///     (or AddScoped/AddTransient variants). This shape is intentional regardless of project
    ///     type — it is the legacy way to register a hosted service that also exposes additional
    ///     interfaces. KFS-006 made this mostly obsolete via [RegisterAs<T>] on hosted classes,
    ///     but legacy code still uses it. Always skip IOC081/082 for this shape.
    /// </summary>
    private static bool IsHostedServiceFactoryBridge(InvocationExpressionSyntax invocation,
        IMethodSymbol methodSymbol,
        SemanticModel semanticModel)
    {
        // Only single-arg generic Add{Lifetime}<IHostedService>(factory) calls qualify.
        var typeArgs = methodSymbol.TypeArguments;
        if (typeArgs.Length == 0 && methodSymbol.ReducedFrom?.TypeArguments.Length > 0)
            typeArgs = methodSymbol.ReducedFrom.TypeArguments;
        if (typeArgs.Length != 1) return false;

        var serviceType = typeArgs[0];
        var serviceFqn = serviceType.ToDisplayString();
        if (serviceFqn != "Microsoft.Extensions.Hosting.IHostedService" &&
            serviceFqn != "global::Microsoft.Extensions.Hosting.IHostedService")
            return false;

        // The factory must reference sp.GetRequiredService<TImpl>() or sp.GetService<TImpl>()
        // so the registration is bridging an existing concrete registration rather than
        // duplicating it. A factory that calls 'new TImpl(...)' is genuine duplication.
        var args = invocation.ArgumentList.Arguments;
        if (args.Count != 1) return false;
        var factory = args[0].Expression;
        if (factory is not LambdaExpressionSyntax lambda) return false;

        foreach (var inner in lambda.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var innerSymbol = semanticModel.GetSymbolInfo(inner).Symbol as IMethodSymbol
                ?? semanticModel.GetSymbolInfo(inner).CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
            if (innerSymbol is null) continue;
            if (innerSymbol.Name is not ("GetRequiredService" or "GetService")) continue;
            var innerNs = innerSymbol.ContainingType?.ContainingNamespace?.ToDisplayString() ?? string.Empty;
            if (innerNs.StartsWith("Microsoft.Extensions.DependencyInjection", StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    ///     Detects explicit-factory registration shapes:
    ///     <c>services.AddSingleton&lt;TService&gt;(sp =&gt; ...)</c>,
    ///     <c>services.TryAddSingleton&lt;TService&gt;(sp =&gt; ...)</c>, etc.
    ///     The factory body is the consumer's deliberate composition expression; IoCTools
    ///     attributes cannot capture that logic, so suggesting "remove the manual call and add
    ///     attributes" via IOC086 is incorrect for these shapes.
    /// </summary>
    private static bool IsFactoryRegistration(InvocationExpressionSyntax invocation)
    {
        var args = invocation.ArgumentList.Arguments;
        if (args.Count == 0) return false;

        // Walk arguments; any LambdaExpressionSyntax (simple or parenthesized) is the
        // factory delegate. Roslyn binds these to the
        // Func<IServiceProvider, T> overload at the call site.
        foreach (var arg in args)
        {
            if (arg.Expression is LambdaExpressionSyntax) return true;
        }

        return false;
    }

    /// <summary>
    ///     Detects <c>services.TryAddEnumerable(ServiceDescriptor.{Lifetime}&lt;TService, TImpl&gt;(...))</c>
    ///     calls. The inner <c>ServiceDescriptor</c> factory call is the invocation IOC086 would
    ///     otherwise fire on; this helper detects when that inner call is wrapped by
    ///     <c>TryAddEnumerable</c>, which is intentionally additive (one of N contributors to an
    ///     enumerable resolution) and is not a duplicate or replacement registration.
    /// </summary>
    private static bool IsInsideTryAddEnumerableCall(InvocationExpressionSyntax invocation,
        SemanticModel semanticModel)
    {
        for (SyntaxNode? node = invocation.Parent; node is not null; node = node.Parent)
        {
            if (node is InvocationExpressionSyntax outer)
            {
                var outerSymbol = semanticModel.GetSymbolInfo(outer).Symbol as IMethodSymbol
                    ?? semanticModel.GetSymbolInfo(outer).CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
                if (outerSymbol is null) return false;
                if (outerSymbol.Name != "TryAddEnumerable") return false;
                var containingNamespace = outerSymbol.ContainingType?.ContainingNamespace?.ToDisplayString() ?? string.Empty;
                return containingNamespace.StartsWith("Microsoft.Extensions.DependencyInjection", StringComparison.Ordinal);
            }
        }

        return false;
    }

    /// <summary>
    ///     Returns <c>true</c> when the given type is defined in an assembly that does not reference
    ///     <c>IoCTools.Abstractions</c>. Such types cannot be IoCTools-attributed by the consumer
    ///     (they don't own the source), so IOC086's "use IoCTools attributes instead" suggestion is
    ///     not actionable. Common cases: <c>IHttpContextAccessor</c>, <c>IPostConfigureOptions&lt;T&gt;</c>,
    ///     and other framework/third-party types. The check is principled: we look at the type's
    ///     containing assembly and ask whether it has any reference to the IoCTools.Abstractions
    ///     assembly. The current assembly under compilation is treated as IoCTools-aware so we don't
    ///     accidentally suppress legitimate manual-registration warnings on the consumer's own types.
    /// </summary>
    private static bool IsTypeFromIoCToolsUnawareAssembly(ITypeSymbol type, Compilation compilation)
    {
        var containingAssembly = type.ContainingAssembly;
        if (containingAssembly is null) return false;

        // Types defined in the current compilation are owned by the consumer; suppression here
        // would over-suppress the rule. Only types from referenced assemblies can be "external".
        if (SymbolEqualityComparer.Default.Equals(containingAssembly, compilation.Assembly))
            return false;

        // The principled signal: does the assembly reference IoCTools.Abstractions? Iterate the
        // assembly's referenced module identities. If any references IoCTools.Abstractions, the
        // assembly author could have added IoCTools attributes to the type, so we keep IOC086
        // active. If none do, the type is from an IoCTools-unaware assembly and IOC086 is not
        // actionable for the consumer.
        foreach (var module in containingAssembly.Modules)
        {
            foreach (var referenced in module.ReferencedAssemblies)
            {
                if (referenced.Name is "IoCTools.Abstractions") return false;
            }
        }

        return true;
    }

    internal static void ValidateAllTrees(SourceProductionContext context,
        Compilation compilation,
        Dictionary<string, string> serviceLifetimes,
        DiagnosticConfiguration diagnosticConfig,
        HashSet<string>? autoConfigOptionTypes = null,
        AnalyzerConfigOptionsProvider? configOptions = null)
    {
        autoConfigOptionTypes ??= new HashSet<string>(StringComparer.Ordinal);
        // Resolve once per validator pass; the answer cannot change mid-compilation.
        var isTestProject = configOptions is not null && DiagnosticGate.IsTestProject(configOptions);
        foreach (var tree in compilation.SyntaxTrees)
        {
            var semanticModel = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();

            var invocations = root.DescendantNodes().OfType<InvocationExpressionSyntax>();
            foreach (var invocation in invocations)
            {
                var symbolInfo = semanticModel.GetSymbolInfo(invocation);
                var methodSymbol = symbolInfo.Symbol as IMethodSymbol ??
                                   symbolInfo.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
                if (methodSymbol == null) continue;
                var name = methodSymbol.Name;
                var containing = methodSymbol.ContainingType?.ToDisplayString();

                // Options duplication detection
                if (name is "AddOptions" or "Configure" or "ConfigureOptions" ||
                    name is "Bind" or "BindConfiguration" ||
                    name is "BindOptions")
                {
                    var typeArgs = methodSymbol.TypeArguments;
                    if (typeArgs.Length == 0 && methodSymbol.ReducedFrom?.TypeArguments.Length > 0)
                        typeArgs = methodSymbol.ReducedFrom.TypeArguments;
                    if (typeArgs.Length > 0)
                    {
                        var opt = typeArgs[0];
                        var optName = Normalize(opt.ToDisplayString());
                        if (autoConfigOptionTypes.Contains(optName) &&
                            DiagnosticGate.ShouldReport(isTestProject, DiagnosticDescriptors.ManualOptionsRegistrationDuplicatesIoCTools))
                        {
                            var diag = Diagnostic.Create(
                                ApplyManualSeverity(DiagnosticDescriptors.ManualOptionsRegistrationDuplicatesIoCTools, diagnosticConfig),
                                invocation.GetLocation(),
                                optName);
                            context.ReportDiagnostic(diag);
                            continue;
                        }
                    }

                    // Extension methods on OptionsBuilder<T>
                    var receiver = methodSymbol.ReducedFrom?.ReceiverType ?? methodSymbol.ReceiverType;
                    if (receiver != null && receiver.Name.StartsWith("OptionsBuilder", StringComparison.Ordinal) &&
                        receiver is INamedTypeSymbol { TypeArguments.Length: > 0 } optBuilder &&
                        optBuilder.TypeArguments[0] is ITypeSymbol optArg)
                    {
                        var optName = Normalize(optArg.ToDisplayString());
                        if (autoConfigOptionTypes.Contains(optName) &&
                            DiagnosticGate.ShouldReport(isTestProject, DiagnosticDescriptors.ManualOptionsRegistrationDuplicatesIoCTools))
                        {
                            var diag = Diagnostic.Create(
                                ApplyManualSeverity(DiagnosticDescriptors.ManualOptionsRegistrationDuplicatesIoCTools, diagnosticConfig),
                                invocation.GetLocation(),
                                optName);
                            context.ReportDiagnostic(diag);
                            continue;
                        }
                    }
                }

                if (containing == null || !containing.Contains("Microsoft.Extensions.DependencyInjection")) continue;

                // Check for IServiceCollection.Add{Lifetime}() extension methods
                var isAddMethod = name is "AddScoped" or "AddSingleton" or "AddTransient";
                // Check for ServiceDescriptor.{Lifetime}() static factory methods (D-02)
                var isServiceDescriptorFactory = name is "Scoped" or "Singleton" or "Transient"
                    && methodSymbol.ContainingType?.Name == "ServiceDescriptor";
                if (!isAddMethod && !isServiceDescriptorFactory) continue;

                // services.Replace(ServiceDescriptor.X<T>(...)) is the canonical override
                // pattern — the consumer is intentionally swapping the IoCTools-registered
                // implementation for a different one (a fake in tests, an alternate impl in
                // composition root). That is not a duplicate registration.
                if (IsInsideServicesReplaceCall(invocation, semanticModel)) continue;

                // services.TryAddEnumerable(ServiceDescriptor.X<T, TImpl>(...)) is the canonical
                // additive registration shape — one of N contributors to an enumerable resolution.
                // The inner ServiceDescriptor.X<T, TImpl>() call is intentional regardless of
                // whether T or TImpl carry IoCTools attributes; suggesting the user replace it
                // with attribute-driven registration would lose the additive semantics.
                if (isServiceDescriptorFactory && IsInsideTryAddEnumerableCall(invocation, semanticModel))
                    continue;

                // services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<TImpl>())
                // is the legacy companion-interface bridge for hosted services (mostly obsolete
                // since KFS-006 made [RegisterAs<T>] handle this on hosted classes, but legacy
                // code still uses it). Always skip — this shape is intentional regardless of
                // project type.
                if (isAddMethod && IsHostedServiceFactoryBridge(invocation, methodSymbol, semanticModel))
                    continue;

                var lifetime = name switch
                {
                    "AddScoped" or "Scoped" => "Scoped",
                    "AddSingleton" or "Singleton" => "Singleton",
                    "AddTransient" or "Transient" => "Transient",
                    _ => null
                };
                if (lifetime == null) continue;

                var typeArgsSymbol = methodSymbol.TypeArguments;
                if (typeArgsSymbol.Length == 0 && methodSymbol.ReducedFrom?.TypeArguments.Length > 0)
                    typeArgsSymbol = methodSymbol.ReducedFrom.TypeArguments;

                INamedTypeSymbol? svcNamed = null;
                INamedTypeSymbol? implNamed = null;
                bool isTypeOfRegistration = false;
                bool isOpenGenericTypeOfRegistration = false;

                if (typeArgsSymbol.Length > 0)
                {
                    // Generic type argument path (existing behavior)
                    svcNamed = typeArgsSymbol[0] as INamedTypeSymbol;
                    implNamed = typeArgsSymbol.Length > 1
                        ? typeArgsSymbol[1] as INamedTypeSymbol
                        : svcNamed;
                    if (svcNamed == null || implNamed == null) continue;
                }
                else
                {
                    // typeof() argument path (DIAG-01)
                    var args = invocation.ArgumentList.Arguments;
                    if (args.Count < 1) continue;

                    var svcType = ExtractTypeFromTypeOf(args[0], semanticModel);
                    var implType = args.Count >= 2
                        ? ExtractTypeFromTypeOf(args[1], semanticModel)
                        : svcType;

                    if (svcType == null || implType == null) continue;

                    svcNamed = svcType;
                    implNamed = implType;
                    isTypeOfRegistration = true;
                    isOpenGenericTypeOfRegistration =
                        IsOpenGenericTypeOf(args[0]) || (args.Count >= 2 && IsOpenGenericTypeOf(args[1]));
                    // Fall through to the shared diagnostic emission logic below
                }

                var serviceTypeName = Normalize(svcNamed.ToDisplayString());
                var implTypeName = Normalize(implNamed.ToDisplayString());
                var serviceLookupName = Normalize(svcNamed.OriginalDefinition.ToDisplayString());
                var implementationLookupName = Normalize(implNamed.OriginalDefinition.ToDisplayString());
                var openGenericMappingValid = !isOpenGenericTypeOfRegistration ||
                                              IsValidOpenGenericTypeOfMapping(svcNamed, implNamed);
                var openGenericMappingExpressibleByIoCTools = !isOpenGenericTypeOfRegistration ||
                                                              (openGenericMappingValid &&
                                                               CanExpressOpenGenericMappingWithIoCToolsAttributes(
                                                                   svcNamed,
                                                                   implNamed));
                var openGenericMappingCoveredByIoCTools = !isOpenGenericTypeOfRegistration ||
                                                          (openGenericMappingExpressibleByIoCTools &&
                                                           IsOpenGenericMappingCoveredByIoCTools(svcNamed, implNamed));

                if (isTypeOfRegistration && isOpenGenericTypeOfRegistration && !openGenericMappingValid)
                    continue;

                if (isTypeOfRegistration && isOpenGenericTypeOfRegistration && !openGenericMappingExpressibleByIoCTools)
                    continue;

                if (isTypeOfRegistration && isOpenGenericTypeOfRegistration && !openGenericMappingCoveredByIoCTools)
                {
                    var diag = Diagnostic.Create(
                        ApplyManualSeverity(DiagnosticDescriptors.OpenGenericTypeOfCouldUseAttributes, diagnosticConfig),
                        invocation.GetLocation(),
                        serviceTypeName);
                    context.ReportDiagnostic(diag);
                    continue;
                }

                var iocLifetime = GetLifetimeIfAttributed(implNamed) ?? GetLifetimeIfAttributed(svcNamed);
                if (iocLifetime == null && isOpenGenericTypeOfRegistration)
                    iocLifetime = GetLifetimeIfAttributed(implNamed.OriginalDefinition) ??
                                  GetLifetimeIfAttributed(svcNamed.OriginalDefinition);

                if (iocLifetime == null)
                {
                    if (serviceLifetimes.TryGetValue(
                            isOpenGenericTypeOfRegistration ? serviceLookupName : serviceTypeName,
                            out var mappedLifetime))
                        iocLifetime = mappedLifetime;
                    else if ((isOpenGenericTypeOfRegistration ? implementationLookupName : implTypeName) !=
                             (isOpenGenericTypeOfRegistration ? serviceLookupName : serviceTypeName) &&
                             serviceLifetimes.TryGetValue(
                                 isOpenGenericTypeOfRegistration ? implementationLookupName : implTypeName,
                                 out var implMappedLifetime))
                        iocLifetime = implMappedLifetime;
                }

                if (iocLifetime == null)
                {
                    // IOC086 carve-outs (also apply to the typeof / open-generic variants):
                    //
                    //   1. Factory registrations — services.AddSingleton<T>(sp => ...) and
                    //      services.TryAddSingleton<T>(sp => ...) etc. The lambda body is the
                    //      consumer's deliberate composition; IoCTools attributes cannot capture
                    //      that logic, so "use IoCTools attributes instead" is not actionable.
                    //   2. Implementation type defined in an assembly that has no reference to
                    //      IoCTools.Abstractions — e.g. IHttpContextAccessor, IPostConfigureOptions<T>,
                    //      third-party framework types. The consumer cannot add IoCTools attributes
                    //      to source they don't own, so the suggestion is not actionable.
                    //
                    // Lifetime-conflict diagnostics (IOC081 / IOC082) still fire on factory shapes
                    // when an IoCTools-attributed type is involved — those carve-outs only apply
                    // to the IOC086 / "could use attributes" emission below.
                    if (IsFactoryRegistration(invocation)) continue;
                    if (IsTypeFromIoCToolsUnawareAssembly(implNamed, compilation)) continue;
                    if (!SymbolEqualityComparer.Default.Equals(svcNamed, implNamed) &&
                        IsTypeFromIoCToolsUnawareAssembly(svcNamed, compilation)) continue;

                    var baseDescriptor =
                        isTypeOfRegistration && isOpenGenericTypeOfRegistration
                            ? DiagnosticDescriptors.OpenGenericTypeOfCouldUseAttributes
                            : isTypeOfRegistration
                                ? DiagnosticDescriptors.TypeOfRegistrationCouldUseAttributes
                                : DiagnosticDescriptors.ManualRegistrationCouldUseAttributes;
                    if (DiagnosticGate.ShouldReport(isTestProject, baseDescriptor))
                    {
                        var diag = Diagnostic.Create(
                            ApplyManualSeverity(baseDescriptor, diagnosticConfig),
                            invocation.GetLocation(),
                            isTypeOfRegistration && isOpenGenericTypeOfRegistration
                                ? new object[] { serviceTypeName }
                                : new object[]
                                {
                                    serviceTypeName, lifetime, implTypeName,
                                    SymbolEqualityComparer.Default.Equals(implNamed.ContainingAssembly, compilation.Assembly)
                                        ? "Add" + implNamed.ContainingAssembly.Name.Replace("-", "_").Replace(" ", "_").Replace(".", "") + "RegisteredServices(...)"
                                        : "registration method for implementation assembly '" + implNamed.ContainingAssembly.Name + "'"
                                });
                        context.ReportDiagnostic(diag);
                    }
                    continue;
                }

                if (iocLifetime == lifetime)
                {
                    var baseDescriptor = isTypeOfRegistration
                        ? DiagnosticDescriptors.TypeOfRegistrationDuplicatesIoCTools
                        : DiagnosticDescriptors.ManualRegistrationDuplicatesIoCTools;
                    if (DiagnosticGate.ShouldReport(isTestProject, baseDescriptor))
                    {
                        var diag = Diagnostic.Create(
                            ApplyManualSeverity(baseDescriptor, diagnosticConfig),
                            invocation.GetLocation(), serviceTypeName, lifetime, implTypeName);
                        context.ReportDiagnostic(diag);
                    }
                }
                else
                {
                    var baseDescriptor = isTypeOfRegistration
                        ? DiagnosticDescriptors.TypeOfRegistrationLifetimeMismatch
                        : DiagnosticDescriptors.ManualRegistrationLifetimeMismatch;
                    if (DiagnosticGate.ShouldReport(isTestProject, baseDescriptor))
                    {
                        var diag = Diagnostic.Create(
                            ApplyManualSeverity(baseDescriptor, diagnosticConfig),
                            invocation.GetLocation(), serviceTypeName, lifetime, iocLifetime);
                        context.ReportDiagnostic(diag);
                    }
                }
            }
        }

        static string Normalize(string name)
        {
            return name.StartsWith("global::", StringComparison.Ordinal)
                ? name.Substring("global::".Length)
                : name;
        }

        static string? GetLifetimeIfAttributed(INamedTypeSymbol symbol)
        {
            var (hasLifetime, _, _, _) = ServiceDiscovery.GetDirectLifetimeAttributes(symbol);
            return hasLifetime ? LifetimeUtilities.GetServiceLifetimeFromSymbol(symbol, "Scoped") : null;
        }

        static INamedTypeSymbol? ExtractTypeFromTypeOf(ArgumentSyntax arg, SemanticModel semanticModel)
        {
            if (arg.Expression is not TypeOfExpressionSyntax typeOfExpr)
                return null;
            // CRITICAL: GetTypeInfo on typeOfExpr.Type, NOT on typeOfExpr itself (Pitfall 1)
            var typeInfo = semanticModel.GetTypeInfo(typeOfExpr.Type);
            return typeInfo.Type as INamedTypeSymbol;
        }

        static bool IsOpenGenericTypeOf(ArgumentSyntax arg)
        {
            if (arg.Expression is not TypeOfExpressionSyntax typeOfExpr)
                return false;
            return typeOfExpr.Type
                .DescendantNodesAndSelf()
                .OfType<GenericNameSyntax>()
                .Any(generic => generic.TypeArgumentList.Arguments.Any(a => a is OmittedTypeArgumentSyntax));
        }

        static bool IsValidOpenGenericTypeOfMapping(INamedTypeSymbol serviceSymbol, INamedTypeSymbol implementationSymbol)
        {
            if (!IsOpenGenericRegistrationType(serviceSymbol) || !IsOpenGenericRegistrationType(implementationSymbol))
                return false;

            var serviceDefinition = serviceSymbol.OriginalDefinition;
            var implementationDefinition = implementationSymbol.OriginalDefinition;

            return OpenGenericSymbolsMatch(serviceDefinition, implementationDefinition) ||
                   ImplementsOpenGenericService(implementationDefinition, serviceDefinition) ||
                   InheritsOpenGenericService(implementationDefinition, serviceDefinition);
        }

        static bool CanExpressOpenGenericMappingWithIoCToolsAttributes(INamedTypeSymbol serviceSymbol,
            INamedTypeSymbol implementationSymbol)
        {
            var serviceDefinition = serviceSymbol.OriginalDefinition;
            var implementationDefinition = implementationSymbol.OriginalDefinition;

            return OpenGenericSymbolsMatch(serviceDefinition, implementationDefinition) ||
                   ImplementsOpenGenericService(implementationDefinition, serviceDefinition);
        }

        static bool IsOpenGenericMappingCoveredByIoCTools(INamedTypeSymbol serviceSymbol, INamedTypeSymbol implementationSymbol)
        {
            var serviceDefinition = serviceSymbol.OriginalDefinition;
            var implementationDefinition = implementationSymbol.OriginalDefinition;

            if (implementationDefinition.GetAttributes().Any(AttributeTypeChecker.IsNonGenericSkipRegistrationAttribute))
                return false;

            var registerAsAllAttribute = implementationDefinition.GetAttributes()
                .FirstOrDefault(attr => AttributeTypeChecker.IsAttribute(attr, AttributeTypeChecker.RegisterAsAllAttribute));

            if (registerAsAllAttribute != null)
            {
                var registrationMode = AttributeParser.GetRegistrationMode(registerAsAllAttribute);
                if (OpenGenericSymbolsMatch(serviceDefinition, implementationDefinition))
                    return registrationMode is not "Exclusionary";

                if (ImplementsOpenGenericService(implementationDefinition, serviceDefinition))
                    return registrationMode is not "DirectOnly" &&
                           !IsOpenGenericServiceSkippedByIoCTools(implementationDefinition, serviceDefinition);
            }

            foreach (var registerAsAttribute in implementationDefinition.GetAttributes()
                         .Where(AttributeTypeChecker.IsRegisterAsAttribute))
            {
                if (OpenGenericSymbolsMatch(serviceDefinition, implementationDefinition))
                    return false;

                if (RegisterAsCoversOpenGenericService(registerAsAttribute, serviceDefinition))
                    return true;
            }

            return OpenGenericSymbolsMatch(serviceDefinition, implementationDefinition) &&
                   GetLifetimeIfAttributed(implementationDefinition) != null;
        }

        static bool ImplementsOpenGenericService(INamedTypeSymbol implementationSymbol, INamedTypeSymbol serviceSymbol)
        {
            return implementationSymbol.AllInterfaces.Any(@interface => OpenGenericSymbolsMatch(@interface, serviceSymbol));
        }

        static bool InheritsOpenGenericService(INamedTypeSymbol implementationSymbol, INamedTypeSymbol serviceSymbol)
        {
            var baseType = implementationSymbol.BaseType;
            while (baseType != null)
            {
                if (OpenGenericSymbolsMatch(baseType, serviceSymbol))
                    return true;

                baseType = baseType.BaseType;
            }

            return false;
        }

        static bool IsOpenGenericServiceSkippedByIoCTools(INamedTypeSymbol implementationSymbol,
            INamedTypeSymbol serviceSymbol)
        {
            var skippedInterfaceDisplays = new HashSet<string>(implementationSymbol.GetAttributes()
                .Where(AttributeTypeChecker.IsGenericSkipRegistrationAttribute)
                .SelectMany(attribute => attribute.AttributeClass?.TypeArguments ?? default)
                .OfType<INamedTypeSymbol>()
                .Select(typeArgument => typeArgument.ToDisplayString()), StringComparer.Ordinal);

            return implementationSymbol.AllInterfaces.Any(@interface =>
                OpenGenericSymbolsMatch(@interface, serviceSymbol) &&
                skippedInterfaceDisplays.Contains(@interface.ToDisplayString()));
        }

        static bool RegisterAsCoversOpenGenericService(AttributeData registerAsAttribute, INamedTypeSymbol serviceSymbol)
        {
            var typeArguments = registerAsAttribute.AttributeClass?.TypeArguments ?? default;
            return !typeArguments.IsDefaultOrEmpty && typeArguments.Any(typeArgument =>
                typeArgument is INamedTypeSymbol namedType && OpenGenericSymbolsMatch(namedType, serviceSymbol));
        }

        static bool OpenGenericSymbolsMatch(INamedTypeSymbol left, INamedTypeSymbol right)
        {
            return string.Equals(GetOpenGenericSymbolKey(left), GetOpenGenericSymbolKey(right), StringComparison.Ordinal);
        }

        static bool IsOpenGenericRegistrationType(INamedTypeSymbol symbol)
        {
            return symbol.IsUnboundGenericType ||
                   (symbol.IsGenericType &&
                    symbol.TypeArguments.Length > 0 &&
                    symbol.TypeArguments.All(typeArgument => typeArgument is ITypeParameterSymbol));
        }

        static string GetOpenGenericSymbolKey(INamedTypeSymbol symbol)
        {
            var definition = symbol.OriginalDefinition;
            var containingTypes = new Stack<string>();
            var currentContainingType = definition.ContainingType;

            while (currentContainingType != null)
            {
                containingTypes.Push(currentContainingType.MetadataName);
                currentContainingType = currentContainingType.ContainingType;
            }

            var namespaceName = definition.ContainingNamespace?.ToDisplayString() ?? string.Empty;
            var containingTypePath = containingTypes.Count == 0
                ? string.Empty
                : string.Join("+", containingTypes) + "+";

            return namespaceName + "|" + containingTypePath + definition.MetadataName;
        }

        static DiagnosticDescriptor ApplyManualSeverity(DiagnosticDescriptor baseDescriptor,
            DiagnosticConfiguration diagnosticConfig)
        {
            return diagnosticConfig.ManualImplementationSeverityConfigured
                ? DiagnosticUtilities.CreateDynamicDescriptor(baseDescriptor, diagnosticConfig.ManualImplementationSeverity)
                : baseDescriptor;
        }
    }
}
