namespace IoCTools.Sample.Services;

using System.Text;
using System.Text.Json;

using Abstractions.Annotations;

using Interfaces;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Comprehensive examples demonstrating external service integration patterns.
/// Shows the difference between services that need complex manual configuration 
/// vs those that can be automatically registered by IoCTools.
/// </summary>

#region External Services That Need Manual Configuration

/// <summary>
///     HTTP client service that requires manual configuration with named clients.
///     Uses [ExternalService] because it needs complex HttpClient factory setup.
///     This suppresses all IoCTools diagnostics since the service is managed externally.
/// </summary>
[ExternalService]
public partial class HttpClientService : IHttpClientService
{
    [Inject] private readonly IConfiguration _configuration;
    [Inject] private readonly IHttpClientFactory _httpClientFactory;
    [Inject] private readonly ILogger<HttpClientService> _logger;

    public async Task<ApiResponse<T>> GetAsync<T>(string clientName,
        string endpoint) where T : class
    {
        try
        {
            var client = _httpClientFactory.CreateClient(clientName);
            var response = await client.GetAsync(endpoint);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("API call failed: {StatusCode} for {Endpoint}",
                    response.StatusCode, endpoint);
                return new ApiResponse<T>(false, $"API call failed: {response.StatusCode}", default);
            }

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<T>(json);

            _logger.LogInformation("Successfully retrieved data from {Endpoint}", endpoint);
            return new ApiResponse<T>(true, "Success", data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling external API: {Endpoint}", endpoint);
            return new ApiResponse<T>(false, ex.Message, default);
        }
    }

    public async Task<ApiResponse<TResponse>> PostAsync<TRequest, TResponse>(
        string clientName,
        string endpoint,
        TRequest data)
        where TRequest : class
        where TResponse : class
    {
        try
        {
            var client = _httpClientFactory.CreateClient(clientName);
            var json = JsonSerializer.Serialize(data);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(endpoint, content);

            if (!response.IsSuccessStatusCode)
                return new ApiResponse<TResponse>(false, $"API call failed: {response.StatusCode}", default);

            var responseJson = await response.Content.ReadAsStringAsync();
            var responseData = JsonSerializer.Deserialize<TResponse>(responseJson);

            return new ApiResponse<TResponse>(true, "Success", responseData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting to external API: {Endpoint}", endpoint);
            return new ApiResponse<TResponse>(false, ex.Message, default);
        }
    }
}

/// <summary>
///     Database context service that requires manual configuration with connection strings.
///     Simulates Entity Framework context registration patterns.
///     Uses [ExternalService] to indicate this is manually registered and managed.
/// </summary>
[ExternalService]
public partial class DatabaseContextService : IDatabaseContextService
{
    [Inject] private readonly IConfiguration _configuration;

    private readonly string _connectionString;
    [Inject] private readonly ILogger<DatabaseContextService> _logger;

    public async Task<T?> FindByIdAsync<T>(int id) where T : class
    {
        await Task.Delay(10); // Simulate database query
        _logger.LogDebug("Finding entity of type {Type} with ID {Id}", typeof(T).Name, id);

        // Simulate returning mock data
        return Activator.CreateInstance<T>();
    }

    public async Task<IEnumerable<T>> GetAllAsync<T>() where T : class
    {
        await Task.Delay(20); // Simulate database query
        _logger.LogDebug("Retrieving all entities of type {Type}", typeof(T).Name);

        return new[] { Activator.CreateInstance<T>() };
    }

    public async Task<T> SaveAsync<T>(T entity) where T : class
    {
        await Task.Delay(15); // Simulate database save
        _logger.LogInformation("Saved entity of type {Type}", typeof(T).Name);
        return entity;
    }

    public async Task<bool> DeleteAsync<T>(int id) where T : class
    {
        await Task.Delay(10); // Simulate database delete
        _logger.LogInformation("Deleted entity of type {Type} with ID {Id}", typeof(T).Name, id);
        return true;
    }

    // Additional constructor for manual configuration (generated constructor will handle DI)
    public void Initialize()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection")
                               ?? throw new InvalidOperationException("Connection string not found");
        _logger.LogInformation("Database context initialized with connection: {Database}",
            GetDatabaseName(connectionString));
    }

    private static string GetDatabaseName(string connectionString) =>
        // Simple parsing - in real app would use proper connection string builder
        connectionString.Contains("Database=")
            ? connectionString.Split("Database=")[1].Split(";")[0]
            : "Unknown";
}

/// <summary>
///     Redis cache service that requires manual configuration with connection strings.
///     Uses [ExternalService] because Redis client needs complex setup.
///     All dependencies marked as external to suppress diagnostics.
/// </summary>
[ExternalService]
public partial class ExternalRedisCacheService : IDistributedCacheService
{
    [Inject] private readonly IConfiguration _configuration;
    [Inject] private readonly ILogger<ExternalRedisCacheService> _logger;

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        // In real implementation, would use StackExchange.Redis
        await Task.Delay(5); // Simulate Redis call
        _logger.LogDebug("Retrieved cache key: {Key}", key);

        // Simulate cache miss/hit
        return Random.Shared.NextDouble() > 0.5 ? Activator.CreateInstance<T>() : null;
    }

    public async Task SetAsync<T>(string key,
        T value,
        TimeSpan? expiration = null) where T : class
    {
        await Task.Delay(3); // Simulate Redis call
        _logger.LogDebug("Set cache key: {Key} with expiration: {Expiration}",
            key, expiration?.ToString() ?? "None");
    }

    public async Task RemoveAsync(string key)
    {
        await Task.Delay(2); // Simulate Redis call
        _logger.LogDebug("Removed cache key: {Key}", key);
    }

    public async Task<bool> ExistsAsync(string key)
    {
        await Task.Delay(2); // Simulate Redis call
        return Random.Shared.NextDouble() > 0.5; // Simulate existence check
    }

    public string CacheType => "Redis";
}

/// <summary>
///     Third-party API service that requires manual configuration with API keys and endpoints.
///     Shows integration with external SaaS services.
///     Uses [ExternalService] to indicate external management and suppress validation.
/// </summary>
[ExternalService]
public partial class ThirdPartyApiService : IThirdPartyApiService
{
    [Inject] private readonly IConfiguration _configuration;
    [Inject] private readonly IHttpClientService _httpClientService;
    [Inject] private readonly ILogger<ThirdPartyApiService> _logger;

    public async Task<PaymentProcessingResult> ProcessPaymentAsync(ExternalPaymentRequest request)
    {
        try
        {
            _logger.LogInformation("Processing external payment for amount: ${Amount}", request.Amount);

            var apiResponse = await _httpClientService.PostAsync<ExternalPaymentRequest, ExternalPaymentResponse>(
                "payment-gateway", "/api/v1/payments", request);

            if (!apiResponse.Success || apiResponse.Data == null)
                return new PaymentProcessingResult(false, apiResponse.Message);

            _logger.LogInformation("Payment processed successfully: {TransactionId}",
                apiResponse.Data.TransactionId);

            return new PaymentProcessingResult(true, "Payment processed", apiResponse.Data.TransactionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Payment processing failed for amount: ${Amount}", request.Amount);
            return new PaymentProcessingResult(false, ex.Message);
        }
    }

    public async Task<ExternalNotificationResult> SendNotificationAsync(ExternalNotificationRequest request)
    {
        try
        {
            var apiResponse =
                await _httpClientService.PostAsync<ExternalNotificationRequest, ExternalNotificationResponse>(
                    "notification-service", "/api/v1/notifications", request);

            return apiResponse.Success
                ? new ExternalNotificationResult(true, "Notification sent", apiResponse.Data?.MessageId)
                : new ExternalNotificationResult(false, apiResponse.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification to {Recipient}", request.Recipient);
            return new ExternalNotificationResult(false, ex.Message);
        }
    }

    public async Task<ValidationResult> ValidateDataAsync(object data,
        string validationType)
    {
        try
        {
            var request = new { Data = data, ValidationType = validationType };
            var apiResponse = await _httpClientService.PostAsync<object, ExternalValidationResponse>(
                "validation-service", "/api/v1/validate", request);

            return apiResponse.Success && apiResponse.Data != null
                ? new ValidationResult(apiResponse.Data.IsValid, apiResponse.Data.Errors ?? [])
                : new ValidationResult(false, [apiResponse.Message]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Validation failed for type: {ValidationType}", validationType);
            return new ValidationResult(false, [ex.Message]);
        }
    }
}

#endregion

#region IoCTools Services That Use External Services

/// <summary>
///     Business service that automatically gets registered but depends on external services.
///     This shows how IoCTools services can depend on manually configured external services.
/// </summary>
[Scoped]
public partial class OrderProcessingBusinessService : IOrderProcessingBusinessService
{
    [Inject] private readonly IDistributedCacheService _cacheService;
    [Inject] private readonly IDatabaseContextService _databaseContext;
    [Inject] private readonly ILogger<OrderProcessingBusinessService> _logger;
    [Inject] private readonly IThirdPartyApiService _thirdPartyApi;

    public async Task<OrderProcessingResult> ProcessOrderAsync(ExternalOrder order)
    {
        try
        {
            _logger.LogInformation("Processing order: {OrderId}", order.Id);

            // 1. Check cache first
            var cachedOrder = await _cacheService.GetAsync<ExternalOrder>($"order:{order.Id}");
            if (cachedOrder != null)
            {
                _logger.LogInformation("Order found in cache: {OrderId}", order.Id);
                return new OrderProcessingResult(true, "Order retrieved from cache");
            }

            // 2. Validate with third-party service
            var validation = await _thirdPartyApi.ValidateDataAsync(order, "order-validation");
            if (!validation.IsValid)
                return new OrderProcessingResult(false, "Order validation failed", validation.Errors);

            // 3. Process payment through third-party API
            var paymentRequest = new ExternalPaymentRequest(order.PaymentAmount, order.Currency, order.PaymentMethod);
            var paymentResult = await _thirdPartyApi.ProcessPaymentAsync(paymentRequest);

            if (!paymentResult.Success) return new OrderProcessingResult(false, "Payment processing failed");

            // 4. Save to database
            var savedOrder = await _databaseContext.SaveAsync(order);

            // 5. Cache the result
            await _cacheService.SetAsync($"order:{order.Id}", savedOrder, TimeSpan.FromMinutes(30));

            // 6. Send notification
            var notificationRequest = new ExternalNotificationRequest(
                order.CustomerEmail,
                "Order Processed",
                $"Your order {order.Id} has been processed successfully.");
            await _thirdPartyApi.SendNotificationAsync(notificationRequest);

            _logger.LogInformation("Order processing completed: {OrderId}", order.Id);
            return new OrderProcessingResult(true, "Order processed successfully",
                PaymentTransactionId: paymentResult.TransactionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Order processing failed: {OrderId}", order.Id);
            return new OrderProcessingResult(false, ex.Message);
        }
    }

    public async Task<OrderRetrievalResult> GetOrderAsync(int orderId)
    {
        try
        {
            // Check cache first
            var cachedOrder = await _cacheService.GetAsync<ExternalOrder>($"order:{orderId}");
            if (cachedOrder != null) return new OrderRetrievalResult(true, "Retrieved from cache", cachedOrder);

            // Fallback to database
            var order = await _databaseContext.FindByIdAsync<ExternalOrder>(orderId);
            if (order != null)
            {
                // Cache for next time
                await _cacheService.SetAsync($"order:{orderId}", order, TimeSpan.FromMinutes(30));
                return new OrderRetrievalResult(true, "Retrieved from database", order);
            }

            return new OrderRetrievalResult(false, "Order not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve order: {OrderId}", orderId);
            return new OrderRetrievalResult(false, ex.Message);
        }
    }
}

/// <summary>
///     Framework integration service that uses built-in .NET services.
///     Shows how IoCTools services can depend on framework services like IMemoryCache, ILogger, etc.
/// </summary>
[Scoped]
public partial class FrameworkIntegrationService : IFrameworkIntegrationService
{
    [Inject] private readonly IConfiguration _configuration;
    [Inject] private readonly ILogger<FrameworkIntegrationService> _logger;
    [Inject] private readonly IMemoryCache _memoryCache;
    [Inject] private readonly IServiceProvider _serviceProvider;

    public async Task<CacheOperationResult> CacheDataWithMemoryCacheAsync<T>(string key,
        T data,
        TimeSpan? expiration = null) where T : class
    {
        try
        {
            var options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(5),
                Priority = CacheItemPriority.Normal
            };

            _memoryCache.Set(key, data, options);
            _logger.LogDebug("Cached data with key: {Key}", key);

            await Task.Delay(1); // Simulate async operation
            return new CacheOperationResult(true, "Data cached successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache data with key: {Key}", key);
            return new CacheOperationResult(false, ex.Message);
        }
    }

    public async Task<ConfigurationResult> GetConfigurationValuesAsync(string sectionName)
    {
        try
        {
            var section = _configuration.GetSection(sectionName);
            var values = new Dictionary<string, string>();

            foreach (var item in section.GetChildren()) values[item.Key] = item.Value ?? string.Empty;

            _logger.LogDebug("Retrieved {Count} configuration values from section: {Section}",
                values.Count, sectionName);

            await Task.Delay(1); // Simulate async operation
            return new ConfigurationResult(true, "Configuration retrieved", values);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve configuration section: {Section}", sectionName);
            return new ConfigurationResult(false, ex.Message, []);
        }
    }

    public async Task<ServiceResolutionResult> ResolveServiceDynamicallyAsync<T>() where T : class
    {
        try
        {
            var service = _serviceProvider.GetService<T>();
            var serviceType = typeof(T);

            if (service != null)
            {
                _logger.LogDebug("Successfully resolved service: {ServiceType}", serviceType.Name);
                await Task.Delay(1); // Simulate async operation
                return new ServiceResolutionResult(true, "Service resolved", serviceType.Name, service.GetHashCode());
            }

            _logger.LogWarning("Service not found: {ServiceType}", serviceType.Name);
            return new ServiceResolutionResult(false, "Service not registered", serviceType.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve service: {ServiceType}", typeof(T).Name);
            return new ServiceResolutionResult(false, ex.Message, typeof(T).Name);
        }
    }
}

/// <summary>
///     Mixed registration example that combines automatic IoCTools registration with manual dependencies.
///     Shows the hybrid pattern where some dependencies are automatic and others are manual.
/// </summary>
[Scoped]
public partial class HybridIntegrationService : IHybridIntegrationService
{
    [Inject] private readonly IDatabaseContextService _databaseContext; // External service
    [Inject] private readonly IEmailService _emailService; // IoCTools registered service

    // These need to be manually registered
    [Inject] private readonly IHttpClientService _httpClientService; // External service

    [Inject] private readonly ILogger<HybridIntegrationService> _logger; // Framework service

    // These are automatically resolved by IoCTools
    [Inject] private readonly IPaymentService _paymentService; // IoCTools registered service
    [Inject] private readonly IServiceProvider _serviceProvider; // For health checks
    [Inject] private readonly IThirdPartyApiService _thirdPartyApiService; // External service

    public async Task<IntegrationResult> ProcessBusinessWorkflowAsync(BusinessWorkflowRequest request)
    {
        try
        {
            _logger.LogInformation("Starting business workflow: {WorkflowId}", request.WorkflowId);

            // 1. Use IoCTools service for payment processing
            var paymentResult = await _paymentService.ProcessPaymentAsync(new Payment(request.Amount));
            if (!paymentResult.Success) return new IntegrationResult(false, "Payment failed", request.WorkflowId);

            // 2. Use external database service to save workflow state
            await _databaseContext.SaveAsync(request);

            // 3. Call external API for additional processing
            var apiRequest = new ExternalPaymentRequest(request.Amount, "USD", "CreditCard");
            var externalResult = await _thirdPartyApiService.ProcessPaymentAsync(apiRequest);

            if (!externalResult.Success) _logger.LogWarning("External API call failed but continuing workflow");

            // 4. Use IoCTools service for email notification
            await _emailService.SendEmailAsync(
                request.CustomerEmail,
                "Workflow Completed",
                $"Your workflow {request.WorkflowId} has been processed."
            );

            // 5. Make direct HTTP call for final confirmation
            var confirmationResponse = await _httpClientService.GetAsync<WorkflowConfirmation>(
                "workflow-service", $"/api/workflows/{request.WorkflowId}/confirm");

            _logger.LogInformation("Business workflow completed: {WorkflowId}", request.WorkflowId);
            return new IntegrationResult(true, "Workflow completed successfully", request.WorkflowId,
                Guid.NewGuid().ToString(),
                externalResult.TransactionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Business workflow failed: {WorkflowId}", request.WorkflowId);
            return new IntegrationResult(false, ex.Message, request.WorkflowId);
        }
    }

    public async Task<HealthCheckResult> CheckDependencyHealthAsync()
    {
        var results = new Dictionary<string, bool>();
        var messages = new List<string>();

        try
        {
            // Check IoCTools services
            var paymentHealth = await CheckServiceHealth<IPaymentService>("PaymentService");
            results["PaymentService"] = paymentHealth;
            if (!paymentHealth) messages.Add("PaymentService is unhealthy");

            var emailHealth = await CheckServiceHealth<IEmailService>("EmailService");
            results["EmailService"] = emailHealth;
            if (!emailHealth) messages.Add("EmailService is unhealthy");

            // Check external services
            var httpHealth = await CheckServiceHealth<IHttpClientService>("HttpClientService");
            results["HttpClientService"] = httpHealth;
            if (!httpHealth) messages.Add("HttpClientService is unavailable");

            var dbHealth = await CheckServiceHealth<IDatabaseContextService>("DatabaseService");
            results["DatabaseService"] = dbHealth;
            if (!dbHealth) messages.Add("DatabaseService is unavailable");

            var allHealthy = results.Values.All(h => h);
            var summary = allHealthy ? "All dependencies are healthy" : "Some dependencies have issues";

            return new HealthCheckResult(allHealthy, summary, results, messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return new HealthCheckResult(false, ex.Message, results, [ex.Message]);
        }
    }

    private async Task<bool> CheckServiceHealth<T>(string serviceName) where T : class
    {
        try
        {
            var service = _serviceProvider.GetService<T>();
            await Task.Delay(10); // Simulate health check
            return service != null;
        }
        catch
        {
            return false;
        }
    }
}

#endregion

#region Registration Helper Service

/// <summary>
///     Service that demonstrates how to manually configure and register external services.
///     This would typically be called from Program.cs during application startup.
/// </summary>
[Singleton]
public partial class ExternalServiceRegistrationHelper : IExternalServiceRegistrationHelper
{
    [Inject] private readonly ILogger<ExternalServiceRegistrationHelper> _logger;

    public void ConfigureExternalServices(IServiceCollection services,
        IConfiguration configuration)
    {
        _logger.LogInformation("Configuring external services for manual registration");

        // Configure HTTP clients with different configurations
        ConfigureHttpClients(services, configuration);

        // Register external services that need manual registration
        RegisterExternalServices(services);

        _logger.LogInformation("External service configuration completed");
    }

    public void ValidateExternalServiceConfiguration(IConfiguration configuration)
    {
        var missingConfigurations = new List<string>();

        // Validate required configuration sections
        var requiredConfigs = new[]
        {
            "ExternalServices:PaymentGateway:BaseUrl", "ExternalServices:PaymentGateway:ApiKey",
            "ExternalServices:NotificationService:BaseUrl", "ExternalServices:NotificationService:ApiKey",
            "ConnectionStrings:DefaultConnection"
        };

        foreach (var config in requiredConfigs)
            if (string.IsNullOrWhiteSpace(configuration.GetValue<string>(config)))
                missingConfigurations.Add(config);

        if (missingConfigurations.Count > 0)
            _logger.LogWarning("Missing external service configurations: {Configs}",
                string.Join(", ", missingConfigurations));
        else
            _logger.LogInformation("All external service configurations are present");
    }

    private static void ConfigureHttpClients(IServiceCollection services,
        IConfiguration configuration)
    {
        // Payment Gateway HTTP Client
        services.AddHttpClient("payment-gateway", client =>
        {
            client.BaseAddress = new Uri(configuration.GetValue<string>("ExternalServices:PaymentGateway:BaseUrl")
                                         ?? "https://api.paymentgateway.example.com/");
            client.DefaultRequestHeaders.Add("Authorization",
                $"Bearer {configuration.GetValue<string>("ExternalServices:PaymentGateway:ApiKey")}");
            client.DefaultRequestHeaders.Add("User-Agent", "IoCTools-Sample/1.0");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Notification Service HTTP Client
        services.AddHttpClient("notification-service", client =>
        {
            client.BaseAddress = new Uri(configuration.GetValue<string>("ExternalServices:NotificationService:BaseUrl")
                                         ?? "https://api.notifications.example.com/");
            client.DefaultRequestHeaders.Add("X-API-Key",
                configuration.GetValue<string>("ExternalServices:NotificationService:ApiKey") ?? "demo-key");
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        // Validation Service HTTP Client
        services.AddHttpClient("validation-service", client =>
        {
            client.BaseAddress = new Uri(configuration.GetValue<string>("ExternalServices:ValidationService:BaseUrl")
                                         ?? "https://api.validation.example.com/");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        // Workflow Service HTTP Client
        services.AddHttpClient("workflow-service", client =>
        {
            client.BaseAddress = new Uri(configuration.GetValue<string>("ExternalServices:WorkflowService:BaseUrl")
                                         ?? "https://api.workflow.example.com/");
            client.DefaultRequestHeaders.Add("Content-Type", "application/json");
        });
    }

    private void RegisterExternalServices(IServiceCollection services)
    {
        // Register external services that have [ExternalService]
        // These need manual registration because they require complex configuration

        services.AddScoped<IHttpClientService, HttpClientService>();
        services.AddScoped<IDatabaseContextService, DatabaseContextService>();
        services.AddScoped<IDistributedCacheService, ExternalRedisCacheService>();
        services.AddScoped<IThirdPartyApiService, ThirdPartyApiService>();

        _logger.LogDebug("Registered external services: HttpClient, Database, Redis, ThirdPartyApi");
    }
}

#endregion

#region Interface Definitions for External Services

public interface IHttpClientService
{
    Task<ApiResponse<T>> GetAsync<T>(string clientName,
        string endpoint) where T : class;

    Task<ApiResponse<TResponse>> PostAsync<TRequest, TResponse>(string clientName,
        string endpoint,
        TRequest data)
        where TRequest : class where TResponse : class;
}

public interface IDatabaseContextService
{
    Task<T?> FindByIdAsync<T>(int id) where T : class;
    Task<IEnumerable<T>> GetAllAsync<T>() where T : class;
    Task<T> SaveAsync<T>(T entity) where T : class;
    Task<bool> DeleteAsync<T>(int id) where T : class;
}

public interface IDistributedCacheService
{
    string CacheType { get; }
    Task<T?> GetAsync<T>(string key) where T : class;

    Task SetAsync<T>(string key,
        T value,
        TimeSpan? expiration = null) where T : class;

    Task RemoveAsync(string key);
    Task<bool> ExistsAsync(string key);
}

public interface IThirdPartyApiService
{
    Task<PaymentProcessingResult> ProcessPaymentAsync(ExternalPaymentRequest request);
    Task<ExternalNotificationResult> SendNotificationAsync(ExternalNotificationRequest request);

    Task<ValidationResult> ValidateDataAsync(object data,
        string validationType);
}

public interface IOrderProcessingBusinessService
{
    Task<OrderProcessingResult> ProcessOrderAsync(ExternalOrder order);
    Task<OrderRetrievalResult> GetOrderAsync(int orderId);
}

public interface IFrameworkIntegrationService
{
    Task<CacheOperationResult> CacheDataWithMemoryCacheAsync<T>(string key,
        T data,
        TimeSpan? expiration = null) where T : class;

    Task<ConfigurationResult> GetConfigurationValuesAsync(string sectionName);
    Task<ServiceResolutionResult> ResolveServiceDynamicallyAsync<T>() where T : class;
}

public interface IHybridIntegrationService
{
    Task<IntegrationResult> ProcessBusinessWorkflowAsync(BusinessWorkflowRequest request);
    Task<HealthCheckResult> CheckDependencyHealthAsync();
}

public interface IExternalServiceRegistrationHelper
{
    void ConfigureExternalServices(IServiceCollection services,
        IConfiguration configuration);

    void ValidateExternalServiceConfiguration(IConfiguration configuration);
}

#endregion

#region Data Models for External Services

public record ApiResponse<T>(bool Success, string Message, T? Data);

public record ExternalPaymentRequest(decimal Amount, string Currency, string PaymentMethod)
{
    public string CardNumber { get; init; } = "****-****-****-1234";
    public string MerchantId { get; init; } = "DEMO_MERCHANT";
}

public record ExternalPaymentResponse(string TransactionId, string Status, decimal Amount);

public record ExternalNotificationRequest(string Recipient, string Subject, string Message)
{
    public string Channel { get; init; } = "email";
    public Dictionary<string, object> Metadata { get; init; } = new();
}

public record ExternalNotificationResponse(string MessageId, string Status);

public record ExternalValidationResponse(bool IsValid, string[] Errors);

public record ExternalOrder(int Id, string CustomerEmail, decimal PaymentAmount)
{
    public string Currency { get; init; } = "USD";
    public string PaymentMethod { get; init; } = "CreditCard";
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string Status { get; init; } = "Pending";
}

public record PaymentProcessingResult(bool Success, string Message, string? TransactionId = null);

public record ExternalNotificationResult(bool Success, string Message, string? MessageId = null);

public record OrderProcessingResult(
    bool Success,
    string Message,
    IEnumerable<string>? ValidationErrors = null,
    string? PaymentTransactionId = null);

public record OrderRetrievalResult(bool Success, string Message, ExternalOrder? Order = null);

public record CacheOperationResult(bool Success, string Message);

public record ConfigurationResult(bool Success, string Message, Dictionary<string, string> Values);

public record ServiceResolutionResult(bool Success, string Message, string ServiceType, int? InstanceHashCode = null);

public record BusinessWorkflowRequest(string WorkflowId, string CustomerEmail, decimal Amount);

public record IntegrationResult(
    bool Success,
    string Message,
    string WorkflowId,
    string? PaymentTransactionId = null,
    string? ExternalTransactionId = null);

public record HealthCheckResult(
    bool IsHealthy,
    string Message,
    Dictionary<string, bool> ServiceHealth,
    IEnumerable<string> Issues);

public record WorkflowConfirmation(string WorkflowId, string Status, DateTime ConfirmedAt);

#endregion
