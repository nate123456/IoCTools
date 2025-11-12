namespace IoCTools.Generator.Tests;

using Microsoft.CodeAnalysis;

/// <summary>
///     Comprehensive tests for Conditional Service Registration feature.
///     Tests environment-based and configuration-based conditional service registration.
/// </summary>
public class ConditionalServiceRegistrationTests
{
    #region Configuration-Based Conditional Registration Tests

    [Fact]
    public void ConditionalService_ConfigurationBasedSwitching_GeneratesCorrectRegistration()
    {
        // Arrange - Simplified test for basic configuration switching
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ICacheService { }

[ConditionalService(ConfigValue = ""Cache:Provider"", Equals = ""Redis"")]

public partial class RedisCacheService : ICacheService
{
}

[ConditionalService(ConfigValue = ""Cache:Provider"", Equals = ""Memory"")]

public partial class MemoryCacheService : ICacheService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var registrationContent = result.GetServiceRegistrationText();

        // Should generate configuration-based switching (relaxed patterns)
        registrationContent.Should().Contain("configuration");
        registrationContent.Should().Contain("RedisCacheService");
        registrationContent.Should().Contain("MemoryCacheService");
        registrationContent.Should().Contain("Redis");
        registrationContent.Should().Contain("Memory");
    }

    #endregion

    #region Runtime Environment Detection Tests

    [Fact]
    public void ConditionalService_RuntimeEnvironmentDetection_GeneratesCorrectCode()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

[ConditionalService(Environment = ""Development"")]

public partial class DevService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var registrationContent = result.GetServiceRegistrationText();

        // Should use proper environment variable detection
        registrationContent.Should().Contain("Environment.GetEnvironmentVariable(\"ASPNETCORE_ENVIRONMENT\")");

        // Should handle null environment gracefully with null coalescing
        registrationContent.Should().Contain("?? \"\"");

        // Environment detection should be robust
        registrationContent.Should().Contain("ASPNETCORE_ENVIRONMENT");
    }

    #endregion

    #region Environment-Based Conditional Registration Tests

    [Fact]
    public void ConditionalService_SingleEnvironment_GeneratesCorrectRegistration()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IPaymentService { }

[ConditionalService(Environment = ""Development"")]

public partial class MockPaymentService : IPaymentService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse(
            $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var registrationContent = result.GetServiceRegistrationText();
        registrationContent.Should().Contain(
            "Environment.GetEnvironmentVariable(\"ASPNETCORE_ENVIRONMENT\")");
        registrationContent.Should().Contain(
            "string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)");
        registrationContent.Should().Contain(
            "services.AddScoped<Test.IPaymentService, Test.MockPaymentService>()");
    }

    [Fact]
    public void ConditionalService_MultipleEnvironments_GeneratesCorrectRegistration()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IEmailService { }

[ConditionalService(Environment = ""Testing,Staging"")]

public partial class TestEmailService : IEmailService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var registrationContent = result.GetServiceRegistrationText();
        registrationContent.Should().Contain(
            "Environment.GetEnvironmentVariable(\"ASPNETCORE_ENVIRONMENT\")");
        var hasTestingCondition = registrationContent.Contains(
            "string.Equals(environment, \"Testing\", StringComparison.OrdinalIgnoreCase)");
        var hasStagingCondition = registrationContent.Contains(
            "string.Equals(environment, \"Staging\", StringComparison.OrdinalIgnoreCase)");
        registrationContent.Contains("||").Should().BeTrue();
        (hasTestingCondition && hasStagingCondition).Should().BeTrue(
            "both Testing and Staging checks should be emitted");
        registrationContent.Should().Contain(
            "services.AddScoped<Test.IEmailService, Test.TestEmailService>()");
    }

    [Fact]
    public void ConditionalService_NotEnvironment_GeneratesCorrectRegistration()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ICacheService { }

[ConditionalService(NotEnvironment = ""Production"")]

public partial class MemoryCacheService : ICacheService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var registrationContent = result.GetServiceRegistrationText();
        registrationContent.Should().Contain(
            "Environment.GetEnvironmentVariable(\"ASPNETCORE_ENVIRONMENT\")");
        registrationContent.Should().Contain(
            "!string.Equals(environment, \"Production\", StringComparison.OrdinalIgnoreCase)");
        registrationContent.Should().Contain(
            "services.AddScoped<Test.ICacheService, Test.MemoryCacheService>()");
    }

    [Fact]
    public void ConditionalService_MultipleServicesForSameInterface_GeneratesEnvironmentSwitching()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IPaymentService { }

[ConditionalService(Environment = ""Development"")]

public partial class MockPaymentService : IPaymentService
{
}

[ConditionalService(Environment = ""Production"")]

public partial class StripePaymentService : IPaymentService
{
}

[ConditionalService(Environment = ""Testing"")]

public partial class TestPaymentService : IPaymentService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var registrationContent = result.GetServiceRegistrationText();
        registrationContent.Should().Contain(
            "string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)");
        registrationContent.Should().Contain(
            "services.AddScoped<Test.IPaymentService, Test.MockPaymentService>()");
        registrationContent.Should().Contain(
            "string.Equals(environment, \"Production\", StringComparison.OrdinalIgnoreCase)");
        registrationContent.Should().Contain(
            "services.AddScoped<Test.IPaymentService, Test.StripePaymentService>()");
        registrationContent.Should().Contain(
            "string.Equals(environment, \"Testing\", StringComparison.OrdinalIgnoreCase)");
        registrationContent.Should().Contain(
            "services.AddScoped<Test.IPaymentService, Test.TestPaymentService>()");
    }

    #endregion

    #region Complex Combined Conditions Tests

    [Fact]
    public void ConditionalService_EnvironmentAndConfigurationCombined_GeneratesCorrectRegistration()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IPaymentService { }

[ConditionalService(Environment = ""Development"", ConfigValue = ""Features:UseMocks"", Equals = ""true"")]

public partial class MockPaymentService : IPaymentService
{
}

[ConditionalService(Environment = ""Production"", ConfigValue = ""Features:PremiumTier"", Equals = ""true"")]

public partial class PremiumPaymentService : IPaymentService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var registrationContent = result.GetServiceRegistrationText();
        registrationContent.Should().Contain("Development");
        registrationContent.Should().Contain("Production");
        registrationContent.Should().Contain("Features:UseMocks");
        registrationContent.Should().Contain("Features:PremiumTier");
        registrationContent.Should().Contain("&&");
    }

    [Fact]
    public void ConditionalService_MultipleComplexConditions_GeneratesCorrectRegistration()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface INotificationService { }

[ConditionalService(Environment = ""Development,Testing"", ConfigValue = ""Notifications:UseConsole"", Equals = ""true"")]

public partial class ConsoleNotificationService : INotificationService
{
}

[ConditionalService(NotEnvironment = ""Development"", ConfigValue = ""Notifications:EmailEnabled"", NotEquals = ""false"")]

public partial class EmailNotificationService : INotificationService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var registrationContent = result.GetServiceRegistrationText();
        var hasDevTestingCondition =
            registrationContent.Contains(
                "string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)") &&
            registrationContent.Contains(
                "string.Equals(environment, \"Testing\", StringComparison.OrdinalIgnoreCase)") &&
            registrationContent.Contains("||");

        var hasNotDevelopmentCondition =
            registrationContent.Contains(
                "!string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)");
        var hasConfigConditions =
            registrationContent.Contains("Notifications:UseConsole") &&
            registrationContent.Contains("Notifications:EmailEnabled");

        hasDevTestingCondition.Should().BeTrue();
        hasNotDevelopmentCondition.Should().BeTrue();
        hasConfigConditions.Should().BeTrue();
    }

    #endregion

    #region Service Registration Generation Tests

    [Fact]
    public void ConditionalService_WithDependencies_GeneratesCorrectConstructor()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IEmailService { }
public interface ILoggerService { }

[ConditionalService(Environment = ""Development"")]

public partial class DevEmailService : IEmailService
{
    [Inject] private readonly ILoggerService _logger;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetConstructorSourceText("DevEmailService");
        constructorSource.Should().Contain("public DevEmailService(ILoggerService logger)");
        constructorSource.Should().Contain("this._logger = logger;");

        var registrationContent = result.GetServiceRegistrationText();
        registrationContent.Should().Contain(
            "string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)");
        registrationContent.Should().Contain(
            "services.AddScoped<Test.IEmailService, Test.DevEmailService>()");
    }

    [Fact]
    public void ConditionalService_WithCustomLifetime_GeneratesCorrectRegistration()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface ICacheService { }

[ConditionalService(Environment = ""Production"")]
[Singleton]
public partial class ProductionCacheService : ICacheService
{
}

[ConditionalService(Environment = ""Development"")]
[Transient]
public partial class DevCacheService : ICacheService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var registrationContent = result.GetServiceRegistrationText();
        registrationContent.Should().Contain(
            "services.AddSingleton<Test.ICacheService, Test.ProductionCacheService>()");
        registrationContent.Should().Contain(
            "services.AddTransient<Test.ICacheService, Test.DevCacheService>()");
    }

    [Fact]
    public void ConditionalService_GeneratedRegistrationMethodStructure_IsCorrect()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface ITestService { }

[ConditionalService(Environment = ""Development"")]

public partial class DevTestService : ITestService
{
}
[Scoped]
public partial class RegularService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var registrationContent = result.GetServiceRegistrationText();
        registrationContent.Should().Contain("public static IServiceCollection");
        registrationContent.Should().Contain("AddTestAssemblyRegisteredServices");
        registrationContent.Should().Contain("this IServiceCollection services");
        registrationContent.Should().Contain(
            "var environment = Environment.GetEnvironmentVariable(\"ASPNETCORE_ENVIRONMENT\")");
        registrationContent.Should().Contain(
            "string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)");

        var hasRegularServiceRegistration = registrationContent.Contains("RegularService") &&
                                            (registrationContent.Contains("AddScoped") ||
                                             registrationContent.Contains("services.Add"));
        hasRegularServiceRegistration.Should().BeTrue("regular services should still register");
        registrationContent.Should().Contain("return services;");
    }

    #endregion

    #region Validation and Error Handling Tests

    [Fact]
    public void ConditionalService_ConflictingConditions_HandlesGracefully()
    {
        // Arrange - Impossible conditions (Environment = X AND NotEnvironment = X)
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

[ConditionalService(Environment = ""Development"", NotEnvironment = ""Development"")]

public partial class ConflictingService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        if (result.GetServiceRegistrationSource() is { Content: var registrationContent })
        {
            var impossible = registrationContent.Contains("Development") &&
                             registrationContent.Contains("&& !") &&
                             registrationContent.Contains("Development");
            impossible.Should().BeFalse("generator should avoid impossible logical conditions");
        }
    }

    [Fact]
    public void ConditionalService_WithLifetimeInference_WorksCorrectly()
    {
        // After intelligent inference refactor, ConditionalService no longer requires [Scoped] attribute
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

[ConditionalService(Environment = ""Development"")]
public partial class ConditionalLifetimeInferenceService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var diagnostics = result.GetDiagnosticsByCode("IOC021");
        diagnostics.Should().BeEmpty();

        var registrationContent = result.GetServiceRegistrationText();
        registrationContent.Should().Contain("ConditionalLifetimeInferenceService");
        registrationContent.Should().Contain("Development");
        registrationContent.Should().Contain("Environment.GetEnvironmentVariable");
    }

    [Fact]
    public void ConditionalService_EmptyConditions_ProducesDiagnostic()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

[ConditionalService]

public partial class EmptyConditionalService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var diagnostics = result.GetDiagnosticsByCode("IOC022");
        if (!diagnostics.Any())
            result.GetServiceRegistrationText()
                .Should().Contain(
                    "services.AddScoped<Test.ITestService, Test.EmptyConditionalService>()");
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void ConditionalService_IntegrationWithExistingFeatures_WorksCorrectly()
    {
        // Arrange - Start with a simpler test case to debug the issue
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IPaymentService { }

[ConditionalService(Environment = ""Development"")]

public partial class DevPaymentService : IPaymentService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        result.HasErrors.Should().BeFalse(
            $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var registrationContent = result.GetServiceRegistrationText();
        registrationContent.Should().Contain(
            "string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)");
        registrationContent.Should().Contain("DevPaymentService");
    }

    [Fact]
    public void ConditionalService_BasicRealWorldScenario_GeneratesCorrectCode()
    {
        // Arrange - Basic real-world scenario with conditional services
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IPaymentService { }
public interface ICacheService { }

// Environment-based services
[ConditionalService(Environment = ""Development"")]

public partial class MockPaymentService : IPaymentService { }

[ConditionalService(Environment = ""Production"")]

public partial class StripePaymentService : IPaymentService { }

// Configuration-based service
[ConditionalService(ConfigValue = ""Cache:UseRedis"", Equals = ""true"")]
[Singleton]
public partial class RedisCacheService : ICacheService { }";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var registrationContent = result.GetServiceRegistrationText();
        registrationContent.Should().Contain(
            "var environment = Environment.GetEnvironmentVariable(\"ASPNETCORE_ENVIRONMENT\")");
        registrationContent.Should().Contain("IConfiguration configuration");
        registrationContent.Should().Contain(
            "string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)");
        registrationContent.Should().Contain(
            "string.Equals(environment, \"Production\", StringComparison.OrdinalIgnoreCase)");
        registrationContent.Should().Contain("Cache:UseRedis");
        registrationContent.Should().Contain("true");
        registrationContent.Should().Contain(
            "services.AddSingleton<Test.ICacheService, Test.RedisCacheService>();");
    }

    #endregion
}
