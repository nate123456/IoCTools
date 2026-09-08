namespace IoCTools.Generator.Diagnostics;

internal static partial class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor RegisterAsAllRequiresService = new(
        "IOC004",
        "RegisterAsAll attribute requires Service attribute",
        "Class '{0}' has [RegisterAsAll] attribute but is missing lifetime attribute",
        "IoCTools.Registration",
        DiagnosticSeverity.Error,
        true,
        "Add lifetime attribute ([Scoped], [Singleton], or [Transient]) to the class to enable multi-interface registration.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc004");

    public static readonly DiagnosticDescriptor SkipRegistrationWithoutRegisterAsAll = new(
        "IOC005",
        "SkipRegistration attribute has no effect without RegisterAsAll",
        "Class '{0}' has [SkipRegistration] attribute but no [RegisterAsAll] attribute",
        "IoCTools.Registration",
        DiagnosticSeverity.Warning,
        true,
        "Fix the attribute combination by: 1) Adding [RegisterAsAll] attribute to make [SkipRegistration] meaningful, or 2) Removing the unnecessary [SkipRegistration] attribute.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc005");

    public static readonly DiagnosticDescriptor DuplicateServiceRegistration = new(
        "IOC027",
        "Potential duplicate service registration",
        "Service '{0}' may be registered multiple times due to inheritance or attribute combinations",
        "IoCTools.Registration",
        DiagnosticSeverity.Info,
        true,
        "Review service registration patterns to ensure no unintended duplicates. The generator automatically deduplicates identical registrations.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc027");

    public static readonly DiagnosticDescriptor RegisterAsRequiresService = new(
        "IOC028",
        "RegisterAs attribute requires service indicators",
        "Class '{0}' has [RegisterAs] attribute but lacks service indicators like [Lifetime], [Inject] fields, or other registration attributes",
        "IoCTools.Registration",
        DiagnosticSeverity.Error,
        true,
        "Add [Lifetime], [Inject] fields, or other service indicators to enable selective interface registration.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc028");

    public static readonly DiagnosticDescriptor RegisterAsInterfaceNotImplemented = new(
        "IOC029",
        "RegisterAs specifies unimplemented interface",
        "Class '{0}' has [RegisterAs] attribute specifying interface '{1}' but does not implement this interface",
        "IoCTools.Registration",
        DiagnosticSeverity.Error,
        true,
        "Ensure that all interfaces specified in [RegisterAs] are actually implemented by the class.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc029");

    public static readonly DiagnosticDescriptor RegisterAsDuplicateInterface = new(
        "IOC030",
        "RegisterAs contains duplicate interface",
        "Class '{0}' has [RegisterAs] attribute with duplicate interface '{1}'",
        "IoCTools.Registration",
        DiagnosticSeverity.Warning,
        true,
        "Remove duplicate interface specifications from the [RegisterAs] attribute.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc030");

    public static readonly DiagnosticDescriptor RegisterAsNonInterfaceType = new(
        "IOC031",
        "RegisterAs specifies non-interface type",
        "Class '{0}' has [RegisterAs] attribute specifying non-interface type '{1}'",
        "IoCTools.Registration",
        DiagnosticSeverity.Error,
        true,
        "RegisterAs can only specify interface types. Use concrete class types for direct registration.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc031");

    public static readonly DiagnosticDescriptor RedundantRegisterAsAttribute = new(
        "IOC032",
        "RegisterAs attribute is redundant",
        "Class '{0}' already registers interfaces {1} by default. Remove redundant [RegisterAs] attribute or reduce the interface list.",
        "IoCTools.Registration",
        DiagnosticSeverity.Warning,
        true,
        "RegisterAs should only be used when selectively registering a subset of interfaces.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc032");

    public static readonly DiagnosticDescriptor RedundantRegisterAsWithRegisterAsAll = new(
        "IOC034",
        "RegisterAsAll already registers every interface",
        "Class '{0}' uses both [RegisterAsAll] and [RegisterAs]; selective RegisterAs attributes have no effect when RegisterAsAll is present",
        "IoCTools.Registration",
        DiagnosticSeverity.Warning,
        true,
        "Remove redundant [RegisterAs] attributes or drop [RegisterAsAll] if selective registration is required.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc034");

    public static readonly DiagnosticDescriptor InjectFieldPreferDependsOn = new(
        "IOC035",
        "Inject field should use DependsOn",
        "Field '{0}' in class '{1}' uses [Inject] but matches the default DependsOn naming for dependency '{2}'. [Inject] is compatibility-only; never use [Inject] in new code. Prefer [DependsOn<{2}>] unless you are preserving existing legacy shape.",
        "IoCTools.Registration",
        DiagnosticSeverity.Warning,
        true,
        "Replace the [Inject] field with a [DependsOn] attribute so the generator can produce constructor parameters and backing fields automatically. [Inject] remains supported for compatibility-only migration scenarios; do not introduce new [Inject] fields.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc035");

    public static readonly DiagnosticDescriptor MultipleLifetimeAttributes = new(
        "IOC036",
        "Multiple lifetime attributes declared",
        "Class '{0}' applies multiple lifetime attributes ({1}). Choose a single lifetime to avoid conflicting registrations.",
        "IoCTools.Registration",
        DiagnosticSeverity.Warning,
        true,
        "Remove redundant lifetime attributes so only one of [Scoped], [Singleton], or [Transient] remains on the class.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc036");

    public static readonly DiagnosticDescriptor SkipRegistrationOverridesOtherAttributes = new(
        "IOC037",
        "SkipRegistration override other registration attributes",
        "Class '{0}' uses [SkipRegistration] along with {1}, but SkipRegistration prevents those attributes from taking effect",
        "IoCTools.Registration",
        DiagnosticSeverity.Warning,
        true,
        "Remove redundant registration attributes or drop [SkipRegistration] so the class can register as intended.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc037");

    public static readonly DiagnosticDescriptor SkipRegistrationIneffectiveInDirectMode = new(
        "IOC038",
        "SkipRegistration for interfaces has no effect in RegisterAsAll(DirectOnly)",
        "Class '{0}' declares [SkipRegistration] for interfaces, but RegisterAsAll is set to DirectOnly so no interfaces would register anyway",
        "IoCTools.Registration",
        DiagnosticSeverity.Warning,
        true,
        "Change RegisterAsAll to RegistrationMode.All/Exclusionary or remove the ineffective [SkipRegistration] declaration.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc038");

    public static readonly DiagnosticDescriptor PreferParamsStyleAttributeArguments = new(
        "IOC047",
        "Use params-style attribute arguments",
        "Attribute '{0}' on '{1}' uses the '{2}' named argument; pass these values via the params argument instead (e.g., memberNames: value or configurationKeys: value)",
        "IoCTools.Registration",
        DiagnosticSeverity.Info,
        true,
        "Prefer params-style constructor arguments for [DependsOn] member names and [DependsOnConfiguration] keys so analyzers and generators can align argument order and defaults consistently.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc047");

    public static readonly DiagnosticDescriptor RedundantMemberName = new(
        "IOC085",
        "Member name matches default",
        "Member name '{0}' for dependency '{1}' on '{2}' matches the generator's default name; remove the explicit name to reduce redundancy",
        "IoCTools.Registration",
        DiagnosticSeverity.Warning,
        true,
        "You can omit memberNames when they equal the generator's default (based on naming convention, strip-I, and prefix settings). Keeping only overrides reduces noise and future merge conflicts.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc085");

    public static readonly DiagnosticDescriptor RegisterAsMissingLifetime = new(
        "IOC069",
        "RegisterAs requires a lifetime attribute",
        "Class '{0}' uses [RegisterAs] but has no lifetime attribute. Add [Scoped], [Singleton], or [Transient].",
        "IoCTools.Registration",
        DiagnosticSeverity.Warning,
        true,
        "Registration attributes still need a lifetime indicator; add a lifetime attribute once on the class.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc069");

    public static readonly DiagnosticDescriptor DependsOnMissingLifetime = new(
        "IOC070",
        "DependsOn/Inject used without lifetime",
        "Class '{0}' declares dependencies but has no lifetime attribute. Add [Scoped], [Singleton], or [Transient].",
        "IoCTools.Registration",
        DiagnosticSeverity.Warning,
        true,
        "When a class has [DependsOn] or [Inject], add a lifetime so it will be registered and validated.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc070");

    public static readonly DiagnosticDescriptor ConditionalMissingLifetime = new(
        "IOC071",
        "ConditionalService missing lifetime",
        "Class '{0}' uses [ConditionalService] but has no lifetime attribute. Add [Scoped], [Singleton], or [Transient].",
        "IoCTools.Registration",
        DiagnosticSeverity.Warning,
        true,
        "Conditional services still require an explicit lifetime; add one to enable registration.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc071");

    public static readonly DiagnosticDescriptor HostedServiceMissingLifetime = new(
        "IOC072",
        "Hosted service lifetime should be implicit",
        "Class '{0}' implements IHostedService/BackgroundService and declares a lifetime attribute. Hosted services are registered implicitly; remove the lifetime attribute unless the class also exposes additional service interfaces.",
        "IoCTools.Registration",
        DiagnosticSeverity.Warning,
        true,
        "Let IoCTools register hosted services implicitly. Only add a lifetime when the hosted service also registers additional interfaces.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc072");

    public static readonly DiagnosticDescriptor MissingRegisterAsAllForMultiInterface = new(
        "IOC074",
        "Multi-interface class could use RegisterAsAll",
        "Class '{0}' implements multiple interfaces but only has a lifetime attribute. Consider adding [RegisterAsAll] to register all interfaces automatically.",
        "IoCTools.Registration",
        DiagnosticSeverity.Info,
        true,
        "When a class implements multiple interfaces, [RegisterAsAll] makes intent explicit and prevents partial registrations.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc074");

    public static readonly DiagnosticDescriptor ManualRegistrationDuplicatesIoCTools = new(
        "IOC081",
        "Manual registration duplicates IoCTools registration",
        "Service '{0}' is registered manually with lifetime '{1}' but IoCTools already registers it with the same lifetime. Remove the manual registration and rely on IoCTools attributes ({2}).",
        "IoCTools.Registration",
        DiagnosticSeverity.Warning,
        true,
        "Avoid duplicate manual registrations when IoCTools already emits the same service/implementation.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc081");

    public static readonly DiagnosticDescriptor ManualRegistrationLifetimeMismatch = new(
        "IOC082",
        "Manual registration lifetime differs from IoCTools",
        "Service '{0}' is registered manually with lifetime '{1}' but IoCTools registers it with lifetime '{2}'. Align lifetimes or remove the manual registration.",
        "IoCTools.Registration",
        DiagnosticSeverity.Error,
        true,
        "Keep manual registrations aligned with IoCTools-generated lifetimes to avoid duplicate or conflicting registrations.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc082");

    public static readonly DiagnosticDescriptor ManualOptionsRegistrationDuplicatesIoCTools = new(
        "IOC083",
        "Manual options registration duplicates IoCTools binding",
        "Options type '{0}' is manually bound via AddOptions/Configure, but IoCTools already binds it from configuration. Remove the manual binding and rely on generated options registration.",
        "IoCTools.Registration",
        DiagnosticSeverity.Error,
        true,
        "IoCTools automatically binds configuration-backed options types referenced via InjectConfiguration/DependsOnConfiguration. Avoid manual AddOptions/Configure calls for these types to prevent duplicate registrations and diverging configuration.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc083");

    public static readonly DiagnosticDescriptor ManualRegistrationCouldUseAttributes = new(
        "IOC086",
        "Manual registration could use IoCTools attributes",
        "'{0}' is registered manually as {1} but the implementation '{2}' lacks IoCTools lifetime attributes. To migrate, add [Scoped]/[Singleton]/[Transient] (and [RegisterAs] as needed), replace the manual registration, and call the generated {3} extension before building the service provider. Attributes alone do not register services.",
        "IoCTools.Registration",
        DiagnosticSeverity.Warning,
        true,
        "Prefer IoCTools attributes over manual registrations to unlock diagnostics and generated registration.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc086");

    public static readonly DiagnosticDescriptor TypeOfRegistrationCouldUseAttributes = new(
        "IOC090",
        "typeof() registration could use IoCTools attributes",
        "'{0}' is registered via typeof() as {1}, but the implementation '{2}' lacks IoCTools attributes. Consider adding [{1}] (and [RegisterAs]) instead.",
        "IoCTools.Registration",
        DiagnosticSeverity.Warning,
        true,
        "Prefer IoCTools lifetime attributes over typeof()-based manual registrations for consistent DI management and build-time validation.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc090");

    public static readonly DiagnosticDescriptor TypeOfRegistrationDuplicatesIoCTools = new(
        "IOC091",
        "typeof() registration duplicates IoCTools registration",
        "Service '{0}' is registered via typeof() with lifetime '{1}' but IoCTools already registers implementation '{2}' with the same lifetime. Remove the typeof() registration.",
        "IoCTools.Registration",
        DiagnosticSeverity.Warning,
        true,
        "Avoid duplicate typeof()-based registrations when IoCTools already emits the same service/implementation pair.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc091");

    public static readonly DiagnosticDescriptor TypeOfRegistrationLifetimeMismatch = new(
        "IOC092",
        "typeof() registration lifetime differs from IoCTools",
        "Service '{0}' is registered via typeof() with lifetime '{1}' but IoCTools registers it with lifetime '{2}'. Align lifetimes or remove the typeof() registration.",
        "IoCTools.Registration",
        DiagnosticSeverity.Error,
        true,
        "Keep typeof() registrations aligned with IoCTools-generated lifetimes to avoid duplicate or conflicting registrations.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc092");

    public static readonly DiagnosticDescriptor OpenGenericTypeOfCouldUseAttributes = new(
        "IOC094",
        "Open generic typeof() registration could use IoCTools attributes",
        "'{0}' is registered as an open generic via typeof(). Prefer expressing the same registration through IoCTools attributes on the generic implementation when possible.",
        "IoCTools.Registration",
        DiagnosticSeverity.Info,
        true,
        "The common open-generic registration path is supported in IoCTools 1.5.1. Prefer IoCTools attributes over manual typeof() registrations so diagnostics and generated registrations stay aligned.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc094");

    public static readonly DiagnosticDescriptor OpenGenericSharedInstanceFallsBackToSeparate = new(
        "IOC095",
        "Open generic shared aliases fall back to separate registrations",
        "Class '{0}' requests InstanceSharing.Shared for open generic interface registrations, but Microsoft.Extensions.DependencyInjection does not support open generic implementation factories. IoCTools will register those interfaces separately.",
        "IoCTools.Registration",
        DiagnosticSeverity.Warning,
        true,
        "Use InstanceSharing.Separate for open generic interface aliases, or wire the shared behavior manually in a container that supports open generic factory aliases.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc095");

    public static readonly DiagnosticDescriptor RedundantRegisterAsInheritance = new(
        "IOC063",
        "RegisterAs attribute is redundant on derived class",
        "Class '{0}' inherits RegisterAs interfaces {1} from '{2}'. Remove redundant [RegisterAs] on the derived class.",
        "IoCTools.Registration",
        DiagnosticSeverity.Warning,
        true,
        "Put [RegisterAs] on the base class once when all derived types share the same interface list; derived classes need it only when narrowing or extending the list.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc063");

    public static readonly DiagnosticDescriptor RegisterAsBaseSuggestion = new(
        "IOC064",
        "Move shared RegisterAs to base class",
        "Services derived from '{0}' all specify [RegisterAs({1})]. Move the attribute to the base class to reduce duplication.",
        "IoCTools.Registration",
        DiagnosticSeverity.Info,
        true,
        "When multiple derived classes repeat the same RegisterAs interfaces, place the attribute on their shared base type instead.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc064");

    public static readonly DiagnosticDescriptor RedundantRegisterAsAllInheritance = new(
        "IOC065",
        "RegisterAsAll attribute is redundant on derived class",
        "Class '{0}' inherits [RegisterAsAll] intent from '{1}'. Remove redundant [RegisterAsAll] on the derived class.",
        "IoCTools.Registration",
        DiagnosticSeverity.Warning,
        true,
        "Only one [RegisterAsAll] is needed in an inheritance chain; place it on the base when all descendants should register all implemented interfaces.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc065");

    public static readonly DiagnosticDescriptor OpenGenericHostedServiceSkipped = new(
        "IOC073",
        "Open-generic IHostedService implementer skipped from auto-registration",
        "Class '{0}' implements IHostedService and is an open generic type. Microsoft.Extensions.DependencyInjection's services.AddHostedService<T>() requires a closed type, so IoCTools cannot auto-register it. Register a closed subtype (e.g., 'class Closed : {0}<...> {{}}') instead.",
        "IoCTools.Registration",
        DiagnosticSeverity.Info,
        true,
        "AddHostedService<T>() resolves a single instance at startup and cannot accept open generic type definitions. Provide a closed-generic subclass and register that, or wire the hosted service manually with closed type arguments.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc073");

    public static readonly DiagnosticDescriptor InaccessibleHostedServiceSkipped = new(
        "IOC066",
        "Inaccessible IHostedService implementer skipped from auto-registration",
        "Class '{0}' implements IHostedService but its effective accessibility ({1}) is below 'internal'. The generated registration extension lives in a separate type and namespace and cannot reference it (CS0122). Make the type internal+ or wire it manually.",
        "IoCTools.Registration",
        DiagnosticSeverity.Info,
        true,
        "Generated registration extensions are emitted alongside the consumer assembly but live in a separate type. Private, protected, or private-nested hosted-service types are unreachable from that emission site. Raise the accessibility to internal or above, or register the type manually in composition root.",
        "https://github.com/sansiquay/IoCTools/blob/main/docs/diagnostics.md#ioc066");
}
