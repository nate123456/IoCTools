namespace IoCTools.Sample.Configuration;

using System.ComponentModel.DataAnnotations;

// === CORE CONFIGURATION CLASSES ===

/// <summary>
///     Configuration settings for email functionality
///     Binds to "Email" section in appsettings.json
/// </summary>
public class EmailSettings
{
    [Required] public string SmtpHost { get; set; } = string.Empty;

    [Range(1, 65535)] public int SmtpPort { get; set; }

    public bool UseSsl { get; set; }

    public string ApiKey { get; set; } = string.Empty;

    [Required] [EmailAddress] public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = string.Empty;

    /// <summary>
    ///     Nested settings for SMTP configuration
    /// </summary>
    public SmtpSettings Settings { get; set; } = new();
}

public class SmtpSettings
{
    public string SmtpHost { get; set; } = string.Empty;

    [Range(1, 65535)] public int Port { get; set; }

    public bool UseSsl { get; set; }
}

/// <summary>
///     Configuration settings for caching functionality
///     Binds to "Cache" section in appsettings.json
/// </summary>
public class CacheSettings
{
    [Required] public string Provider { get; set; } = string.Empty;

    [Range(1, 10080)] // Max 1 week in minutes
    public int ExpirationMinutes { get; set; }

    [Range(1, 1000000)] public int MaxItems { get; set; }

    public RedisSettings Redis { get; set; } = new();
}

public class RedisSettings
{
    [Required] public string ConnectionString { get; set; } = string.Empty;
}

/// <summary>
///     Configuration settings for database connectivity
///     Binds to "Database" section in appsettings.json
/// </summary>
public class DatabaseSettings
{
    [Required] public string ConnectionString { get; set; } = string.Empty;

    [Range(1, 3600)] // Max 1 hour timeout
    public int TimeoutSeconds { get; set; }

    public bool EnableRetry { get; set; }

    [Range(0, 10)] public int MaxRetries { get; set; }

    [Required] public string Provider { get; set; } = string.Empty;

    // Alternative timeout property for different use cases
    public int DefaultTimeout { get; set; }
}

/// <summary>
///     Feature flags configuration
///     Binds to "Features" section in appsettings.json
/// </summary>
public class FeatureFlags
{
    public bool EnableAdvancedLogging { get; set; }
    public bool UseAdvancedSecurity { get; set; }
    public string NewPaymentProcessor { get; set; } = string.Empty;
    public string EnableOptionalService { get; set; } = string.Empty;
    public string EnableDistributedCache { get; set; } = string.Empty;
    public string EnablePremiumFeatures { get; set; } = string.Empty;
    public string EnableNotifications { get; set; } = string.Empty;
    public string Enabled { get; set; } = string.Empty;
}

/// <summary>
///     Application-level settings
///     Binds to "App" section in appsettings.json
/// </summary>
public class AppSettings
{
    [Required] public string Name { get; set; } = string.Empty;

    [Range(1, int.MaxValue)] public int Version { get; set; }

    public bool IsProduction { get; set; }

    public TimeSpan Timeout { get; set; }

    [Range(0.01, double.MaxValue)] public decimal Price { get; set; }

    public string? OptionalFeature { get; set; }
}

/// <summary>
///     Background services configuration
///     Binds to "BackgroundServices" section in appsettings.json
/// </summary>
public class BackgroundServiceSettings
{
    public EmailProcessorSettings EmailProcessor { get; set; } = new();
    public DataSyncSettings DataSync { get; set; } = new();
}

public class EmailProcessorSettings
{
    public bool Enabled { get; set; }

    [Range(1, 3600)] // 1 second to 1 hour
    public int IntervalSeconds { get; set; }

    [Range(1, 1000)] public int BatchSize { get; set; }

    [Range(0, 10)] public int MaxRetries { get; set; }

    [Required] public string QueueName { get; set; } = string.Empty;
}

public class DataSyncSettings
{
    public bool Enabled { get; set; }

    [Range(1, 1440)] // 1 minute to 24 hours
    public int IntervalMinutes { get; set; }
}

/// <summary>
///     Enhanced logging configuration settings
///     Binds to "Logging:Custom" section in appsettings.json
/// </summary>
public class LoggingSettings
{
    [Required] public string Level { get; set; } = string.Empty;

    public bool IncludeTimestamp { get; set; }
    public bool IncludeScope { get; set; }

    public FileLoggingSettings File { get; set; } = new();
    public ConsoleLoggingSettings Console { get; set; } = new();
}

public class FileLoggingSettings
{
    [Required] public string Path { get; set; } = string.Empty;

    [Range(1, 1000)] public int MaxSizeInMB { get; set; }

    [Range(1, 365)] public int RetainDays { get; set; }

    public bool CompressArchive { get; set; }
}

public class ConsoleLoggingSettings
{
    public bool Enabled { get; set; }

    [Required] public string Format { get; set; } = string.Empty;

    public bool IncludeColors { get; set; }
}

// === API CONFIGURATION CLASSES ===

/// <summary>
///     API configuration settings
///     Binds to "Api" section in appsettings.json
/// </summary>
public class ApiSettings
{
    [Required] [Url] public string BaseUrl { get; set; } = string.Empty;

    [Range(1, 3600)] public int TimeoutSeconds { get; set; }

    public ApiClientSettings Settings { get; set; } = new();
    public ApiClientSettings ClientSettings { get; set; } = new();
    public RetryPolicySettings RetryPolicy { get; set; } = new();
}

public class ApiClientSettings
{
    public string BaseUrl { get; set; } = string.Empty;

    [Range(1, 3600)] public int TimeoutSeconds { get; set; }

    public string ApiKey { get; set; } = string.Empty;
    public bool EnableRetry { get; set; }

    [Range(0, 10)] public int RetryCount { get; set; }

    [Range(0, 10)] public int MaxRetries { get; set; }

    public string UserAgent { get; set; } = string.Empty;
}

public class RetryPolicySettings
{
    [Range(1, 10)] public int MaxAttempts { get; set; }

    [Range(100, 60000)] public int DelayMs { get; set; }

    public bool ExponentialBackoff { get; set; }
}

// === VALIDATION AND MONITORING SETTINGS ===

/// <summary>
///     Validation settings configuration
///     Binds to "ValidationSettings" section in appsettings.json
/// </summary>
public class ValidationSettings
{
    [Range(1, 1000)] public int MaxLength { get; set; }

    [Range(1, 100)] public int MinLength { get; set; }

    public bool RequireNumbers { get; set; }
    public bool RequireSymbols { get; set; }

    [Required] public string AllowedCharacters { get; set; } = string.Empty;

    public List<string> Patterns { get; set; } = new();

    [Required] public string Level { get; set; } = string.Empty;
}

/// <summary>
///     Data cleanup service configuration
///     Binds to "DataCleanupSettings" section in appsettings.json
/// </summary>
public class DataCleanupSettings
{
    public bool Enabled { get; set; }

    [Range(1, 8760)] // 1 hour to 1 year
    public int IntervalHours { get; set; }

    [Range(1, 3650)] // 1 day to 10 years
    public int RetentionDays { get; set; }

    public bool CompressOldData { get; set; }

    public List<string> TableNames { get; set; } = new();
}

/// <summary>
///     Health monitoring configuration
///     Binds to "HealthMonitorSettings" section in appsettings.json
/// </summary>
public class HealthMonitorSettings
{
    public bool Enabled { get; set; }

    [Range(1, 1440)] public int IntervalMinutes { get; set; }

    public List<string> EndpointsToCheck { get; set; } = new();

    [Range(1, 300)] public int TimeoutSeconds { get; set; }

    [EmailAddress] public string NotificationEmail { get; set; } = string.Empty;
}

/// <summary>
///     File watcher service configuration
///     Binds to "FileWatcherSettings" section in appsettings.json
/// </summary>
public class FileWatcherSettings
{
    public bool Enabled { get; set; }

    [Required] public string WatchPath { get; set; } = string.Empty;

    public List<string> FileExtensions { get; set; } = new();

    public bool ProcessSubdirectories { get; set; }

    [Range(100, 60000)] public int ProcessingDelayMs { get; set; }
}

/// <summary>
///     Notification scheduler configuration
///     Binds to "NotificationSchedulerSettings" section in appsettings.json
/// </summary>
public class NotificationSchedulerSettings
{
    public bool Enabled { get; set; }

    [Range(1, 1440)] public int IntervalMinutes { get; set; }

    [Required] public string DefaultNotificationProvider { get; set; } = string.Empty;

    public bool SendDailyDigest { get; set; }

    [Required] public string DigestTime { get; set; } = string.Empty;
}

// === SPECIALIZED CONFIGURATION CLASSES ===

/// <summary>
///     Hot reload settings for configuration changes
///     Binds to "HotReload" section in appsettings.json
/// </summary>
public class HotReloadSettings
{
    [Required] public string Setting { get; set; } = string.Empty;
}

/// <summary>
///     Generic services configuration
///     Binds to "GenericServices" section in appsettings.json
/// </summary>
public class GenericServicesSettings
{
    [Range(1, 10080)] public int CacheExpirationMinutes { get; set; }

    public bool EnableValidation { get; set; }

    [Range(1, 10000)] public int MaxEntitiesPerQuery { get; set; }
}

/// <summary>
///     Base settings for common configuration patterns
///     Binds to "Base:Settings" section in appsettings.json
/// </summary>
public class BaseSettings
{
    [Required] public string Environment { get; set; } = string.Empty;

    public bool Debug { get; set; }
}

/// <summary>
///     Notification settings configuration
///     Binds to "Notification:Settings" section in appsettings.json
/// </summary>
public class NotificationSettings
{
    [Required] public string Provider { get; set; } = string.Empty;

    public bool Enabled { get; set; }
}

/// <summary>
///     Retry configuration for various operations
///     Binds to "Retry" section in appsettings.json
/// </summary>
public class RetrySettings
{
    [Range(1, 20)] public int MaxAttempts { get; set; }
}

// === SECTION-SPECIFIC CONFIGURATION CLASSES ===

/// <summary>
///     Configuration for section-based binding tests
/// </summary>
public class Section1Settings
{
    [Required] public string Value { get; set; } = string.Empty;
}

public class Section2Settings
{
    [Required] public string Value { get; set; } = string.Empty;
}

public class Section3Settings
{
    [Required] public string Value { get; set; } = string.Empty;
}

/// <summary>
///     Email section settings for validation examples
///     Binds to "EmailSection" section in appsettings.json
/// </summary>
public class EmailSectionSettings
{
    [Required] public string SmtpServer { get; set; } = string.Empty;

    [Range(1, 65535)] public int Port { get; set; }

    [Required] public string Username { get; set; } = string.Empty;

    [Required] public string Password { get; set; } = string.Empty;
}

/// <summary>
///     Validation demo settings
///     Binds to "ValidationDemo" section in appsettings.json
/// </summary>
public class ValidationDemoSettings
{
    public string TestValue { get; set; } = string.Empty;
    public string EmptyKey { get; set; } = string.Empty;
    public string WhitespaceKey { get; set; } = string.Empty;
}

/// <summary>
///     Optional configuration settings
///     Binds to "Optional" section in appsettings.json
/// </summary>
public class OptionalSettings
{
    public string Feature { get; set; } = string.Empty;
}

/// <summary>
///     Simple section configuration
///     Binds to "SomeSection" section in appsettings.json
/// </summary>
public class SomeSectionSettings
{
    public string Value { get; set; } = string.Empty;
}
