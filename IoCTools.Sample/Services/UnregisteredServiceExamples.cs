using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IoCTools.Sample.Services;

/// <summary>
/// Comprehensive examples demonstrating UnregisteredService attribute usage patterns.
/// These services generate constructors but are not automatically registered in DI container.
/// </summary>

#region Basic Unregistered Service Examples

/// <summary>
///     Simple unregistered service - generates constructor but no registration
///     Must be manually registered if needed
/// </summary>
[UnregisteredService]
public partial class ManualRegistrationService : IManualRegistrationService
{
    [Inject] private readonly ILogger<ManualRegistrationService> _logger;

    public async Task ProcessAsync(string message)
    {
        _logger.LogInformation("Manual service processed message: {Message}", message);
        await Task.Delay(10);
    }
}

/// <summary>
///     Legacy service that's been replaced but still needs constructor generation
///     Not registered to prevent accidental use
/// </summary>
[UnregisteredService]
public partial class UnregisteredLegacyPaymentProcessor : ILegacyPaymentProcessor
{
    [Inject] private readonly ILogger<UnregisteredLegacyPaymentProcessor> _logger;

    public async Task<PaymentResult> ProcessLegacyPaymentAsync(Payment payment)
    {
        _logger.LogWarning("Legacy payment processor used - should migrate to new processor");

        // Legacy logic here
        await Task.Delay(50);
        return new PaymentResult(true, "Legacy processing completed");
    }
}

/// <summary>
///     Test helper service - generates constructor but not registered
///     Used for testing without affecting production DI container
/// </summary>
[UnregisteredService]
public partial class TestHelperService : ITestHelperService
{
    [Inject] private readonly ILogger<TestHelperService> _logger;

    public async Task<bool> ValidateAndCreateUserAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            _logger.LogWarning("Invalid name for test user creation: {Name}", name);
            return false;
        }

        _logger.LogInformation("Test user created: {Name}", name);
        await Task.Delay(10);
        return true;
    }
}

#endregion

#region Inheritance with Unregistered Services

/// <summary>
///     Base service that provides common functionality but shouldn't be registered
///     Derived classes decide their registration strategy
/// </summary>
[UnregisteredService]
public partial class BasePaymentProcessor
{
    [Inject] protected readonly ILogger<BasePaymentProcessor> Logger;

    protected async Task LogPaymentAttemptAsync(string paymentMethod,
        decimal amount)
    {
        Logger.LogInformation("Payment attempt: {Method} for ${Amount}", paymentMethod, amount);
        await Task.Delay(5);
    }
}

/// <summary>
///     New payment processor - inherits from unregistered base but is registered
///     This demonstrates that [Service] classes can inherit from [UnregisteredService] bases
/// </summary>
[Service()]
public partial class UnregisteredNewPaymentProcessor : BasePaymentProcessor, INewPaymentProcessor
{
    [Inject] private readonly ILogger<UnregisteredNewPaymentProcessor> _specificLogger;

    public async Task<PaymentResult> ProcessNewPaymentAsync(Payment payment)
    {
        await LogPaymentAttemptAsync("new-processor", payment.Amount);

        _specificLogger.LogInformation("Processing payment via new processor");
        await Task.Delay(20); // Simulate processing

        return new PaymentResult(true, "Payment processed successfully");
    }
}

/// <summary>
///     Another unregistered service in the inheritance chain
///     Demonstrates complex inheritance scenarios
/// </summary>
[UnregisteredService]
public partial class AdvancedPaymentBase : BasePaymentProcessor
{
    [Inject] private readonly ILogger<AdvancedPaymentBase> _advancedLogger;

    protected async Task<bool> ValidatePaymentAsync(decimal amount)
    {
        if (amount <= 0)
        {
            _advancedLogger.LogWarning("Invalid payment amount: {Amount}", amount);
            return false;
        }

        return true;
    }
}

/// <summary>
///     Final registered service in complex inheritance chain
/// </summary>
[Service()]
public partial class EnterprisePaymentProcessor : AdvancedPaymentBase, IEnterprisePaymentProcessor
{
    [Inject] private readonly ILogger<EnterprisePaymentProcessor> _enterpriseLogger;

    public async Task<PaymentResult> ProcessEnterprisePaymentAsync(Payment payment,
        int orderId)
    {
        // Validate payment from AdvancedPaymentBase
        if (!await ValidatePaymentAsync(payment.Amount)) return new PaymentResult(false, "Invalid payment amount");

        // Log attempt from BasePaymentProcessor  
        await LogPaymentAttemptAsync("enterprise", payment.Amount);

        _enterpriseLogger.LogInformation("Processing enterprise payment for order {OrderId}", orderId);
        await Task.Delay(30);

        return new PaymentResult(true, $"Enterprise payment processed for order: {orderId}");
    }
}

#endregion

#region Factory Pattern with Unregistered Services

/// <summary>
///     Factory that creates unregistered services manually
///     Demonstrates controlled instantiation of unregistered services
/// </summary>
[Service(Lifetime.Singleton)]
public partial class UnregisteredServiceFactory : IUnregisteredServiceFactory
{
    [Inject] private readonly ILogger<UnregisteredServiceFactory> _logger;
    [Inject] private readonly IServiceProvider _serviceProvider;

    public ManualRegistrationService CreateManualRegistrationService()
    {
        _logger.LogDebug("Creating manual registration service via factory");

        // Manual constructor call with resolved dependencies
        var logger = _serviceProvider.GetRequiredService<ILogger<ManualRegistrationService>>();

        return new ManualRegistrationService(logger);
    }

    public TestHelperService CreateTestHelperService()
    {
        _logger.LogDebug("Creating test helper service via factory");

        var logger = _serviceProvider.GetRequiredService<ILogger<TestHelperService>>();

        return new TestHelperService(logger);
    }

    public UnregisteredLegacyPaymentProcessor CreateLegacyProcessor()
    {
        _logger.LogDebug("Creating legacy payment processor via factory");

        var logger = _serviceProvider.GetRequiredService<ILogger<UnregisteredLegacyPaymentProcessor>>();

        return new UnregisteredLegacyPaymentProcessor(logger);
    }
}

#endregion

#region Interface Definitions for Unregistered Services

public interface IManualRegistrationService
{
    Task ProcessAsync(string message);
}

public interface ILegacyPaymentProcessor
{
    Task<PaymentResult> ProcessLegacyPaymentAsync(Payment payment);
}

public interface INewPaymentProcessor
{
    Task<PaymentResult> ProcessNewPaymentAsync(Payment payment);
}

public interface IEnterprisePaymentProcessor
{
    Task<PaymentResult> ProcessEnterprisePaymentAsync(Payment payment,
        int orderId);
}

public interface ITestHelperService
{
    Task<bool> ValidateAndCreateUserAsync(string name);
}

public interface IUnregisteredServiceFactory
{
    ManualRegistrationService CreateManualRegistrationService();
    TestHelperService CreateTestHelperService();
    UnregisteredLegacyPaymentProcessor CreateLegacyProcessor();
}

#endregion