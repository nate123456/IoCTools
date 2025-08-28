namespace IoCTools.Sample.Services;

using System.Collections.Concurrent;

using Abstractions.Annotations;

using Interfaces;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using ValidationResult = System.ComponentModel.DataAnnotations.ValidationResult;

// ===== INTERFACES =====

public interface IEntity
{
    int Id { get; set; }
    DateTime CreatedAt { get; set; }
}

// Note: Using simpler constraints since existing types may not implement all interfaces

public interface IRepository<T> where T : class, IEntity
{
    Task<T?> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<int> CreateAsync(T entity);
    Task<bool> UpdateAsync(T entity);
    Task<bool> DeleteAsync(int id);
}

public interface IGenericValidator<T> where T : class
{
    Task<System.ComponentModel.DataAnnotations.ValidationResult?> ValidateAsync(T entity);
}

public interface IProcessor<TInput, TOutput>
{
    Task<TOutput> ProcessAsync(TInput input);
}

public interface ICache<T> where T : class
{
    Task<T?> GetAsync(string key);

    Task SetAsync(string key,
        T value,
        TimeSpan? expiration = null);

    Task RemoveAsync(string key);
}

public interface IFactory<T> where T : class
{
    T Create();
    T Create(params object[] args);
}

// Note: Using existing User, Product, and other types from other service files to avoid duplicates

// ===== 1. BASIC GENERIC REPOSITORY PATTERN =====

/// <summary>
///     Example 1: Basic generic repository with type constraints
///     Demonstrates: Simple generics with class constraint and dependency injection
/// </summary>
[Scoped]
public partial class GenericRepository<T> : IRepository<T> where T : class, IEntity, new()
{
    // Simulated database storage
    private static readonly ConcurrentDictionary<Type, ConcurrentDictionary<int, object>> _storage = new();
    [Inject] private readonly IMemoryCache _cache;
    [Inject] private readonly ILogger<GenericRepository<T>> _logger;

    public async Task<T?> GetByIdAsync(int id)
    {
        _logger.LogInformation("Getting {EntityType} with ID: {Id}", typeof(T).Name, id);

        var typeStorage = GetTypeStorage();
        await Task.Delay(10); // Simulate async operation

        return typeStorage.TryGetValue(id, out var entity) ? (T)entity : null;
    }

    public async Task<IEnumerable<T>> GetAllAsync()
    {
        _logger.LogInformation("Getting all {EntityType} entities", typeof(T).Name);

        var typeStorage = GetTypeStorage();
        await Task.Delay(10);

        return typeStorage.Values.Cast<T>().ToList();
    }

    public async Task<int> CreateAsync(T entity)
    {
        _logger.LogInformation("Creating {EntityType}: {Entity}", typeof(T).Name, entity);

        // Set basic properties directly since T : IEntity
        entity.Id = GenerateId();
        entity.CreatedAt = DateTime.UtcNow;

        var typeStorage = GetTypeStorage();
        typeStorage[entity.Id] = entity;

        await Task.Delay(10);
        return entity.Id;
    }

    public async Task<bool> UpdateAsync(T entity)
    {
        _logger.LogInformation("Updating {EntityType} with ID: {Id}", typeof(T).Name, entity.Id);

        var typeStorage = GetTypeStorage();
        if (!typeStorage.ContainsKey(entity.Id))
            return false;

        typeStorage[entity.Id] = entity;
        await Task.Delay(10);
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        _logger.LogInformation("Deleting {EntityType} with ID: {Id}", typeof(T).Name, id);

        var typeStorage = GetTypeStorage();
        await Task.Delay(10);
        return typeStorage.TryRemove(id, out _);
    }

    private ConcurrentDictionary<int, object> GetTypeStorage() =>
        _storage.GetOrAdd(typeof(T), _ => new ConcurrentDictionary<int, object>());

    private static int GenerateId() => Random.Shared.Next(1000, 9999);
}

// ===== 2. GENERIC VALIDATOR WITH COMPLEX CONSTRAINTS =====

/// <summary>
///     Example 2: Generic validator with complex type constraints
///     Demonstrates: Multiple constraints (class, IValidatable) and advanced generic patterns
/// </summary>
[Scoped]
public partial class GenericValidator<T> : IGenericValidator<T> where T : class
{
    [Inject] private readonly ILogger<GenericValidator<T>> _logger;

    public async Task<ValidationResult?> ValidateAsync(T entity)
    {
        _logger.LogDebug("Validating {EntityType}", typeof(T).Name);

        await Task.Delay(5); // Simulate async validation

        // Simple validation - just check if entity is not null
        if (entity == null)
        {
            var result = new ValidationResult($"{typeof(T).Name} cannot be null");
            _logger.LogInformation("Validation result for {EntityType}: {IsValid}",
                typeof(T).Name, "Invalid");
            return result;
        }

        _logger.LogInformation("Validation result for {EntityType}: {IsValid}",
            typeof(T).Name, "Valid");

        return null;
    }
}

// ===== 3. GENERIC PROCESSOR WITH MULTIPLE TYPE PARAMETERS =====

/// <summary>
///     Example 3: Generic service with multiple type parameters
///     Demonstrates: Multi-generic processing patterns and data transformation
/// </summary>
[Transient]
public partial class DataProcessor<TInput, TOutput> : IProcessor<TInput, TOutput>
    where TInput : class
    where TOutput : class, new()
{
    [Inject] private readonly ILogger<DataProcessor<TInput, TOutput>> _logger;

    public async Task<TOutput> ProcessAsync(TInput input)
    {
        _logger.LogInformation("Processing {InputType} to {OutputType}",
            typeof(TInput).Name, typeof(TOutput).Name);

        await Task.Delay(20); // Simulate processing time

        // Create output using the new() constraint
        var output = new TOutput();

        _logger.LogInformation("Successfully processed {InputType} to {OutputType}",
            typeof(TInput).Name, typeof(TOutput).Name);

        return output;
    }
}

// ===== 4. GENERIC CACHE SERVICE =====

/// <summary>
///     Example 4: Generic caching service with lifetime management
///     Demonstrates: Generic caching patterns and memory management
/// </summary>
[Singleton]
public partial class Cache<T> : ICache<T> where T : class
{
    [Inject] private readonly IConfiguration _configuration;

    private readonly TimeSpan _defaultExpiration = TimeSpan.FromMinutes(30);
    [Inject] private readonly ILogger<Cache<T>> _logger;
    [Inject] private readonly IMemoryCache _memoryCache;

    public async Task<T?> GetAsync(string key)
    {
        var cacheKey = GetCacheKey(key);

        if (_memoryCache.TryGetValue(cacheKey, out T? cachedItem))
        {
            _logger.LogDebug("Cache hit for {Type}:{Key}", typeof(T).Name, key);
            return cachedItem;
        }

        _logger.LogDebug("Cache miss for {Type}:{Key}", typeof(T).Name, key);
        await Task.CompletedTask;
        return null;
    }

    public async Task SetAsync(string key,
        T value,
        TimeSpan? expiration = null)
    {
        var cacheKey = GetCacheKey(key);
        var cacheExpiration = expiration ?? _defaultExpiration;

        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = cacheExpiration, SlidingExpiration = TimeSpan.FromMinutes(5)
        };

        _memoryCache.Set(cacheKey, value, options);

        _logger.LogDebug("Cached {Type}:{Key} with expiration {Expiration}",
            typeof(T).Name, key, cacheExpiration);

        await Task.CompletedTask;
    }

    public async Task RemoveAsync(string key)
    {
        var cacheKey = GetCacheKey(key);
        _memoryCache.Remove(cacheKey);

        _logger.LogDebug("Removed {Type}:{Key} from cache", typeof(T).Name, key);
        await Task.CompletedTask;
    }

    private string GetCacheKey(string key) => $"Cache:{typeof(T).Name}:{key}";
}

// ===== 5. GENERIC FACTORY PATTERN =====

/// <summary>
///     Example 5: Generic factory service
///     Demonstrates: Factory patterns with generics and service provider integration
/// </summary>
[Singleton]
public partial class Factory<T> : IFactory<T> where T : class, new()
{
    [Inject] private readonly ILogger<Factory<T>> _logger;
    [Inject] private readonly IServiceProvider _serviceProvider;

    public T Create()
    {
        _logger.LogDebug("Creating instance of {Type}", typeof(T).Name);

        // Try to resolve from DI container first
        var service = _serviceProvider.GetService<T>();
        if (service != null)
        {
            _logger.LogDebug("Resolved {Type} from service container", typeof(T).Name);
            return service;
        }

        // Fallback to new() constraint
        _logger.LogDebug("Creating new instance of {Type} using parameterless constructor", typeof(T).Name);
        return new T();
    }

    public T Create(params object[] args)
    {
        _logger.LogDebug("Creating instance of {Type} with {ArgCount} arguments",
            typeof(T).Name, args.Length);

        // Use reflection for complex construction scenarios
        var constructors = typeof(T).GetConstructors()
            .Where(c => c.GetParameters().Length == args.Length)
            .ToArray();

        if (constructors.Length > 0)
        {
            var constructor = constructors.First();
            var instance = (T)constructor.Invoke(args);
            _logger.LogDebug("Created {Type} using constructor with {ArgCount} parameters",
                typeof(T).Name, args.Length);
            return instance;
        }

        // Fallback to parameterless constructor
        _logger.LogWarning("No matching constructor found for {Type}, using parameterless constructor",
            typeof(T).Name);
        return new T();
    }
}

// ===== 6. GENERIC INHERITANCE CHAIN =====

/// <summary>
///     Example 6a: Generic base service class
///     Demonstrates: Generic inheritance patterns and base class dependencies
/// </summary>
public partial class BaseBusinessService<T> where T : class, IEntity
{
    [Inject] private readonly ILogger<BaseBusinessService<T>> _logger;
    [Inject] private readonly IRepository<T> _repository;

    protected virtual async Task<T?> GetEntityAsync(int id)
    {
        _logger.LogInformation("Base service getting {EntityType} with ID: {Id}", typeof(T).Name, id);
        return await _repository.GetByIdAsync(id);
    }

    protected virtual async Task<int> SaveEntityAsync(T entity)
    {
        _logger.LogInformation("Base service saving {EntityType}", typeof(T).Name);
        return await _repository.CreateAsync(entity);
    }
}

/// <summary>
///     Example 6b: Generic derived service class
///     Demonstrates: Generic inheritance with additional dependencies
/// </summary>
[Scoped]
public partial class AdvancedBusinessService<T> : BaseBusinessService<T>
    where T : class, IEntity, new()
{
    [Inject] private readonly ICache<T> _cache;
    [Inject] private readonly IGenericValidator<T> _validator;

    public async Task<T?> GetValidatedEntityAsync(int id)
    {
        var entity = await GetEntityAsync(id); // From base class

        if (entity != null)
        {
            var validationResult = await _validator.ValidateAsync(entity);
            if (validationResult != null)
                throw new InvalidOperationException($"Entity validation failed: {validationResult.ErrorMessage}");
        }

        return entity;
    }

    public async Task<int> CreateValidatedEntityAsync(T entity)
    {
        // Validate first
        var validationResult = await _validator.ValidateAsync(entity);
        if (validationResult != null)
            throw new InvalidOperationException($"Validation failed: {validationResult.ErrorMessage}");

        // Save using base class method
        var id = await SaveEntityAsync(entity);

        // Cache the result
        await _cache.SetAsync($"entity:{id}", entity, TimeSpan.FromHours(1));

        return id;
    }
}

// ===== 7. COMPLEX GENERIC SCENARIO WITH CONDITIONAL REGISTRATION =====

/// <summary>
///     Example 7: Generic service with conditional registration and configuration injection
///     Demonstrates: Advanced generic patterns with configuration and conditional features
/// </summary>
[Scoped]
public partial class EnhancedGenericProcessor<T> where T : class, IEntity, new()
{
    // Configuration injection for type-specific settings
    [InjectConfiguration("Processing:BatchSize", DefaultValue = "100")]
    private readonly int _batchSize;

    [Inject] private readonly ICache<T> _cache;
    [Inject] private readonly IConfiguration _configuration;

    [InjectConfiguration("Processing:EnableCaching", DefaultValue = "true")]
    private readonly bool _enableCaching;

    [Inject] private readonly ILogger<EnhancedGenericProcessor<T>> _logger;
    [Inject] private readonly IRepository<T> _repository;

    [InjectConfiguration("Processing:TimeoutSeconds", DefaultValue = "30")]
    private readonly int _timeoutSeconds;

    public async Task<ProcessingResult<T>> ProcessBatchAsync(IEnumerable<T> entities)
    {
        var entitiesList = entities.ToList();
        _logger.LogInformation("Processing batch of {Count} {EntityType} entities (batch size: {BatchSize})",
            entitiesList.Count, typeof(T).Name, _batchSize);

        var results = new List<T>();
        var errors = new List<string>();
        var timeout = TimeSpan.FromSeconds(_timeoutSeconds);

        using var cts = new CancellationTokenSource(timeout);

        try
        {
            // Process in batches
            for (var i = 0; i < entitiesList.Count; i += _batchSize)
            {
                var batch = entitiesList.Skip(i).Take(_batchSize);

                foreach (var entity in batch)
                {
                    cts.Token.ThrowIfCancellationRequested();

                    try
                    {
                        // Create the entity
                        var id = await _repository.CreateAsync(entity);
                        entity.Id = id;

                        // Cache if enabled
                        if (_enableCaching) await _cache.SetAsync($"batch:{id}", entity);

                        results.Add(entity);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process {EntityType}", typeof(T).Name);
                        errors.Add($"Failed to process {typeof(T).Name}: {ex.Message}");
                    }
                }
            }

            _logger.LogInformation("Successfully processed {SuccessCount}/{TotalCount} {EntityType} entities",
                results.Count, entitiesList.Count, typeof(T).Name);

            return new ProcessingResult<T>
            {
                ProcessedEntities = results,
                Errors = errors,
                TotalProcessed = results.Count,
                TotalErrors = errors.Count
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Batch processing timed out after {TimeoutSeconds} seconds for {EntityType}",
                _timeoutSeconds, typeof(T).Name);

            errors.Add($"Processing timed out after {_timeoutSeconds} seconds");
            return new ProcessingResult<T>
            {
                ProcessedEntities = results,
                Errors = errors,
                TotalProcessed = results.Count,
                TotalErrors = errors.Count + 1
            };
        }
    }
}

// ===== 8. DEMONSTRATION SERVICE =====

/// <summary>
///     Example 8: Service that demonstrates all generic patterns
///     Shows how to use all the generic services together
/// </summary>
public partial class GenericServiceDemonstrator
{
    [Inject] private readonly ILogger<GenericServiceDemonstrator> _logger;
    [Inject] private readonly AdvancedBusinessService<User> _userBusinessService;
    [Inject] private readonly ICache<User> _userCache;
    [Inject] private readonly EnhancedGenericProcessor<User> _userEnhancedProcessor;
    [Inject] private readonly IFactory<User> _userFactory;
    [Inject] private readonly IProcessor<User, User> _userProcessor;

    // Inject specific generic services
    [Inject] private readonly IRepository<User> _userRepository;
    [Inject] private readonly IGenericValidator<User> _userValidator;

    public async Task DemonstrateGenericPatternsAsync()
    {
        _logger.LogInformation("=== Generic Service Patterns Demonstration ===");

        try
        {
            // 1. Basic Repository Pattern
            _logger.LogInformation("1. Testing Generic Repository Pattern");
            var user = new User
            {
                Username = "johndoe", Email = "john@example.com", FirstName = "John", LastName = "Doe"
            };
            var userId = await _userRepository.CreateAsync(user);
            var retrievedUser = await _userRepository.GetByIdAsync(userId);
            _logger.LogInformation("   Created and retrieved user: {UserName}", retrievedUser?.FirstName ?? "null");

            // 2. Generic Validation
            _logger.LogInformation("2. Testing Generic Validation");
            if (retrievedUser != null)
            {
                var validationResult = await _userValidator.ValidateAsync(retrievedUser);
                _logger.LogInformation("   User validation: {IsValid}",
                    validationResult == null ? "Valid" : "Invalid");
            }

            // 3. Multi-Generic Processing
            _logger.LogInformation("3. Testing Multi-Generic Processing");
            if (retrievedUser != null)
            {
                var processedUser = await _userProcessor.ProcessAsync(retrievedUser);
                _logger.LogInformation("   Processed User to User: {UserType}", processedUser.GetType().Name);
            }

            // 4. Generic Caching
            _logger.LogInformation("4. Testing Generic Caching");
            if (retrievedUser != null)
            {
                await _userCache.SetAsync("demo-user", retrievedUser);
                var cachedUser = await _userCache.GetAsync("demo-user");
                _logger.LogInformation("   Cached and retrieved user: {UserName}", cachedUser?.FirstName ?? "null");
            }

            // 5. Generic Factory
            _logger.LogInformation("5. Testing Generic Factory");
            var newUser = _userFactory.Create();
            _logger.LogInformation("   Created User with type: {UserType}", newUser.GetType().Name);

            // 6. Generic Inheritance
            _logger.LogInformation("6. Testing Generic Inheritance");
            if (retrievedUser != null)
                try
                {
                    var validatedUser = await _userBusinessService.GetValidatedEntityAsync(retrievedUser.Id);
                    _logger.LogInformation("   Advanced business service retrieved: {UserName}",
                        validatedUser?.FirstName ?? "null");
                }
                catch (Exception ex)
                {
                    _logger.LogInformation("   Advanced business service validation failed: {Error}", ex.Message);
                }

            // 7. Complex Generic Processing
            _logger.LogInformation("7. Testing Enhanced Generic Processing");
            var users = new[]
            {
                new User { Username = "user1", Email = "user1@example.com", FirstName = "User", LastName = "One" },
                new User { Username = "user2", Email = "user2@example.com", FirstName = "User", LastName = "Two" },
                new User { Username = "user3", Email = "user3@example.com", FirstName = "User", LastName = "Three" }
            };

            var processingResult = await _userEnhancedProcessor.ProcessBatchAsync(users);
            _logger.LogInformation("   Batch processed {ProcessedCount}/{TotalCount} users with {ErrorCount} errors",
                processingResult.TotalProcessed, users.Length, processingResult.TotalErrors);

            _logger.LogInformation("=== Generic Service Patterns Demonstration Complete ===");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during generic patterns demonstration");
        }
    }
}

// ===== SUPPORTING DATA MODELS =====

public class ProcessingResult<T>
{
    public List<T> ProcessedEntities { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public int TotalProcessed { get; set; }
    public int TotalErrors { get; set; }
}

// Note: ValidationResult and other types already exist in other service files
