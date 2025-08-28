namespace IoCTools.Sample.Services;

using System.Diagnostics;

using Abstractions.Annotations;

using Microsoft.Extensions.Logging;

/// <summary>
/// Showcases the major architectural enhancements delivered during the 100% test success rate campaign
/// These examples demonstrate the intelligent service registration patterns and enhanced capabilities
/// </summary>

// ===== 1. INDIVIDUAL LIFETIME ATTRIBUTES =====
// Clean syntax using [Scoped], [Singleton], [Transient] instead of [Service(Lifetime.X)]

/// <summary>
///     Example service using the new [Scoped] attribute
///     Demonstrates clean lifetime specification
/// </summary>
[Scoped]
public partial class ModernScopedService
{
    [Inject] private readonly ILogger<ModernScopedService> _logger;

    public void ProcessRequest(string requestId) =>
        _logger.LogInformation("Processing request {RequestId} in scoped service", requestId);
}

/// <summary>
///     Example service using the new [Singleton] attribute
///     Perfect for services that maintain state across the application
/// </summary>
[Singleton]
public partial class ModernSingletonService
{
    private readonly Dictionary<string, int> _counters = new();
    [Inject] private readonly ILogger<ModernSingletonService> _logger;

    public int GetAndIncrement(string key)
    {
        if (!_counters.ContainsKey(key))
            _counters[key] = 0;

        var value = ++_counters[key];
        _logger.LogDebug("Counter {Key} incremented to {Value}", key, value);
        return value;
    }
}

/// <summary>
///     Example service using the new [Transient] attribute
///     Fresh instance on every injection
/// </summary>
[Transient]
public partial class ModernTransientService
{
    private readonly string _instanceId = Guid.NewGuid().ToString("N")[..8];
    [Inject] private readonly ILogger<ModernTransientService> _logger;

    public string GetInstanceId()
    {
        _logger.LogDebug("Transient service instance ID: {InstanceId}", _instanceId);
        return _instanceId;
    }
}

// ===== 2. INTELLIGENT SERVICE REGISTRATION PATTERNS =====
// Automatic detection of partial classes implementing interfaces with enhanced logic

/// <summary>
///     Interface for automatic collection injection demonstration
/// </summary>
public interface IAutomaticService
{
    string ServiceName { get; }
    Task<string> ProcessAsync(string input);
}

/// <summary>
///     Service with ONLY [Inject] fields → automatically registered as IAutomaticService
///     Demonstrates intelligent service registration for interface implementations
/// </summary>
public partial class AutoDetectedService : IAutomaticService
{
    [Inject] private readonly ILogger<AutoDetectedService> _logger;

    public string ServiceName => "AutoDetected";

    public async Task<string> ProcessAsync(string input)
    {
        _logger.LogInformation("AutoDetectedService processing: {Input}", input);
        await Task.Delay(10);
        return $"AutoDetected: {input}";
    }
}

/// <summary>
///     Another service implementing the same interface for collection injection
/// </summary>
public partial class SmartRegistrationService : IAutomaticService
{
    [Inject] private readonly ILogger<SmartRegistrationService> _logger;

    public string ServiceName => "SmartRegistration";

    public async Task<string> ProcessAsync(string input)
    {
        _logger.LogInformation("SmartRegistrationService processing: {Input}", input);
        await Task.Delay(15);
        return $"SmartRegistration: {input}";
    }
}

/// <summary>
///     Service that demonstrates enhanced IEnumerable
///     <T>
///         dependency injection
///         Automatically gets all IAutomaticService implementations
/// </summary>
[Scoped]
public partial class CollectionAwareService
{
    [Inject] private readonly IEnumerable<IAutomaticService> _automaticServices;
    [Inject] private readonly ILogger<CollectionAwareService> _logger;

    public async Task<ArchitecturalProcessingResult> ProcessWithAllServicesAsync(string input)
    {
        _logger.LogInformation("Processing with {ServiceCount} automatic services", _automaticServices.Count());

        var results = new List<string>();

        foreach (var service in _automaticServices)
        {
            var result = await service.ProcessAsync(input);
            results.Add(result);
            _logger.LogDebug("Service {ServiceName} completed", service.ServiceName);
        }

        return new ArchitecturalProcessingResult(
            input,
            results,
            _automaticServices.Select(s => s.ServiceName).ToList(),
            DateTime.UtcNow
        );
    }
}

// ===== 3. MIXED DEPENDENCY SCENARIOS =====
// Services with BOTH [Inject] and [InjectConfiguration] → require explicit lifetime

/// <summary>
///     Example configuration class for mixed dependency scenarios
/// </summary>
public class ProcessingConfiguration
{
    public int BatchSize { get; set; } = 10;
    public int TimeoutSeconds { get; set; } = 30;
    public string ProcessingMode { get; set; } = "Standard";
    public bool EnableMetrics { get; set; } = true;
}

/// <summary>
///     Service with BOTH field injection AND configuration injection
///     Requires explicit lifetime attribute as per new intelligent logic
/// </summary>
[Scoped] // Explicit lifetime required for mixed scenarios
public partial class MixedDependencyService
{
    // Configuration injection
    [InjectConfiguration("Processing")] private readonly ProcessingConfiguration _config;

    // Field injection
    [Inject] private readonly ILogger<MixedDependencyService> _logger;
    [Inject] private readonly ModernSingletonService _singletonService;

    public async Task<ProcessingResult> ProcessDataAsync(IEnumerable<string> data)
    {
        _logger.LogInformation("Processing data with batch size {BatchSize}, timeout {Timeout}s",
            _config.BatchSize, _config.TimeoutSeconds);

        var dataList = data.ToList();
        var batches = dataList.Chunk(_config.BatchSize).ToList();
        var results = new List<string>();

        foreach (var batch in batches)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.TimeoutSeconds));

            try
            {
                await Task.Delay(50, cts.Token); // Simulate processing

                foreach (var item in batch)
                {
                    var counter = _singletonService.GetAndIncrement("processing");
                    results.Add($"Processed {item} (#{counter})");
                }

                if (_config.EnableMetrics) _logger.LogInformation("Batch processed: {BatchCount} items", batch.Length);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Batch processing timed out after {Timeout}s", _config.TimeoutSeconds);
                results.Add("TIMEOUT");
            }
        }

        return new ProcessingResult(
            dataList.Count,
            results.Count(r => r != "TIMEOUT"),
            batches.Count,
            results,
            $"BatchSize={_config.BatchSize}, Mode={_config.ProcessingMode}"
        );
    }
}

// ===== 4. CONFIGURATION-ONLY SERVICES =====
// Services with ONLY [InjectConfiguration] → constructor generation only (no auto-registration)

/// <summary>
///     Service with ONLY configuration injection
///     Gets constructor generation but no automatic registration
///     Must be manually registered if needed in DI container
/// </summary>
public partial class ConfigurationOnlyService
{
    [InjectConfiguration("App")] private readonly AppSettings _appSettings;

    public string GetAppInfo() => $"App: {_appSettings.Name} v{_appSettings.Version}";

    // This service gets constructor generation but is not automatically registered
    // Would need explicit registration: services.AddScoped<ConfigurationOnlyService>();
}

// ===== 5. DEPENDS-ON-ONLY SERVICES =====
// Services with ONLY [DependsOn] → constructor generation only

/// <summary>
///     Service with ONLY DependsOn dependencies
///     Gets constructor generation but no automatic registration
/// </summary>
[DependsOn<ModernSingletonService>]
[DependsOn<ILogger<DependsOnOnlyService>>]
public partial class DependsOnOnlyService
{
    public void DoWork()
    {
        // Constructor is generated with ModernSingletonService and ILogger<T> parameters
        // but service is not automatically registered
        // Would need explicit registration if desired
    }
}

// ===== 6. ENHANCED CONFIGURATION INTEGRATION =====
// Better handling of configuration injection scenarios with validation

/// <summary>
///     Advanced configuration with validation
/// </summary>
public class AdvancedProcessingConfig
{
    public int WorkerCount { get; set; } = 4;
    public string DatabaseConnection { get; set; } = "";
    public List<string> AllowedOperations { get; set; } = new();
    public Dictionary<string, int> Limits { get; set; } = new();

    public bool IsValid =>
        WorkerCount > 0 &&
        !string.IsNullOrEmpty(DatabaseConnection) &&
        AllowedOperations.Count > 0;
}

/// <summary>
///     Service demonstrating enhanced configuration integration with validation
/// </summary>
[Singleton] // Explicit lifetime for configuration-heavy service
public partial class EnhancedConfigurationService
{
    [InjectConfiguration("AdvancedProcessing")]
    private readonly AdvancedProcessingConfig _config;

    [Inject] private readonly ILogger<EnhancedConfigurationService> _logger;

    public ConfigurationValidationResult ValidateConfiguration()
    {
        var issues = new List<string>();

        if (!_config.IsValid)
        {
            if (_config.WorkerCount <= 0)
                issues.Add("WorkerCount must be greater than 0");

            if (string.IsNullOrEmpty(_config.DatabaseConnection))
                issues.Add("DatabaseConnection is required");

            if (_config.AllowedOperations.Count == 0)
                issues.Add("At least one AllowedOperation must be specified");
        }

        var result = new ConfigurationValidationResult(_config.IsValid, issues);

        if (result.IsValid)
            _logger.LogInformation("Configuration validation passed");
        else
            _logger.LogWarning("Configuration validation failed: {Issues}", string.Join(", ", issues));

        return result;
    }

    public async Task<ProcessingMetrics> GetProcessingMetricsAsync()
    {
        await Task.Delay(10);

        return new ProcessingMetrics(
            _config.WorkerCount,
            _config.AllowedOperations.Count,
            _config.Limits.Count,
            !string.IsNullOrEmpty(_config.DatabaseConnection),
            ValidateConfiguration().IsValid ? "Valid" : "Invalid"
        );
    }
}

// ===== 7. INTERFACE REGISTRATION SHOWCASE =====
// Demonstrating automatic interface registration for IEnumerable<T> scenarios

/// <summary>
///     Handler interface for automatic collection registration
/// </summary>
public interface IEventHandler
{
    string HandlerName { get; }
    int Priority { get; }
    Task<bool> CanHandleAsync(string eventType);

    Task HandleAsync(string eventType,
        object eventData);
}

/// <summary>
///     First event handler - automatically registered for IEnumerable<IEventHandler>
/// </summary>
[Transient] // Fresh instance per event
public partial class EmailEventHandler : IEventHandler
{
    [Inject] private readonly ILogger<EmailEventHandler> _logger;

    public string HandlerName => "Email";
    public int Priority => 1;

    public async Task<bool> CanHandleAsync(string eventType)
    {
        await Task.Delay(1);
        return eventType.Contains("email", StringComparison.OrdinalIgnoreCase);
    }

    public async Task HandleAsync(string eventType,
        object eventData)
    {
        _logger.LogInformation("Email handler processing event: {EventType}", eventType);
        await Task.Delay(25);
        _logger.LogInformation("Email event processed successfully");
    }
}

/// <summary>
///     Second event handler - also automatically registered
/// </summary>
[Transient]
public partial class AuditEventHandler : IEventHandler
{
    [Inject] private readonly ILogger<AuditEventHandler> _logger;

    public string HandlerName => "Audit";
    public int Priority => 2;

    public async Task<bool> CanHandleAsync(string eventType)
    {
        await Task.Delay(1);
        return true; // Audit handles all events
    }

    public async Task HandleAsync(string eventType,
        object eventData)
    {
        _logger.LogInformation("Audit handler recording event: {EventType}", eventType);
        await Task.Delay(10);
        _logger.LogInformation("Audit event recorded");
    }
}

/// <summary>
///     Event processing service that uses all available handlers
///     Demonstrates enhanced collection injection
/// </summary>
[Scoped]
public partial class EventProcessingService
{
    [Inject] private readonly IEnumerable<IEventHandler> _eventHandlers;
    [Inject] private readonly ILogger<EventProcessingService> _logger;

    public async Task<EventProcessingResult> ProcessEventAsync(string eventType,
        object eventData)
    {
        _logger.LogInformation("Processing event {EventType} with {HandlerCount} available handlers",
            eventType, _eventHandlers.Count());

        var handlerResults = new List<HandlerResult>();
        var eligibleHandlers = new List<IEventHandler>();

        // Find eligible handlers
        foreach (var handler in _eventHandlers.OrderBy(h => h.Priority))
            if (await handler.CanHandleAsync(eventType))
                eligibleHandlers.Add(handler);

        _logger.LogInformation("Found {EligibleCount} eligible handlers for event {EventType}",
            eligibleHandlers.Count, eventType);

        // Process with eligible handlers
        foreach (var handler in eligibleHandlers)
            try
            {
                var stopwatch = Stopwatch.StartNew();
                await handler.HandleAsync(eventType, eventData);
                stopwatch.Stop();

                handlerResults.Add(new HandlerResult(
                    handler.HandlerName,
                    true,
                    stopwatch.Elapsed.TotalMilliseconds,
                    "Success"
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Handler {HandlerName} failed to process event {EventType}",
                    handler.HandlerName, eventType);

                handlerResults.Add(new HandlerResult(
                    handler.HandlerName,
                    false,
                    0,
                    ex.Message
                ));
            }

        var successCount = handlerResults.Count(r => r.Success);
        _logger.LogInformation("Event processing completed: {SuccessCount}/{TotalCount} handlers succeeded",
            successCount, handlerResults.Count);

        return new EventProcessingResult(
            eventType,
            _eventHandlers.Count(),
            eligibleHandlers.Count,
            handlerResults.Count,
            successCount,
            handlerResults,
            DateTime.UtcNow
        );
    }
}

// ===== SUPPORTING DATA MODELS =====

public record ArchitecturalProcessingResult(
    string Input,
    IEnumerable<string> Results,
    IEnumerable<string> ProcessedBy,
    DateTime ProcessedAt
);

public record ProcessingResult(
    int TotalItems,
    int ProcessedItems,
    int BatchCount,
    IEnumerable<string> Results,
    string Configuration
);

public record ConfigurationValidationResult(bool IsValid, IEnumerable<string> Issues);

public record ProcessingMetrics(
    int ConfiguredWorkers,
    int AllowedOperations,
    int ConfiguredLimits,
    bool DatabaseConfigured,
    string ValidationStatus
);

public record HandlerResult(
    string HandlerName,
    bool Success,
    double ProcessingTimeMs,
    string Message
);

public record EventProcessingResult(
    string EventType,
    int TotalHandlers,
    int EligibleHandlers,
    int ProcessedHandlers,
    int SuccessfulHandlers,
    IEnumerable<HandlerResult> HandlerResults,
    DateTime ProcessedAt
);
