// For configuration models

namespace IoCTools.Sample.Services;

using System.Collections.Concurrent;

using Abstractions.Annotations;

using Configuration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// === BACKGROUND SERVICE CONFIGURATION CLASSES ===

/// <summary>
///     Configuration for email queue processing background service
/// </summary>
public class EmailProcessorSettings
{
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 30;
    public int BatchSize { get; set; } = 50;
    public int MaxRetries { get; set; } = 3;
    public string QueueName { get; set; } = "email-queue";
}

/// <summary>
///     Configuration for data cleanup background service
/// </summary>
public class DataCleanupSettings
{
    public bool Enabled { get; set; } = true;
    public int IntervalHours { get; set; } = 24;
    public int RetentionDays { get; set; } = 30;
    public bool CompressOldData { get; set; } = true;
    public string[] TableNames { get; set; } = { "Logs", "TempFiles", "Sessions" };
}

/// <summary>
///     Configuration for health check monitoring background service
/// </summary>
public class HealthMonitorSettings
{
    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 5;
    public string[] EndpointsToCheck { get; set; } = { "/health", "/api/status" };
    public int TimeoutSeconds { get; set; } = 30;
    public string NotificationEmail { get; set; } = "admin@example.com";
}

/// <summary>
///     Configuration for file watcher background service
/// </summary>
public class FileWatcherSettings
{
    public bool Enabled { get; set; } = true;
    public string WatchPath { get; set; } = "./uploads";
    public string[] FileExtensions { get; set; } = { ".txt", ".pdf", ".docx" };
    public bool ProcessSubdirectories { get; set; } = true;
    public int ProcessingDelayMs { get; set; } = 1000;
}

// NOTE: NotificationSchedulerSettings is defined in Configuration/ConfigurationModels.cs to avoid duplicates

// === 1. SIMPLE BACKGROUND SERVICE ===

/// <summary>
///     Basic background service example that inherits from BackgroundService
/// </summary>
// Background services are automatically detected by intelligent inference
public partial class SimpleBackgroundWorker : BackgroundService
{
    [Inject] private readonly ILogger<SimpleBackgroundWorker> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SimpleBackgroundWorker started at: {Time}", DateTimeOffset.Now);

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("SimpleBackgroundWorker running at: {Time}", DateTimeOffset.Now);

            // Simulate work
            await DoWorkAsync(stoppingToken);

            // Wait for next execution
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }

        _logger.LogInformation("SimpleBackgroundWorker stopped at: {Time}", DateTimeOffset.Now);
    }

    private async Task DoWorkAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Performing simple background work...");
        await Task.Delay(100, cancellationToken); // Simulate processing
        _logger.LogInformation("Simple background work completed");
    }
}

// === 2. EMAIL QUEUE PROCESSOR WITH DEPENDENCY INJECTION ===

/// <summary>
///     Background service that processes email queue with full dependency injection
/// </summary>
public partial class EmailQueueProcessor : BackgroundService
{
    [Inject] private readonly ILogger<EmailQueueProcessor> _logger;
    [Inject] private readonly IServiceScopeFactory _scopeFactory;

    [InjectConfiguration("BackgroundServices:EmailProcessor")]
    private readonly EmailProcessorSettings _settings;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("EmailQueueProcessor is disabled in configuration");
            return;
        }

        _logger.LogInformation("EmailQueueProcessor started. Interval: {IntervalSeconds}s, Batch Size: {BatchSize}",
            _settings.IntervalSeconds, _settings.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessEmailBatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing email batch");
            }

            await Task.Delay(TimeSpan.FromSeconds(_settings.IntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("EmailQueueProcessor stopped");
    }

    private async Task ProcessEmailBatchAsync(CancellationToken cancellationToken)
    {
        // Create a scope to resolve scoped services
        using var scope = _scopeFactory.CreateScope();
        var scopedEmailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        _logger.LogInformation("Processing email batch of up to {BatchSize} emails from queue {QueueName}",
            _settings.BatchSize, _settings.QueueName);

        // Simulate retrieving and processing emails from queue
        var emailsToProcess = GetPendingEmails(_settings.BatchSize);

        foreach (var email in emailsToProcess)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                await scopedEmailService.SendConfirmationAsync(email);
                _logger.LogDebug("Successfully sent email to {Email}", email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email}", email);
                // In a real implementation, you might retry or add to dead letter queue
            }
        }

        _logger.LogInformation("Processed {Count} emails from queue", emailsToProcess.Length);
    }

    private string[] GetPendingEmails(int batchSize)
    {
        // Simulate retrieving emails from a queue
        // In reality, this would query a database or message queue
        return new[] { "user1@example.com", "user2@example.com", "admin@example.com" }
            .Take(batchSize)
            .ToArray();
    }
}

// === 3. DATA CLEANUP SERVICE WITH CONFIGURATION ===

/// <summary>
///     Background service that performs periodic data cleanup operations
/// </summary>
public partial class DataCleanupService : BackgroundService
{
    [InjectConfiguration("DataCleanupSettings")]
    private readonly DataCleanupSettings _cleanupSettings;

    [InjectConfiguration("Database:ConnectionString")]
    private readonly string _connectionString;

    [Inject] private readonly ILogger<DataCleanupService> _logger;
    [Inject] private readonly IServiceScopeFactory _scopeFactory;

    [InjectConfiguration("Database:DefaultTimeout", DefaultValue = 30)]
    private readonly int _timeoutSeconds;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_cleanupSettings.Enabled)
        {
            _logger.LogInformation("DataCleanupService is disabled in configuration");
            return;
        }

        _logger.LogInformation(
            "DataCleanupService started. Interval: {IntervalHours}h, Retention: {RetentionDays} days",
            _cleanupSettings.IntervalHours, _cleanupSettings.RetentionDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformCleanupAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during data cleanup operation");
            }

            var nextRun = TimeSpan.FromHours(_cleanupSettings.IntervalHours);
            _logger.LogInformation("Next cleanup scheduled in {NextRun}", nextRun);
            await Task.Delay(nextRun, stoppingToken);
        }

        _logger.LogInformation("DataCleanupService stopped");
    }

    private async Task PerformCleanupAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting data cleanup operation for tables: {Tables}",
            string.Join(", ", _cleanupSettings.TableNames));

        var cutoffDate = DateTime.UtcNow.AddDays(-_cleanupSettings.RetentionDays);
        var totalRecordsDeleted = 0;

        foreach (var tableName in _cleanupSettings.TableNames)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var recordsDeleted = await CleanupTableAsync(tableName, cutoffDate, cancellationToken);
            totalRecordsDeleted += recordsDeleted;

            _logger.LogInformation("Cleaned up {RecordsDeleted} records from {TableName}",
                recordsDeleted, tableName);
        }

        if (_cleanupSettings.CompressOldData) await CompressOldDataAsync(cancellationToken);

        _logger.LogInformation("Data cleanup completed. Total records deleted: {TotalDeleted}",
            totalRecordsDeleted);
    }

    private async Task<int> CleanupTableAsync(string tableName,
        DateTime cutoffDate,
        CancellationToken cancellationToken)
    {
        // Simulate database cleanup operation
        _logger.LogDebug("Cleaning up table {TableName} for records older than {CutoffDate}",
            tableName, cutoffDate);

        await Task.Delay(500, cancellationToken); // Simulate database operation

        // Return simulated count of deleted records
        return Random.Shared.Next(0, 100);
    }

    private async Task CompressOldDataAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Compressing old data archives...");
        await Task.Delay(1000, cancellationToken); // Simulate compression
        _logger.LogInformation("Data compression completed");
    }
}

// === 4. HEALTH CHECK MONITORING SERVICE ===

/// <summary>
///     Background service that monitors application health endpoints
/// </summary>
public partial class HealthCheckService : BackgroundService
{
    [InjectConfiguration("Api:BaseUrl")] private readonly string _baseUrl;

    [InjectConfiguration("HealthMonitorSettings")]
    private readonly HealthMonitorSettings _healthSettings;

    [Inject] private readonly IHttpClientFactory _httpClientFactory;
    [Inject] private readonly ILogger<HealthCheckService> _logger;
    [Inject] private readonly IServiceScopeFactory _scopeFactory;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_healthSettings.Enabled)
        {
            _logger.LogInformation("HealthCheckService is disabled in configuration");
            return;
        }

        _logger.LogInformation(
            "HealthCheckService started. Monitoring {EndpointCount} endpoints every {IntervalMinutes} minutes",
            _healthSettings.EndpointsToCheck.Length, _healthSettings.IntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformHealthChecksAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during health check operation");
            }

            await Task.Delay(TimeSpan.FromMinutes(_healthSettings.IntervalMinutes), stoppingToken);
        }

        _logger.LogInformation("HealthCheckService stopped");
    }

    private async Task PerformHealthChecksAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Performing health checks on {EndpointCount} endpoints",
            _healthSettings.EndpointsToCheck.Length);

        using var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(_healthSettings.TimeoutSeconds);

        var healthResults = new List<(string endpoint, bool healthy, string? error)>();

        foreach (var endpoint in _healthSettings.EndpointsToCheck)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var (healthy, error) = await CheckEndpointAsync(httpClient, endpoint, cancellationToken);
            healthResults.Add((endpoint, healthy, error));

            var status = healthy ? "HEALTHY" : "UNHEALTHY";
            _logger.LogInformation("Health check for {Endpoint}: {Status}", endpoint, status);

            if (!healthy) _logger.LogWarning("Health check failed for {Endpoint}: {Error}", endpoint, error);
        }

        // Send notification if any endpoints are unhealthy
        var unhealthyEndpoints = healthResults.Where(r => !r.healthy).ToArray();
        if (unhealthyEndpoints.Any()) await NotifyHealthIssuesAsync(unhealthyEndpoints);

        _logger.LogInformation("Health check completed. {HealthyCount}/{TotalCount} endpoints healthy",
            healthResults.Count(r => r.healthy), healthResults.Count);
    }

    private async Task<(bool healthy, string? error)> CheckEndpointAsync(HttpClient httpClient,
        string endpoint,
        CancellationToken cancellationToken)
    {
        try
        {
            var fullUrl = $"{_baseUrl.TrimEnd('/')}{endpoint}";
            var response = await httpClient.GetAsync(fullUrl, cancellationToken);

            return (response.IsSuccessStatusCode,
                response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private async Task NotifyHealthIssuesAsync((string endpoint, bool healthy, string? error)[] unhealthyEndpoints)
    {
        _logger.LogWarning("Sending health issue notification for {Count} unhealthy endpoints",
            unhealthyEndpoints.Length);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var emailService = scope.ServiceProvider.GetService<IEmailService>();

            if (emailService != null)
            {
                await emailService.SendConfirmationAsync(_healthSettings.NotificationEmail);
                _logger.LogInformation("Health issue notification sent to {Email}", _healthSettings.NotificationEmail);
            }
            else
            {
                _logger.LogWarning("Email service not available for health notifications");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send health issue notification");
        }
    }
}

// === 5. FILE WATCHER SERVICE ===

/// <summary>
///     Background service that monitors file system changes and processes new files
/// </summary>
public partial class FileWatcherService : BackgroundService
{
    private readonly ConcurrentQueue<string> _filesToProcess = new();
    [Inject] private readonly ILogger<FileWatcherService> _logger;
    private readonly SemaphoreSlim _processingLock = new(1, 1);
    [Inject] private readonly IServiceScopeFactory _scopeFactory;

    [InjectConfiguration("FileWatcherSettings")]
    private readonly FileWatcherSettings _watcherSettings;

    private FileSystemWatcher? _fileWatcher;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_watcherSettings.Enabled)
        {
            _logger.LogInformation("FileWatcherService is disabled in configuration");
            return;
        }

        _logger.LogInformation("FileWatcherService started. Watching path: {WatchPath}",
            _watcherSettings.WatchPath);

        try
        {
            StartFileWatcher();

            // Process files in background
            while (!stoppingToken.IsCancellationRequested)
            {
                await ProcessPendingFilesAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromMilliseconds(_watcherSettings.ProcessingDelayMs), stoppingToken);
            }
        }
        finally
        {
            StopFileWatcher();
        }

        _logger.LogInformation("FileWatcherService stopped");
    }

    private void StartFileWatcher()
    {
        if (!Directory.Exists(_watcherSettings.WatchPath))
        {
            Directory.CreateDirectory(_watcherSettings.WatchPath);
            _logger.LogInformation("Created watch directory: {WatchPath}", _watcherSettings.WatchPath);
        }

        _fileWatcher = new FileSystemWatcher(_watcherSettings.WatchPath)
        {
            IncludeSubdirectories = _watcherSettings.ProcessSubdirectories,
            NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.LastWrite | NotifyFilters.FileName
        };

        // Set up event handlers
        _fileWatcher.Created += OnFileCreated;
        _fileWatcher.Changed += OnFileChanged;
        _fileWatcher.Error += OnError;

        // Start watching
        _fileWatcher.EnableRaisingEvents = true;

        _logger.LogInformation("File watcher started for extensions: {Extensions}",
            string.Join(", ", _watcherSettings.FileExtensions));
    }

    private void StopFileWatcher()
    {
        if (_fileWatcher != null)
        {
            _fileWatcher.EnableRaisingEvents = false;
            _fileWatcher.Dispose();
            _fileWatcher = null;
        }
    }

    private void OnFileCreated(object sender,
        FileSystemEventArgs e)
    {
        if (ShouldProcessFile(e.FullPath))
        {
            _logger.LogInformation("File created: {FilePath}", e.FullPath);
            _filesToProcess.Enqueue(e.FullPath);
        }
    }

    private void OnFileChanged(object sender,
        FileSystemEventArgs e)
    {
        if (ShouldProcessFile(e.FullPath))
        {
            _logger.LogDebug("File changed: {FilePath}", e.FullPath);
            _filesToProcess.Enqueue(e.FullPath);
        }
    }

    private void OnError(object sender,
        ErrorEventArgs e) => _logger.LogError(e.GetException(), "File watcher error");

    private bool ShouldProcessFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return _watcherSettings.FileExtensions.Contains(extension);
    }

    private async Task ProcessPendingFilesAsync(CancellationToken cancellationToken)
    {
        if (_filesToProcess.IsEmpty) return;

        await _processingLock.WaitAsync(cancellationToken);

        try
        {
            var processedCount = 0;
            while (_filesToProcess.TryDequeue(out var filePath) && !cancellationToken.IsCancellationRequested)
            {
                await ProcessFileAsync(filePath, cancellationToken);
                processedCount++;

                // Limit batch processing to avoid blocking
                if (processedCount >= 10) break;
            }

            if (processedCount > 0) _logger.LogInformation("Processed {Count} files from queue", processedCount);
        }
        finally
        {
            _processingLock.Release();
        }
    }

    private async Task ProcessFileAsync(string filePath,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("File no longer exists: {FilePath}", filePath);
                return;
            }

            _logger.LogInformation("Processing file: {FilePath}", filePath);

            // Simulate file processing
            var fileInfo = new FileInfo(filePath);
            _logger.LogInformation("File details - Name: {Name}, Size: {Size} bytes, Modified: {Modified}",
                fileInfo.Name, fileInfo.Length, fileInfo.LastWriteTime);

            // In a real implementation, you might:
            // - Validate file format
            // - Parse file contents
            // - Store file metadata in database
            // - Move file to processed folder
            // - Send notifications

            await Task.Delay(100, cancellationToken); // Simulate processing time

            _logger.LogInformation("Successfully processed file: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process file: {FilePath}", filePath);
        }
    }
}

// === 6. NOTIFICATION SCHEDULER SERVICE WITH CONDITIONAL REGISTRATION ===

/// <summary>
///     Background service that sends scheduled notifications with conditional registration
/// </summary>
[ConditionalService(ConfigValue = "Features:EnableNotifications", Equals = "true")]
public partial class NotificationSchedulerService : BackgroundService
{
    [Inject] private readonly ILogger<NotificationSchedulerService> _logger;

    [InjectConfiguration("Features:EnableNotifications", DefaultValue = "false")]
    private readonly string _notificationsEnabled;

    [InjectConfiguration("NotificationSchedulerSettings")]
    private readonly NotificationSchedulerSettings _schedulerSettings;

    [Inject] private readonly IServiceScopeFactory _scopeFactory;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_schedulerSettings.Enabled)
        {
            _logger.LogInformation("NotificationSchedulerService is disabled in configuration");
            return;
        }

        _logger.LogInformation(
            "NotificationSchedulerService started. Interval: {IntervalMinutes} minutes, Provider: {Provider}",
            _schedulerSettings.IntervalMinutes, _schedulerSettings.DefaultNotificationProvider);

        _logger.LogInformation(
            "NotificationSchedulerService will resolve notification providers dynamically through scoped services");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessScheduledNotificationsAsync(stoppingToken);

                if (_schedulerSettings.SendDailyDigest) await CheckDailyDigestAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during notification processing");
            }

            await Task.Delay(TimeSpan.FromMinutes(_schedulerSettings.IntervalMinutes), stoppingToken);
        }

        _logger.LogInformation("NotificationSchedulerService stopped");
    }

    private async Task ProcessScheduledNotificationsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing scheduled notifications...");

        // Simulate retrieving scheduled notifications from database
        var notifications = GetPendingNotifications();

        foreach (var notification in notifications)
        {
            if (cancellationToken.IsCancellationRequested) break;

            try
            {
                await SendNotificationAsync(notification, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification {NotificationId}", notification.Id);
            }
        }

        _logger.LogInformation("Processed {Count} scheduled notifications", notifications.Length);
    }

    private async Task SendNotificationAsync(ScheduledNotification notification,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var notificationServices = scope.ServiceProvider.GetServices<INotificationService>();
        var notificationService = notificationServices.FirstOrDefault();

        if (notificationService != null)
        {
            await notificationService.SendNotificationAsync($"Scheduled: {notification.Message}");
            _logger.LogInformation("Sent notification {NotificationId} to {Recipient}",
                notification.Id, notification.Recipient);
        }
        else
        {
            _logger.LogWarning("No notification service available for notification {NotificationId}", notification.Id);
        }
    }

    private async Task CheckDailyDigestAsync(CancellationToken cancellationToken)
    {
        var currentTime = DateTime.Now.ToString("HH:mm");
        if (currentTime == _schedulerSettings.DigestTime)
        {
            _logger.LogInformation("Sending daily digest at scheduled time: {DigestTime}",
                _schedulerSettings.DigestTime);
            await SendDailyDigestAsync(cancellationToken);
        }
    }

    private async Task SendDailyDigestAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var emailService = scope.ServiceProvider.GetService<IEmailService>();

        if (emailService != null)
        {
            await emailService.SendConfirmationAsync("admin@example.com");
            _logger.LogInformation("Daily digest sent successfully");
        }
    }

    private ScheduledNotification[] GetPendingNotifications()
    {
        // Simulate database query for pending notifications
        return new[]
        {
            new ScheduledNotification(1, "System maintenance reminder", "admin@example.com", DateTime.Now),
            new ScheduledNotification(2, "Weekly report available", "user@example.com", DateTime.Now)
        };
    }
}

// === 7. COMPLEX BACKGROUND SERVICE WITH MULTIPLE DEPENDENCIES ===

/// <summary>
///     Complex background service demonstrating multiple dependency types and patterns
/// </summary>
public partial class ComplexBackgroundService : BackgroundService
{
    [Inject] private readonly ICacheService _cacheService;

    // Options pattern injection - Use IOptions<T> (Singleton) instead of IOptionsSnapshot<T> (Scoped) for BackgroundServices
    [InjectConfiguration] private readonly IOptions<DataCleanupSettings> _cleanupOptions;

    // Configuration injection examples
    [InjectConfiguration("Database:ConnectionString")]
    private readonly string _connectionString;

    [InjectConfiguration] private readonly EmailProcessorSettings _emailSettings;

    [InjectConfiguration("Features:EnableAdvancedLogging", DefaultValue = false)]
    private readonly bool _enableAdvancedLogging;

    [InjectConfiguration("Email:FromAddress")]
    private readonly string _fromAddress;

    [Inject] private readonly ILogger<ComplexBackgroundService> _logger;
    [Inject] private readonly IServiceScopeFactory _scopeFactory;

    [InjectConfiguration("BackgroundServices:DataSync:IntervalMinutes", DefaultValue = 15)]
    private readonly int _syncInterval;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ComplexBackgroundService started - will resolve notification providers dynamically");

        if (_enableAdvancedLogging)
            _logger.LogInformation(
                "Advanced logging enabled. Email settings: {EmailSettings}, Sync interval: {SyncInterval} minutes",
                _emailSettings.QueueName, _syncInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformComplexOperationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in complex background operations");
            }

            await Task.Delay(TimeSpan.FromMinutes(_syncInterval), stoppingToken);
        }

        _logger.LogInformation("ComplexBackgroundService stopped");
    }

    private async Task PerformComplexOperationsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting complex background operations...");

        // 1. Cache operations
        var cacheKey = $"complex-operation-{DateTime.Now:yyyyMMdd}";
        var cachedData = _cacheService.GetOrSet(cacheKey, () => $"Processed at {DateTime.Now}");
        _logger.LogInformation("Cache operation result: {CachedData}", cachedData);

        // 2. Use scoped services
        using var scope = _scopeFactory.CreateScope();
        var scopedEmailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var scopedNotificationServices = scope.ServiceProvider.GetServices<INotificationService>();

        // 3. Multi-service operations
        foreach (var notificationService in scopedNotificationServices.Take(2)) // Limit for demo
        {
            if (cancellationToken.IsCancellationRequested) break;

            await notificationService.SendNotificationAsync("Complex background service is running");
            _logger.LogInformation("Sent notification via {ServiceType}", notificationService.GetType().Name);
        }

        // 4. Configuration-driven operations
        if (_enableAdvancedLogging)
            _logger.LogInformation(
                "Advanced operation with cleanup settings. Retention: {RetentionDays} days, Tables: {Tables}",
                _cleanupOptions.Value.RetentionDays, string.Join(", ", _cleanupOptions.Value.TableNames));

        // 5. Email operations using injected configuration
        if (!string.IsNullOrEmpty(_fromAddress))
        {
            await scopedEmailService.SendConfirmationAsync(_fromAddress);
            _logger.LogInformation("Sent confirmation email from configured address: {FromAddress}", _fromAddress);
        }

        _logger.LogInformation("Complex background operations completed");
    }
}

// === SUPPORTING CLASSES ===

/// <summary>
///     Represents a scheduled notification
/// </summary>
public record ScheduledNotification(int Id, string Message, string Recipient, DateTime ScheduledTime);

// === 8. CUSTOM IHOSTEDSERVICE IMPLEMENTATION (TESTING REFACTOR) ===

/// <summary>
///     Custom IHostedService implementation that directly implements the interface
///     This tests the refactored unified IHostedService detection logic
/// </summary>
public partial class CustomHostedService : IHostedService
{
    [Inject] private readonly ILogger<CustomHostedService> _logger;
    private Timer? _timer;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("CustomHostedService started");

        // Create a timer that fires every 30 seconds
        _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("CustomHostedService stopped");

        _timer?.Change(Timeout.Infinite, 0);
        _timer?.Dispose();

        return Task.CompletedTask;
    }

    private void DoWork(object? state) =>
        _logger.LogInformation("CustomHostedService executing work at: {Time}", DateTimeOffset.Now);
}

// Note: IHttpClientFactory is provided by Microsoft.Extensions.Http
// We register it in Program.cs with services.AddHttpClient()
/// <summary>
/// Concurrent queue for thread-safe file processing
/// </summary>
// ConcurrentQueue is defined at the top of the file
