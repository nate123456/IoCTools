namespace IoCTools.Sample.Services;

using System.Diagnostics;

using Abstractions.Annotations;

using Interfaces;

using Microsoft.Extensions.Logging;

// ===== COLLECTION INJECTION EXAMPLES =====

// === 1. MULTIPLE IMPLEMENTATIONS WITH IENUMERABLE<T> ===

/// <summary>
///     Collection notification service interface for collection injection examples
/// </summary>
public interface ICollectionNotificationService
{
    string NotificationType { get; }
    int Priority { get; }

    Task<bool> SendNotificationAsync(string recipient,
        string message);

    Task<bool> IsAvailableAsync();
}

/// <summary>
///     Email notification implementation
/// </summary>
[Scoped]
public partial class CollectionEmailNotificationService : ICollectionNotificationService
{
    [Inject] private readonly ILogger<CollectionEmailNotificationService> _logger;

    public string NotificationType => "Email";
    public int Priority => 1; // High priority

    public async Task<bool> SendNotificationAsync(string recipient,
        string message)
    {
        _logger.LogInformation("Sending email notification to {Recipient}: {Message}", recipient, message);
        await Task.Delay(50); // Simulate email sending
        return true;
    }

    public async Task<bool> IsAvailableAsync()
    {
        await Task.Delay(10);
        return true; // Email is always available
    }
}

/// <summary>
///     SMS notification implementation
/// </summary>
[Scoped]
public partial class CollectionSmsNotificationService : ICollectionNotificationService
{
    [Inject] private readonly ILogger<CollectionSmsNotificationService> _logger;

    public string NotificationType => "SMS";
    public int Priority => 2; // Medium priority

    public async Task<bool> SendNotificationAsync(string recipient,
        string message)
    {
        _logger.LogInformation("Sending SMS notification to {Recipient}: {Message}", recipient, message);
        await Task.Delay(30); // Simulate SMS sending
        return true;
    }

    public async Task<bool> IsAvailableAsync()
    {
        await Task.Delay(5);
        return DateTime.Now.Hour >= 8 && DateTime.Now.Hour <= 22; // SMS available during business hours
    }
}

/// <summary>
///     Push notification implementation
/// </summary>
[Scoped]
public partial class CollectionPushNotificationService : ICollectionNotificationService
{
    [Inject] private readonly ILogger<CollectionPushNotificationService> _logger;

    public string NotificationType => "Push";
    public int Priority => 3; // Low priority

    public async Task<bool> SendNotificationAsync(string recipient,
        string message)
    {
        _logger.LogInformation("Sending push notification to {Recipient}: {Message}", recipient, message);
        await Task.Delay(20); // Simulate push notification
        return true;
    }

    public async Task<bool> IsAvailableAsync()
    {
        await Task.Delay(5);
        return Random.Shared.NextDouble() > 0.1; // 90% availability rate
    }
}

/// <summary>
///     Slack notification implementation
/// </summary>
[Scoped]
public partial class CollectionSlackNotificationService : ICollectionNotificationService
{
    [Inject] private readonly ILogger<CollectionSlackNotificationService> _logger;

    public string NotificationType => "Slack";
    public int Priority => 4; // Lowest priority

    public async Task<bool> SendNotificationAsync(string recipient,
        string message)
    {
        _logger.LogInformation("Sending Slack notification to {Recipient}: {Message}", recipient, message);
        await Task.Delay(40); // Simulate Slack API call
        return true;
    }

    public async Task<bool> IsAvailableAsync()
    {
        await Task.Delay(15);
        return true; // Slack is always available
    }
}

/// <summary>
///     Notification manager that uses IEnumerable
///     <INotificationService> to send notifications through all available channels
/// </summary>
[Scoped]
public partial class NotificationManager
{
    [Inject] private readonly ILogger<NotificationManager> _logger;
    [Inject] private readonly IEnumerable<ICollectionNotificationService> _notificationServices;

    /// <summary>
    ///     Sends notification through all available services
    /// </summary>
    public async Task<NotificationResult> SendToAllAsync(string recipient,
        string message)
    {
        _logger.LogInformation("Sending notification to all available services for {Recipient}", recipient);

        var results = new List<ServiceResult>();
        var tasks = _notificationServices.Select(async service =>
        {
            try
            {
                var isAvailable = await service.IsAvailableAsync();
                if (!isAvailable)
                {
                    _logger.LogWarning("{ServiceType} is not available", service.NotificationType);
                    return new ServiceResult(service.NotificationType, false, "Service unavailable");
                }

                var success = await service.SendNotificationAsync(recipient, message);
                return new ServiceResult(service.NotificationType, success,
                    success ? "Sent successfully" : "Failed to send");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification via {ServiceType}", service.NotificationType);
                return new ServiceResult(service.NotificationType, false, $"Exception: {ex.Message}");
            }
        });

        results = (await Task.WhenAll(tasks)).ToList();

        var successCount = results.Count(r => r.Success);
        _logger.LogInformation("Notification sent via {SuccessCount}/{TotalCount} services", successCount,
            results.Count);

        return new NotificationResult(recipient, message, results, successCount > 0);
    }

    /// <summary>
    ///     Sends notification through the first available service (fail-fast pattern)
    /// </summary>
    public async Task<ServiceResult> SendToFirstAvailableAsync(string recipient,
        string message)
    {
        _logger.LogInformation("Sending notification via first available service for {Recipient}", recipient);

        // Try services in priority order (lower Priority number = higher priority)
        var orderedServices = _notificationServices.OrderBy(s => s.Priority);

        foreach (var service in orderedServices)
            try
            {
                var isAvailable = await service.IsAvailableAsync();
                if (!isAvailable)
                {
                    _logger.LogDebug("{ServiceType} is not available, trying next", service.NotificationType);
                    continue;
                }

                var success = await service.SendNotificationAsync(recipient, message);
                if (success)
                {
                    _logger.LogInformation("Notification sent successfully via {ServiceType}",
                        service.NotificationType);
                    return new ServiceResult(service.NotificationType, true, "Sent successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error sending notification via {ServiceType}, trying next",
                    service.NotificationType);
            }

        _logger.LogError("Failed to send notification via any available service");
        return new ServiceResult("None", false, "All services failed or unavailable");
    }

    /// <summary>
    ///     Gets statistics about available notification services
    /// </summary>
    public async Task<NotificationStatistics> GetServiceStatisticsAsync()
    {
        var serviceStats = new List<ServiceStatistic>();

        foreach (var service in _notificationServices)
        {
            var isAvailable = await service.IsAvailableAsync();
            serviceStats.Add(new ServiceStatistic(service.NotificationType, service.Priority, isAvailable));
        }

        var totalServices = serviceStats.Count;
        var availableServices = serviceStats.Count(s => s.IsAvailable);

        return new NotificationStatistics(totalServices, availableServices, serviceStats.OrderBy(s => s.Priority));
    }
}

// === 2. ILIST<T> AND IREADONLYLIST<T> INJECTION ===

/// <summary>
///     Data processor interface for chain processing
/// </summary>
public interface IProcessor
{
    string ProcessorName { get; }
    int Order { get; }
    Task<CollectionProcessingResult> ProcessAsync(CollectionProcessingData data);
    bool CanProcess(CollectionProcessingData data);
}

/// <summary>
///     First processor in the chain
/// </summary>
[Transient]
public partial class ValidationProcessor : IProcessor
{
    [Inject] private readonly ILogger<ValidationProcessor> _logger;

    public string ProcessorName => "Validation";
    public int Order => 1;

    public bool CanProcess(CollectionProcessingData data) => !string.IsNullOrEmpty(data.Content);

    public async Task<CollectionProcessingResult> ProcessAsync(CollectionProcessingData data)
    {
        _logger.LogInformation("Validating data with ID: {Id}", data.Id);
        await Task.Delay(10);

        if (string.IsNullOrEmpty(data.Content) || data.Content.Length > 1000)
            return CollectionProcessingResult.CreateFailure("Validation failed");

        return CollectionProcessingResult.CreateSuccess($"Validated: {data.Content}");
    }
}

/// <summary>
///     Second processor in the chain
/// </summary>
[Transient]
public partial class TransformationProcessor : IProcessor
{
    [Inject] private readonly ILogger<TransformationProcessor> _logger;

    public string ProcessorName => "Transformation";
    public int Order => 2;

    public bool CanProcess(CollectionProcessingData data) => data.Content.Contains("transform");

    public async Task<CollectionProcessingResult> ProcessAsync(CollectionProcessingData data)
    {
        _logger.LogInformation("Transforming data with ID: {Id}", data.Id);
        await Task.Delay(20);

        var transformed = data.Content.ToUpperInvariant();
        return CollectionProcessingResult.CreateSuccess($"Transformed: {transformed}");
    }
}

/// <summary>
///     Third processor in the chain
/// </summary>
[Transient]
public partial class EnrichmentProcessor : IProcessor
{
    [Inject] private readonly ILogger<EnrichmentProcessor> _logger;

    public string ProcessorName => "Enrichment";
    public int Order => 3;

    public bool CanProcess(CollectionProcessingData data) => data.Metadata?.Count > 0;

    public async Task<CollectionProcessingResult> ProcessAsync(CollectionProcessingData data)
    {
        _logger.LogInformation("Enriching data with ID: {Id}", data.Id);
        await Task.Delay(15);

        var enriched = $"{data.Content} [Enriched with {data.Metadata?.Count ?? 0} metadata items]";
        return CollectionProcessingResult.CreateSuccess(enriched);
    }
}

/// <summary>
///     Processing chain service that uses IList<IProcessor> for ordered processing
/// </summary>
[Scoped]
public partial class ProcessorChain
{
    [Inject] private readonly ILogger<ProcessorChain> _logger;
    [Inject] private readonly IList<IProcessor> _processors;

    /// <summary>
    ///     Processes data through the entire chain in order
    /// </summary>
    public async Task<ChainResult> ProcessChainAsync(CollectionProcessingData data)
    {
        _logger.LogInformation("Processing data {Id} through processor chain", data.Id);

        // Sort processors by order
        var orderedProcessors = _processors.OrderBy(p => p.Order).ToList();
        var results = new List<CollectionProcessingResult>();
        var currentData = data;

        foreach (var processor in orderedProcessors)
        {
            if (!processor.CanProcess(currentData))
            {
                _logger.LogDebug("Processor {ProcessorName} skipped - cannot process data", processor.ProcessorName);
                continue;
            }

            var result = await processor.ProcessAsync(currentData);
            results.Add(result);

            if (!result.Success)
            {
                _logger.LogError("Processor {ProcessorName} failed: {Message}", processor.ProcessorName,
                    result.Message);
                break;
            }

            // Update data for next processor
            currentData = currentData with { Content = result.ProcessedContent ?? currentData.Content };
            _logger.LogDebug("Processor {ProcessorName} completed successfully", processor.ProcessorName);
        }

        var overallSuccess = results.All(r => r.Success);
        return new ChainResult(data.Id, overallSuccess, results, currentData);
    }

    /// <summary>
    ///     Gets information about the processor chain
    /// </summary>
    public ChainInfo GetChainInfo()
    {
        var orderedProcessors = _processors.OrderBy(p => p.Order).ToList();
        var processorInfos = orderedProcessors.Select(p => new ProcessorInfo(p.ProcessorName, p.Order)).ToList();

        return new ChainInfo(processorInfos.Count, processorInfos);
    }
}

/// <summary>
///     Read-only aggregator service that uses IReadOnlyList<IProcessor> for analysis
/// </summary>
[Singleton]
public partial class ProcessorAnalyzer
{
    [Inject] private readonly ILogger<ProcessorAnalyzer> _logger;
    [Inject] private readonly IReadOnlyList<IProcessor> _processors;

    /// <summary>
    ///     Analyzes all processors without modifying them
    /// </summary>
    public async Task<AnalysisReport> AnalyzeProcessorsAsync()
    {
        _logger.LogInformation("Analyzing {Count} processors", _processors.Count);

        var analysisResults = new List<ProcessorAnalysis>();

        foreach (var processor in _processors)
        {
            // Test with sample data
            var sampleData = new CollectionProcessingData(
                Guid.NewGuid().ToString(),
                "transform sample content",
                new Dictionary<string, string> { { "test", "value" } }
            );

            var canProcess = processor.CanProcess(sampleData);
            var processingTime = await MeasureProcessingTimeAsync(processor, sampleData);

            analysisResults.Add(new ProcessorAnalysis(
                processor.ProcessorName,
                processor.Order,
                canProcess,
                processingTime
            ));
        }

        var totalProcessors = analysisResults.Count;
        var activeProcessors = analysisResults.Count(a => a.CanProcessSample);
        var averageTime = analysisResults.Where(a => a.ProcessingTimeMs > 0).Average(a => a.ProcessingTimeMs);

        return new AnalysisReport(totalProcessors, activeProcessors, averageTime,
            analysisResults.OrderBy(a => a.Order));
    }

    private async Task<double> MeasureProcessingTimeAsync(IProcessor processor,
        CollectionProcessingData data)
    {
        if (!processor.CanProcess(data))
            return 0;

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await processor.ProcessAsync(data);
            stopwatch.Stop();
            return stopwatch.Elapsed.TotalMilliseconds;
        }
        catch
        {
            stopwatch.Stop();
            return -1; // Indicates error
        }
    }
}

// === 3. GENERIC COLLECTION INJECTION ===

/// <summary>
///     Generic validator interface for different entity types
/// </summary>
public interface IValidator<T> where T : class
{
    string ValidatorName { get; }
    int Severity { get; } // 1 = Error, 2 = Warning, 3 = Info
    Task<ValidationResult> ValidateAsync(T entity);
}

/// <summary>
///     User validator implementation
/// </summary>
[Transient]
public partial class UserValidator : IValidator<User>
{
    [Inject] private readonly ILogger<UserValidator> _logger;

    public string ValidatorName => "User Validator";
    public int Severity => 1; // Error level

    public async Task<ValidationResult> ValidateAsync(User entity)
    {
        _logger.LogDebug("Validating user: {UserId}", entity.Id);
        await Task.Delay(5);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(entity.Name))
            errors.Add("Name is required");

        if (string.IsNullOrWhiteSpace(entity.Email) || !entity.Email.Contains("@"))
            errors.Add("Valid email is required");

        return new ValidationResult(errors.Count == 0, errors);
    }
}

/// <summary>
///     User business rules validator
/// </summary>
[Transient]
public partial class UserBusinessValidator : IValidator<User>
{
    [Inject] private readonly ILogger<UserBusinessValidator> _logger;

    public string ValidatorName => "User Business Rules";
    public int Severity => 2; // Warning level

    public async Task<ValidationResult> ValidateAsync(User entity)
    {
        _logger.LogDebug("Validating user business rules: {UserId}", entity.Id);
        await Task.Delay(10);

        var errors = new List<string>();

        if (entity.Email.EndsWith("@tempmail.com"))
            errors.Add("Temporary email addresses are discouraged");

        if (entity.Name.Length < 2)
            errors.Add("Name should be at least 2 characters");

        return new ValidationResult(errors.Count == 0, errors);
    }
}

/// <summary>
///     Order validator implementation
/// </summary>
[Transient]
public partial class OrderValidator : IValidator<Order>
{
    [Inject] private readonly ILogger<OrderValidator> _logger;

    public string ValidatorName => "Order Validator";
    public int Severity => 1; // Error level

    public async Task<ValidationResult> ValidateAsync(Order entity)
    {
        _logger.LogDebug("Validating order: {OrderId}", entity.Id);
        await Task.Delay(8);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(entity.CustomerEmail))
            errors.Add("Customer email is required");

        if (entity.Payment.Amount <= 0)
            errors.Add("Payment amount must be positive");

        return new ValidationResult(errors.Count == 0, errors);
    }
}

/// <summary>
///     Comprehensive validation service that uses IEnumerable<IValidator<T>> for each entity type
/// </summary>
[Scoped]
public partial class ValidationService
{
    [Inject] private readonly ILogger<ValidationService> _logger;
    [Inject] private readonly IEnumerable<IValidator<Order>> _orderValidators;
    [Inject] private readonly IEnumerable<IValidator<User>> _userValidators;

    /// <summary>
    ///     Validates a user entity using all available user validators
    /// </summary>
    public async Task<ComprehensiveValidationResult> ValidateUserAsync(User user)
    {
        _logger.LogInformation("Validating user {UserId} with {ValidatorCount} validators", user.Id,
            _userValidators.Count());

        var validationTasks = _userValidators.Select(async validator =>
        {
            var result = await validator.ValidateAsync(user);
            return new ValidatorResult(validator.ValidatorName, validator.Severity, result.IsValid, result.Errors);
        });

        var validatorResults = await Task.WhenAll(validationTasks);

        var hasErrors = validatorResults.Any(r => !r.IsValid && r.Severity == 1);
        var hasWarnings = validatorResults.Any(r => !r.IsValid && r.Severity == 2);

        return new ComprehensiveValidationResult(
            "User",
            user.Id.ToString(),
            !hasErrors,
            hasWarnings,
            validatorResults.OrderBy(r => r.Severity)
        );
    }

    /// <summary>
    ///     Validates an order entity using all available order validators
    /// </summary>
    public async Task<ComprehensiveValidationResult> ValidateOrderAsync(Order order)
    {
        _logger.LogInformation("Validating order {OrderId} with {ValidatorCount} validators", order.Id,
            _orderValidators.Count());

        var validationTasks = _orderValidators.Select(async validator =>
        {
            var result = await validator.ValidateAsync(order);
            return new ValidatorResult(validator.ValidatorName, validator.Severity, result.IsValid, result.Errors);
        });

        var validatorResults = await Task.WhenAll(validationTasks);

        var hasErrors = validatorResults.Any(r => !r.IsValid && r.Severity == 1);
        var hasWarnings = validatorResults.Any(r => !r.IsValid && r.Severity == 2);

        return new ComprehensiveValidationResult(
            "Order",
            order.Id.ToString(),
            !hasErrors,
            hasWarnings,
            validatorResults.OrderBy(r => r.Severity)
        );
    }

    /// <summary>
    ///     Gets validation statistics for both entity types
    /// </summary>
    public ValidationStatistics GetValidationStatistics() => new(
        _userValidators.Count(),
        _orderValidators.Count(),
        _userValidators.Count() + _orderValidators.Count()
    );
}

// === 4. AGGREGATOR PATTERN WITH COLLECTIONS ===

/// <summary>
///     Data aggregator interface for combining results
/// </summary>
public interface IAggregator<T>
{
    string AggregatorName { get; }
    int Priority { get; }
    Task<T> AggregateAsync(IEnumerable<T> items);
}

/// <summary>
///     Sum aggregator for numeric data
/// </summary>
[Transient]
public partial class SumAggregator : IAggregator<decimal>
{
    [Inject] private readonly ILogger<SumAggregator> _logger;

    public string AggregatorName => "Sum";
    public int Priority => 1;

    public async Task<decimal> AggregateAsync(IEnumerable<decimal> items)
    {
        _logger.LogDebug("Calculating sum of {Count} items", items.Count());
        await Task.Delay(5);
        return items.Sum();
    }
}

/// <summary>
///     Average aggregator for numeric data
/// </summary>
[Transient]
public partial class AverageAggregator : IAggregator<decimal>
{
    [Inject] private readonly ILogger<AverageAggregator> _logger;

    public string AggregatorName => "Average";
    public int Priority => 2;

    public async Task<decimal> AggregateAsync(IEnumerable<decimal> items)
    {
        _logger.LogDebug("Calculating average of {Count} items", items.Count());
        await Task.Delay(5);
        var itemList = items.ToList();
        return itemList.Count > 0 ? itemList.Average() : 0;
    }
}

/// <summary>
///     Multi-aggregator service that uses all available aggregators
/// </summary>
[Scoped]
public partial class AggregatorService
{
    [Inject] private readonly IReadOnlyList<IAggregator<decimal>> _aggregators;
    [Inject] private readonly ILogger<AggregatorService> _logger;

    /// <summary>
    ///     Performs all available aggregations on the data
    /// </summary>
    public async Task<AggregationReport> PerformAllAggregationsAsync(IEnumerable<decimal> data)
    {
        var dataList = data.ToList();
        _logger.LogInformation("Performing {AggregatorCount} aggregations on {DataCount} items", _aggregators.Count,
            dataList.Count);

        var aggregationTasks = _aggregators.Select(async aggregator =>
        {
            var result = await aggregator.AggregateAsync(dataList);
            return new AggregationResult(aggregator.AggregatorName, aggregator.Priority, result);
        });

        var results = await Task.WhenAll(aggregationTasks);

        return new AggregationReport(
            dataList.Count,
            _aggregators.Count,
            results.OrderBy(r => r.Priority)
        );
    }

    /// <summary>
    ///     Gets the primary aggregation result (highest priority aggregator)
    /// </summary>
    public async Task<decimal> GetPrimaryAggregationAsync(IEnumerable<decimal> data)
    {
        var primaryAggregator = _aggregators.OrderBy(a => a.Priority).First();
        _logger.LogInformation("Using primary aggregator: {AggregatorName}", primaryAggregator.AggregatorName);
        return await primaryAggregator.AggregateAsync(data);
    }
}

// === 5. COLLECTION INJECTION WITH DIFFERENT LIFETIMES ===

/// <summary>
///     Provider interface for demonstrating lifetime mixing in collections
/// </summary>
public interface IDataProvider
{
    string ProviderName { get; }
    string InstanceId { get; }
    Task<string> GetDataAsync(string key);
}

/// <summary>
///     Singleton data provider (shared instance)
/// </summary>
[Singleton]
public partial class CachedDataProvider : IDataProvider
{
    [Inject] private readonly ILogger<CachedDataProvider> _logger;

    public string ProviderName => "Cached";
    public string InstanceId { get; } = Guid.NewGuid().ToString("N")[..8];

    public async Task<string> GetDataAsync(string key)
    {
        _logger.LogDebug("Getting cached data for key: {Key} (Instance: {InstanceId})", key, InstanceId);
        await Task.Delay(10);
        return $"Cached-{key}-{InstanceId}";
    }
}

/// <summary>
///     Scoped data provider (per-request instance)
/// </summary>
[Scoped]
public partial class DatabaseDataProvider : IDataProvider
{
    [Inject] private readonly ILogger<DatabaseDataProvider> _logger;

    public string ProviderName => "Database";
    public string InstanceId { get; } = Guid.NewGuid().ToString("N")[..8];

    public async Task<string> GetDataAsync(string key)
    {
        _logger.LogDebug("Getting database data for key: {Key} (Instance: {InstanceId})", key, InstanceId);
        await Task.Delay(25);
        return $"DB-{key}-{InstanceId}";
    }
}

/// <summary>
///     Transient data provider (new instance each time)
/// </summary>
[Transient]
public partial class ApiDataProvider : IDataProvider
{
    [Inject] private readonly ILogger<ApiDataProvider> _logger;

    public string ProviderName => "API";
    public string InstanceId { get; } = Guid.NewGuid().ToString("N")[..8];

    public async Task<string> GetDataAsync(string key)
    {
        _logger.LogDebug("Getting API data for key: {Key} (Instance: {InstanceId})", key, InstanceId);
        await Task.Delay(40);
        return $"API-{key}-{InstanceId}";
    }
}

/// <summary>
///     Service that demonstrates how different lifetime services work together in collections
/// </summary>
[Scoped]
public partial class MultiProviderService
{
    [Inject] private readonly IEnumerable<IDataProvider> _dataProviders;
    [Inject] private readonly ILogger<MultiProviderService> _logger;

    /// <summary>
    ///     Retrieves data from all providers and shows lifetime behavior
    /// </summary>
    public async Task<MultiProviderResult> GetFromAllProvidersAsync(string key)
    {
        _logger.LogInformation("Fetching data from {ProviderCount} providers for key: {Key}", _dataProviders.Count(),
            key);

        var providerTasks = _dataProviders.Select(async provider =>
        {
            var data = await provider.GetDataAsync(key);
            return new ProviderResult(provider.ProviderName, provider.InstanceId, data);
        });

        var results = await Task.WhenAll(providerTasks);

        return new MultiProviderResult(key, results);
    }

    /// <summary>
    ///     Demonstrates that instances maintain their lifetime behavior across multiple calls
    /// </summary>
    public async Task<LifetimeDemonstration> DemonstrateLifetimeBehaviorAsync()
    {
        _logger.LogInformation("Demonstrating lifetime behavior across multiple calls");

        var call1Results = new List<ProviderInstanceInfo>();
        var call2Results = new List<ProviderInstanceInfo>();

        // First call
        foreach (var provider in _dataProviders)
            call1Results.Add(new ProviderInstanceInfo(provider.ProviderName, provider.InstanceId));

        // Wait a bit
        await Task.Delay(50);

        // Second call - check if instance IDs changed
        foreach (var provider in _dataProviders)
            call2Results.Add(new ProviderInstanceInfo(provider.ProviderName, provider.InstanceId));

        return new LifetimeDemonstration(call1Results, call2Results);
    }
}

// === SUPPORTING DATA MODELS ===

public record ServiceResult(string ServiceType, bool Success, string Message);

public record NotificationResult(string Recipient, string Message, IEnumerable<ServiceResult> Results, bool AnySuccess);

public record ServiceStatistic(string ServiceType, int Priority, bool IsAvailable);

public record NotificationStatistics(int TotalServices, int AvailableServices, IEnumerable<ServiceStatistic> Services);

public record CollectionProcessingData(string Id, string Content, Dictionary<string, string>? Metadata = null);

public record CollectionProcessingResult(bool Success, string Message, string? ProcessedContent = null)
{
    public static CollectionProcessingResult CreateSuccess(string processedContent) =>
        new(true, "Success", processedContent);

    public static CollectionProcessingResult CreateFailure(string message) => new(false, message);
}

public record ChainResult(
    string DataId,
    bool Success,
    IEnumerable<CollectionProcessingResult> ProcessingResults,
    CollectionProcessingData FinalData);

public record ProcessorInfo(string Name, int Order);

public record ChainInfo(int ProcessorCount, IEnumerable<ProcessorInfo> Processors);

public record ProcessorAnalysis(string ProcessorName, int Order, bool CanProcessSample, double ProcessingTimeMs);

public record AnalysisReport(
    int TotalProcessors,
    int ActiveProcessors,
    double AverageProcessingTimeMs,
    IEnumerable<ProcessorAnalysis> ProcessorAnalyses);

public record ValidatorResult(string ValidatorName, int Severity, bool IsValid, IEnumerable<string> Errors);

public record ComprehensiveValidationResult(
    string EntityType,
    string EntityId,
    bool IsValid,
    bool HasWarnings,
    IEnumerable<ValidatorResult> ValidatorResults);

public record ValidationStatistics(int UserValidatorCount, int OrderValidatorCount, int TotalValidators);

public record AggregationResult(string AggregatorName, int Priority, decimal Value);

public record AggregationReport(int DataCount, int AggregatorCount, IEnumerable<AggregationResult> Results);

public record ProviderResult(string ProviderName, string InstanceId, string Data);

public record MultiProviderResult(string Key, IEnumerable<ProviderResult> Results);

public record ProviderInstanceInfo(string ProviderName, string InstanceId);

public record LifetimeDemonstration(
    IEnumerable<ProviderInstanceInfo> FirstCall,
    IEnumerable<ProviderInstanceInfo> SecondCall)
{
    public IEnumerable<string> GetLifetimeAnalysis()
    {
        var analysis = new List<string>();
        var firstCallDict = FirstCall.ToDictionary(p => p.ProviderName, p => p.InstanceId);
        var secondCallDict = SecondCall.ToDictionary(p => p.ProviderName, p => p.InstanceId);

        foreach (var providerName in firstCallDict.Keys)
        {
            var firstId = firstCallDict[providerName];
            var secondId = secondCallDict[providerName];

            if (firstId == secondId)
                analysis.Add($"{providerName}: Same instance ({firstId}) - Singleton or Scoped behavior");
            else
                analysis.Add($"{providerName}: Different instances ({firstId} -> {secondId}) - Transient behavior");
        }

        return analysis;
    }
}
