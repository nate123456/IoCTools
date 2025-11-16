namespace IoCTools.Generator.Diagnostics;

using System;

using Microsoft.CodeAnalysis;

internal static class DiagnosticDescriptors
{
    public static readonly DiagnosticDescriptor NoImplementationFound = new(
        "IOC001",
        "No implementation found for interface",
        "Service '{0}' depends on '{1}' but no implementation of this interface exists in the project",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Fix options: 1) Create a class implementing '{1}' with lifetime attribute ([Scoped], [Singleton], or [Transient]), 2) Add [ExternalService] attribute to this class if dependency is provided externally, or 3) Register manually with services.AddScoped<{1}, Implementation>() in Program.cs.");

    public static readonly DiagnosticDescriptor ImplementationNotRegistered = new(
        "IOC002",
        "Implementation exists but not registered",
        "Service '{0}' depends on '{1}' - implementation exists but lacks lifetime attribute ([Scoped], [Singleton], or [Transient])",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Fix options: 1) Add lifetime attribute ([Scoped], [Singleton], or [Transient]) to the implementation of '{1}', 2) Add [ExternalService] attribute to this class if dependency is provided externally, or 3) Register manually with services.AddScoped<{1}, Implementation>() in Program.cs.");

    public static readonly DiagnosticDescriptor CircularDependency = new(
        "IOC003",
        "Circular dependency detected",
        "Circular dependency detected: {0}",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Break the circular dependency by: 1) Using dependency injection with interfaces, 2) Introducing a mediator pattern, or 3) Refactoring to eliminate the circular reference.");

    public static readonly DiagnosticDescriptor RegisterAsAllRequiresService = new(
        "IOC004",
        "RegisterAsAll attribute requires Service attribute",
        "Class '{0}' has [RegisterAsAll] attribute but is missing lifetime attribute",
        "IoCTools",
        DiagnosticSeverity.Error,
        true,
        "Add lifetime attribute ([Scoped], [Singleton], or [Transient]) to the class to enable multi-interface registration.");

    public static readonly DiagnosticDescriptor SkipRegistrationWithoutRegisterAsAll = new(
        "IOC005",
        "SkipRegistration attribute has no effect without RegisterAsAll",
        "Class '{0}' has [SkipRegistration] attribute but no [RegisterAsAll] attribute",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Fix the attribute combination by: 1) Adding [RegisterAsAll] attribute to make [SkipRegistration] meaningful, or 2) Removing the unnecessary [SkipRegistration] attribute.");

    public static readonly DiagnosticDescriptor DuplicateDependsOnType = new(
        "IOC006",
        "Duplicate dependency type in DependsOn attributes",
        "Type '{0}' is declared multiple times in [DependsOn] attributes on class '{1}'",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Remove duplicate dependency declarations.");

    public static readonly DiagnosticDescriptor DependsOnConflictsWithInject = new(
        "IOC007",
        "DependsOn type conflicts with Inject field",
        "Type '{0}' is declared in [DependsOn] attribute but also exists as [Inject] field in class '{1}'",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Remove the [DependsOn] declaration or the [Inject] field to avoid duplication.");

    public static readonly DiagnosticDescriptor DuplicateTypeInSingleDependsOn = new(
        "IOC008",
        "Duplicate type in single DependsOn attribute",
        "Type '{0}' is declared multiple times in the same [DependsOn] attribute on class '{1}'",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Remove duplicate type declarations from the [DependsOn] attribute.");

    public static readonly DiagnosticDescriptor SkipRegistrationForNonRegisteredInterface = new(
        "IOC009",
        "SkipRegistration for interface not registered by RegisterAsAll",
        "Type '{0}' in [SkipRegistration] is not an interface that would be registered by [RegisterAsAll] on class '{1}'",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Remove unnecessary [SkipRegistration] declaration.");

    [Obsolete("IOC010 has been consolidated into IOC014 to eliminate duplicate diagnostics. Use IOC014 instead.")]
    public static readonly DiagnosticDescriptor BackgroundServiceLifetimeConflict = new(
        "IOC010",
        "Background service with non-Singleton lifetime (deprecated)",
        "Background service '{0}' has lifetime attribute with '{1}' lifetime. Background services should typically be Singleton. Note: This diagnostic is deprecated - use IOC014 instead.",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "This diagnostic has been deprecated. Use IOC014 for background service lifetime validation.");

    public static readonly DiagnosticDescriptor BackgroundServiceNotPartial = new(
        "IOC011",
        "Background service class must be partial",
        "Background service '{0}' inherits from BackgroundService but is not marked as partial",
        "IoCTools",
        DiagnosticSeverity.Error,
        true,
        "Add 'partial' modifier to the class declaration: 'public partial class {0}' to enable dependency injection constructor generation.");

    public static readonly DiagnosticDescriptor SingletonDependsOnScoped = new(
        "IOC012",
        "Singleton service depends on Scoped service",
        "Singleton service '{0}' depends on Scoped service '{1}'. Singleton services cannot capture shorter-lived dependencies.",
        "IoCTools",
        DiagnosticSeverity.Error,
        true,
        "Fix the lifetime mismatch by: 1) Changing dependency '{1}' to [Singleton], 2) Changing this service to [Scoped] or [Transient], or 3) Use dependency factories/scoped service locator pattern.");

    public static readonly DiagnosticDescriptor SingletonDependsOnTransient = new(
        "IOC013",
        "Singleton service depends on Transient service",
        "Singleton service '{0}' depends on Transient service '{1}'. Consider if this transient should be Singleton or if the dependency is appropriate.",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Review the design: 1) If '{1}' should be shared, change it to [Singleton], 2) If truly transient, this may cause issues as the singleton will capture only one instance - consider using IServiceProvider or factory pattern instead.");

    public static readonly DiagnosticDescriptor BackgroundServiceLifetimeValidation = new(
        "IOC014",
        "Background service with non-Singleton lifetime",
        "Background service '{0}' has {1} lifetime. Background services should typically be Singleton.",
        "IoCTools",
        DiagnosticSeverity.Error,
        true,
        "Fix options: 1) Change to [Singleton] for optimal background service lifetime, 2) Use [BackgroundService(SuppressLifetimeWarnings = true)] to suppress this warning if the current lifetime is intentional, or 3) Consider if this should inherit from BackgroundService at all.");

    public static readonly DiagnosticDescriptor InheritanceChainLifetimeValidation = new(
        "IOC015",
        "Service lifetime mismatch in inheritance chain",
        "Service lifetime mismatch in inheritance chain: '{0}' ({1}) inherits from dependencies with {2} lifetime",
        "IoCTools",
        DiagnosticSeverity.Error,
        true,
        "Fix the inheritance lifetime hierarchy by: 1) Making all services in the chain Singleton, 2) Changing consuming service to Scoped/Transient, or 3) Breaking the inheritance chain to avoid lifetime conflicts.");

    // Configuration Injection Validation Diagnostics (IOC016-IOC019)
    public static readonly DiagnosticDescriptor InvalidConfigurationKey = new(
        "IOC016",
        "Invalid configuration key",
        "Configuration key '{0}' is invalid: {1}",
        "IoCTools",
        DiagnosticSeverity.Error,
        true,
        "Provide a valid configuration key. Keys cannot be empty, whitespace-only, or contain invalid characters like double colons.");

    public static readonly DiagnosticDescriptor UnsupportedConfigurationType = new(
        "IOC017",
        "Unsupported configuration type",
        "Type '{0}' cannot be bound from configuration: {1}",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Use a supported configuration type (primitives, POCOs with parameterless constructors, collections) or provide a custom converter.");

    public static readonly DiagnosticDescriptor ConfigurationOnNonPartialClass = new(
        "IOC018",
        "InjectConfiguration requires partial class",
        "Class '{0}' uses [InjectConfiguration] but is not marked as partial",
        "IoCTools",
        DiagnosticSeverity.Error,
        true,
        "Add 'partial' modifier to the class declaration: 'public partial class {0}' to enable configuration injection constructor generation.");

    public static readonly DiagnosticDescriptor ConfigurationOnStaticField = new(
        "IOC019",
        "InjectConfiguration on static field not supported",
        "Field '{0}' in class '{1}' is marked with [InjectConfiguration] but is static",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Remove [InjectConfiguration] from static fields. Configuration injection only supports instance fields.");

    // Conditional Service Diagnostics (IOC020-IOC022)
    public static readonly DiagnosticDescriptor ConditionalServiceConflictingConditions = new(
        "IOC020",
        "Conditional service has conflicting conditions",
        "Conditional service '{0}' has conflicting conditions: {1}",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Ensure that Environment and NotEnvironment conditions do not overlap, and Equals and NotEquals conditions are not contradictory.");

    public static readonly DiagnosticDescriptor ConditionalServiceMissingServiceAttribute = new(
        "IOC021",
        "ConditionalService attribute requires Service attribute",
        "Class '{0}' has [ConditionalService] attribute but lifetime attribute is required",
        "IoCTools",
        DiagnosticSeverity.Error, // Changed to Error to match test expectations
        true,
        "Add lifetime attribute ([Scoped], [Singleton], or [Transient]) to the class to enable conditional service registration.");

    public static readonly DiagnosticDescriptor ConditionalServiceEmptyConditions = new(
        "IOC022",
        "ConditionalService attribute has no conditions",
        "Class '{0}' has [ConditionalService] attribute but at least one condition is required",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Specify at least one Environment, NotEnvironment, or ConfigValue condition for conditional registration.");

    public static readonly DiagnosticDescriptor ConditionalServiceConfigValueWithoutComparison = new(
        "IOC023",
        "ConfigValue specified without Equals or NotEquals",
        "Class '{0}' has ConfigValue '{1}' specified but Equals or NotEquals condition is required",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "When ConfigValue is specified, provide at least one Equals or NotEquals condition for comparison.");

    public static readonly DiagnosticDescriptor ConditionalServiceComparisonWithoutConfigValue = new(
        "IOC024",
        "Equals or NotEquals specified without ConfigValue",
        "Class '{0}' has Equals or NotEquals condition but ConfigValue is required",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "When using Equals or NotEquals, specify the ConfigValue property to define which configuration key to check.");

    public static readonly DiagnosticDescriptor ConditionalServiceEmptyConfigKey = new(
        "IOC025",
        "ConfigValue is empty or whitespace",
        "Class '{0}' has an empty or whitespace-only ConfigValue",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Provide a valid configuration key path for ConfigValue.");

    public static readonly DiagnosticDescriptor ConditionalServiceMultipleAttributes = new(
        "IOC026",
        "Multiple ConditionalService attributes on same class",
        "Class '{0}' has multiple [ConditionalService] attributes which may lead to unexpected behavior",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Consider combining conditions into a single [ConditionalService] attribute or use separate classes for different conditions.");

    public static readonly DiagnosticDescriptor DuplicateServiceRegistration = new(
        "IOC027",
        "Potential duplicate service registration",
        "Service '{0}' may be registered multiple times due to inheritance or attribute combinations",
        "IoCTools",
        DiagnosticSeverity.Info,
        true,
        "Review service registration patterns to ensure no unintended duplicates. The generator automatically deduplicates identical registrations.");

    // RegisterAs Attribute Diagnostics (IOC028-IOC031)
    public static readonly DiagnosticDescriptor RegisterAsRequiresService = new(
        "IOC028",
        "RegisterAs attribute requires service indicators",
        "Class '{0}' has [RegisterAs] attribute but lacks service indicators like [Lifetime], [Inject] fields, or other registration attributes",
        "IoCTools",
        DiagnosticSeverity.Error,
        true,
        "Add [Lifetime], [Inject] fields, or other service indicators to enable selective interface registration.");

    public static readonly DiagnosticDescriptor RegisterAsInterfaceNotImplemented = new(
        "IOC029",
        "RegisterAs specifies unimplemented interface",
        "Class '{0}' has [RegisterAs] attribute specifying interface '{1}' but does not implement this interface",
        "IoCTools",
        DiagnosticSeverity.Error,
        true,
        "Ensure that all interfaces specified in [RegisterAs] are actually implemented by the class.");

    public static readonly DiagnosticDescriptor RegisterAsDuplicateInterface = new(
        "IOC030",
        "RegisterAs contains duplicate interface",
        "Class '{0}' has [RegisterAs] attribute with duplicate interface '{1}'",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Remove duplicate interface specifications from the [RegisterAs] attribute.");

    public static readonly DiagnosticDescriptor RegisterAsNonInterfaceType = new(
        "IOC031",
        "RegisterAs specifies non-interface type",
        "Class '{0}' has [RegisterAs] attribute specifying non-interface type '{1}'",
        "IoCTools",
        DiagnosticSeverity.Error,
        true,
        "RegisterAs can only specify interface types. Use concrete class types for direct registration.");

    public static readonly DiagnosticDescriptor RedundantRegisterAsAttribute = new(
        "IOC032",
        "RegisterAs attribute is redundant",
        "Class '{0}' already registers interfaces {1} by default. Remove redundant [RegisterAs] attribute or reduce the interface list.",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "RegisterAs should only be used when selectively registering a subset of interfaces.");

    public static readonly DiagnosticDescriptor RedundantScopedLifetimeAttribute = new(
        "IOC033",
        "Scoped lifetime attribute is redundant",
        "Class '{0}' is already implicitly registered as Scoped via {1}. Remove redundant [Scoped] attribute or change the lifetime to a non-default value.",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Scoped is the default lifetime for implicit services; only specify it when clarifying intent or when no other service indicators exist.");

    public static readonly DiagnosticDescriptor RedundantRegisterAsWithRegisterAsAll = new(
        "IOC034",
        "RegisterAsAll already registers every interface",
        "Class '{0}' uses both [RegisterAsAll] and [RegisterAs]; selective RegisterAs attributes have no effect when RegisterAsAll is present",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Remove redundant [RegisterAs] attributes or drop [RegisterAsAll] if selective registration is required.");

    public static readonly DiagnosticDescriptor InjectFieldPreferDependsOn = new(
        "IOC035",
        "Inject field can be simplified to DependsOn",
        "Field '{0}' in class '{1}' uses [Inject] but matches the default DependsOn naming for dependency '{2}'. Prefer [DependsOn<{2}>] unless you require a custom field name or mutability.",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Replace the [Inject] field with a [DependsOn] attribute so the generator can produce constructor parameters and backing fields automatically. Keep [Inject] only when a custom field name or non-readonly behavior is required.");

    public static readonly DiagnosticDescriptor MultipleLifetimeAttributes = new(
        "IOC036",
        "Multiple lifetime attributes declared",
        "Class '{0}' applies multiple lifetime attributes ({1}). Choose a single lifetime to avoid conflicting registrations.",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Remove redundant lifetime attributes so only one of [Scoped], [Singleton], or [Transient] remains on the class.");

    public static readonly DiagnosticDescriptor SkipRegistrationOverridesOtherAttributes = new(
        "IOC037",
        "SkipRegistration overrides other registration attributes",
        "Class '{0}' uses [SkipRegistration] along with {1}, but SkipRegistration prevents those attributes from taking effect",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Remove redundant registration attributes or drop [SkipRegistration] so the class can register as intended.");

    public static readonly DiagnosticDescriptor SkipRegistrationIneffectiveInDirectMode = new(
        "IOC038",
        "SkipRegistration for interfaces has no effect in RegisterAsAll(DirectOnly)",
        "Class '{0}' declares [SkipRegistration] for interfaces, but RegisterAsAll is set to DirectOnly so no interfaces would register anyway",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Change RegisterAsAll to RegistrationMode.All/Exclusionary or remove the ineffective [SkipRegistration] declaration.");

    public static readonly DiagnosticDescriptor UnusedDependency = new(
        "IOC039",
        "Dependency declared but never used",
        "Dependency field '{0}' of type '{1}' declared via {2} on class '{3}' is never referenced. Remove the declaration or use the dependency.",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Keep only the dependencies that are actually consumed by the class. Remove unused [Inject]/[DependsOn] declarations or reference the generated field in your implementation.");

    public static readonly DiagnosticDescriptor RedundantDependencyDeclarations = new(
        "IOC040",
        "Redundant dependency declarations",
        "Dependency type '{0}' is declared multiple times via {1} on class '{2}'. Remove the duplicate declarations so the generator only binds it once.",
        "IoCTools",
        DiagnosticSeverity.Warning,
        true,
        "Declare each dependency a single time. Prefer [DependsOn] when no custom field is required and drop extra [Inject] fields or duplicate attributes to avoid confusing constructor graphs.");
}
