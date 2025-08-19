using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IoCTools.Sample.Services;

/// <summary>
///     Demonstrates advanced patterns that IoCTools supports and areas for improvement
/// </summary>
public interface IAdvancedPatternsService
{
    Task DemonstrateCurrentCapabilitiesAsync();
    Task DemonstrateFutureEnhancementsAsync();
}

[Service]
public partial class AdvancedPatternsService : IAdvancedPatternsService
{
    [Inject] private readonly ILogger<AdvancedPatternsService> _logger;

    // Current IoCTools capabilities - these work perfectly
    [Inject] private readonly IServiceProvider _serviceProvider;

    // Future enhancements - these would need additional DI container configuration
    // [Inject] private readonly Lazy<ICacheService> _lazyCacheService;
    // [Inject] private readonly Func<string, IGreetingService> _greetingServiceFactory;

    public async Task DemonstrateCurrentCapabilitiesAsync()
    {
        _logger.LogInformation("=== Current IoCTools Advanced Capabilities ===");

        // 1. Service provider injection for manual resolution
        _logger.LogInformation("1. Manual service resolution via IServiceProvider:");
        using var scope = _serviceProvider.CreateScope();
        var greetingService = scope.ServiceProvider.GetService<IGreetingService>();
        if (greetingService != null)
        {
            var greeting = greetingService.GetGreeting("Advanced User");
            _logger.LogInformation("   Resolved greeting: {Greeting}", greeting);
        }

        // 2. Collection injection (demonstrated in AdvancedInjectionService)
        _logger.LogInformation("2. Collection injection: See AdvancedInjectionService for IEnumerable<T> examples");

        // 3. Inheritance support with dependency injection
        _logger.LogInformation("3. Inheritance support: See NewPaymentProcessor extending BasePaymentProcessor");

        // 4. Configuration injection
        _logger.LogInformation("4. Configuration injection: IConfiguration is injected into services");

        // 5. Multiple service lifetimes
        _logger.LogInformation("5. Multiple lifetimes: Singleton, Scoped, Transient all work correctly");

        await Task.CompletedTask;
    }

    public async Task DemonstrateFutureEnhancementsAsync()
    {
        _logger.LogInformation("=== Future Enhancement Opportunities ===");

        // These would require manual DI container setup currently
        _logger.LogInformation(
            "1. Lazy<T> injection: Would require services.AddTransient<Lazy<ICacheService>>(provider => new Lazy<ICacheService>(provider.GetService<ICacheService>))");

        _logger.LogInformation("2. Func<T> factory injection: Would require custom factory registration");

        _logger.LogInformation("3. Named service resolution: Currently not supported by IoCTools");

        _logger.LogInformation("4. Conditional service resolution: Would need custom resolvers");

        _logger.LogInformation("5. Keyed services (planned for .NET 8+): Future enhancement opportunity");

        await Task.CompletedTask;
    }
}