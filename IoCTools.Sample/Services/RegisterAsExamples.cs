namespace IoCTools.Sample.Services;

using Abstractions.Annotations;
using Abstractions.Enumerations;

using Microsoft.Extensions.Logging;

// ===============================================
// REGISTERAS<T1, T2, T3> ATTRIBUTE EXAMPLES
// ===============================================
// The RegisterAs attribute provides selective interface registration,
// allowing fine-grained control over which interfaces are registered
// for dependency injection (as an alternative to RegisterAsAll).

// === 1. BASIC REGISTERAS EXAMPLES ===

public interface IRegisterAsUserService
{
    void ProcessUser();
}

public interface IRegisterAsEmailService
{
    Task SendEmailAsync(string to,
        string subject,
        string body);

    Task SendConfirmationAsync(string email);
}

public interface IRegisterAsValidationService
{
    bool ValidateUser(string user);
}

public interface IRegisterAsAuditService
{
    Task LogActionAsync(string action,
        string details);
}

// RegisterAs<T1> - Single interface registration
[RegisterAs<IRegisterAsUserService>] // Only IRegisterAsUserService is registered (defaults to Scoped)
public partial class BasicUserService : IRegisterAsUserService, IRegisterAsEmailService, IRegisterAsValidationService
{
    [Inject] private readonly ILogger<BasicUserService> _logger;

    public Task SendEmailAsync(string to,
        string subject,
        string body) => Task.CompletedTask;

    public Task SendConfirmationAsync(string email) => Task.CompletedTask;

    public void ProcessUser() => _logger.LogInformation("Processing user");
    public bool ValidateUser(string user) => true;
}

// RegisterAs<T1, T2> - Two interfaces registration
[RegisterAs<IRegisterAsUserService, IRegisterAsEmailService>] // Default Scoped lifetime inferred
public partial class UserEmailService : IRegisterAsUserService, IRegisterAsEmailService, IRegisterAsValidationService
{
    [Inject] private readonly ILogger<UserEmailService> _logger;

    public Task SendEmailAsync(string to,
        string subject,
        string body) => Task.CompletedTask;

    public Task SendConfirmationAsync(string email) => Task.CompletedTask;

    public void ProcessUser() => _logger.LogInformation("Processing user with email");
    public bool ValidateUser(string user) => true;
}

// RegisterAs<T1, T2, T3> - Three interfaces registration
[RegisterAs<IRegisterAsUserService, IRegisterAsEmailService, IRegisterAsValidationService>] // All three registered
public partial class FullUserService : IRegisterAsUserService, IRegisterAsEmailService, IRegisterAsValidationService,
    IRegisterAsAuditService
{
    [Inject] private readonly ILogger<FullUserService> _logger;

    public Task LogActionAsync(string action,
        string details) => Task.CompletedTask;

    public Task SendEmailAsync(string to,
        string subject,
        string body) => Task.CompletedTask;

    public Task SendConfirmationAsync(string email) => Task.CompletedTask;

    public void ProcessUser() => _logger.LogInformation("Processing full user service");
    public bool ValidateUser(string user) => true;
}

// === 2. REGISTERAS WITH DIFFERENT LIFETIMES ===

[Singleton]
[RegisterAs<IRegisterAsUserService>]
public partial class SingletonUserService : IRegisterAsUserService, IRegisterAsEmailService
{
    [Inject] private readonly ILogger<SingletonUserService> _logger;

    public Task SendEmailAsync(string to,
        string subject,
        string body) => Task.CompletedTask;

    public Task SendConfirmationAsync(string email) => Task.CompletedTask;

    public void ProcessUser() => _logger.LogInformation("Singleton user processing");
}

[Transient]
[RegisterAs<IRegisterAsEmailService, IRegisterAsValidationService>]
public partial class TransientEmailValidationService : IRegisterAsEmailService, IRegisterAsValidationService,
    IRegisterAsAuditService
{
    [Inject] private readonly ILogger<TransientEmailValidationService> _logger;

    public Task LogActionAsync(string action,
        string details) => Task.CompletedTask;

    public Task SendEmailAsync(string to,
        string subject,
        string body) => Task.CompletedTask;

    public Task SendConfirmationAsync(string email) => Task.CompletedTask;
    public bool ValidateUser(string user) => true;
}

// === 3. REGISTERAS WITH MANUALSERVICE (E.G., DBCONTEXT PATTERN) ===

public interface ITransactionService
{
    void BeginTransaction();
}

public interface IRepository
{
    void SaveChanges();
}

// ManualService + RegisterAs: Registers only interfaces, not the concrete class
// Concrete class won't be registered
[RegisterAs<ITransactionService, IRepository>] // But these interfaces will be
public partial class DatabaseContext : ITransactionService, IRepository
{
    [Inject] private readonly ILogger<DatabaseContext> _logger;
    public void SaveChanges() => _logger.LogInformation("Saving changes");

    public void BeginTransaction() => _logger.LogInformation("Beginning transaction");
}

// === 4. REGISTERAS WITH MULTIPLE INTERFACES (UP TO 8) ===

public interface IService1
{
    void Method1();
}

public interface IService2
{
    void Method2();
}

public interface IService3
{
    void Method3();
}

public interface IService4
{
    void Method4();
}

public interface IService5
{
    void Method5();
}

public interface IService6
{
    void Method6();
}

public interface IService7
{
    void Method7();
}

public interface IService8
{
    void Method8();
}

public interface IService9
{
    void Method9();
} // This won't be registered

[RegisterAs<IService1, IService2, IService3, IService4, IService5, IService6, IService7, IService8>]
public partial class MaxInterfaceService : IService1, IService2, IService3, IService4, IService5, IService6, IService7,
    IService8, IService9
{
    [Inject] private readonly ILogger<MaxInterfaceService> _logger;

    public void Method1() => _logger.LogInformation("Method1 called");
    public void Method2() => _logger.LogInformation("Method2 called");
    public void Method3() => _logger.LogInformation("Method3 called");
    public void Method4() => _logger.LogInformation("Method4 called");
    public void Method5() => _logger.LogInformation("Method5 called");
    public void Method6() => _logger.LogInformation("Method6 called");
    public void Method7() => _logger.LogInformation("Method7 called");
    public void Method8() => _logger.LogInformation("Method8 called");
    public void Method9() => _logger.LogInformation("Method9 called - not registered");
}

// === 5. REGISTERAS WITH CONFIGURATION INJECTION ===

public interface IRegisterAsConfigurableService
{
    void ProcessWithConfig();
}

public interface IRegisterAsNotificationService
{
    void SendNotification();
}

public class NotificationConfig
{
    public string SmtpServer { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
}

[RegisterAs<IRegisterAsConfigurableService, IRegisterAsNotificationService>]
public partial class ConfigurableNotificationService : IRegisterAsConfigurableService, IRegisterAsNotificationService
{
    [InjectConfiguration("Notification")] // Automatic configuration binding
    private readonly NotificationConfig _config;

    [Inject] private readonly ILogger<ConfigurableNotificationService> _logger;

    public void ProcessWithConfig() => _logger.LogInformation("Processing with config");

    public void SendNotification() =>
        _logger.LogInformation("Sending via {Server}:{Port}", _config.SmtpServer, _config.Port);
}

// === 6. REGISTERAS VS REGISTERASALL COMPARISON ===

public interface ICompareService1
{
    void Execute();
}

public interface ICompareService2
{
    void Process();
}

public interface ICompareService3
{
    void Handle();
}

// RegisterAsAll approach - registers ALL interfaces
[RegisterAsAll]
public partial class RegisterAsAllExample : ICompareService1, ICompareService2, ICompareService3
{
    [Inject] private readonly ILogger<RegisterAsAllExample> _logger;

    public void Execute() => _logger.LogInformation("Execute called");
    public void Process() => _logger.LogInformation("Process called");
    public void Handle() => _logger.LogInformation("Handle called");

    // Registers: RegisterAsAllExample, ICompareService1, ICompareService2, ICompareService3
}

// RegisterAs approach - selective registration
[RegisterAs<ICompareService1, ICompareService2>] // Only registers these two
public partial class RegisterAsExample : ICompareService1, ICompareService2, ICompareService3
{
    [Inject] private readonly ILogger<RegisterAsExample> _logger;

    public void Execute() => _logger.LogInformation("Selective Execute called");
    public void Process() => _logger.LogInformation("Selective Process called");
    public void Handle() => _logger.LogInformation("Selective Handle called");

    // Registers: RegisterAsExample, ICompareService1, ICompareService2
    // Does NOT register: ICompareService3
}

/*
REGISTERAS<T1, T2, T3> SUMMARY:

RegisterAs<T1, T2, T3> provides precise control over interface registration:

1. Use RegisterAs<T> when you want only specific interfaces registered
2. Use RegisterAsAll when you want all implemented interfaces registered
3. Combine with ManualService for DbContext-like scenarios
4. Supports 1-8 generic type parameters for maximum flexibility
5. Works with all service lifetimes (Singleton, Scoped, Transient)
6. Integrates with configuration injection and inheritance
7. Provides comprehensive compile-time validation via diagnostics

Example registrations generated:
- services.AddScoped<BasicUserService>()
- services.AddScoped<IUserService>(provider => provider.GetRequiredService<BasicUserService>())
- // IEmailService and IValidationService are NOT registered
*/

// ===============================================
// REGISTERAS<T> INSTANCESHARING EXAMPLES
// ===============================================
// NEW: RegisterAs now supports InstanceSharing parameter for advanced scenarios
// This enables sophisticated service registration patterns including EF Core integration

// === InstanceSharing.Separate (Default Behavior) ===
// Each interface gets a separate service registration - different instances

public interface IRegistrationService
{
    void Register(string item);
}

public interface IValidationServiceSeparate
{
    bool Validate(string item);
}

// Default behavior: InstanceSharing.Separate creates separate instances without explicit lifetime
[RegisterAs<IRegistrationService, IValidationServiceSeparate>] // Implicit InstanceSharing.Separate
public partial class SeparateInstanceService : IRegistrationService, IValidationServiceSeparate
{
    private readonly Guid _instanceId = Guid.NewGuid();
    [Inject] private readonly ILogger<SeparateInstanceService> _logger;

    public void Register(string item) =>
        _logger.LogInformation("Registering {Item} in instance {Id}", item, _instanceId);

    public bool Validate(string item) => true;
}

// Explicit InstanceSharing.Separate
[Transient]
[RegisterAs<IRegistrationService, IValidationServiceSeparate>]
public partial class ExplicitSeparateService : IRegistrationService, IValidationServiceSeparate
{
    private readonly Guid _instanceId = Guid.NewGuid();
    [Inject] private readonly ILogger<ExplicitSeparateService> _logger;

    public void Register(string item) => _logger.LogInformation("Explicit separate {Item} in {Id}", item, _instanceId);
    public bool Validate(string item) => true;
}

// === InstanceSharing.Shared - Same Instance for All Interfaces ===
// All interfaces resolve to the same service instance - shared state

public interface ISharedCacheService
{
    void CacheItem(string key,
        object value);

    T GetItem<T>(string key);
}

public interface ISharedStatsService
{
    void IncrementCounter(string name);
    int GetCount(string name);
}

public interface ISharedHealthService
{
    bool IsHealthy();
    void ReportHealth(bool healthy);
}

// InstanceSharing.Shared: ALL interfaces get the SAME instance - shared state
[Singleton] // Explicit lifetime required for shared instances with state
[RegisterAs<ISharedCacheService, ISharedStatsService, ISharedHealthService>(InstanceSharing.Shared)]
public partial class SharedStateService : ISharedCacheService, ISharedStatsService, ISharedHealthService
{
    private readonly Dictionary<string, object> _cache = new();
    private readonly Dictionary<string, int> _counters = new();
    private readonly Guid _instanceId = Guid.NewGuid();
    [Inject] private readonly ILogger<SharedStateService> _logger;
    private bool _isHealthy = true;

    // All three interfaces share this same state
    public void CacheItem(string key,
        object value)
    {
        _cache[key] = value;
        _logger.LogInformation("Cached {Key} in shared instance {Id}", key, _instanceId);
    }

    public T GetItem<T>(string key) => _cache.ContainsKey(key) ? (T)_cache[key] : default;

    public bool IsHealthy() => _isHealthy;
    public void ReportHealth(bool healthy) => _isHealthy = healthy;

    public void IncrementCounter(string name)
    {
        _counters[name] = _counters.GetValueOrDefault(name, 0) + 1;
        _logger.LogInformation("Counter {Name} = {Count} in shared instance {Id}", name, _counters[name], _instanceId);
    }

    public int GetCount(string name) => _counters.GetValueOrDefault(name, 0);
}

// === EF Core DbContext Integration Pattern ===
// RegisterAs-only services with InstanceSharing.Shared for external registration

public interface IDbTransactionService
{
    void BeginTransaction();
    void CommitTransaction();
    void RollbackTransaction();
}

public interface IDbDataService
{
    Task<T> ExecuteQueryAsync<T>(string sql);
    Task<int> ExecuteCommandAsync(string sql);
}

// EF Core Integration: Service registered elsewhere (AddDbContext), interfaces use factory pattern
[RegisterAs<IDbTransactionService, IDbDataService>(InstanceSharing.Shared)]
public class ApplicationDbContext : IDbTransactionService, IDbDataService
{
    public Task<T> ExecuteQueryAsync<T>(string sql) => Task.FromResult(default(T));

    public Task<int> ExecuteCommandAsync(string sql) => Task.FromResult(0);
    // NOTE: No [Scoped] attribute - DbContext lifetime managed by EF Core
    // This generates ONLY interface factory registrations:
    // services.AddScoped<IDbTransactionService>(provider => provider.GetRequiredService<ApplicationDbContext>());
    // services.AddScoped<IDbDataService>(provider => provider.GetRequiredService<ApplicationDbContext>());
    // Concrete class registration handled by services.AddDbContext<ApplicationDbContext>()

    public void BeginTransaction() => Console.WriteLine("Transaction started");
    public void CommitTransaction() => Console.WriteLine("Transaction committed");
    public void RollbackTransaction() => Console.WriteLine("Transaction rolled back");
}

// === Advanced Multi-Interface Shared Pattern ===
// Demonstrates complex shared state across many interfaces

public interface IMetricsCollector
{
    void RecordMetric(string name,
        double value);
}

public interface IEventPublisher
{
    Task PublishAsync(string eventName,
        object data);
}

public interface IHealthReporter
{
    void ReportHealth(string component,
        bool healthy);
}

public interface IConfigurationWatcher
{
    void WatchConfiguration(string key,
        Action<string> callback);
}

[Singleton] // Explicit non-default lifetime + InstanceSharing.Shared = factory patterns
[RegisterAs<IMetricsCollector, IEventPublisher, IHealthReporter, IConfigurationWatcher>(InstanceSharing.Shared)]
public partial class ComprehensiveSharedService : IMetricsCollector, IEventPublisher, IHealthReporter,
    IConfigurationWatcher
{
    private readonly List<string> _events = new();
    private readonly Dictionary<string, bool> _healthStatus = new();
    [Inject] private readonly ILogger<ComprehensiveSharedService> _logger;
    private readonly Guid _sharedInstanceId = Guid.NewGuid();

    public void WatchConfiguration(string key,
        Action<string> callback) =>
        _logger.LogInformation("Watching config {Key} in shared instance {Id}", key, _sharedInstanceId);

    public Task PublishAsync(string eventName,
        object data)
    {
        _events.Add($"Event: {eventName}");
        _logger.LogInformation("Event {Event} published from shared instance {Id}", eventName, _sharedInstanceId);
        return Task.CompletedTask;
    }

    public void ReportHealth(string component,
        bool healthy)
    {
        _healthStatus[component] = healthy;
        _logger.LogInformation("Health {Component}={Healthy} in shared instance {Id}", component, healthy,
            _sharedInstanceId);
    }

    public void RecordMetric(string name,
        double value)
    {
        _logger.LogInformation("Metric {Name}={Value} recorded in shared instance {Id}", name, value,
            _sharedInstanceId);
        _events.Add($"Metric: {name}={value}");
    }
}
