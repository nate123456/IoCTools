namespace IoCTools.Sample.Interfaces;

using Services;

// ===== 1. CONDITIONAL SERVICES INTERFACES =====

/// <summary>
///     Email provider interface for conditional service examples
/// </summary>
public interface IEmailProvider
{
    string ProviderType { get; }

    Task SendEmailAsync(string to,
        string subject,
        string body);

    Task<bool> IsAvailableAsync();
}

/// <summary>
///     Cache provider interface for conditional service examples
/// </summary>
public interface ICacheProvider
{
    string CacheType { get; }
    Task<T?> GetAsync<T>(string key) where T : class;

    Task SetAsync<T>(string key,
        T value,
        TimeSpan? expiration = null) where T : class;

    Task RemoveAsync(string key);
    Task ClearAsync();
}

/// <summary>
///     Optional service interface for feature flag demonstrations
/// </summary>
public interface IOptionalService
{
    bool IsEnabled { get; }
    string FeatureName { get; }
    Task<string> ExecuteFeatureAsync(string input);
}

// ===== 2. DEPENDSON EXAMPLES INTERFACES =====

/// <summary>
///     Payment service interface for DependsOn examples
/// </summary>
public interface IPaymentService
{
    Task<PaymentResult> ProcessPaymentAsync(Payment payment);
    Task<bool> ValidatePaymentMethodAsync(string method);
    Task<decimal> CalculateFeesAsync(decimal amount);
}

/// <summary>
///     Inventory service interface for stock management
/// </summary>
public interface IInventoryService
{
    Task<bool> CheckStockAsync(int productId,
        int quantity);

    Task ReserveAsync(int productId,
        int quantity);

    Task ReleaseReservationAsync(int productId,
        int quantity);

    Task<int> GetAvailableStockAsync(int productId);
}

/// <summary>
///     Report generator interface for business reports
/// </summary>
public interface IReportGenerator
{
    Task<string> GenerateOrderReportAsync(int orderId);
    Task<string> GenerateInventoryReportAsync();

    Task<string> GenerateCustomReportAsync(string reportType,
        Dictionary<string, object> parameters);

    Task<byte[]> ExportReportToPdfAsync(string reportContent);
}

// ===== 3. BACKGROUND SERVICES INTERFACES =====

/// <summary>
///     Email queue processor interface for background processing
/// </summary>
public interface IEmailQueueProcessor
{
    bool IsProcessing { get; }
    Task ProcessEmailQueueAsync(CancellationToken cancellationToken);
    Task<int> GetQueueSizeAsync();
    Task EnqueueEmailAsync(EmailMessage email);
}

/// <summary>
///     Data cleanup service interface for maintenance tasks
/// </summary>
public interface IDataCleanupService
{
    Task CleanupOldDataAsync(CancellationToken cancellationToken);
    Task<long> GetDataSizeAsync(string category);
    Task ArchiveDataAsync(DateTime olderThan);
    Task<CleanupReport> GetLastCleanupReportAsync();
}

/// <summary>
///     Health monitor interface for system monitoring
/// </summary>
public interface IHealthMonitor
{
    Task<HealthStatus> CheckSystemHealthAsync();
    Task<bool> IsServiceHealthyAsync(string serviceName);

    Task RecordHealthMetricAsync(string metric,
        double value);

    Task<HealthReport> GetHealthReportAsync(TimeSpan period);
}

// ===== 4. MULTI-INTERFACE REGISTRATION INTERFACES =====

/// <summary>
///     User service interface for user management
/// </summary>
public interface IUserService
{
    Task<User> GetUserAsync(int userId);

    Task<User> CreateUserAsync(string name,
        string email);

    Task<bool> UpdateUserAsync(User user);
    Task<bool> DeleteUserAsync(int userId);
}

/// <summary>
///     User repository interface for data access
/// </summary>
public interface IUserRepository
{
    Task<User?> FindByIdAsync(int id);
    Task<User> SaveAsync(User user);
    Task<IEnumerable<User>> GetAllAsync();
    Task<IEnumerable<User>> FindByEmailAsync(string email);
    Task<bool> ExistsAsync(int id);
}

/// <summary>
///     User validator interface for validation logic
/// </summary>
public interface IUserValidator
{
    bool IsValidEmail(string email);
    bool IsValidName(string name);
    ValidationResult ValidateUser(User user);

    Task<bool> IsEmailUniqueAsync(string email,
        int? excludeUserId = null);
}

// ===== 5. GENERIC SERVICE INTERFACES =====

/// <summary>
///     Generic repository interface with comprehensive CRUD operations
/// </summary>
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<IEnumerable<T>> FindAsync(Func<T, bool> predicate);
    Task AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(int id);
    Task<int> CountAsync();
}

/// <summary>
///     Generic validator interface for entity validation
/// </summary>
public interface IValidator<T> where T : class
{
    Task<ValidationResult> ValidateAsync(T entity);
    Task<bool> IsValidAsync(T entity);
    IEnumerable<string> GetValidationRules();
}

/// <summary>
///     Generic processor interface for input/output transformations
/// </summary>
public interface IProcessor<TInput, TOutput>
{
    Task<TOutput> ProcessAsync(TInput input);

    Task<TOutput> ProcessAsync(TInput input,
        ProcessingOptions options);

    Task<IEnumerable<TOutput>> ProcessBatchAsync(IEnumerable<TInput> inputs);
    bool CanProcess(TInput input);
}

// ===== 6. INHERITANCE CHAIN INTERFACES =====

/// <summary>
///     Base entity interface for common properties
/// </summary>
public interface IBaseEntity
{
    int Id { get; set; }
    DateTime CreatedAt { get; set; }
    DateTime? UpdatedAt { get; set; }
    bool IsDeleted { get; set; }
}

/// <summary>
///     Generic base repository interface
/// </summary>
public interface IBaseRepository<T> where T : class, IBaseEntity
{
    Task<T?> GetByIdAsync(int id);
    Task<T?> GetActiveByIdAsync(int id);
    Task<IEnumerable<T>> GetAllActiveAsync();
    Task<T> CreateAsync(T entity);
    Task<T> UpdateAsync(T entity);
    Task SoftDeleteAsync(int id);
    Task HardDeleteAsync(int id);
}

/// <summary>
///     Business service interface for domain logic
/// </summary>
public interface IBusinessService
{
    Task<BusinessResult> ExecuteBusinessLogicAsync(BusinessRequest request);
    Task<bool> ValidateBusinessRulesAsync(BusinessRequest request);

    Task AuditBusinessActionAsync(string action,
        object data);
}

// ===== 7. TRANSIENT SERVICE INTERFACES =====

/// <summary>
///     Email validator interface for email validation
/// </summary>
public interface IEmailValidator
{
    bool IsValidFormat(string email);
    Task<bool> IsDeliverableAsync(string email);
    Task<EmailValidationResult> ValidateComprehensiveAsync(string email);
    IEnumerable<string> GetCommonEmailProviders();
}

/// <summary>
///     Data transformer interface for data manipulation
/// </summary>
public interface IDataTransformer
{
    T Transform<T>(object source) where T : class, new();
    Task<T> TransformAsync<T>(object source) where T : class, new();
    IEnumerable<T> TransformCollection<T>(IEnumerable<object> sources) where T : class, new();

    bool CanTransform(Type sourceType,
        Type targetType);
}

/// <summary>
///     Request processor interface for HTTP request processing
/// </summary>
public interface IRequestProcessor
{
    string ProcessorName { get; }
    Task<RequestResult> ProcessRequestAsync(ProcessingRequest request);
    Task<bool> ValidateRequestAsync(ProcessingRequest request);

    Task<RequestResult> ProcessWithRetryAsync(ProcessingRequest request,
        int maxRetries = 3);
}

// ===== 8. SPECIALIZED INTERFACES FOR COMPLEX SCENARIOS =====

/// <summary>
///     File processor interface for file operations
/// </summary>
public interface IFileProcessor
{
    Task<FileProcessingResult> ProcessFileAsync(Stream fileStream,
        string fileName);

    Task<bool> IsValidFileTypeAsync(string fileName);
    Task<IEnumerable<string>> GetSupportedExtensionsAsync();
    Task<long> GetMaxFileSizeAsync();
}

/// <summary>
///     Configuration manager interface for settings management
/// </summary>
public interface IConfigurationManager
{
    T GetValue<T>(string key,
        T defaultValue = default!);

    Task<T> GetValueAsync<T>(string key,
        T defaultValue = default!);

    Task SetValueAsync<T>(string key,
        T value);

    Task<bool> ExistsAsync(string key);
    Task<Dictionary<string, object>> GetSectionAsync(string sectionName);
}

/// <summary>
///     Notification dispatcher interface for notification management
/// </summary>
public interface INotificationDispatcher
{
    Task SendNotificationAsync(NotificationMessage message);
    Task SendBulkNotificationAsync(IEnumerable<NotificationMessage> messages);
    Task<NotificationStatus> GetNotificationStatusAsync(string notificationId);
    Task<IEnumerable<string>> GetAvailableChannelsAsync();
}

// ===== 9. AUDIT AND LOGGING INTERFACES =====

/// <summary>
///     Audit service interface for tracking changes
/// </summary>
public interface IAuditService
{
    Task LogActionAsync(string action,
        string details);

    Task LogActionAsync(string action,
        object data,
        string? userId = null);

    Task<IEnumerable<AuditEntry>> GetAuditTrailAsync(string entityType,
        int entityId);

    Task<IEnumerable<AuditEntry>> GetUserActionsAsync(string userId,
        DateTime? from = null,
        DateTime? to = null);
}

/// <summary>
///     Security service interface for security operations
/// </summary>
public interface ISecurityService
{
    Task<bool> ValidatePermissionsAsync(int userId,
        string action);

    Task LogSecurityEventAsync(string eventType,
        string details);

    Task<SecurityContext> GetUserSecurityContextAsync(int userId);

    Task<bool> IsActionAllowedAsync(int userId,
        string resource,
        string action);
}

// ===== DATA MODELS AND SUPPORTING TYPES =====

public class User : IEntity
{
    public User()
    {
    }

    public User(int id,
        string name,
        string email)
    {
        Id = id;
        Name = name;
        Email = email;
    }

    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public record Order(int Id, string CustomerEmail, Payment Payment)
{
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public OrderStatus Status { get; init; } = OrderStatus.Pending;
}

public record Payment(decimal Amount)
{
    public string Method { get; init; } = "CreditCard";
    public string Currency { get; init; } = "USD";
}

public record PaymentResult(bool Success, string Message)
{
    public string TransactionId { get; init; } = Guid.NewGuid().ToString();
    public DateTime ProcessedAt { get; init; } = DateTime.UtcNow;
}

public record EmailMessage(string To, string Subject, string Body)
{
    public string From { get; init; } = "noreply@example.com";
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public int Priority { get; init; } = 0;
}

public record NotificationMessage(string Recipient, string Content, string Channel)
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public Dictionary<string, object> Metadata { get; init; } = new();
}

public record BusinessRequest(string Operation, Dictionary<string, object> Parameters)
{
    public string UserId { get; init; } = string.Empty;
    public DateTime RequestedAt { get; init; } = DateTime.UtcNow;
}

public record BusinessResult(bool Success, string Message, object? Data = null)
{
    public TimeSpan ProcessingTime { get; init; }
}

public record ProcessingRequest(string Type, object Data)
{
    public Dictionary<string, string> Headers { get; init; } = new();
    public string RequestId { get; init; } = Guid.NewGuid().ToString();
}

public record RequestResult(bool Success, string Message, object? Data = null)
{
    public string RequestId { get; init; } = string.Empty;
    public DateTime ProcessedAt { get; init; } = DateTime.UtcNow;
}

public record ProcessingOptions(int Timeout = 30000, int MaxRetries = 3)
{
    public Dictionary<string, object> Settings { get; init; } = new();
}

public record ValidationResult(bool IsValid, IEnumerable<string> Errors)
{
    public static ValidationResult Success => new(true, Enumerable.Empty<string>());
    public static ValidationResult Failure(params string[] errors) => new(false, errors);
}

public record EmailValidationResult(bool IsValid, string Reason, bool IsDeliverable = false)
{
    public string NormalizedEmail { get; init; } = string.Empty;
}

public record FileProcessingResult(bool Success, string Message, long ProcessedBytes = 0)
{
    public string ProcessedFileName { get; init; } = string.Empty;
    public Dictionary<string, object> Metadata { get; init; } = new();
}

public record HealthStatus(bool IsHealthy, string StatusMessage)
{
    public Dictionary<string, object> Details { get; init; } = new();
    public DateTime CheckedAt { get; init; } = DateTime.UtcNow;
}

public record HealthReport(TimeSpan Period, IEnumerable<HealthMetric> Metrics)
{
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
}

public record HealthMetric(string Name, double Value, string Unit)
{
    public DateTime RecordedAt { get; init; } = DateTime.UtcNow;
}

public record CleanupReport(DateTime CleanupDate, long DataCleaned, TimeSpan Duration)
{
    public IEnumerable<string> CategoriesCleaned { get; init; } = Enumerable.Empty<string>();
}

public record AuditEntry(string Action, string Details, DateTime Timestamp)
{
    public string UserId { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public int EntityId { get; init; }
}

public record SecurityContext(int UserId, IEnumerable<string> Roles, IEnumerable<string> Permissions)
{
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

public enum OrderStatus
{
    Pending,
    Processing,
    Completed,
    Cancelled,
    Failed
}

public enum NotificationStatus
{
    Pending,
    Sent,
    Delivered,
    Failed,
    Cancelled
}

// ===== SPECIALIZED CONSTRAINT INTERFACES =====

/// <summary>
///     Constraint interface for auditable entities
/// </summary>
public interface IAuditable
{
    string CreatedBy { get; set; }
    DateTime CreatedAt { get; set; }
    string ModifiedBy { get; set; }
    DateTime? ModifiedAt { get; set; }
}

/// <summary>
///     Constraint interface for soft-deletable entities
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
    string DeletedBy { get; set; }
}

/// <summary>
///     Constraint interface for versioned entities
/// </summary>
public interface IVersioned
{
    int Version { get; set; }
    byte[] RowVersion { get; set; }
}

/// <summary>
///     Constraint interface for tenant-aware entities
/// </summary>
public interface ITenantAware
{
    string TenantId { get; set; }
}
