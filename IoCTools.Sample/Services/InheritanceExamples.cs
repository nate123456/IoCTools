namespace IoCTools.Sample.Services;

using Abstractions.Annotations;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Comprehensive inheritance chain examples demonstrating sophisticated inheritance support
/// These examples showcase:
/// - Multi-level inheritance with proper constructor generation
/// - Mixed [Inject] and [DependsOn] across hierarchy
/// - Generic inheritance patterns
/// - Complex dependency chains
/// - Lifetime validation across inheritance (IOC015)
/// </summary>

#region 1. Repository Pattern - 3-Level Inheritance Chain

/// <summary>
///     Base entity providing common properties for all domain entities
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
///     User entity extending base entity
/// </summary>
public class InheritanceUser : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}

/// <summary>
///     Generic base repository providing common database operations
///     because this is an abstract base class
/// </summary>
public abstract partial class BaseRepository<T> where T : BaseEntity
{
    [Inject] protected readonly IConfiguration Configuration;
    [Inject] protected readonly ILogger<BaseRepository<T>> Logger;

    protected virtual async Task<T?> GetByIdAsync(int id)
    {
        Logger.LogDebug("Getting entity of type {EntityType} with ID {Id}", typeof(T).Name, id);
        // Simulate database call
        await Task.Delay(10);
        return default;
    }

    protected virtual async Task<IEnumerable<T>> GetAllAsync()
    {
        Logger.LogDebug("Getting all entities of type {EntityType}", typeof(T).Name);
        await Task.Delay(20);
        return new List<T>();
    }

    protected virtual async Task<bool> SaveAsync(T entity)
    {
        Logger.LogDebug("Saving entity of type {EntityType}", typeof(T).Name);
        entity.UpdatedAt = DateTime.UtcNow;
        await Task.Delay(15);
        return true;
    }
}

/// <summary>
///     User repository implementing specific business logic for inheritance examples
///     Inherits from BaseRepository<InheritanceUser> and adds user-specific operations
/// </summary>
public interface IInheritanceUserRepository
{
    Task<InheritanceUser?> GetUserByIdAsync(int id);
    Task<InheritanceUser?> GetUserByUsernameAsync(string username);
    Task<IEnumerable<InheritanceUser>> GetAllUsersAsync();
    Task<bool> CreateUserAsync(InheritanceUser user);
    Task<bool> UpdateUserAsync(InheritanceUser user);
    Task<bool> ValidateUsernameAvailabilityAsync(string username);
}

[Scoped]
public partial class UserRepository : BaseRepository<InheritanceUser>, IInheritanceUserRepository
{
    // Demonstrates multiple dependency types in inheritance
    [Inject] private readonly ICacheService _cacheService;

    // Additional specific dependencies for UserRepository
    [Inject] private readonly ILogger<UserRepository> _specificLogger;

    public async Task<InheritanceUser?> GetUserByIdAsync(int id)
    {
        _specificLogger.LogInformation("Getting user by ID: {UserId}", id);

        // Use cache from DependsOn injection
        var cacheKey = $"user:{id}";
        return _cacheService.GetOrSet(cacheKey, () => GetByIdAsync(id).Result);
    }

    public async Task<InheritanceUser?> GetUserByUsernameAsync(string username)
    {
        _specificLogger.LogInformation("Getting user by username: {Username}", username);
        await Task.Delay(10);

        // Simulate database query
        return new InheritanceUser
        {
            Id = 1,
            Username = username,
            Email = $"{username}@example.com",
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "system"
        };
    }

    public async Task<IEnumerable<InheritanceUser>> GetAllUsersAsync()
    {
        _specificLogger.LogInformation("Getting all users");
        return await GetAllAsync(); // Uses base implementation
    }

    public async Task<bool> CreateUserAsync(InheritanceUser user)
    {
        _specificLogger.LogInformation("Creating new user: {Username}", user.Username);
        user.CreatedAt = DateTime.UtcNow;
        user.CreatedBy = "system";
        return await SaveAsync(user); // Uses base implementation
    }

    public async Task<bool> UpdateUserAsync(InheritanceUser user)
    {
        _specificLogger.LogInformation("Updating user: {Username}", user.Username);
        user.UpdatedBy = "system";
        return await SaveAsync(user); // Uses base implementation
    }

    public async Task<bool> ValidateUsernameAvailabilityAsync(string username)
    {
        _specificLogger.LogDebug("Validating username availability: {Username}", username);
        await Task.Delay(5);
        return !string.IsNullOrEmpty(username) && username.Length > 3;
    }
}

#endregion

#region 2. Service Layer Pattern - Generic Business Services

/// <summary>
///     Generic base service providing common operations
/// </summary>
public abstract partial class BaseService<T> where T : class
{
    [Inject] protected readonly ILogger<BaseService<T>> Logger;
    [Inject] protected readonly IServiceProvider ServiceProvider;

    protected virtual async Task<bool> ValidateAsync(T entity)
    {
        Logger.LogDebug("Validating entity of type {EntityType}", typeof(T).Name);
        await Task.Delay(5);
        return entity != null;
    }

    protected virtual async Task LogOperationAsync(string operation,
        string details = "")
    {
        Logger.LogInformation("Operation {Operation} on {EntityType}: {Details}",
            operation, typeof(T).Name, details);
        await Task.Delay(2);
    }
}

/// <summary>
///     Business service layer extending base service
/// </summary>
public abstract partial class BusinessService : BaseService<InheritanceUser>
{
    [Inject] protected readonly IConfiguration Configuration;

    // Mixed dependency injection patterns - using Inject instead of DependsOn for fields
    [Inject] protected readonly IInheritanceUserRepository UserRepository;

    protected virtual async Task<bool> ValidateBusinessRulesAsync(InheritanceUser user)
    {
        Logger.LogDebug("Validating business rules for user: {Username}", user.Username);

        // Business validation logic
        if (string.IsNullOrEmpty(user.Email) || !user.Email.Contains("@"))
        {
            Logger.LogWarning("Invalid email format for user: {Username}", user.Username);
            return false;
        }

        await Task.Delay(10);
        return await ValidateAsync(user); // Call base validation
    }

    protected virtual async Task NotifyUserCreatedAsync(InheritanceUser user) =>
        await LogOperationAsync("UserCreated", $"User {user.Username} was created");
}

/// <summary>
///     Order processing service - concrete implementation with complex inheritance
/// </summary>
public interface IInheritanceOrderProcessingService
{
    Task<bool> ProcessUserOrderAsync(int userId,
        decimal orderAmount);

    Task<bool> ValidateUserForOrderAsync(int userId);

    Task NotifyOrderProcessedAsync(int userId,
        decimal amount);
}

[Scoped]
public partial class InheritanceOrderProcessingService : BusinessService, IInheritanceOrderProcessingService
{
    // Multiple injection patterns using Inject for fields
    [Inject] private readonly IEmailService _emailService;

    // Service-specific dependencies
    [Inject] private readonly ILogger<InheritanceOrderProcessingService> _orderLogger;

    [Inject] private readonly IPaymentService _paymentService;

    public async Task<bool> ProcessUserOrderAsync(int userId,
        decimal orderAmount)
    {
        _orderLogger.LogInformation("Processing order for user {UserId}, amount: {Amount}", userId, orderAmount);

        // Use inherited UserRepository via BusinessService
        var user = await UserRepository.GetUserByIdAsync(userId);
        if (user == null)
        {
            _orderLogger.LogWarning("User not found: {UserId}", userId);
            return false;
        }

        // Validate using inherited business rules
        if (!await ValidateBusinessRulesAsync(user))
        {
            _orderLogger.LogWarning("Business rule validation failed for user: {UserId}", userId);
            return false;
        }

        // Process payment using DependsOn injection
        var payment = new Payment(orderAmount);
        var paymentResult = await _paymentService.ProcessPaymentAsync(payment);

        if (paymentResult.Success)
        {
            await NotifyUserCreatedAsync(user); // Use inherited notification
            await NotifyOrderProcessedAsync(userId, orderAmount);
            return true;
        }

        _orderLogger.LogError("Payment failed for user {UserId}", userId);
        return false;
    }

    public async Task<bool> ValidateUserForOrderAsync(int userId)
    {
        var user = await UserRepository.GetUserByIdAsync(userId);
        return user != null && await ValidateBusinessRulesAsync(user);
    }

    public async Task NotifyOrderProcessedAsync(int userId,
        decimal amount)
    {
        _orderLogger.LogInformation("Sending order confirmation for user {UserId}", userId);
        // Use inherited email service
        await _emailService.SendConfirmationAsync($"user{userId}@example.com");
    }
}

#endregion

#region 3. Processor Chain Pattern - Multi-Level Processing

/// <summary>
///     Base processor providing common processing infrastructure
/// </summary>
public abstract partial class BaseProcessor
{
    [Inject] protected readonly ILogger<BaseProcessor> Logger;
    [Inject] protected readonly IServiceProvider ServiceProvider;

    protected virtual async Task<InheritanceExampleProcessingResult> PreProcessAsync(string input)
    {
        Logger.LogDebug("Pre-processing input: {Input}", input);
        await Task.Delay(5);

        if (string.IsNullOrEmpty(input))
            return InheritanceExampleProcessingResult.CreateFailed("Input cannot be empty");

        return InheritanceExampleProcessingResult.CreateSuccess("Pre-processing completed");
    }

    protected virtual async Task<InheritanceExampleProcessingResult> PostProcessAsync(string result)
    {
        Logger.LogDebug("Post-processing result: {Result}", result);
        await Task.Delay(3);
        return InheritanceExampleProcessingResult.CreateSuccess($"Processed: {result}");
    }
}

/// <summary>
///     Payment processor extending base processor
/// </summary>
public abstract partial class PaymentProcessor : BaseProcessor
{
    // Configuration injection in inheritance chain using Inject for fields
    [Inject] protected readonly ICacheService CacheService;
    [Inject] protected readonly IConfiguration Configuration;

    protected virtual async Task<decimal> CalculateFeesAsync(decimal amount)
    {
        Logger.LogDebug("Calculating fees for amount: {Amount}", amount);

        // Use cache from DependsOn
        var feeRate = CacheService.GetOrSet("fee-rate", () => 0.03m);
        await Task.Delay(8);

        return amount * feeRate;
    }

    protected virtual async Task<bool> ValidatePaymentAmountAsync(decimal amount)
    {
        Logger.LogDebug("Validating payment amount: {Amount}", amount);

        var maxAmount = Configuration.GetValue("PaymentSettings:MaxAmount", 10000m);
        await Task.Delay(5);

        return amount > 0 && amount <= maxAmount;
    }
}

/// <summary>
///     Credit card processor - final concrete implementation
/// </summary>
public interface ICreditCardProcessor
{
    Task<InheritanceExampleProcessingResult> ProcessCreditCardPaymentAsync(decimal amount,
        string cardNumber);

    Task<bool> ValidateCreditCardAsync(string cardNumber);

    Task<InheritanceExampleProcessingResult> RefundPaymentAsync(decimal amount,
        string transactionId);
}

[Scoped]
public partial class CreditCardProcessor : PaymentProcessor, ICreditCardProcessor
{
    [Inject] private readonly ILogger<CreditCardProcessor> _creditCardLogger;

    // Complex dependency injection at final level using Inject for fields
    [Inject] private readonly IEmailService _emailService;

    public async Task<InheritanceExampleProcessingResult> ProcessCreditCardPaymentAsync(decimal amount,
        string cardNumber)
    {
        _creditCardLogger.LogInformation("Processing credit card payment: {Amount}", amount);

        // Use inherited validation from PaymentProcessor
        if (!await ValidatePaymentAmountAsync(amount))
        {
            _creditCardLogger.LogWarning("Invalid payment amount: {Amount}", amount);
            return InheritanceExampleProcessingResult.CreateFailed("Invalid payment amount");
        }

        // Use inherited pre-processing from BaseProcessor
        var preProcessResult = await PreProcessAsync(cardNumber);
        if (!preProcessResult.Success) return preProcessResult;

        // Validate credit card
        if (!await ValidateCreditCardAsync(cardNumber))
        {
            _creditCardLogger.LogWarning("Invalid credit card number");
            return InheritanceExampleProcessingResult.CreateFailed("Invalid credit card");
        }

        // Calculate fees using inherited method
        var fees = await CalculateFeesAsync(amount);
        var totalAmount = amount + fees;

        _creditCardLogger.LogInformation("Processing payment: {Amount} + {Fees} = {Total}",
            amount, fees, totalAmount);

        // Simulate payment processing
        await Task.Delay(50);

        // Use inherited post-processing
        var postProcessResult = await PostProcessAsync($"Payment of {totalAmount:C} processed");

        // Send confirmation via DependsOn injection
        await _emailService.SendConfirmationAsync("customer@example.com");

        return InheritanceExampleProcessingResult.CreateSuccess($"Credit card payment processed: {totalAmount:C}");
    }

    public async Task<bool> ValidateCreditCardAsync(string cardNumber)
    {
        _creditCardLogger.LogDebug("Validating credit card: {CardNumber}",
            cardNumber.Substring(0, Math.Min(4, cardNumber.Length)) + "****");

        await Task.Delay(15);

        // Simple validation - real implementation would use Luhn algorithm
        return !string.IsNullOrEmpty(cardNumber) && cardNumber.Length >= 13 && cardNumber.Length <= 19;
    }

    public async Task<InheritanceExampleProcessingResult> RefundPaymentAsync(decimal amount,
        string transactionId)
    {
        _creditCardLogger.LogInformation("Processing refund: {Amount} for transaction {TransactionId}",
            amount, transactionId);

        // Use inherited fee calculation for refund processing fees
        var processingFee = await CalculateFeesAsync(amount);
        var refundAmount = amount - processingFee;

        await Task.Delay(30);

        return InheritanceExampleProcessingResult.CreateSuccess($"Refund processed: {refundAmount:C}");
    }
}

#endregion

#region 4. Validation Chain Pattern - Generic Validators

/// <summary>
///     Base validator providing common validation infrastructure
/// </summary>
public abstract partial class BaseValidator<T> where T : class
{
    [Inject] protected readonly ILogger<BaseValidator<T>> Logger;

    protected virtual async Task<InheritanceExampleValidationResult> ValidateRequiredFieldsAsync(T entity)
    {
        Logger.LogDebug("Validating required fields for {EntityType}", typeof(T).Name);
        await Task.Delay(5);

        if (entity == null) return InheritanceExampleValidationResult.CreateFailed("Entity cannot be null");

        return InheritanceExampleValidationResult.CreateSuccess();
    }

    protected virtual async Task<InheritanceExampleValidationResult> ValidateBusinessRulesAsync(T entity)
    {
        Logger.LogDebug("Validating business rules for {EntityType}", typeof(T).Name);
        await Task.Delay(8);
        return InheritanceExampleValidationResult.CreateSuccess();
    }
}

/// <summary>
///     Entity validator extending base validator with entity-specific logic
/// </summary>
public abstract partial class EntityValidator<T> : BaseValidator<T> where T : BaseEntity
{
    [Inject] protected readonly IConfiguration Configuration;

    protected virtual async Task<InheritanceExampleValidationResult> ValidateEntityPropertiesAsync(T entity)
    {
        Logger.LogDebug("Validating entity properties for {EntityType}", typeof(T).Name);

        var requiredFieldsResult = await ValidateRequiredFieldsAsync(entity);
        if (!requiredFieldsResult.Success) return requiredFieldsResult;

        // Validate BaseEntity properties
        if (entity.Id < 0) return InheritanceExampleValidationResult.CreateFailed("Entity ID must be non-negative");

        if (entity.CreatedAt > DateTime.UtcNow.AddDays(1))
            return InheritanceExampleValidationResult.CreateFailed("Created date cannot be in the future");

        await Task.Delay(10);
        return InheritanceExampleValidationResult.CreateSuccess();
    }
}

/// <summary>
///     User validator - final concrete validator implementation for inheritance examples
/// </summary>
public interface IInheritanceUserValidator
{
    Task<InheritanceExampleValidationResult> ValidateUserAsync(InheritanceUser user);
    Task<InheritanceExampleValidationResult> ValidateUserCreationAsync(InheritanceUser user);
    Task<InheritanceExampleValidationResult> ValidateUserUpdateAsync(InheritanceUser user);
}

[Scoped]
public partial class InheritanceUserValidator : EntityValidator<InheritanceUser>, IInheritanceUserValidator
{
    [Inject] private readonly ILogger<InheritanceUserValidator> _userLogger;

    // Complex dependency chain in validation using Inject for fields
    [Inject] private readonly IInheritanceUserRepository _userRepository;

    public async Task<InheritanceExampleValidationResult> ValidateUserAsync(InheritanceUser user)
    {
        _userLogger.LogInformation("Validating user: {Username}", user?.Username);

        // Use inherited entity validation
        var entityResult = await ValidateEntityPropertiesAsync(user);
        if (!entityResult.Success) return entityResult;

        // Use inherited business rules validation
        var businessResult = await ValidateBusinessRulesAsync(user);
        if (!businessResult.Success) return businessResult;

        // User-specific validation
        if (string.IsNullOrEmpty(user.Username) || user.Username.Length < 3)
            return InheritanceExampleValidationResult.CreateFailed("Username must be at least 3 characters long");

        if (string.IsNullOrEmpty(user.Email) || !user.Email.Contains("@"))
            return InheritanceExampleValidationResult.CreateFailed("Valid email address is required");

        _userLogger.LogDebug("User validation completed successfully");
        return InheritanceExampleValidationResult.CreateSuccess();
    }

    public async Task<InheritanceExampleValidationResult> ValidateUserCreationAsync(InheritanceUser user)
    {
        _userLogger.LogInformation("Validating user creation: {Username}", user?.Username);

        var basicValidation = await ValidateUserAsync(user);
        if (!basicValidation.Success) return basicValidation;

        // Check username availability using Inject injection
        var isAvailable = await _userRepository.ValidateUsernameAvailabilityAsync(user.Username);
        if (!isAvailable)
            return InheritanceExampleValidationResult.CreateFailed($"Username '{user.Username}' is not available");

        return InheritanceExampleValidationResult.CreateSuccess();
    }

    public async Task<InheritanceExampleValidationResult> ValidateUserUpdateAsync(InheritanceUser user)
    {
        _userLogger.LogInformation("Validating user update: {Username}", user?.Username);

        var basicValidation = await ValidateUserAsync(user);
        if (!basicValidation.Success) return basicValidation;

        // Additional update-specific validation
        if (user.Id <= 0)
            return InheritanceExampleValidationResult.CreateFailed("Valid user ID is required for updates");

        return InheritanceExampleValidationResult.CreateSuccess();
    }
}

#endregion

#region 5. Configuration Injection Inheritance Examples

/// <summary>
///     Base configuration service demonstrating configuration injection in inheritance
/// </summary>
public abstract partial class BaseConfigurationService
{
    [Inject] protected readonly IConfiguration Configuration;
    [Inject] protected readonly ILogger<BaseConfigurationService> Logger;

    protected virtual string GetConnectionString(string name)
    {
        var connectionString = Configuration.GetConnectionString(name);
        Logger.LogDebug("Retrieved connection string for: {Name}", name);
        return connectionString ?? string.Empty;
    }

    protected virtual T GetConfigurationValue<T>(string key,
        T defaultValue)
    {
        var value = Configuration.GetValue(key, defaultValue);
        Logger.LogDebug("Retrieved configuration value for key {Key}: {Value}", key, value);
        return value;
    }
}

/// <summary>
///     Database configuration service extending base configuration
/// </summary>
public abstract partial class DatabaseConfigurationService : BaseConfigurationService
{
    [Inject] protected readonly ICacheService CacheService;

    protected virtual async Task<InheritanceDatabaseSettings> GetDatabaseSettingsAsync()
    {
        Logger.LogDebug("Getting database settings");

        // Use cache from DependsOn and inherited configuration
        return CacheService.GetOrSet("database-settings",
            () => new InheritanceDatabaseSettings
            {
                ConnectionString = GetConnectionString("DefaultConnection"),
                CommandTimeout = GetConfigurationValue("Database:CommandTimeout", 30),
                MaxRetries = GetConfigurationValue("Database:MaxRetries", 3),
                EnableLogging = GetConfigurationValue("Database:EnableLogging", true)
            });
    }
}

/// <summary>
///     Application settings service - final implementation with complex configuration inheritance
/// </summary>
public interface IApplicationSettingsService
{
    Task<InheritanceApplicationSettings> GetApplicationSettingsAsync();
    Task<InheritanceDatabaseSettings> GetDatabaseSettingsAsync();
    Task<bool> ValidateConfigurationAsync();
}

[Singleton]
public partial class ApplicationSettingsService : DatabaseConfigurationService, IApplicationSettingsService
{
    [Inject] private readonly ILogger<ApplicationSettingsService> _appLogger;

    public async Task<InheritanceApplicationSettings> GetApplicationSettingsAsync()
    {
        _appLogger.LogInformation("Loading application settings");

        // Use inherited configuration methods
        var settings = new InheritanceApplicationSettings
        {
            ApplicationName = GetConfigurationValue("Application:Name", "IoCTools Sample"),
            Version = GetConfigurationValue("Application:Version", "1.0.0"),
            Environment = GetConfigurationValue("ASPNETCORE_ENVIRONMENT", "Development"),
            Database = await GetDatabaseSettingsAsync(), // Use inherited method
            MaxConcurrentUsers = GetConfigurationValue("Application:MaxConcurrentUsers", 100),
            EnableFeatureFlags = GetConfigurationValue("Application:EnableFeatureFlags", false)
        };

        _appLogger.LogInformation("Application settings loaded: {AppName} v{Version}",
            settings.ApplicationName, settings.Version);

        return settings;
    }

    async Task<InheritanceDatabaseSettings> IApplicationSettingsService.GetDatabaseSettingsAsync() =>
        await GetDatabaseSettingsAsync(); // Expose inherited protected method

    public async Task<bool> ValidateConfigurationAsync()
    {
        _appLogger.LogInformation("Validating application configuration");

        try
        {
            var appSettings = await GetApplicationSettingsAsync();
            var dbSettings = await GetDatabaseSettingsAsync();

            // Validate critical settings
            if (string.IsNullOrEmpty(appSettings.ApplicationName))
            {
                _appLogger.LogError("Application name is not configured");
                return false;
            }

            if (string.IsNullOrEmpty(dbSettings.ConnectionString))
            {
                _appLogger.LogError("Database connection string is not configured");
                return false;
            }

            _appLogger.LogInformation("Configuration validation completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            _appLogger.LogError(ex, "Configuration validation failed");
            return false;
        }
    }
}

#endregion

#region Data Models and Results

/// <summary>
///     Processing result model for inheritance examples
/// </summary>
public class InheritanceExampleProcessingResult
{
    private InheritanceExampleProcessingResult(bool success,
        string message,
        Exception? exception = null)
    {
        Success = success;
        Message = message;
        Exception = exception;
    }

    public bool Success { get; }
    public string Message { get; private set; }
    public Exception? Exception { get; private set; }

    public static InheritanceExampleProcessingResult CreateSuccess(string message = "Operation completed successfully")
        => new(true, message);

    public static InheritanceExampleProcessingResult CreateFailed(string message,
        Exception? exception = null)
        => new(false, message, exception);
}

/// <summary>
///     Validation result model for inheritance examples
/// </summary>
public class InheritanceExampleValidationResult
{
    private InheritanceExampleValidationResult(bool success,
        string message,
        List<string>? errors = null)
    {
        Success = success;
        Message = message;
        Errors = errors ?? new List<string>();
    }

    public bool Success { get; }
    public string Message { get; private set; }
    public List<string> Errors { get; private set; }

    public static InheritanceExampleValidationResult CreateSuccess(string message = "Validation completed successfully")
        => new(true, message);

    public static InheritanceExampleValidationResult CreateFailed(string message)
        => new(false, message, new List<string> { message });

    public static InheritanceExampleValidationResult CreateFailed(List<string> errors)
        => new(false, "Validation failed", errors);
}

/// <summary>
///     Database settings configuration model for inheritance examples
/// </summary>
public class InheritanceDatabaseSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public int CommandTimeout { get; set; } = 30;
    public int MaxRetries { get; set; } = 3;
    public bool EnableLogging { get; set; } = true;
}

/// <summary>
///     Application settings configuration model for inheritance examples
/// </summary>
public class InheritanceApplicationSettings
{
    public string ApplicationName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public InheritanceDatabaseSettings Database { get; set; } = new();
    public int MaxConcurrentUsers { get; set; } = 100;
    public bool EnableFeatureFlags { get; set; }
}

#endregion
