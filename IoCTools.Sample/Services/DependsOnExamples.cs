using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using IoCTools.Sample.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IoCTools.Sample.Services;

/// <summary>
/// Comprehensive examples demonstrating the DependsOn attribute patterns
/// This file covers all documented DependsOn functionality with practical, working examples
/// </summary>

// ===== INTERFACES FOR DEMONSTRATION =====

public interface IInventoryService
{
    Task<bool> CheckStockAsync(int productId, int quantity);
    Task ReserveAsync(int productId, int quantity);
}

public interface IShippingService
{
    Task<string> CalculateShippingCostAsync(string address, double weight);
    Task<bool> ScheduleDeliveryAsync(string address, DateTime preferredDate);
}

public interface IUserManagementService
{
    Task<User> GetUserAsync(int userId);
    Task UpdateUserAsync(User user);
}

public interface IReportGenerator
{
    Task<string> GenerateOrderReportAsync(int orderId);
    Task<string> GenerateInventoryReportAsync();
}

public interface IAuditService
{
    Task LogActionAsync(string action, string details);
}

public interface ISecurityService
{
    Task<bool> ValidatePermissionsAsync(int userId, string action);
    Task LogSecurityEventAsync(string eventType, string details);
}

public interface IDependsOnGenericRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();
    Task AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(int id);
}

// ===== 1. BASIC DEPENDSON WITH MULTIPLE DEPENDENCIES =====

/// <summary>
/// Example 1: Basic DependsOn with multiple dependencies
/// Default settings: CamelCase naming, stripI=true, prefix="_"
/// Generated fields: _paymentService, _emailService, _inventoryService
/// </summary>
[Service]
[DependsOn<IPaymentService, IEmailService, IInventoryService>]
public partial class OrderProcessingService
{
    [Inject] private readonly ILogger<OrderProcessingService> _logger;

    public async Task<bool> ProcessOrderAsync(Order order)
    {
        _logger.LogInformation("Processing order {OrderId}", order.Id);

        // Use generated dependencies
        var stockAvailable = await _inventoryService.CheckStockAsync(order.Id, 1);
        if (!stockAvailable)
        {
            _logger.LogWarning("Insufficient stock for order {OrderId}", order.Id);
            return false;
        }

        await _inventoryService.ReserveAsync(order.Id, 1);

        var paymentResult = await _paymentService.ProcessPaymentAsync(order.Payment);
        if (paymentResult.Success)
        {
            await _emailService.SendConfirmationAsync(order.CustomerEmail);
            _logger.LogInformation("Order {OrderId} processed successfully", order.Id);
            return true;
        }

        _logger.LogWarning("Payment failed for order {OrderId}", order.Id);
        return false;
    }
}

// ===== 2. ALL NAMING CONVENTIONS =====

/// <summary>
/// Example 2a: CamelCase naming (default)
/// Generated fields: _userManagementService, _auditService
/// </summary>
[Service]
[DependsOn<IUserManagementService, IAuditService>(NamingConvention.CamelCase)]
public partial class CamelCaseExampleService
{
    [Inject] private readonly ILogger<CamelCaseExampleService> _logger;

    public async Task ProcessUserActionAsync(int userId, string action)
    {
        _logger.LogInformation("Processing action {Action} for user {UserId}", action, userId);
        
        var user = await _userManagementService.GetUserAsync(userId);
        await _auditService.LogActionAsync(action, $"User: {user.Name}");
    }
}

/// <summary>
/// Example 2b: PascalCase naming
/// Generated fields: _UserManagementService, _AuditService
/// </summary>
[Service]
[DependsOn<IUserManagementService, IAuditService>(NamingConvention.PascalCase)]
public partial class PascalCaseExampleService
{
    [Inject] private readonly ILogger<PascalCaseExampleService> _logger;

    public async Task ProcessUserActionAsync(int userId, string action)
    {
        _logger.LogInformation("Processing action {Action} for user {UserId} (PascalCase)", action, userId);
        
        var user = await _UserManagementService.GetUserAsync(userId);
        await _AuditService.LogActionAsync(action, $"User: {user.Name} (PascalCase)");
    }
}

/// <summary>
/// Example 2c: SnakeCase naming
/// Generated fields: _user_management_service, _audit_service
/// </summary>
[Service]
[DependsOn<IUserManagementService, IAuditService>(NamingConvention.SnakeCase)]
public partial class SnakeCaseExampleService
{
    [Inject] private readonly ILogger<SnakeCaseExampleService> _logger;

    public async Task ProcessUserActionAsync(int userId, string action)
    {
        _logger.LogInformation("Processing action {Action} for user {UserId} (snake_case)", action, userId);
        
        var user = await _user_management_service.GetUserAsync(userId);
        await _audit_service.LogActionAsync(action, $"User: {user.Name} (snake_case)");
    }
}

// ===== 3. CUSTOM PREFIXES AND STRIPI PARAMETER =====

/// <summary>
/// Example 3a: Custom prefix with stripI=true
/// Generated fields: svc_paymentService, svc_emailService (I is stripped from interface names)
/// </summary>
[Service]
[DependsOn<IPaymentService, IEmailService>(prefix: "svc_", stripI: true)]
public partial class CustomPrefixService
{
    [Inject] private readonly ILogger<CustomPrefixService> _logger;

    public async Task ProcessPaymentWithEmailAsync(Payment payment, string email)
    {
        _logger.LogInformation("Processing payment with custom prefix");
        
        var result = await svc_paymentService.ProcessPaymentAsync(payment);
        if (result.Success)
        {
            await svc_emailService.SendConfirmationAsync(email);
        }
    }
}

/// <summary>
/// Example 3b: No prefix, keep interface prefix (stripI=false)
/// Generated fields: paymentService, emailService (semantic camelCase naming even with stripI=false)
/// </summary>
[Service]
[DependsOn<IPaymentService, IEmailService>(prefix: "", stripI: false)]
public partial class NoStripIService
{
    [Inject] private readonly ILogger<NoStripIService> _logger;

    public async Task ProcessPaymentPreservingInterfaceNameAsync(Payment payment, string email)
    {
        _logger.LogInformation("Processing payment preserving interface names");
        
        var result = await paymentService.ProcessPaymentAsync(payment);
        if (result.Success)
        {
            await emailService.SendConfirmationAsync(email);
        }
    }
}

/// <summary>
/// Example 3c: Multiple custom configurations
/// Shows how different DependsOn attributes can have different configurations
/// </summary>
[Service]
[DependsOn<IPaymentService>(prefix: "payment_", stripI: true, namingConvention: NamingConvention.SnakeCase)]
[DependsOn<IEmailService>(prefix: "notification_", stripI: true, namingConvention: NamingConvention.CamelCase)]
public partial class MixedConfigurationService
{
    [Inject] private readonly ILogger<MixedConfigurationService> _logger;

    public async Task ProcessWithMixedConfigurationsAsync(Payment payment, string email)
    {
        _logger.LogInformation("Using mixed DependsOn configurations");
        
        // Uses snake_case: payment_payment_service
        var result = await payment_payment_service.ProcessPaymentAsync(payment);
        
        if (result.Success)
        {
            // Uses camelCase: notification_emailService
            await notification_emailService.SendConfirmationAsync(email);
        }
    }
}

// ===== 4. INHERITANCE SCENARIOS WITH DEPENDSON =====

/// <summary>
/// Example 4a: Base service with DependsOn
/// Generated fields: _auditService, _securityService
/// </summary>
[Service]
[DependsOn<IAuditService, ISecurityService>]
public abstract partial class BaseSecureService
{
    [Inject] protected readonly ILogger<BaseSecureService> _logger;

    protected virtual async Task<bool> ValidateAndAuditAsync(int userId, string action)
    {
        _logger.LogInformation("Validating action {Action} for user {UserId}", action, userId);
        
        var isValid = await _securityService.ValidatePermissionsAsync(userId, action);
        await _auditService.LogActionAsync(action, $"UserId: {userId}, Valid: {isValid}");
        
        return isValid;
    }
}

/// <summary>
/// Example 4b: Derived service with additional DependsOn
/// Inherits: _auditService, _securityService from base
/// Adds: _userManagementService from its own DependsOn
/// </summary>
[Service]
[DependsOn<IUserManagementService>]
public partial class EnhancedSecureService : BaseSecureService
{
    public async Task<bool> ProcessSecureUserActionAsync(int userId, string action)
    {
        _logger.LogInformation("Processing secure user action with inheritance");

        // Use inherited dependencies
        var isValid = await ValidateAndAuditAsync(userId, action);
        if (!isValid) return false;

        // Use own dependency
        var user = await _userManagementService.GetUserAsync(userId);
        _logger.LogInformation("Action validated for user {UserName}", user.Name);

        return true;
    }
}

/// <summary>
/// Example 4c: Deep inheritance with DependsOn at multiple levels
/// </summary>
[Service]
public abstract partial class ConfigurableBaseService
{
    [Inject] protected readonly ILogger<ConfigurableBaseService> _logger;
    [Inject] protected readonly IConfiguration _configuration;

    protected string GetConfigValue(string key) => _configuration[key] ?? "";
}

[Service]
[DependsOn<IAuditService>]
public partial class ConfigurableAuditService : ConfigurableBaseService
{
    public async Task ProcessWithConfigAndAuditAsync(string action)
    {
        var setting = GetConfigValue("ProcessingSetting"); // Uses inherited _configuration
        await _auditService.LogActionAsync(action, $"Setting: {setting}"); // Uses own _auditService
    }
}

// ===== 5. MIXED [INJECT] AND [DEPENDSON] PATTERNS =====

/// <summary>
/// Example 5: Mixing Inject fields and DependsOn dependencies
/// Shows how both approaches can coexist in the same service
/// </summary>
[Service]
[DependsOn<IPaymentService, IInventoryService>]
public partial class MixedDependencyPatternService
{
    // Manual inject fields
    [Inject] private readonly ILogger<MixedDependencyPatternService> _logger;
    [Inject] private readonly IConfiguration _configuration;

    // DependsOn generates: _paymentService, _inventoryService

    public async Task<bool> ProcessOrderWithMixedPatternsAsync(Order order)
    {
        _logger.LogInformation("Processing order with mixed dependency patterns");

        // Use configuration (manual inject)
        var processingTimeout = _configuration.GetValue<int>("ProcessingTimeout");
        
        // Use DependsOn generated dependencies
        var stockAvailable = await _inventoryService.CheckStockAsync(order.Id, 1);
        if (stockAvailable)
        {
            var paymentResult = await _paymentService.ProcessPaymentAsync(order.Payment);
            return paymentResult.Success;
        }

        return false;
    }
}

// ===== 6. EXTERNAL SERVICE HANDLING =====

/// <summary>
/// Example 6: External services with DependsOn
/// External=true indicates these services are registered elsewhere (not by IoCTools)
/// </summary>
[Service]
[DependsOn<IConfiguration>(external: true)]
[DependsOn<ILogger<ExternalServiceDemoService>>(external: true, prefix: "ext_", stripI: false)]
public partial class ExternalServiceDemoService
{
    // These dependencies are generated but marked as external
    // Generated: _configuration, ext_logger

    public async Task DemonstrateExternalServicesAsync()
    {
        var setting = _configuration["DemoSetting"] ?? "default";
        ext_logger.LogInformation("Demonstrating external services with setting: {Setting}", setting);
        
        await Task.CompletedTask;
    }
}

// ===== 7. GENERIC SERVICES WITH DEPENDSON =====

/// <summary>
/// Example 7a: Generic service with DependsOn
/// Note: Generic types in DependsOn attributes have limitations
/// </summary>
[Service]
public partial class GenericRepositoryService<T> where T : class
{
    [Inject] private readonly ILogger<GenericRepositoryService<T>> _logger;
    [Inject] private readonly IDependsOnGenericRepository<T> _repository;

    public async Task<T?> GetEntityAsync(int id)
    {
        _logger.LogInformation("Getting entity of type {TypeName} with id {Id}", typeof(T).Name, id);
        return await _repository.GetByIdAsync(id);
    }

    public async Task SaveEntityAsync(T entity)
    {
        _logger.LogInformation("Saving entity of type {TypeName}", typeof(T).Name);
        await _repository.AddAsync(entity);
    }
}

/// <summary>
/// Example 7b: Service using generic dependencies
/// Note: Multiple generic repositories may require manual disambiguation
/// </summary>
[Service]
public partial class MultiGenericRepositoryService
{
    [Inject] private readonly ILogger<MultiGenericRepositoryService> _logger;
    [Inject] private readonly IDependsOnGenericRepository<DependsOnUser> _userRepository;
    [Inject] private readonly IDependsOnGenericRepository<Order> _orderRepository;

    public async Task<string> GenerateUserOrderReportAsync(int userId)
    {
        _logger.LogInformation("Generating report for user {UserId}", userId);
        
        // Note: The exact field names depend on the generator's disambiguation logic
        // This example shows the intended usage pattern
        
        return $"Report for user {userId}";
    }
}

// ===== IMPLEMENTATION SERVICES FOR DEMO =====

[Service]
public partial class InventoryService : IInventoryService
{
    [Inject] private readonly ILogger<InventoryService> _logger;

    public async Task<bool> CheckStockAsync(int productId, int quantity)
    {
        _logger.LogInformation("Checking stock for product {ProductId}, quantity {Quantity}", productId, quantity);
        await Task.Delay(10); // Simulate async operation
        return true; // Simplified - always in stock
    }

    public async Task ReserveAsync(int productId, int quantity)
    {
        _logger.LogInformation("Reserving {Quantity} units of product {ProductId}", quantity, productId);
        await Task.Delay(10); // Simulate async operation
    }
}

[Service]
public partial class ShippingService : IShippingService
{
    [Inject] private readonly ILogger<ShippingService> _logger;

    public async Task<string> CalculateShippingCostAsync(string address, double weight)
    {
        _logger.LogInformation("Calculating shipping cost for {Address}, weight {Weight}", address, weight);
        await Task.Delay(10);
        return $"${weight * 2.50:F2}"; // $2.50 per unit weight
    }

    public async Task<bool> ScheduleDeliveryAsync(string address, DateTime preferredDate)
    {
        _logger.LogInformation("Scheduling delivery to {Address} for {Date}", address, preferredDate.ToShortDateString());
        await Task.Delay(10);
        return true;
    }
}

[Service]
public partial class UserManagementService : IUserManagementService
{
    [Inject] private readonly ILogger<UserManagementService> _logger;

    public async Task<User> GetUserAsync(int userId)
    {
        _logger.LogInformation("Getting user {UserId}", userId);
        await Task.Delay(10);
        return new User(userId, $"User{userId}", $"user{userId}@example.com");
    }

    public async Task UpdateUserAsync(User user)
    {
        _logger.LogInformation("Updating user {UserId}", user.Id);
        await Task.Delay(10);
    }
}

[Service]
public partial class ReportGenerator : IReportGenerator
{
    [Inject] private readonly ILogger<ReportGenerator> _logger;

    public async Task<string> GenerateOrderReportAsync(int orderId)
    {
        _logger.LogInformation("Generating report for order {OrderId}", orderId);
        await Task.Delay(50); // Simulate report generation
        return $"Order Report #{orderId} - Generated at {DateTime.Now}";
    }

    public async Task<string> GenerateInventoryReportAsync()
    {
        _logger.LogInformation("Generating inventory report");
        await Task.Delay(100); // Simulate report generation
        return $"Inventory Report - Generated at {DateTime.Now}";
    }
}

[Service]
public partial class AuditService : IAuditService
{
    [Inject] private readonly ILogger<AuditService> _logger;

    public async Task LogActionAsync(string action, string details)
    {
        _logger.LogInformation("AUDIT: {Action} - {Details}", action, details);
        await Task.Delay(5); // Simulate audit logging
    }
}

[Service]
public partial class DemoSecurityService : ISecurityService
{
    [Inject] private readonly ILogger<DemoSecurityService> _logger;

    public async Task<bool> ValidatePermissionsAsync(int userId, string action)
    {
        _logger.LogInformation("Validating permissions for user {UserId} to perform {Action}", userId, action);
        await Task.Delay(20); // Simulate security check
        return true; // Simplified - always grant permission
    }

    public async Task LogSecurityEventAsync(string eventType, string details)
    {
        _logger.LogWarning("SECURITY EVENT: {EventType} - {Details}", eventType, details);
        await Task.Delay(5);
    }
}

// Repository implementations for generic examples
[Service]
public partial class DependsOnUserRepository : IDependsOnGenericRepository<DependsOnUser>
{
    [Inject] private readonly ILogger<DependsOnUserRepository> _logger;

    public async Task<DependsOnUser?> GetByIdAsync(int id)
    {
        _logger.LogInformation("Getting user by id {Id}", id);
        await Task.Delay(10);
        return new DependsOnUser(id, $"User{id}", $"user{id}@example.com");
    }

    public async Task<IEnumerable<DependsOnUser>> GetAllAsync()
    {
        _logger.LogInformation("Getting all users");
        await Task.Delay(20);
        return new[] { new DependsOnUser(1, "User1", "user1@example.com"), new DependsOnUser(2, "User2", "user2@example.com") };
    }

    public async Task AddAsync(DependsOnUser entity)
    {
        _logger.LogInformation("Adding user {UserName}", entity.Name);
        await Task.Delay(10);
    }

    public async Task UpdateAsync(DependsOnUser entity)
    {
        _logger.LogInformation("Updating user {UserName}", entity.Name);
        await Task.Delay(10);
    }

    public async Task DeleteAsync(int id)
    {
        _logger.LogInformation("Deleting user {Id}", id);
        await Task.Delay(10);
    }
}

[Service]
public partial class DependsOnOrderRepository : IDependsOnGenericRepository<Order>
{
    [Inject] private readonly ILogger<DependsOnOrderRepository> _logger;

    public async Task<Order?> GetByIdAsync(int id)
    {
        _logger.LogInformation("Getting order by id {Id}", id);
        await Task.Delay(10);
        return new Order(id, "test@example.com", new Payment(100m));
    }

    public async Task<IEnumerable<Order>> GetAllAsync()
    {
        _logger.LogInformation("Getting all orders");
        await Task.Delay(20);
        return new[] { new Order(1, "test1@example.com", new Payment(100m)) };
    }

    public async Task AddAsync(Order entity)
    {
        _logger.LogInformation("Adding order {OrderId}", entity.Id);
        await Task.Delay(10);
    }

    public async Task UpdateAsync(Order entity)
    {
        _logger.LogInformation("Updating order {OrderId}", entity.Id);
        await Task.Delay(10);
    }

    public async Task DeleteAsync(int id)
    {
        _logger.LogInformation("Deleting order {Id}", id);
        await Task.Delay(10);
    }
}

// Data model for DependsOn examples
public record DependsOnUser(int Id, string Name, string Email);