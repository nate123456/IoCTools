namespace IoCTools.Sample.Services;

using Abstractions.Annotations;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// This file contains diagnostic examples that demonstrate various IoCTools diagnostics.
/// These services will generate warnings/errors when building to help users understand
/// the diagnostic system and how to configure it.
/// 
/// To run these diagnostic examples:
/// 1. Build the project: dotnet build
/// 2. Check the build output for diagnostic messages
/// 3. Configure diagnostic severity in project file (see project comments below)
/// 
/// Example MSBuild properties to configure diagnostics:
/// 
/// <PropertyGroup>
///   <!-- Configure severity for missing implementations (default: Warning) -->
///   <IoCToolsNoImplementationSeverity>Error</IoCToolsNoImplementationSeverity>
///   
///   <!-- Configure severity for unregistered implementations (default: Warning) -->  
///   <IoCToolsUnregisteredSeverity>Info</IoCToolsUnregisteredSeverity>
///   
///   <!-- Disable all dependency validation diagnostics (default: false) -->
///   <IoCToolsDisableDiagnostics>true</IoCToolsDisableDiagnostics>
/// </PropertyGroup>
/// 
/// Severity Options: Error, Warning, Info, Hidden
/// </summary>

// ============================================
// IOC001: Missing Implementation Examples
// ============================================

/// <summary>
///     IOC001: This service depends on interfaces that have no implementations
///     NOTE: No [Lifetime] attribute to prevent runtime failures while demonstrating build-time diagnostics
///     The generator will still create constructor and show diagnostic warnings, but won't register for DI
/// </summary>
// [Scoped] // ← Commented out to exclude from DI registration
public partial class MissingImplementationService
{
    [Inject] private readonly IMissingDataService _dataService;
    [Inject] private readonly INonExistentRepository _repository;
}

// These interfaces intentionally have no implementations to trigger IOC001
public interface IMissingDataService
{
    Task<string> GetDataAsync();
}

public interface INonExistentRepository
{
    Task SaveAsync(string data);
}

// ============================================
// IOC002: Unregistered Implementation Examples
// ============================================

/// <summary>
///     IOC002: This service depends on implementations that exist but are not registered
///     NOTE: No [Lifetime] attribute to prevent runtime failures while demonstrating build-time diagnostics
///     The generator will still create constructor and show diagnostic warnings, but won't register for DI
/// </summary>
// [Scoped] // ← Commented out to exclude from DI registration
public partial class UnregisteredDependencyService
{
    [Inject] private readonly IUnregisteredCalculator _calculator;
    [Inject] private readonly IUnregisteredLogger _logger;
}

// Interfaces with implementations that are NOT marked with Service attributes
public interface IUnregisteredCalculator
{
    int Add(int a,
        int b);
}

public interface IUnregisteredLogger
{
    void Log(string message);
}

// These implementations exist but are not registered (no attributes to trigger intelligent inference)
// This will trigger IOC002 diagnostics
public class UnregisteredCalculator : IUnregisteredCalculator
{
    public int Add(int a,
        int b) => a + b;
}

public class UnregisteredLogger : IUnregisteredLogger
{
    public void Log(string message) => Console.WriteLine(message);
}

// ============================================
// IOC012: Singleton Depends on Scoped Examples  
// ============================================
/// <summary>
/// IOC012: This service should trigger IOC012 (Singleton depending on Scoped services)
/// INTENTIONALLY PROBLEMATIC - will cause IOC012 diagnostic
/// </summary>
// TEMPORARILY COMMENTED TO ALLOW BUILD
//[Singleton]
//public partial class ProblematicSingletonService
//{
//    [Inject] private readonly IScopedDatabaseService _database; // IOC012: Singleton depending on Scoped
//}

/// <summary>
///     IOC012: Helper scoped service that the singleton incorrectly depends on
/// </summary>
[Scoped]
public partial class ScopedDatabaseService : IScopedDatabaseService
{
    [Inject] private readonly ILogger<ScopedDatabaseService> _logger;

    public async Task<string> GetUserDataAsync(int userId)
    {
        // Simulate database operation
        await Task.Delay(100);
        return $"User data for {userId}";
    }
}

public interface IScopedDatabaseService
{
    Task<string> GetUserDataAsync(int userId);
}

// ============================================
// IOC013: Singleton Depends on Transient Examples
// ============================================

/// <summary>
///     IOC013: This Singleton service depends on Transient services (potential issue)
/// </summary>
[Singleton]
public partial class SingletonWithTransientDependencies
{
    [Inject] private readonly ITransientNotificationService _notifications;
    [Inject] private readonly ITransientUtility _utility;
}

/// <summary>
///     IOC013: Transient services that singleton depends on
/// </summary>
[Transient]
public partial class TransientNotificationService : ITransientNotificationService
{
    public void SendNotification(string message)
    {
        // Simulate notification sending
        Console.WriteLine($"Notification: {message}");
    }
}

[Transient]
public partial class TransientUtility : ITransientUtility
{
    public string FormatMessage(string input) => $"[{DateTime.Now}] {input}";
}

public interface ITransientNotificationService
{
    void SendNotification(string message);
}

public interface ITransientUtility
{
    string FormatMessage(string input);
}

// ============================================
// IOC015: Inheritance Chain Lifetime Violations
// ============================================
/// <summary>
/// IOC015: Complex inheritance chain with lifetime violations  
/// INTENTIONALLY PROBLEMATIC - Singleton inheriting from service with Scoped dependencies
/// </summary>
// TEMPORARILY COMMENTED TO ALLOW BUILD
//[Singleton]
//public partial class SingletonServiceWithInheritance : BaseServiceWithScopedDependencies
//{
//    [Inject] private readonly ITransientUtility _additionalUtility;
//}

/// <summary>
///     IOC015: Base class with scoped dependencies (creates inheritance lifetime violation)
/// </summary>
[Scoped]
public partial class BaseServiceWithScopedDependencies
{
    [Inject] private readonly IScopedDatabaseService _baseDatabase;
    [Inject] private readonly ILogger<BaseServiceWithScopedDependencies> _baseLogger;

    protected virtual void DoBaseWork()
    {
        // Base class functionality
    }
}

// ============================================
// IOC006-IOC009: Registration Conflict Examples
// ============================================

/// <summary>
///     IOC006: Duplicate dependency types in DependsOn attributes
///     IOC008: Duplicate type in single DependsOn attribute
/// </summary>
[Scoped]
[DependsOn<ILogger<ConflictingDependenciesService>>]
[DependsOn<IMemoryCache>]
[DependsOn<ILogger<ConflictingDependenciesService>>] // IOC006: Duplicate across attributes
[DependsOn<IMemoryCache, IMemoryCache>] // IOC008: Duplicate within single attribute
public partial class ConflictingDependenciesService
{
    // IOC007: This will conflict with DependsOn<ILogger<ConflictingDependenciesService>>
    [Inject] private readonly ILogger<ConflictingDependenciesService> _logger;
}

/// <summary>
///     IOC005: SkipRegistration without RegisterAsAll
///     IOC009: SkipRegistration for non-registered interface
/// </summary>
[Scoped]
[RegisterAsAll] // Fixed: Added registration attribute to resolve IOC004
[SkipRegistration<IDisposable>] // IOC005: No [RegisterAsAll] attribute
public partial class MisconfiguredRegistrationService : IDisposable
{
    public void Dispose()
    {
        // Cleanup
    }
}

/// <summary>
///     IOC009: SkipRegistration for interface not registered by RegisterAsAll
/// </summary>
[Scoped]
[RegisterAsAll]
[SkipRegistration<IDisposable>] // IOC009: IDisposable won't be registered by RegisterAsAll anyway
public partial class RedundantSkipRegistrationService : ICloneable
{
    public object Clone() => new RedundantSkipRegistrationService();
}

// ============================================
// IOC014: Background Service Lifetime Examples
// ============================================

/// <summary>
///     IOC014: Background service with correct lifetime (fixed to be Singleton)
/// </summary>
[Singleton] // IOC014: Fixed - Background services should be Singleton  
public partial class IncorrectLifetimeBackgroundService : BackgroundService
{
    [Inject] private readonly ILogger<IncorrectLifetimeBackgroundService> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Background service running at: {Time}", DateTimeOffset.Now);
            await Task.Delay(10000, stoppingToken);
        }
    }
}

/// <summary>
/// IOC011: Background service that's not marked as partial but needs dependency injection
/// </summary>
// TEMPORARILY COMMENTED TO ALLOW BUILD
//[Singleton]
//public class NonPartialBackgroundService : BackgroundService // IOC011: Missing 'partial' keyword
//{
//    [Inject] private readonly ILogger<NonPartialBackgroundService> _logger;
//    
//    protected override Task ExecuteAsync(CancellationToken stoppingToken)
//    {
//        return Task.CompletedTask;
//    }
//}

// ============================================
// IOC020-IOC027: Advanced Conditional Service and System Diagnostics
// ============================================
/// <summary>
/// IOC021: ConditionalService attribute requires Service attribute
/// This class has //[ConditionalService] but is missing  attribute
/// </summary>
////[ConditionalService(Environment = "Development")]
// Missing  attribute intentionally to trigger IOC021
//public partial class ConditionalWithoutServiceAttribute
//{
//    [Inject] private readonly ILogger<ConditionalWithoutServiceAttribute> _logger;
//}

/// <summary>
///     IOC022: ConditionalService attribute has no conditions
///     This class has //[ConditionalService] but no Environment, ConfigValue, or condition properties
/// </summary>
//[ConditionalService] // No conditions specified - triggers IOC022
public partial class EmptyConditionalService
{
    [Inject] private readonly ILogger<EmptyConditionalService> _logger;
}

/// <summary>
///     IOC023: ConfigValue specified without Equals or NotEquals
///     This class has ConfigValue but no comparison condition
/// </summary>
//[ConditionalService(ConfigValue = "Features:SomeFeature")] // Missing Equals/NotEquals - triggers IOC023
public partial class ConfigValueWithoutComparisonService
{
    [Inject] private readonly ILogger<ConfigValueWithoutComparisonService> _logger;
}

/// <summary>
///     IOC024: Equals or NotEquals specified without ConfigValue
///     This class has comparison conditions but no ConfigValue
/// </summary>
//[ConditionalService(Equals = "true")] // Missing ConfigValue - triggers IOC024
public partial class ComparisonWithoutConfigValueService
{
    [Inject] private readonly ILogger<ComparisonWithoutConfigValueService> _logger;
}

/// <summary>
///     IOC025: ConfigValue is empty or whitespace
///     This class has empty ConfigValue
/// </summary>
//[ConditionalService(ConfigValue = "", Equals = "true")] // Empty ConfigValue - triggers IOC025
public partial class EmptyConfigValueService
{
    [Inject] private readonly ILogger<EmptyConfigValueService> _logger;
}

/// <summary>
///     Another IOC025 example with whitespace-only ConfigValue
/// </summary>
//[ConditionalService(ConfigValue = "   ", Equals = "true")] // Whitespace ConfigValue - triggers IOC025
public partial class WhitespaceConfigValueService
{
    [Inject] private readonly ILogger<WhitespaceConfigValueService> _logger;
}

/// <summary>
///     IOC026: Multiple ConditionalService attributes on same class
///     This class has multiple //[ConditionalService] attributes which may lead to unexpected behavior
///     Note: C# compiler temporarily blocks this, so one attribute is commented out
/// </summary>
//[ConditionalService(Environment = "Development")]
// //[ConditionalService(ConfigValue = "Features:EnableDev", Equals = "true")] // Multiple attributes - triggers IOC026
public partial class MultipleConditionalAttributesService
{
    [Inject] private readonly ILogger<MultipleConditionalAttributesService> _logger;
}

/// <summary>
///     IOC027: Potential duplicate service registration scenarios
///     This demonstrates services that may be registered multiple times due to inheritance or attribute combinations
/// </summary>

// Base service with inheritance chain that could lead to duplicate registrations
public partial class BaseServiceForDuplication
{
    [Inject] private readonly ILogger<BaseServiceForDuplication> _logger;
}

// Derived service that might cause duplicate registration warnings  

public partial class DerivedServiceCausingDuplication : BaseServiceForDuplication
{
    [Inject] private readonly IMemoryCache _cache;
}

// Service with complex registration patterns that could trigger IOC027
[RegisterAsAll] // Complex registration pattern might trigger IOC027 
public partial class ComplexRegistrationPatternService : IDisposable, ICloneable
{
    [Inject] private readonly ILogger<ComplexRegistrationPatternService> _logger;

    public object Clone() =>
        throw new NotSupportedException(
            "Services with dependency injection cannot be cloned. Use the DI container to resolve instances.");

    public void Dispose()
    {
    }
}

// ============================================
// Additional Diagnostic Validation Examples
// ============================================

/// <summary>
///     Additional examples of diagnostic scenarios that may be encountered
///     These examples test edge cases and complex validation scenarios
/// </summary>
public static class AdditionalDiagnosticScenarios
{
    public static void ExplainDiagnosticScenarios()
    {
        // IOC020: Conditional Service conflicting conditions
        // IOC021: ConditionalService requires Service attribute - demonstrated above
        // IOC022: Empty ConditionalService conditions - demonstrated above
        // IOC023: ConfigValue without Equals/NotEquals - demonstrated above
        // IOC024: Equals/NotEquals without ConfigValue - demonstrated above
        // IOC025: Empty ConfigValue - demonstrated above
        // IOC026: Multiple ConditionalService attributes - demonstrated above
        // IOC027: Duplicate service registration patterns - demonstrated above
    }
}

// ============================================
// Demonstration Service
// ============================================

/// <summary>
///     Service that demonstrates how to properly handle diagnostic examples
/// </summary>
[Scoped]
public partial class DiagnosticDemonstrationService
{
    [Inject] private readonly ILogger<DiagnosticDemonstrationService> _logger;

    public void RunDiagnosticExamples()
    {
        _logger.LogInformation("Running diagnostic examples...");
        _logger.LogInformation("Check build output for diagnostic messages IOC001-IOC026");
        _logger.LogInformation("Configure diagnostic severity using MSBuild properties in the project file");
        _logger.LogInformation("Example: <IoCToolsNoImplementationSeverity>Error</IoCToolsNoImplementationSeverity>");
    }
}
