namespace IoCTools.Sample.Services;

using System.Text.RegularExpressions;

using Abstractions.Annotations;

using Microsoft.Extensions.Logging;

// === TRANSIENT SERVICE EXAMPLES ===
// Transient services are created each time they are requested from the DI container
// Perfect for stateless operations like validation, transformation, and generation

/// <summary>
///     Email validation service - perfect candidate for Transient lifetime
///     Stateless operation that doesn't need to maintain state between calls
/// </summary>
public interface IEmailValidator
{
    bool IsValid(string email);
    EmailValidationResult ValidateWithDetails(string email);
}

[Transient]
public partial class EmailValidator : IEmailValidator
{
    private static readonly Regex EmailRegex = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    [Inject] private readonly ILogger<EmailValidator> _logger;

    public bool IsValid(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;

        var isValid = EmailRegex.IsMatch(email);
        _logger.LogDebug("Email validation for {Email}: {IsValid}", email, isValid);
        return isValid;
    }

    public EmailValidationResult ValidateWithDetails(string email)
    {
        _logger.LogInformation("Performing detailed email validation for: {Email}", email);

        if (string.IsNullOrWhiteSpace(email))
            return new EmailValidationResult(false, "Email cannot be empty");

        if (email.Length > 254)
            return new EmailValidationResult(false, "Email too long (max 254 characters)");

        if (!email.Contains('@'))
            return new EmailValidationResult(false, "Email must contain @ symbol");

        if (!EmailRegex.IsMatch(email))
            return new EmailValidationResult(false, "Email format is invalid");

        return new EmailValidationResult(true, "Email is valid");
    }
}

/// <summary>
///     Data transformation service - another excellent Transient candidate
///     Performs stateless transformations without maintaining any state
/// </summary>
public interface IDataTransformer
{
    T Transform<T>(T input,
        Func<T, T> transformer);

    string NormalizeText(string text);

    decimal CalculatePercentage(decimal value,
        decimal total);

    Dictionary<string, object> TransformToDictionary<T>(T obj);
}

[Transient]
public partial class DataTransformer : IDataTransformer
{
    [Inject] private readonly ILogger<DataTransformer> _logger;

    public T Transform<T>(T input,
        Func<T, T> transformer)
    {
        _logger.LogDebug("Transforming object of type {Type}", typeof(T).Name);
        return transformer(input);
    }

    public string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        _logger.LogDebug("Normalizing text of length {Length}", text.Length);

        return text.Trim()
            .ToLowerInvariant()
            .Replace("  ", " ") // Remove double spaces
            .Replace("\t", " ") // Replace tabs with spaces
            .Replace("\r\n", " ") // Replace line breaks
            .Replace("\n", " ");
    }

    public decimal CalculatePercentage(decimal value,
        decimal total)
    {
        if (total == 0)
        {
            _logger.LogWarning("Cannot calculate percentage with zero total");
            return 0;
        }

        var percentage = value / total * 100;
        _logger.LogDebug("Calculated percentage: {Value}/{Total} = {Percentage}%", value, total, percentage);
        return Math.Round(percentage, 2);
    }

    public Dictionary<string, object> TransformToDictionary<T>(T obj)
    {
        _logger.LogDebug("Transforming {Type} to dictionary", typeof(T).Name);

        if (obj == null) return new Dictionary<string, object>();

        return typeof(T).GetProperties()
            .ToDictionary(prop => prop.Name, prop => prop.GetValue(obj) ?? "null");
    }
}

/// <summary>
///     Request processor - ideal for Transient lifetime
///     Processes individual requests without maintaining state
///     Each request should get a fresh instance
/// </summary>
public interface IRequestProcessor
{
    Task<RequestProcessingResult> ProcessAsync(ProcessingRequest request);
    Task<RequestProcessingResult> ProcessWithValidationAsync(ProcessingRequest request);
    Task<BatchRequestProcessingResult> ProcessBatchAsync(IEnumerable<ProcessingRequest> requests);
}

[Transient]
public partial class RequestProcessor : IRequestProcessor
{
    [Inject] private readonly IDataTransformer _dataTransformer;
    [Inject] private readonly IEmailValidator _emailValidator;
    [Inject] private readonly ILogger<RequestProcessor> _logger;

    public async Task<RequestProcessingResult> ProcessAsync(ProcessingRequest request)
    {
        _logger.LogInformation("Processing request {RequestId} of type {Type}",
            request.Id, request.Type);

        // Simulate processing work
        await Task.Delay(50);

        var normalizedData = _dataTransformer.NormalizeText(request.Data);

        return new RequestProcessingResult(
            request.Id,
            true,
            $"Successfully processed {request.Type} request",
            normalizedData);
    }

    public async Task<RequestProcessingResult> ProcessWithValidationAsync(ProcessingRequest request)
    {
        _logger.LogInformation("Processing request {RequestId} with validation", request.Id);

        // Use injected validator (also Transient)
        if (request.Type == "email" && !_emailValidator.IsValid(request.Data))
            return new RequestProcessingResult(
                request.Id,
                false,
                "Email validation failed",
                request.Data);

        return await ProcessAsync(request);
    }

    public async Task<BatchRequestProcessingResult> ProcessBatchAsync(IEnumerable<ProcessingRequest> requests)
    {
        var requestList = requests.ToList();
        _logger.LogInformation("Processing batch of {Count} requests", requestList.Count);

        var results = new List<RequestProcessingResult>();
        var successful = 0;
        var failed = 0;

        foreach (var request in requestList)
        {
            var result = await ProcessWithValidationAsync(request);
            results.Add(result);

            if (result.Success) successful++;
            else failed++;
        }

        return new BatchRequestProcessingResult(results, successful, failed);
    }
}

/// <summary>
///     GUID generator - perfect example of when to use Transient
///     Stateless, lightweight, should create fresh instance each time
/// </summary>
public interface IGuidGenerator
{
    Guid NewGuid();
    string NewGuidString(GuidFormat format = GuidFormat.Default);
    IEnumerable<Guid> GenerateBatch(int count);
    string NewShortId(int length = 8);
}

[Transient]
public partial class GuidGenerator : IGuidGenerator
{
    [Inject] private readonly ILogger<GuidGenerator> _logger;

    public Guid NewGuid()
    {
        var guid = Guid.NewGuid();
        _logger.LogTrace("Generated new GUID: {Guid}", guid);
        return guid;
    }

    public string NewGuidString(GuidFormat format = GuidFormat.Default)
    {
        var guid = NewGuid();

        return format switch
        {
            GuidFormat.Default => guid.ToString(),
            GuidFormat.NoDashes => guid.ToString("N"),
            GuidFormat.Uppercase => guid.ToString().ToUpperInvariant(),
            GuidFormat.UppercaseNoDashes => guid.ToString("N").ToUpperInvariant(),
            _ => guid.ToString()
        };
    }

    public IEnumerable<Guid> GenerateBatch(int count)
    {
        _logger.LogDebug("Generating batch of {Count} GUIDs", count);

        for (var i = 0; i < count; i++) yield return NewGuid();
    }

    public string NewShortId(int length = 8)
    {
        var guid = NewGuid();
        var shortId = Convert.ToBase64String(guid.ToByteArray())
            .Replace("/", "_")
            .Replace("+", "-")
            .Substring(0, Math.Min(length, 22));

        _logger.LogTrace("Generated short ID: {ShortId}", shortId);
        return shortId;
    }
}

/// <summary>
///     Comparison service - demonstrates Transient services with dependencies
///     Shows how Transient services can depend on other services of different lifetimes
/// </summary>
public interface ILifetimeComparisonService
{
    Task DemonstrateTransientBehaviorAsync();
    Task CompareLifetimesAsync();
    ServiceLifetimeInfo GetServiceInfo();
}

[Transient]
public partial class LifetimeComparisonService : ILifetimeComparisonService
{
    [Inject] private readonly ICacheService _cacheService; // Transient -> Singleton
    [Inject] private readonly IEmailService _emailService; // Transient -> Scoped
    [Inject] private readonly IGuidGenerator _guidGenerator; // Transient -> Transient

    private readonly string _instanceId = Guid.NewGuid().ToString("N")[..8];
    [Inject] private readonly ILogger<LifetimeComparisonService> _logger;

    public async Task DemonstrateTransientBehaviorAsync()
    {
        _logger.LogInformation("=== Transient Service Behavior Demo ===");
        _logger.LogInformation("LifetimeComparisonService Instance ID: {InstanceId}", _instanceId);

        // Each call to a Transient service creates a new instance
        var guid1 = _guidGenerator.NewGuid();
        var guid2 = _guidGenerator.NewGuid();

        _logger.LogInformation("Generated GUIDs from Transient service: {Guid1}, {Guid2}", guid1, guid2);

        // Cache service is Singleton - same instance across all calls
        var cacheKey = $"transient-demo-{_instanceId}";
        var cachedValue = _cacheService.GetOrSet(cacheKey, () => $"cached-by-{_instanceId}");
        _logger.LogInformation("Cached value from Singleton service: {CachedValue}", cachedValue);

        await Task.CompletedTask;
    }

    public async Task CompareLifetimesAsync()
    {
        _logger.LogInformation("=== Service Lifetime Comparison ===");
        _logger.LogInformation("This service (Transient): New instance every time");
        _logger.LogInformation("IGuidGenerator (Transient): New instance every time");
        _logger.LogInformation("ICacheService (Singleton): Same instance always");
        _logger.LogInformation("IEmailService (Scoped): Same instance within scope");

        // Demonstrate behavior differences
        _logger.LogInformation("Current instance behaviors:");
        _logger.LogInformation("  - Transient services: Fresh state, no shared data");
        _logger.LogInformation("  - Scoped services: State maintained within HTTP request/scope");
        _logger.LogInformation("  - Singleton services: State maintained across entire application");

        await Task.CompletedTask;
    }

    public ServiceLifetimeInfo GetServiceInfo() => new(
        "LifetimeComparisonService",
        "Transient",
        _instanceId,
        DateTime.UtcNow,
        new Dictionary<string, string>
        {
            ["InstanceCreatedAt"] = DateTime.UtcNow.ToString("HH:mm:ss.fff"),
            ["DependencyCount"] = "4",
            ["TransientDependencies"] = "1 (IGuidGenerator)",
            ["ScopedDependencies"] = "1 (IEmailService)",
            ["SingletonDependencies"] = "1 (ICacheService)",
            ["FrameworkDependencies"] = "1 (ILogger)"
        });
}

// === DATA MODELS FOR TRANSIENT SERVICES ===

public record EmailValidationResult(bool IsValid, string Message);

public record ProcessingRequest(string Id, string Type, string Data)
{
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

public record RequestProcessingResult(string RequestId, bool Success, string Message, string ProcessedData)
{
    public DateTime CompletedAt { get; init; } = DateTime.UtcNow;
}

public record BatchRequestProcessingResult(
    IReadOnlyList<RequestProcessingResult> Results,
    int SuccessfulCount,
    int FailedCount)
{
    public DateTime CompletedAt { get; init; } = DateTime.UtcNow;
    public int TotalCount => Results.Count;
    public double SuccessRate => TotalCount > 0 ? (double)SuccessfulCount / TotalCount * 100 : 0;
}

public record ServiceLifetimeInfo(
    string ServiceName,
    string Lifetime,
    string InstanceId,
    DateTime CreatedAt,
    Dictionary<string, string> Metadata);

public enum GuidFormat
{
    Default,
    NoDashes,
    Uppercase,
    UppercaseNoDashes
}
