namespace IoCTools.Generator.Tests;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
///     Collection definition for tests that modify environment variables to prevent parallel execution interference
/// </summary>
[CollectionDefinition("EnvironmentDependent")]
public class EnvironmentDependentCollection : ICollectionFixture<EnvironmentDependentCollection>
{
}

/// <summary>
///     COMPREHENSIVE INTEGRATION TEST COVERAGE
///     Tests that verify generated code compiles correctly and behaves properly at runtime:
///     - Generated code compiles without errors across all feature combinations
///     - Runtime behavior matches expectations for all scenarios
///     - DI container can resolve all services correctly
///     - No runtime exceptions from duplicate registrations or configuration issues
///     - End-to-end testing of complex scenarios combining multiple features
///     - Performance characteristics under realistic workloads
///     These tests verify the entire pipeline from source generation through runtime execution.
/// </summary>
[Collection("EnvironmentDependent")]
public class ComprehensiveIntegrationTests
{
    #region Cross-Feature Compatibility Tests

    [Fact]
    public void Integration_ConditionalWithConfiguration_OnlyRegistersWhenConditionMet()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Configuration;

namespace Test;

public class CacheSettings
{
    public string Provider { get; set; } = """";
    public int TTL { get; set; }
}

public interface ICacheService { }

[ConditionalService(ConfigValue = ""Cache:Provider"", Equals = ""Redis"")]

public partial class RedisCacheService : ICacheService
{
    [InjectConfiguration(""Cache"")] 
    private readonly CacheSettings _settings;
    
    public CacheSettings GetSettings() => _settings;
}

[ConditionalService(ConfigValue = ""Cache:Provider"", Equals = ""Memory"")]

public partial class MemoryCacheService : ICacheService
{
}";

        // Test Redis configuration
        var redisConfigData = new Dictionary<string, string> { { "Cache:Provider", "Redis" }, { "Cache:TTL", "300" } };
        var redisConfiguration = SourceGeneratorTestHelper.CreateConfiguration(redisConfigData);

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.HasErrors.Should().BeFalse();

        var runtimeContext = SourceGeneratorTestHelper.CreateRuntimeContext(result);
        var redisServiceProvider =
            SourceGeneratorTestHelper.BuildServiceProvider(runtimeContext, configuration: redisConfiguration);

        var cacheServiceType = runtimeContext.Assembly.GetType("Test.ICacheService") ??
                               throw new InvalidOperationException("ICacheService type not generated.");
        var redisCacheServiceType = runtimeContext.Assembly.GetType("Test.RedisCacheService") ??
                                    throw new InvalidOperationException("RedisCacheService type not generated.");
        var memoryCacheServiceType = runtimeContext.Assembly.GetType("Test.MemoryCacheService") ??
                                     throw new InvalidOperationException("MemoryCacheService type not generated.");

        var redisCacheService = redisServiceProvider.GetRequiredService(cacheServiceType);
        redisCacheService.Should().BeOfType(redisCacheServiceType);

        var getSettingsMethod = redisCacheServiceType.GetMethod("GetSettings") ??
                                throw new InvalidOperationException("GetSettings method missing on RedisCacheService.");

        var settings = getSettingsMethod.Invoke(redisCacheService, null) ??
                       throw new InvalidOperationException("Expected cache settings instance.");

        var settingsType = settings.GetType();
        var providerProperty = settingsType.GetProperty("Provider") ??
                               throw new InvalidOperationException("Provider property missing on settings type.");
        var ttlProperty = settingsType.GetProperty("TTL") ??
                          throw new InvalidOperationException("TTL property missing on settings type.");

        providerProperty.GetValue(settings).Should().Be("Redis");
        ttlProperty.GetValue(settings).Should().Be(300);

        // Test Memory configuration - create new runtime context for different configuration
        var memoryConfigData = new Dictionary<string, string> { { "Cache:Provider", "Memory" } };
        var memoryConfiguration = SourceGeneratorTestHelper.CreateConfiguration(memoryConfigData);

        // Need new runtime context for different configuration
        var memoryRuntimeContext = SourceGeneratorTestHelper.CreateRuntimeContext(result);
        var memoryServiceProvider =
            SourceGeneratorTestHelper.BuildServiceProvider(memoryRuntimeContext, configuration: memoryConfiguration);

        var memoryCacheServiceType2 = memoryRuntimeContext.Assembly.GetType("Test.MemoryCacheService") ??
                                      throw new InvalidOperationException("MemoryCacheService type not generated.");
        var cacheServiceType2 = memoryRuntimeContext.Assembly.GetType("Test.ICacheService") ??
                                throw new InvalidOperationException("ICacheService type not generated.");

        var memoryCacheService = memoryServiceProvider.GetRequiredService(cacheServiceType2);
        memoryCacheService.Should().BeOfType(memoryCacheServiceType2);
    }

    #endregion

    #region Error Handling and Resilience Tests

    [Fact]
    public void Integration_ConditionalServiceNoMatch_ServiceNotAvailable()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

[ConditionalService(Environment = ""Production"")]

public partial class ProductionService : ITestService
{
}";

        // Act - Run in Development environment (no match)
        using var envContext = SourceGeneratorTestHelper.CreateTestEnvironment(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development"
        });

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.HasErrors.Should().BeFalse();

        var runtimeContext = SourceGeneratorTestHelper.CreateRuntimeContext(result);
        var serviceProvider = SourceGeneratorTestHelper.BuildServiceProvider(runtimeContext);

        // Assert - Service should not be available since condition doesn't match
        var testServiceType = runtimeContext.Assembly.GetType("Test.ITestService") ??
                              throw new InvalidOperationException("ITestService type not generated.");

        // ConditionalService should not register when environment doesn't match
        FluentActions.Invoking(() =>
            serviceProvider.GetRequiredService(testServiceType)).Should().Throw<InvalidOperationException>();

        // Optional resolution should return null
        var optionalService = serviceProvider.GetService(testServiceType);
        optionalService.Should().BeNull();
    }

    #endregion

    #region Real-World Scenario Tests

    [Fact]
    public void Integration_ConditionalServiceWithMultipleFeatures_WorksCorrectly()
    {
        // Arrange - Test ConditionalService with other features but keep it simple
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Configuration;

namespace Test;

public class ApiSettings
{
    public string Url { get; set; } = """";
    public string ApiKey { get; set; } = """";
    public bool EnableLogging { get; set; }
}

public interface IApiService { }

// Environment-based conditional service with configuration injection
[ConditionalService(Environment = ""Production"")]

public partial class ProductionApiService : IApiService
{
    [InjectConfiguration(""Api"")] 
    private readonly ApiSettings _settings;
    
    public ApiSettings GetSettings() => _settings;
    public string GetEnvironment() => ""Production"";
}

[ConditionalService(Environment = ""Development"")]

public partial class DevelopmentApiService : IApiService
{
    [InjectConfiguration(""Api:Url"", DefaultValue = ""http://localhost:5000"")] 
    private readonly string _url;
    
    public string GetUrl() => _url;
    public string GetEnvironment() => ""Development"";
}";

        var configData = new Dictionary<string, string>
        {
            { "Api:Url", "https://api.production.com" },
            { "Api:ApiKey", "prod-key-12345" },
            { "Api:EnableLogging", "true" }
        };
        var configuration = SourceGeneratorTestHelper.CreateConfiguration(configData);

        // Test Production environment
        using var envContext = SourceGeneratorTestHelper.CreateTestEnvironment(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production"
        });

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.HasErrors.Should().BeFalse();

        var runtimeContext = SourceGeneratorTestHelper.CreateRuntimeContext(result);
        var serviceProvider =
            SourceGeneratorTestHelper.BuildServiceProvider(runtimeContext, configuration: configuration);

        // Verify Production service is selected
        var apiServiceType = runtimeContext.Assembly.GetType("Test.IApiService") ??
                             throw new InvalidOperationException("IApiService type not generated.");
        var productionApiServiceType = runtimeContext.Assembly.GetType("Test.ProductionApiService") ??
                                       throw new InvalidOperationException("ProductionApiService type not generated.");

        var apiService = serviceProvider.GetRequiredService(apiServiceType);
        apiService.Should().BeOfType(productionApiServiceType);

        // Verify configuration injection works with ConditionalService
        var getSettingsMethod = productionApiServiceType.GetMethod("GetSettings") ??
                                throw new InvalidOperationException("GetSettings missing on ProductionApiService.");
        var getEnvironmentMethod = productionApiServiceType.GetMethod("GetEnvironment") ??
                                   throw new InvalidOperationException(
                                       "GetEnvironment missing on ProductionApiService.");

        getEnvironmentMethod.Invoke(apiService, null).Should().Be("Production");

        var settings = getSettingsMethod.Invoke(apiService, null) ??
                       throw new InvalidOperationException("Expected configuration settings instance.");

        var settingsType = settings.GetType();
        var urlProperty = settingsType.GetProperty("Url") ??
                          throw new InvalidOperationException("Url property missing on settings type.");
        var apiKeyProperty = settingsType.GetProperty("ApiKey") ??
                             throw new InvalidOperationException("ApiKey property missing on settings type.");
        var enableLoggingProperty = settingsType.GetProperty("EnableLogging") ??
                                    throw new InvalidOperationException(
                                        "EnableLogging property missing on settings type.");

        urlProperty.GetValue(settings).Should().Be("https://api.production.com");
        apiKeyProperty.GetValue(settings).Should().Be("prod-key-12345");
        ((bool?)enableLoggingProperty.GetValue(settings)).Should().BeTrue();
    }

    #endregion
}
