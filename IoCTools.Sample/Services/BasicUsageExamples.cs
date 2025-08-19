using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IoCTools.Sample.Services;

// CLEAN IMPLEMENTATION MATCHING BASIC-USAGE.MD DOCUMENTATION

// === 1. BASIC GREETING SERVICE EXAMPLE ===
public interface IGreetingService
{
    string GetGreeting(string name);
}

[Service] // ← This registers the service automatically
public partial class GreetingService : IGreetingService
{
    [Inject] private readonly ILogger<GreetingService> _logger; // ← Auto-injected dependency

    public string GetGreeting(string name)
    {
        _logger.LogInformation("Creating greeting for {Name}", name);
        return $"Hello, {name}!";
    }
}

// === 2. SERVICE WITH MULTIPLE DEPENDENCIES ===
public interface IOrderService
{
    Task ProcessOrderAsync(Order order);
}

public interface IPaymentService
{
    Task<PaymentResult> ProcessPaymentAsync(Payment payment);
}

public interface IEmailService
{
    Task SendConfirmationAsync(string email);
    Task SendEmailAsync(string to, string subject, string body);
}

[Service]
public partial class OrderService : IOrderService
{
    [Inject] private readonly IEmailService _emailService;
    [Inject] private readonly ILogger<OrderService> _logger;
    [Inject] private readonly IPaymentService _paymentService;

    public async Task ProcessOrderAsync(Order order)
    {
        _logger.LogInformation("Processing order {OrderId}", order.Id);

        var paymentResult = await _paymentService.ProcessPaymentAsync(order.Payment);
        if (paymentResult.Success) await _emailService.SendConfirmationAsync(order.CustomerEmail);
    }
}

[Service]
public partial class PaymentService : IPaymentService
{
    [Inject] private readonly ILogger<PaymentService> _logger;

    public async Task<PaymentResult> ProcessPaymentAsync(Payment payment)
    {
        _logger.LogInformation("Processing payment of ${Amount}", payment.Amount);
        await Task.Delay(100); // Simulate processing
        return new PaymentResult(true, "Payment processed");
    }
}

[Service]
public partial class EmailService : IEmailService
{
    [Inject] private readonly ILogger<EmailService> _logger;

    public async Task SendConfirmationAsync(string email)
    {
        _logger.LogInformation("Sending confirmation to {Email}", email);
        await Task.Delay(50); // Simulate sending
    }

    public async Task SendEmailAsync(string to, string subject, string body)
    {
        _logger.LogInformation("Sending email to {To} with subject: {Subject}", to, subject);
        await Task.Delay(50); // Simulate sending
    }
}

// === 3. SERVICE WITH LIFETIME CONTROL ===
public interface ICacheService
{
    T GetOrSet<T>(string key,
        Func<T> factory);
}

[Service(Lifetime.Singleton)] // ← Override default Scoped lifetime
public partial class CacheService : ICacheService
{
    [Inject] private readonly IMemoryCache _cache;

    public T GetOrSet<T>(string key,
        Func<T> factory)
    {
        return _cache.GetOrCreate(key, _ => factory());
    }
}

// === 4. SERVICE WITHOUT INTERFACE ===
[Service] // ← Registers as concrete type only
public partial class BackgroundTaskService
{
    [Inject] private readonly IServiceProvider _serviceProvider;

    public async Task ProcessTasksAsync()
    {
        // Implementation
        await Task.Delay(100);
    }
}

// === 5. ADVANCED FIELD INJECTION PATTERNS ===
public interface INotificationService
{
    Task SendNotificationAsync(string message);
}

[Service]
public partial class EmailNotificationService : INotificationService
{
    [Inject] private readonly ILogger<EmailNotificationService> _logger;

    public async Task SendNotificationAsync(string message)
    {
        _logger.LogInformation("Email notification: {Message}", message);
        await Task.Delay(10);
    }
}

[Service]
public partial class SmsNotificationService : INotificationService
{
    [Inject] private readonly ILogger<SmsNotificationService> _logger;

    public async Task SendNotificationAsync(string message)
    {
        _logger.LogInformation("SMS notification: {Message}", message);
        await Task.Delay(10);
    }
}

public interface IAdvancedInjectionService
{
    Task DemonstrateAdvancedPatternsAsync();
}

[Service]
public partial class AdvancedInjectionService : IAdvancedInjectionService
{
    // Cache service for demonstration
    [Inject] private readonly ICacheService _cacheService;

    // Direct service injection (for factory pattern demo)
    [Inject] private readonly IGreetingService _greetingService;

    [Inject] private readonly ILogger<AdvancedInjectionService> _logger;

    // Collection injection - gets all registered INotificationService implementations
    [Inject] private readonly IEnumerable<INotificationService> _notificationServices;

    // Service provider for manual service resolution
    [Inject] private readonly IServiceProvider _serviceProvider;

    public async Task DemonstrateAdvancedPatternsAsync()
    {
        _logger.LogInformation("Demonstrating advanced injection patterns");

        // 1. Collection injection - Use all registered INotificationService implementations
        _logger.LogInformation("Testing collection injection with {Count} notification services",
            _notificationServices.Count());
        foreach (var notification in _notificationServices)
            await notification.SendNotificationAsync("Advanced pattern demo");

        // 2. Direct service injection
        var greeting = _greetingService.GetGreeting("Advanced Pattern User");
        _logger.LogInformation("Direct injection result: {Greeting}", greeting);

        // 3. Service usage through injected dependency
        _logger.LogInformation("Testing cache service injection");
        var cachedValue = _cacheService.GetOrSet("advanced-test", () => "advanced-cache-value");
        _logger.LogInformation("Cache service result: {CachedValue}", cachedValue);

        // 4. Manual service resolution using IServiceProvider
        var manualService = _serviceProvider.GetService<IPaymentService>();
        if (manualService != null)
        {
            var result = await manualService.ProcessPaymentAsync(new Payment(10.00m));
            _logger.LogInformation("Manual resolution result: {Result}", result.Message);
        }

        _logger.LogInformation("Advanced patterns demonstration completed");
    }
}

// === DATA MODELS ===
public record Order(int Id, string CustomerEmail, Payment Payment);

public record Payment(decimal Amount);

public record PaymentResult(bool Success, string Message);