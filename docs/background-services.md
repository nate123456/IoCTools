# Background Services

IoCTools provides specialized support for .NET's `BackgroundService` with automatic registration, lifetime validation, and dependency injection patterns optimized for long-running services.

## Basic Background Service

### Simple Background Service

```csharp
[Service(Lifetime.Singleton)]
public partial class EmailProcessorService : BackgroundService
{
    [Inject] private readonly IServiceProvider _serviceProvider;
    [Inject] private readonly ILogger<EmailProcessorService> _logger;
    
    // Note: Configuration injection examples shown for reference
    // Full InjectConfiguration support is in development
    private readonly int _intervalSeconds = 30; // Configuration via constructor or initialization
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Email processor started");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingEmailsAsync();
                await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing emails");
                // Continue running despite errors
            }
        }
        
        _logger.LogInformation("Email processor stopped");
    }
    
    private async Task ProcessPendingEmailsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var emailQueue = scope.ServiceProvider.GetRequiredService<IEmailQueue>();
        
        var pendingEmails = await emailQueue.GetPendingEmailsAsync(50);
        foreach (var email in pendingEmails)
        {
            await emailService.SendAsync(email);
            await emailQueue.MarkAsProcessedAsync(email.Id);
        }
        
        _logger.LogInformation("Processed {Count} emails", pendingEmails.Count);
    }
}
```

**Generated Registration:**
```csharp
// Automatically registered as hosted service
services.AddHostedService<EmailProcessorService>();
```

**Configuration (appsettings.json):**
```json
{
  "EmailProcessor": {
    "IntervalSeconds": 30
  }
}
```

## Lifetime Validation

### IOC014: Background Service Lifetime

```csharp
[Service(Lifetime.Scoped)] // ❌ IOC014: Background services should be Singleton
public partial class InvalidBackgroundService : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // This will generate a diagnostic error
        return Task.CompletedTask;
    }
}
```

**Problem:** Background services run for the application lifetime and must be Singleton.

**Solution:**
```csharp
[Service(Lifetime.Singleton)] // ✅ Correct lifetime
public partial class ValidBackgroundService : BackgroundService
{
    [Inject] private readonly IServiceProvider _serviceProvider;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _serviceProvider.CreateScope();
            // Use scoped services within the scope
            var scopedService = scope.ServiceProvider.GetRequiredService<IScopedService>();
            await scopedService.DoWorkAsync();
            
            await Task.Delay(1000, stoppingToken);
        }
    }
}
```

## Configuration Support Status

**⚠️ Important Note:** While `[InjectConfiguration]` attribute exists and has extensive documentation, full implementation is currently in development. The examples throughout this documentation show the intended usage patterns, but the following limitations currently apply:

- Configuration injection may not generate all expected constructor parameters
- Some configuration binding patterns may require manual implementation
- Default values and hot-reloading features are planned but not fully implemented

For production use, consider using `IOptions<T>` pattern or injecting `IConfiguration` directly until full `[InjectConfiguration]` support is available.

## Scoped Dependency Pattern

### Using Scoped Services in Background Services

```csharp
[Service(Lifetime.Singleton)]
public partial class DataSyncService : BackgroundService
{
    [Inject] private readonly IServiceProvider _serviceProvider;
    [Inject] private readonly ILogger<DataSyncService> _logger;
    [Inject] private readonly IConfiguration _configuration;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var syncIntervalMinutes = _configuration.GetValue<int>("DataSync:SyncIntervalMinutes", 15);
        var batchSize = _configuration.GetValue<int>("DataSync:BatchSize", 100);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncDataAsync(batchSize, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Data sync failed");
            }
            
            await Task.Delay(TimeSpan.FromMinutes(syncIntervalMinutes), stoppingToken);
        }
    }
    
    private async Task SyncDataAsync(int batchSize, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        
        var dbContext = scope.ServiceProvider.GetRequiredService<IDbContext>();
        var apiClient = scope.ServiceProvider.GetRequiredService<IApiClient>();
        var mapper = scope.ServiceProvider.GetRequiredService<IMapper>();
        
        var pendingRecords = await dbContext.PendingSyncRecords
            .Take(batchSize)
            .ToListAsync(cancellationToken);
            
        foreach (var record in pendingRecords)
        {
            if (cancellationToken.IsCancellationRequested) break;
            
            try
            {
                var dto = mapper.Map<SyncDto>(record);
                await apiClient.SyncRecordAsync(dto);
                
                record.LastSyncDate = DateTime.UtcNow;
                record.SyncStatus = SyncStatus.Completed;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync record {RecordId}", record.Id);
                record.SyncStatus = SyncStatus.Failed;
                record.FailureReason = ex.Message;
            }
        }
        
        await dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Synced {Count} records", pendingRecords.Count);
    }
}
```

## Conditional Background Services

**⚠️ Note:** The combination of `[ConditionalService]` with `BackgroundService` requires validation. Test your specific scenarios to ensure proper registration and functionality.

### Environment-Specific Background Services

```csharp
[Service(Lifetime.Singleton)]
[ConditionalService(Environment = "Production")]
public partial class ProductionMetricsCollector : BackgroundService
{
    [Inject] private readonly IServiceProvider _serviceProvider;
    [Inject] private readonly ILogger<ProductionMetricsCollector> _logger;
    [Inject] private readonly IConfiguration _configuration;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = _configuration.GetValue<int>("Metrics:CollectionIntervalSeconds", 60);
        _logger.LogInformation("Production metrics collector started");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            await CollectMetricsAsync();
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }
    
    private async Task CollectMetricsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var metricsService = scope.ServiceProvider.GetRequiredService<IMetricsService>();
        var telemetryClient = scope.ServiceProvider.GetRequiredService<ITelemetryClient>();
        
        var metrics = await metricsService.CollectSystemMetricsAsync();
        await telemetryClient.SendMetricsAsync(metrics);
    }
}

[Service(Lifetime.Singleton)]
[ConditionalService(NotEnvironment = "Production")]
public partial class DevelopmentMockCollector : BackgroundService
{
    [Inject] private readonly ILogger<DevelopmentMockCollector> _logger;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Development mock collector - metrics disabled");
        
        // Just wait and do nothing in development
        await Task.Delay(-1, stoppingToken);
    }
}
```

### Feature Flag Controlled Background Services

```csharp
[Service(Lifetime.Singleton)]
[ConditionalService(ConfigValue = "Features:EnableBackgroundProcessing", Equals = "true")]
public partial class FeatureControlledProcessor : BackgroundService
{
    [Inject] private readonly IServiceProvider _serviceProvider;
    [Inject] private readonly ILogger<FeatureControlledProcessor> _logger;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Feature-controlled processor started");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _serviceProvider.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<IBackgroundProcessor>();
            
            await processor.ProcessAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}

[Service(Lifetime.Singleton)]
[ConditionalService(ConfigValue = "Features:EnableBackgroundProcessing", NotEquals = "true")]
public partial class DisabledProcessor : BackgroundService
{
    [Inject] private readonly ILogger<DisabledProcessor> _logger;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background processing disabled via feature flag");
        await Task.Delay(-1, stoppingToken); // Wait indefinitely
    }
}
```

## Advanced Background Service Patterns

### Queue-Based Processing

```csharp
[Service(Lifetime.Singleton)]
public partial class QueueProcessorService : BackgroundService
{
    [Inject] private readonly IServiceProvider _serviceProvider;
    [Inject] private readonly ILogger<QueueProcessorService> _logger;
    [Inject] private readonly IConfiguration _configuration;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var maxConcurrency = _configuration.GetValue<int>("QueueProcessor:MaxConcurrency", 5);
        var batchSize = _configuration.GetValue<int>("QueueProcessor:BatchSize", 10);
        
        var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        var tasks = new List<Task>();
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Get batch of work items
                var workItems = await GetWorkItemsAsync(batchSize);
                
                foreach (var item in workItems)
                {
                    await semaphore.WaitAsync(stoppingToken);
                    
                    var task = ProcessWorkItemAsync(item, semaphore, stoppingToken);
                    tasks.Add(task);
                }
                
                // Clean up completed tasks
                tasks.RemoveAll(t => t.IsCompleted);
                
                // Short delay before next batch
                await Task.Delay(100, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in queue processor main loop");
                await Task.Delay(5000, stoppingToken); // Back off on error
            }
        }
        
        // Wait for all tasks to complete
        await Task.WhenAll(tasks);
    }
    
    private async Task<List<WorkItem>> GetWorkItemsAsync(int batchSize)
    {
        using var scope = _serviceProvider.CreateScope();
        var queueService = scope.ServiceProvider.GetRequiredService<IQueueService>();
        return await queueService.GetPendingItemsAsync(batchSize);
    }
    
    private async Task ProcessWorkItemAsync(WorkItem item, SemaphoreSlim semaphore, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<IWorkItemProcessor>();
            
            await processor.ProcessAsync(item, cancellationToken);
            _logger.LogDebug("Processed work item {ItemId}", item.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process work item {ItemId}", item.Id);
        }
        finally
        {
            semaphore.Release();
        }
    }
}
```

### Timer-Based Background Service

```csharp
[Service(Lifetime.Singleton)]
public partial class ScheduledTaskService : BackgroundService
{
    [Inject] private readonly IServiceProvider _serviceProvider;
    [Inject] private readonly ILogger<ScheduledTaskService> _logger;
    [Inject] private readonly IConfiguration _configuration;
    
    private readonly List<Timer> _timers = new();
    
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Example: Configure timers based on configuration
        var intervalMinutes = _configuration.GetValue<int>("ScheduledTasks:IntervalMinutes", 60);
        var initialDelaySeconds = _configuration.GetValue<int>("ScheduledTasks:InitialDelaySeconds", 30);
        
        var timer = new Timer(
            async _ => await ExecuteScheduledTaskAsync(),
            null,
            TimeSpan.FromSeconds(initialDelaySeconds),
            TimeSpan.FromMinutes(intervalMinutes));
            
        _timers.Add(timer);
        _logger.LogInformation("Scheduled task configured with {Interval} minute interval", intervalMinutes);
        
        // Keep the service running
        return Task.Delay(-1, stoppingToken);
    }
    
    private async Task ExecuteScheduledTaskAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var taskExecutor = scope.ServiceProvider.GetRequiredService<IScheduledTaskExecutor>();
            
            _logger.LogInformation("Executing scheduled task");
            await taskExecutor.ExecuteAsync();
            _logger.LogInformation("Completed scheduled task");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute scheduled task");
        }
    }
    
    public override void Dispose()
    {
        foreach (var timer in _timers)
        {
            timer?.Dispose();
        }
        _timers.Clear();
        base.Dispose();
    }
}
```

## Background Service with Inheritance

### Base Background Service Class

```csharp
[Service(Lifetime.Singleton)]
[DependsOn<IServiceProvider, ILogger<BaseBackgroundService>>]
public abstract partial class BaseBackgroundService : BackgroundService
{
    protected abstract string ServiceName { get; }
    protected abstract TimeSpan ExecutionInterval { get; }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{ServiceName} background service started", ServiceName);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var stopwatch = Stopwatch.StartNew();
                await ExecuteWorkAsync(stoppingToken);
                stopwatch.Stop();
                
                _logger.LogDebug("{ServiceName} completed in {Duration}ms", 
                    ServiceName, stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in {ServiceName} background service", ServiceName);
            }
            
            await Task.Delay(ExecutionInterval, stoppingToken);
        }
        
        _logger.LogInformation("{ServiceName} background service stopped", ServiceName);
    }
    
    protected abstract Task ExecuteWorkAsync(CancellationToken cancellationToken);
    
    protected async Task<T> ExecuteWithScopeAsync<T>(Func<IServiceProvider, Task<T>> operation)
    {
        using var scope = _serviceProvider.CreateScope();
        return await operation(scope.ServiceProvider);
    }
    
    protected async Task ExecuteWithScopeAsync(Func<IServiceProvider, Task> operation)
    {
        using var scope = _serviceProvider.CreateScope();
        await operation(scope.ServiceProvider);
    }
}
```

### Concrete Background Services

```csharp
[Service(Lifetime.Singleton)]
public partial class EmailCleanupService : BaseBackgroundService
{
    [Inject] private readonly IConfiguration _configuration;
    
    private int IntervalHours => _configuration.GetValue<int>("EmailCleanup:IntervalHours", 24);
    private int RetentionDays => _configuration.GetValue<int>("EmailCleanup:RetentionDays", 90);
    
    protected override string ServiceName => "Email Cleanup";
    protected override TimeSpan ExecutionInterval => TimeSpan.FromHours(IntervalHours);
    
    protected override async Task ExecuteWorkAsync(CancellationToken cancellationToken)
    {
        await ExecuteWithScopeAsync(async serviceProvider =>
        {
            var emailRepository = serviceProvider.GetRequiredService<IEmailRepository>();
            var cutoffDate = DateTime.UtcNow.AddDays(-RetentionDays);
            
            var deletedCount = await emailRepository.DeleteOldEmailsAsync(cutoffDate, cancellationToken);
            _logger.LogInformation("Deleted {Count} old emails", deletedCount);
        });
    }
}

[Service(Lifetime.Singleton)]
public partial class CacheWarmupService : BaseBackgroundService
{
    [Inject] private readonly IConfiguration _configuration;
    
    private int IntervalMinutes => _configuration.GetValue<int>("CacheWarmup:IntervalMinutes", 30);
    
    protected override string ServiceName => "Cache Warmup";
    protected override TimeSpan ExecutionInterval => TimeSpan.FromMinutes(IntervalMinutes);
    
    protected override async Task ExecuteWorkAsync(CancellationToken cancellationToken)
    {
        await ExecuteWithScopeAsync(async serviceProvider =>
        {
            var cacheService = serviceProvider.GetRequiredService<ICacheService>();
            var dataService = serviceProvider.GetRequiredService<IDataService>();
            
            var criticalData = await dataService.GetCriticalDataAsync(cancellationToken);
            foreach (var data in criticalData)
            {
                await cacheService.WarmupAsync(data.Key, data.Value, cancellationToken);
            }
            
            _logger.LogInformation("Warmed up {Count} cache entries", criticalData.Count);
        });
    }
}
```

## Health Checks Integration

### Background Service with Health Checks

```csharp
[Service(Lifetime.Singleton)]
public partial class HealthMonitoredService : BackgroundService
{
    [Inject] private readonly IServiceProvider _serviceProvider;
    [Inject] private readonly ILogger<HealthMonitoredService> _logger;
    [Inject] private readonly IConfiguration _configuration;
    
    private DateTime _lastSuccessfulExecution = DateTime.UtcNow;
    private string _lastError = string.Empty;
    private volatile bool _isHealthy = true;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessWorkAsync();
                
                _lastSuccessfulExecution = DateTime.UtcNow;
                _lastError = string.Empty;
                _isHealthy = true;
                
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background service error");
                _lastError = ex.Message;
                
                // Mark unhealthy if failures persist beyond threshold
                var maxFailureThresholdMinutes = _configuration.GetValue<int>("HealthMonitor:MaxFailureThresholdMinutes", 10);
                var minutesSinceSuccess = (DateTime.UtcNow - _lastSuccessfulExecution).TotalMinutes;
                _isHealthy = minutesSinceSuccess < maxFailureThresholdMinutes;
                
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
    
    private async Task ProcessWorkAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var workProcessor = scope.ServiceProvider.GetRequiredService<IWorkProcessor>();
        await workProcessor.ProcessAsync();
    }
    
    // Health check method (called by health check service)
    public HealthCheckResult CheckHealth()
    {
        if (_isHealthy)
        {
            return HealthCheckResult.Healthy($"Last successful execution: {_lastSuccessfulExecution}");
        }
        
        return HealthCheckResult.Unhealthy($"Service unhealthy. Last error: {_lastError}. Last success: {_lastSuccessfulExecution}");
    }
}

// Health check registration (in Program.cs)
// builder.Services.AddHealthChecks()
//     .AddCheck<HealthMonitoredService>("background-service");
```

## Configuration Patterns

### Comprehensive Background Service Configuration

```json
{
  "BackgroundServices": {
    "EmailProcessor": {
      "Enabled": true,
      "IntervalSeconds": 30,
      "BatchSize": 50,
      "MaxRetries": 3,
      "RetryDelaySeconds": 5
    },
    "DataSync": {
      "Enabled": true,
      "SyncIntervalMinutes": 15,
      "BatchSize": 100,
      "ConnectionTimeout": "00:00:30",
      "CommandTimeout": "00:05:00"
    },
    "Cleanup": {
      "Enabled": true,
      "IntervalHours": 24,
      "RetentionDays": 90,
      "MaxItemsPerBatch": 1000
    }
  },
  "HealthChecks": {
    "BackgroundServices": {
      "MaxFailureThresholdMinutes": 10,
      "CheckIntervalSeconds": 30
    }
  }
}
```

### Configuration-Driven Background Service

```csharp
public class BackgroundServiceConfig
{
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 60;
    public int BatchSize { get; set; } = 50;
    public int MaxRetries { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 5;
}

[Service(Lifetime.Singleton)]
[ConditionalService(ConfigValue = "BackgroundServices:EmailProcessor:Enabled", Equals = "true")]
public partial class ConfigurableEmailProcessor : BackgroundService
{
    [Inject] private readonly IServiceProvider _serviceProvider;
    [Inject] private readonly ILogger<ConfigurableEmailProcessor> _logger;
    [Inject] private readonly IOptions<BackgroundServiceConfig> _config;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var config = _config.Value;
        _logger.LogInformation("Email processor started with {IntervalSeconds}s interval, {BatchSize} batch size", 
            config.IntervalSeconds, config.BatchSize);
            
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessEmailBatchAsync(config);
            await Task.Delay(TimeSpan.FromSeconds(config.IntervalSeconds), stoppingToken);
        }
    }
    
    private async Task ProcessEmailBatchAsync(BackgroundServiceConfig config)
    {
        for (int retry = 0; retry <= config.MaxRetries; retry++)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var emailProcessor = scope.ServiceProvider.GetRequiredService<IEmailBatchProcessor>();
                
                await emailProcessor.ProcessBatchAsync(config.BatchSize);
                return; // Success - exit retry loop
            }
            catch (Exception ex) when (retry < config.MaxRetries)
            {
                _logger.LogWarning(ex, "Email processing failed, retry {Retry}/{MaxRetries}", 
                    retry + 1, config.MaxRetries);
                    
                await Task.Delay(TimeSpan.FromSeconds(config.RetryDelaySeconds));
            }
        }
        
        _logger.LogError("Email processing failed after {MaxRetries} retries", config.MaxRetries);
    }
}
```

## Testing Background Services

### Testing Background Service Logic

```csharp
[TestClass]
public class BackgroundServiceTests
{
    [TestMethod]
    public async Task EmailProcessor_Should_ProcessEmails()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(CreateTestConfiguration());
        services.AddSingleton<ILogger<EmailProcessorService>, NullLogger<EmailProcessorService>>();
        services.AddScoped<IEmailService, MockEmailService>();
        services.AddScoped<IEmailQueue, MockEmailQueue>();
        services.AddSingleton<EmailProcessorService>();
        
        var serviceProvider = services.BuildServiceProvider();
        var processor = serviceProvider.GetRequiredService<EmailProcessorService>();
        
        // Act
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(5)); // Stop after 5 seconds
        
        var task = processor.StartAsync(cts.Token);
        await Task.Delay(2000); // Let it run for 2 seconds
        cts.Cancel();
        
        // Assert
        await task; // Should complete without exception
        var mockQueue = (MockEmailQueue)serviceProvider.GetRequiredService<IEmailQueue>();
        Assert.IsTrue(mockQueue.ProcessedCount > 0, "Should have processed some emails");
    }
    
    private IConfiguration CreateTestConfiguration()
    {
        var config = new Dictionary<string, string>
        {
            ["EmailProcessor:IntervalSeconds"] = "1"
        };
        
        return new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();
    }
}
```

## Best Practices

### Design Principles

1. **Always use Singleton lifetime** for background services
2. **Use IServiceProvider for scoped dependencies** within background services
3. **Handle exceptions gracefully** - don't let exceptions stop the service
4. **Implement proper cancellation** support throughout
5. **Add comprehensive logging** for debugging and monitoring
6. **Use configuration** for intervals, batch sizes, and feature flags
7. **Consider health checks** for monitoring service status

### Performance Optimization

```csharp
// ✅ Efficient pattern
[Service(Lifetime.Singleton)]
[BackgroundService]
public partial class OptimizedBackgroundService : BackgroundService
{
    [Inject] private readonly IServiceProvider _serviceProvider;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Single scope per batch
            using var scope = _serviceProvider.CreateScope();
            
            var processor = scope.ServiceProvider.GetRequiredService<IBatchProcessor>();
            await processor.ProcessBatchAsync(stoppingToken);
            
            await Task.Delay(1000, stoppingToken);
        }
    }
}

// ❌ Inefficient - creates scope per item
// Creates excessive scopes and increases memory pressure
```

### Error Handling

```csharp
[Service(Lifetime.Singleton)]
[BackgroundService]
public partial class RobustBackgroundService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessWorkAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected cancellation - exit gracefully
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background service error - continuing");
                // Continue running despite errors
                await Task.Delay(5000, stoppingToken); // Back off on error
            }
        }
    }
}
```

## Sample Applications

**⚠️ Note:** The IoCTools.Sample project currently does not include working background service examples. While configuration sections for background services exist in `appsettings.json`, complete runnable examples are not yet available in the sample application.

For practical implementation, refer to the code examples in this documentation and adapt them to your specific use cases.

## Next Steps

- **[Lifetime Management](lifetime-management.md)** - Understanding singleton patterns for background services
- **[Constructor Generation](constructor-generation.md)** - How constructors are generated for background services
- **[Conditional Services](conditional-services.md)** - Environment-specific background service patterns
- **[Testing](testing.md)** - Advanced testing strategies for background services