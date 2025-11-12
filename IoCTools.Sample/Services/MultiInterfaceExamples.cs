namespace IoCTools.Sample.Services;

using System.Collections.Concurrent;
using System.Diagnostics;

using Abstractions.Annotations;
using Abstractions.Enumerations;

using Interfaces;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// === MULTI-INTERFACE REGISTRATION EXAMPLES ===
// This file demonstrates all aspects of multi-interface registration using the RegisterAsAll attribute

// === 1. BASIC MULTI-INTERFACE SERVICES ===

// User management service with multiple responsibilities
public interface IMultiUserService
{
    Task<User> GetUserAsync(int userId);

    Task<User> CreateUserAsync(string name,
        string email);
}

public interface IMultiUserRepository
{
    Task<User> FindByIdAsync(int id);
    Task<User> SaveAsync(User user);
    Task<IEnumerable<User>> GetAllAsync();
}

public interface IMultiUserValidator
{
    bool IsValidEmail(string email);
    bool IsValidName(string name);
    ValidationResult ValidateUser(User user);
}

// Example 1: RegisterAsAll with All mode (default) - registers concrete type AND all interfaces
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class UserService : IMultiUserService, IMultiUserRepository, IMultiUserValidator
{
    [Inject] private readonly ILogger<UserService> _logger;
    private readonly ConcurrentDictionary<int, User> _users = new();
    private int _nextId = 1;

    public async Task<User> FindByIdAsync(int id)
    {
        await Task.Delay(10); // Simulate async operation
        return _users.GetValueOrDefault(id) ?? throw new KeyNotFoundException($"User {id} not found");
    }

    public async Task<User> SaveAsync(User user)
    {
        await Task.Delay(10); // Simulate async operation
        _users[user.Id] = user;
        _logger.LogInformation("Saved user {UserId}: {UserName}", user.Id, user.Name);
        return user;
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        await Task.Delay(10); // Simulate async operation
        return _users.Values.ToList();
    }

    public async Task<User> GetUserAsync(int userId)
    {
        _logger.LogInformation("Getting user {UserId}", userId);
        return await FindByIdAsync(userId);
    }

    public async Task<User> CreateUserAsync(string name,
        string email)
    {
        if (!IsValidName(name) || !IsValidEmail(email))
            throw new ArgumentException("Invalid user data");

        var user = new User(_nextId++, name, email);
        return await SaveAsync(user);
    }

    public bool IsValidEmail(string email) => !string.IsNullOrWhiteSpace(email) && email.Contains('@');

    public bool IsValidName(string name) => !string.IsNullOrWhiteSpace(name) && name.Length >= 2;

    public ValidationResult ValidateUser(User user)
    {
        var errors = new List<string>();
        if (!IsValidName(user.Name)) errors.Add("Invalid name");
        if (!IsValidEmail(user.Email)) errors.Add("Invalid email");
        return new ValidationResult(errors.Count == 0, errors);
    }
}

// === 2. PAYMENT PROCESSING WITH DIFFERENT REGISTRATION MODES ===

public interface IMultiPaymentService
{
    Task<PaymentResult> ProcessPaymentAsync(Payment payment);
}

public interface IMultiPaymentValidator
{
    bool ValidatePayment(Payment payment);
    bool ValidateAmount(decimal amount);
}

public interface IMultiPaymentLogger
{
    void LogPayment(Payment payment,
        PaymentResult result);

    void LogFailure(Payment payment,
        string error);
}

// Example 2: DirectOnly mode - registers only the concrete type, not interfaces
[RegisterAsAll(RegistrationMode.DirectOnly)]
public partial class DirectOnlyPaymentProcessor : IMultiPaymentService, IMultiPaymentValidator, IMultiPaymentLogger
{
    [Inject] private readonly ILogger<DirectOnlyPaymentProcessor> _logger;

    public void LogPayment(Payment payment,
        PaymentResult result) => _logger.LogInformation("Payment {Amount:C} - {Status}", payment.Amount,
        result.Success ? "Success" : "Failed");

    public void LogFailure(Payment payment,
        string error) => _logger.LogWarning("Payment {Amount:C} failed: {Error}", payment.Amount, error);

    public async Task<PaymentResult> ProcessPaymentAsync(Payment payment)
    {
        _logger.LogInformation("Processing payment directly");
        if (!ValidatePayment(payment))
            return new PaymentResult(false, "Invalid payment");

        await Task.Delay(50);
        var result = new PaymentResult(true, "Payment processed");
        LogPayment(payment, result);
        return result;
    }

    public bool ValidatePayment(Payment payment) => ValidateAmount(payment.Amount);

    public bool ValidateAmount(decimal amount) => amount > 0 && amount <= 10000;
}

// Example 3: Exclusionary mode - registers only interfaces, not the concrete type
[RegisterAsAll(RegistrationMode.Exclusionary, InstanceSharing.Shared)]
public partial class InterfaceOnlyPaymentProcessor : IMultiPaymentService, IMultiPaymentValidator, IMultiPaymentLogger
{
    [Inject] private readonly ILogger<InterfaceOnlyPaymentProcessor> _logger;

    public void LogPayment(Payment payment,
        PaymentResult result)
    {
        _logger.LogInformation("Interface payment {Amount:C} - {Status}", payment.Amount,
            result.Success ? "Success" : "Failed");
    }

    public void LogFailure(Payment payment,
        string error) => _logger.LogWarning("Interface payment {Amount:C} failed: {Error}", payment.Amount, error);

    public async Task<PaymentResult> ProcessPaymentAsync(Payment payment)
    {
        _logger.LogInformation("Processing payment via interface");
        if (!ValidatePayment(payment))
        {
            LogFailure(payment, "Validation failed");
            return new PaymentResult(false, "Invalid payment");
        }

        await Task.Delay(50);
        var result = new PaymentResult(true, "Payment processed");
        LogPayment(payment, result);
        return result;
    }

    public bool ValidatePayment(Payment payment) => ValidateAmount(payment.Amount);

    public bool ValidateAmount(decimal amount) => amount > 0 && amount <= 10000;
}

// === 3. INSTANCE SHARING EXAMPLES ===

public interface IMultiCacheService
{
    void Set<T>(string key,
        T value);

    T Get<T>(string key);
}

public interface IMultiCacheProvider
{
    bool Exists(string key);
    void Remove(string key);
    void Clear();
}

public interface IMultiCacheValidator
{
    bool IsValidKey(string key);
    bool IsValidValue<T>(T value);
}

// Example 4: Separate instances for each interface
[RegisterAsAll]
public partial class SeparateInstanceCacheManager : IMultiCacheService, IMultiCacheProvider, IMultiCacheValidator
{
    private readonly ConcurrentDictionary<string, object> _cache = new();
    private readonly Guid _instanceId = Guid.NewGuid();
    [Inject] private readonly ILogger<SeparateInstanceCacheManager> _logger;

    public SeparateInstanceCacheManager()
    {
        // This will be called for each separate instance
    }

    public bool Exists(string key) => _cache.ContainsKey(key);

    public void Remove(string key) => _cache.TryRemove(key, out _);

    public void Clear() => _cache.Clear();

    public void Set<T>(string key,
        T value)
    {
        _logger.LogDebug("Cache instance {InstanceId}: Setting {Key}", _instanceId, key);
        if (IsValidKey(key) && IsValidValue(value))
            _cache[key] = value!;
    }

    public T Get<T>(string key)
    {
        _logger.LogDebug("Cache instance {InstanceId}: Getting {Key}", _instanceId, key);
        return _cache.TryGetValue(key, out var value) ? (T)value : default!;
    }

    public bool IsValidKey(string key) => !string.IsNullOrWhiteSpace(key);

    public bool IsValidValue<T>(T value) => value != null;
}

// Example 5: Shared instance across all interfaces
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class SharedInstanceCacheManager : IMultiCacheService, IMultiCacheProvider, IMultiCacheValidator
{
    private readonly ConcurrentDictionary<string, object> _cache = new();
    private readonly Guid _instanceId = Guid.NewGuid();
    [Inject] private readonly ILogger<SharedInstanceCacheManager> _logger;

    public SharedInstanceCacheManager()
    {
        // This will be called only once since the instance is shared
        _logger.LogInformation("Creating shared cache instance {InstanceId}", _instanceId);
    }

    public bool Exists(string key) => _cache.ContainsKey(key);

    public void Remove(string key) => _cache.TryRemove(key, out _);

    public void Clear() => _cache.Clear();

    public void Set<T>(string key,
        T value)
    {
        _logger.LogDebug("Shared cache {InstanceId}: Setting {Key}", _instanceId, key);
        if (IsValidKey(key) && IsValidValue(value))
            _cache[key] = value!;
    }

    public T Get<T>(string key) => _cache.TryGetValue(key, out var value) ? (T)value : default!;

    public bool IsValidKey(string key) => !string.IsNullOrWhiteSpace(key);

    public bool IsValidValue<T>(T value) => value != null;
}

// === 4. SKIP REGISTRATION EXAMPLES ===

public interface IDataService
{
    Task<string> GetDataAsync(string id);
}

public interface IDataValidator
{
    bool ValidateId(string id);
}

public interface IDataLogger
{
    void LogAccess(string id);
}

public interface IDataCacheService
{
    void CacheData(string id,
        string data);
}

// Example 6: Skip specific interfaces using generic SkipRegistration
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
[SkipRegistration<IDataLogger>] // Skip IDataLogger interface registration
[SkipRegistration<IDataCacheService>] // Skip IDataCacheService interface registration
public partial class SelectiveDataService : IDataService, IDataValidator, IDataLogger, IDataCacheService
{
    [Inject] private readonly ILogger<SelectiveDataService> _logger;

    public void CacheData(string id,
        string data)
    {
        // Implementation would cache the data
        _logger.LogDebug("Caching data for {Id}", id);
    }

    public void LogAccess(string id) => _logger.LogInformation("Accessing data {Id}", id);

    public async Task<string> GetDataAsync(string id)
    {
        if (!ValidateId(id))
            throw new ArgumentException("Invalid ID");

        LogAccess(id);
        await Task.Delay(10);
        var data = $"Data for {id}";
        CacheData(id, data);
        return data;
    }

    public bool ValidateId(string id) => !string.IsNullOrWhiteSpace(id);
}

// === 5. REPOSITORY PATTERN EXAMPLES ===

public interface IMultiRepository<T>
{
    Task<T> GetByIdAsync(int id);
    Task<T> SaveAsync(T entity);
    Task DeleteAsync(int id);
}

public interface IMultiQueryable<T>
{
    Task<IEnumerable<T>> QueryAsync(Func<T, bool> predicate);
    Task<T> FirstOrDefaultAsync(Func<T, bool> predicate);
}

// Example 7: Generic repository with multiple interfaces (commented out due to DI container limitations)
// Note: Open generic types with RegisterAsAll require special handling not yet implemented
// // [RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class Repository<T> : IMultiRepository<T>, IMultiQueryable<T>, IDisposable where T : class
{
    private readonly ConcurrentDictionary<int, T> _entities = new();
    [Inject] private readonly ILogger<Repository<T>> _logger;
    private bool _disposed;

    public void Dispose()
    {
        if (!_disposed)
        {
            _logger.LogInformation("Disposing {EntityType} repository", typeof(T).Name);
            _entities.Clear();
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }

    public async Task<IEnumerable<T>> QueryAsync(Func<T, bool> predicate)
    {
        await Task.Delay(10);
        return _entities.Values.Where(predicate).ToList();
    }

    public async Task<T> FirstOrDefaultAsync(Func<T, bool> predicate)
    {
        await Task.Delay(10);
        return _entities.Values.FirstOrDefault(predicate)!;
    }

    public async Task<T> GetByIdAsync(int id)
    {
        _logger.LogInformation("Getting {EntityType} with ID {Id}", typeof(T).Name, id);
        await Task.Delay(5);
        return _entities.GetValueOrDefault(id)!;
    }

    public async Task<T> SaveAsync(T entity)
    {
        _logger.LogInformation("Saving {EntityType}", typeof(T).Name);
        await Task.Delay(5);
        // In a real implementation, you'd extract the ID from the entity
        var id = _entities.Count + 1;
        _entities[id] = entity;
        return entity;
    }

    public async Task DeleteAsync(int id)
    {
        _logger.LogInformation("Deleting {EntityType} with ID {Id}", typeof(T).Name, id);
        await Task.Delay(5);
        _entities.TryRemove(id, out _);
    }
}

// === 6. MIXED MULTI-INTERFACE WITH INHERITANCE ===

public interface IEmailNotificationService
{
    Task SendEmailAsync(string to,
        string subject,
        string body);
}

public interface ISmsNotificationService
{
    Task SendSmsAsync(string phoneNumber,
        string message);
}

public interface INotificationLogger
{
    void LogNotificationSent(string type,
        string recipient);
}

// Example 8: Multi-interface service (simplified without inheritance)
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class CompositeNotificationService : IEmailNotificationService, ISmsNotificationService,
    INotificationLogger
{
    [Inject] private readonly ILogger<CompositeNotificationService> _logger;

    public async Task SendEmailAsync(string to,
        string subject,
        string body)
    {
        _logger.LogInformation("Sending email to {To}: {Subject}", to, subject);
        await Task.Delay(20);
        LogNotificationSent("Email", to);
    }

    public void LogNotificationSent(string type,
        string recipient) =>
        _logger.LogInformation("Notification sent - Type: {Type}, Recipient: {Recipient}", type, recipient);

    public async Task SendSmsAsync(string phoneNumber,
        string message)
    {
        _logger.LogInformation("Sending SMS to {Phone}: {Message}", phoneNumber, message);
        await Task.Delay(15);
        LogNotificationSent("SMS", phoneNumber);
    }
}

// === 7. PERFORMANCE COMPARISON EXAMPLES ===

public interface IPerformanceTestService
{
    Task<string> ProcessDataAsync(string data);
}

public interface IPerformanceMetrics
{
    TimeSpan GetAverageProcessingTime();
    long GetTotalProcessedItems();
}

public interface IPerformanceBenchmark
{
    Task<BenchmarkResult> RunBenchmarkAsync(int iterations);
}

// Example 9: Performance-focused service with detailed metrics
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class PerformanceTestService : IPerformanceTestService, IPerformanceMetrics, IPerformanceBenchmark
{
    [Inject] private readonly ILogger<PerformanceTestService> _logger;
    private readonly ConcurrentBag<TimeSpan> _processingTimes = new();
    private long _totalProcessed;

    public async Task<BenchmarkResult> RunBenchmarkAsync(int iterations)
    {
        _logger.LogInformation("Running benchmark with {Iterations} iterations", iterations);
        var overallStopwatch = Stopwatch.StartNew();

        var tasks = new List<Task<string>>();
        for (var i = 0; i < iterations; i++) tasks.Add(ProcessDataAsync($"benchmark-item-{i}"));

        await Task.WhenAll(tasks);
        overallStopwatch.Stop();

        return new BenchmarkResult(
            iterations,
            overallStopwatch.Elapsed,
            GetAverageProcessingTime(),
            GetTotalProcessedItems()
        );
    }

    public TimeSpan GetAverageProcessingTime()
    {
        var times = _processingTimes.ToArray();
        return times.Length > 0 ? new TimeSpan((long)times.Average(t => t.Ticks)) : TimeSpan.Zero;
    }

    public long GetTotalProcessedItems() => _totalProcessed;

    public async Task<string> ProcessDataAsync(string data)
    {
        var stopwatch = Stopwatch.StartNew();

        // Simulate processing
        await Task.Delay(Random.Shared.Next(1, 10));
        var result = $"Processed: {data}";

        stopwatch.Stop();
        _processingTimes.Add(stopwatch.Elapsed);
        Interlocked.Increment(ref _totalProcessed);

        return result;
    }
}

// === 8. DEMONSTRATION SERVICE ===

public interface IMultiInterfaceDemoService
{
    Task RunDemonstrationAsync();
}

// Example 10: Service that demonstrates all the multi-interface patterns
[Scoped]
public partial class MultiInterfaceDemonstrationService : IMultiInterfaceDemoService
{
    [Inject] private readonly ILogger<MultiInterfaceDemonstrationService> _logger;
    [Inject] private readonly IServiceProvider _serviceProvider;

    public async Task RunDemonstrationAsync()
    {
        _logger.LogInformation("=== Multi-Interface Registration Demonstration ===");

        // Demonstrate shared instance behavior
        await DemonstrateSharedInstances();

        // Demonstrate separate instance behavior  
        await DemonstrateSeparateInstances();

        // Demonstrate different registration modes
        await DemonstrateRegistrationModes();

        // Demonstrate skip registration
        await DemonstrateSkipRegistration();

        // Demonstrate repository pattern
        await DemonstrateRepositoryPattern();

        // Demonstrate performance testing
        await DemonstratePerformanceTesting();

        _logger.LogInformation("=== Multi-Interface Registration Demonstration Complete ===");
    }

    private async Task DemonstrateSharedInstances()
    {
        _logger.LogInformation("--- Demonstrating Shared Instances ---");

        var cacheService = _serviceProvider.GetRequiredService<IMultiCacheService>();
        var cacheProvider = _serviceProvider.GetRequiredService<IMultiCacheProvider>();

        cacheService.Set("test", "value");
        var exists = cacheProvider.Exists("test");

        _logger.LogInformation("Shared instance test: Key exists = {Exists}", exists);
    }

    private async Task DemonstrateSeparateInstances()
    {
        _logger.LogInformation("--- Demonstrating Separate Instances ---");

        // With separate instances, data won't be shared between interfaces
        var separateCacheService = _serviceProvider.GetRequiredService<SeparateInstanceCacheManager>();
        _logger.LogInformation("Created separate cache instance for demonstration");
    }

    private async Task DemonstrateRegistrationModes()
    {
        _logger.LogInformation("--- Demonstrating Registration Modes ---");

        // DirectOnly - can resolve concrete type but not interfaces
        var directOnly = _serviceProvider.GetService<DirectOnlyPaymentProcessor>();
        _logger.LogInformation("DirectOnly registration: Concrete type available = {Available}", directOnly != null);

        // Exclusionary - can resolve interfaces but not concrete type
        var interfacePayment = _serviceProvider.GetService<IPaymentService>();
        _logger.LogInformation("Exclusionary registration: Interface available = {Available}",
            interfacePayment != null);
    }

    private async Task DemonstrateSkipRegistration()
    {
        _logger.LogInformation("--- Demonstrating Skip Registration ---");

        var dataService = _serviceProvider.GetService<IDataService>();
        var dataLogger = _serviceProvider.GetService<IDataLogger>();

        _logger.LogInformation("Skip registration test: IDataService = {DataService}, IDataLogger = {DataLogger}",
            dataService != null, dataLogger != null);
    }

    private async Task DemonstrateRepositoryPattern()
    {
        _logger.LogInformation("--- Demonstrating Repository Pattern ---");

        var userRepo = _serviceProvider.GetService<IMultiRepository<User>>();
        var userQueryable = _serviceProvider.GetService<IMultiQueryable<User>>();

        if (userRepo != null && userQueryable != null)
        {
            var user = new User(1, "Test User", "test@example.com");
            await userRepo.SaveAsync(user);
            var found = await userQueryable.FirstOrDefaultAsync(u => u.Name == "Test User");
            _logger.LogInformation("Repository pattern test: User found = {Found}", found?.Name);
        }
    }

    private async Task DemonstratePerformanceTesting()
    {
        _logger.LogInformation("--- Demonstrating Performance Testing ---");

        var perfService = _serviceProvider.GetService<IPerformanceBenchmark>();
        if (perfService != null)
        {
            var result = await perfService.RunBenchmarkAsync(10);
            _logger.LogInformation("Performance test: {Iterations} iterations in {TotalTime}",
                result.Iterations, result.TotalTime);
        }
    }
}

// === SELECTIVE INTERFACE REGISTRATION WITH RegisterAs ===
// These examples demonstrate the new RegisterAs attribute for selective interface registration

// Interfaces for selective registration example
public interface ISelectiveService1
{
    Task<string> ProcessAsync(string input);
}

public interface ISelectiveService2
{
    void LogOperation(string operation);
}

public interface ISelectiveService3
{
    bool Validate(string input);
}

// Example 1: Register as only two specific interfaces (Scoped by default)
[RegisterAs<ISelectiveService1, ISelectiveService2>]
public partial class SelectiveRegistrationService : ISelectiveService1, ISelectiveService2, ISelectiveService3
{
    [Inject] private readonly ILogger<SelectiveRegistrationService> _logger;

    public async Task<string> ProcessAsync(string input)
    {
        _logger.LogInformation("Processing: {Input}", input);
        await Task.Delay(10);
        return $"Processed: {input}";
    }

    public void LogOperation(string operation) => _logger.LogInformation("Operation: {Operation}", operation);

    public bool Validate(string input) => !string.IsNullOrEmpty(input);
}

// Example 2: Register as single interface only
[Transient]
[RegisterAs<ISelectiveService1>] // Only registers for this one interface
public partial class SingleInterfaceService : ISelectiveService1, ISelectiveService2, ISelectiveService3
{
    [Inject] private readonly ILogger<SingleInterfaceService> _logger;

    public async Task<string> ProcessAsync(string input)
    {
        _logger.LogInformation("Single interface processing: {Input}", input);
        await Task.Delay(5);
        return $"Single: {input}";
    }

    public void LogOperation(string operation) =>
        _logger.LogInformation("Single interface operation: {Operation}", operation);

    public bool Validate(string input) => input?.Length > 3;
}

// Example 3: Complex selective registration with configuration injection
public interface IConfigurableService
{
    Task<string> ExecuteAsync(string data);
    string GetConfiguration();
}

public interface ISelectiveAuditService
{
    void LogExecution(string data,
        string result);
}

[Singleton]
[RegisterAs<IConfigurableService, ISelectiveAuditService>]
public partial class ConfigurableSelectiveService : IConfigurableService, ISelectiveAuditService, ISelectiveService3
{
    [InjectConfiguration("Features:SelectiveService")]
    private readonly string _configValue;

    [Inject] private readonly ILogger<ConfigurableSelectiveService> _logger;

    public async Task<string> ExecuteAsync(string data)
    {
        _logger.LogInformation("Executing with config {Config}: {Data}", _configValue, data);
        await Task.Delay(15);
        var result = $"Config:{_configValue} Data:{data}";
        LogExecution(data, result);
        return result;
    }

    public string GetConfiguration() => _configValue ?? "default";

    public void LogExecution(string data,
        string result) => _logger.LogInformation("Audit: Input={Input}, Output={Output}", data, result);

    public bool Validate(string input) => !string.IsNullOrWhiteSpace(input);
}

// === DATA MODELS ===

// BenchmarkResult is specific to this file
public record BenchmarkResult(int Iterations, TimeSpan TotalTime, TimeSpan AverageTime, long TotalProcessed);
