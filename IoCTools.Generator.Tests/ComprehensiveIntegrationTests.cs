using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace IoCTools.Generator.Tests;

/// <summary>
/// COMPREHENSIVE INTEGRATION TEST COVERAGE
/// 
/// Tests that verify generated code compiles correctly and behaves properly at runtime:
/// - Generated code compiles without errors across all feature combinations
/// - Runtime behavior matches expectations for all scenarios
/// - DI container can resolve all services correctly
/// - No runtime exceptions from duplicate registrations or configuration issues
/// - End-to-end testing of complex scenarios combining multiple features
/// - Performance characteristics under realistic workloads
/// 
/// These tests verify the entire pipeline from source generation through runtime execution.
/// </summary>
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
[Service]
public partial class RedisCacheService : ICacheService
{
    [InjectConfiguration(""Cache"")] 
    private readonly CacheSettings _settings;
    
    public CacheSettings GetSettings() => _settings;
}

[ConditionalService(ConfigValue = ""Cache:Provider"", Equals = ""Memory"")]
[Service]
public partial class MemoryCacheService : ICacheService
{
}";

        // Test Redis configuration
        var redisConfigData = new Dictionary<string, string>
        {
            {"Cache:Provider", "Redis"},
            {"Cache:TTL", "300"}
        };
        var redisConfiguration = SourceGeneratorTestHelper.CreateConfiguration(redisConfigData);

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        Assert.False(result.HasErrors);

        var runtimeContext = SourceGeneratorTestHelper.CreateRuntimeContext(result);
        var redisServiceProvider = SourceGeneratorTestHelper.BuildServiceProvider(runtimeContext, configuration: redisConfiguration);

        var cacheServiceType = runtimeContext.Assembly.GetType("Test.ICacheService");
        var redisCacheServiceType = runtimeContext.Assembly.GetType("Test.RedisCacheService");
        var memoryCacheServiceType = runtimeContext.Assembly.GetType("Test.MemoryCacheService");

        Assert.NotNull(cacheServiceType);
        Assert.NotNull(redisCacheServiceType);
        Assert.NotNull(memoryCacheServiceType);

        var redisCacheService = redisServiceProvider.GetRequiredService(cacheServiceType);
        Assert.IsType(redisCacheServiceType, redisCacheService);

        var getSettingsMethod = redisCacheServiceType.GetMethod("GetSettings");
        Assert.NotNull(getSettingsMethod);
        
        var settings = getSettingsMethod.Invoke(redisCacheService, null);
        Assert.NotNull(settings);
        
        var settingsType = settings.GetType();
        var providerProperty = settingsType.GetProperty("Provider");
        var ttlProperty = settingsType.GetProperty("TTL");
        
        Assert.Equal("Redis", providerProperty?.GetValue(settings));
        Assert.Equal(300, ttlProperty?.GetValue(settings));

        // Test Memory configuration - create new runtime context for different configuration
        var memoryConfigData = new Dictionary<string, string>
        {
            {"Cache:Provider", "Memory"}
        };
        var memoryConfiguration = SourceGeneratorTestHelper.CreateConfiguration(memoryConfigData);

        // Need new runtime context for different configuration
        var memoryRuntimeContext = SourceGeneratorTestHelper.CreateRuntimeContext(result);
        var memoryServiceProvider = SourceGeneratorTestHelper.BuildServiceProvider(memoryRuntimeContext, configuration: memoryConfiguration);
        
        var memoryCacheServiceType2 = memoryRuntimeContext.Assembly.GetType("Test.MemoryCacheService");
        var cacheServiceType2 = memoryRuntimeContext.Assembly.GetType("Test.ICacheService");
        Assert.NotNull(memoryCacheServiceType2);
        Assert.NotNull(cacheServiceType2);
        
        var memoryCacheService = memoryServiceProvider.GetRequiredService(cacheServiceType2);
        Assert.IsType(memoryCacheServiceType2, memoryCacheService);
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
[Service]
public partial class ProductionService : ITestService
{
}";

        // Act - Run in Development environment (no match)
        using var envContext = SourceGeneratorTestHelper.CreateTestEnvironment(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Development"
        });

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        Assert.False(result.HasErrors);

        var runtimeContext = SourceGeneratorTestHelper.CreateRuntimeContext(result);
        var serviceProvider = SourceGeneratorTestHelper.BuildServiceProvider(runtimeContext);

        // Assert - Service should not be available since condition doesn't match
        var testServiceType = runtimeContext.Assembly.GetType("Test.ITestService");
        Assert.NotNull(testServiceType);
        
        // ConditionalService should not register when environment doesn't match
        Assert.Throws<InvalidOperationException>(() => 
            serviceProvider.GetRequiredService(testServiceType));

        // Optional resolution should return null
        var optionalService = serviceProvider.GetService(testServiceType);
        Assert.Null(optionalService);
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
[Service]
public partial class ProductionApiService : IApiService
{
    [InjectConfiguration(""Api"")] 
    private readonly ApiSettings _settings;
    
    public ApiSettings GetSettings() => _settings;
    public string GetEnvironment() => ""Production"";
}

[ConditionalService(Environment = ""Development"")]
[Service]
public partial class DevelopmentApiService : IApiService
{
    [InjectConfiguration(""Api:Url"", DefaultValue = ""http://localhost:5000"")] 
    private readonly string _url;
    
    public string GetUrl() => _url;
    public string GetEnvironment() => ""Development"";
}";

        var configData = new Dictionary<string, string>
        {
            {"Api:Url", "https://api.production.com"},
            {"Api:ApiKey", "prod-key-12345"},
            {"Api:EnableLogging", "true"}
        };
        var configuration = SourceGeneratorTestHelper.CreateConfiguration(configData);

        // Test Production environment
        using var envContext = SourceGeneratorTestHelper.CreateTestEnvironment(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production"
        });

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        Assert.False(result.HasErrors);

        var runtimeContext = SourceGeneratorTestHelper.CreateRuntimeContext(result);
        var serviceProvider = SourceGeneratorTestHelper.BuildServiceProvider(runtimeContext, configuration: configuration);

        // Verify Production service is selected
        var apiServiceType = runtimeContext.Assembly.GetType("Test.IApiService");
        var productionApiServiceType = runtimeContext.Assembly.GetType("Test.ProductionApiService");
        
        Assert.NotNull(apiServiceType);
        Assert.NotNull(productionApiServiceType);

        var apiService = serviceProvider.GetRequiredService(apiServiceType);
        Assert.IsType(productionApiServiceType, apiService);

        // Verify configuration injection works with ConditionalService
        var getSettingsMethod = productionApiServiceType.GetMethod("GetSettings");
        var getEnvironmentMethod = productionApiServiceType.GetMethod("GetEnvironment");
        
        Assert.NotNull(getSettingsMethod);
        Assert.NotNull(getEnvironmentMethod);
        
        Assert.Equal("Production", getEnvironmentMethod.Invoke(apiService, null));
        
        var settings = getSettingsMethod.Invoke(apiService, null);
        Assert.NotNull(settings);
        
        var settingsType = settings.GetType();
        var urlProperty = settingsType.GetProperty("Url");
        var apiKeyProperty = settingsType.GetProperty("ApiKey");
        var enableLoggingProperty = settingsType.GetProperty("EnableLogging");
        
        Assert.Equal("https://api.production.com", urlProperty?.GetValue(settings));
        Assert.Equal("prod-key-12345", apiKeyProperty?.GetValue(settings));
        Assert.True((bool?)enableLoggingProperty?.GetValue(settings));
    }

    #endregion

}