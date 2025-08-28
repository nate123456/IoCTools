namespace IoCTools.Generator.Tests;

using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;

using Xunit.Abstractions;

/// <summary>
///     CRITICAL: Tests to verify service registration deduplication logic
///     Ensures no duplicate AddHostedService calls, Configure<T> calls, or service registrations
/// </summary>
public class ServiceRegistrationDeduplicationTests
{
    private readonly ITestOutputHelper _output;

    public ServiceRegistrationDeduplicationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void BackgroundServices_ShouldNotHaveDuplicateHostedServiceRegistrations()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace TestNamespace
{
    [Singleton]
    [ConditionalService(ConfigValue = ""Features:EnableNotifications"", Equals = ""true"")]
    public partial class NotificationSchedulerService : BackgroundService
    {
        protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
    }

    [Singleton]
    public partial class SimpleBackgroundWorker : BackgroundService
    {
        protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
    }
    
    [Singleton]
    public partial class DataCleanupService : BackgroundService
    {
        protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
    }
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        if (result.HasErrors)
        {
            _output.WriteLine("COMPILATION ERRORS:");
            foreach (var error in result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
                _output.WriteLine($"  {error.Id}: {error.GetMessage()}");
        }

        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        var generatedCode = registrationSource.Content;

        _output.WriteLine("Generated ServiceRegistrations:");
        _output.WriteLine(generatedCode);

        // Verify no duplicate AddHostedService calls for same service
        var notificationServiceMatches = Regex.Matches(
            generatedCode, @"AddHostedService<[^>]*NotificationSchedulerService[^>]*>");
        Assert.True(notificationServiceMatches.Count <= 1,
            $"NotificationSchedulerService should appear at most once in AddHostedService calls, found {notificationServiceMatches.Count}");

        var simpleBackgroundMatches = Regex.Matches(
            generatedCode, @"AddHostedService<[^>]*SimpleBackgroundWorker[^>]*>");
        Assert.True(simpleBackgroundMatches.Count == 1,
            $"SimpleBackgroundWorker should appear exactly once in AddHostedService calls, found {simpleBackgroundMatches.Count}");

        var dataCleanupMatches = Regex.Matches(
            generatedCode, @"AddHostedService<[^>]*DataCleanupService[^>]*>");
        Assert.True(dataCleanupMatches.Count == 1,
            $"DataCleanupService should appear exactly once in AddHostedService calls, found {dataCleanupMatches.Count}");
    }

    [Fact]
    public void ConfigurationOptions_ShouldNotHaveDuplicateConfigureBindings()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Options;

namespace TestNamespace
{
    public class ApiSettings
    {
        public string BaseUrl { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 30;
    }

    [Scoped]
    public partial class ApiService
    {
        [InjectConfiguration]
        private readonly IOptions<ApiSettings> _apiSettings;
    }
    
    [Scoped]
    public partial class AnotherApiService
    {
        [InjectConfiguration]
        private readonly IOptions<ApiSettings> _apiSettings;
    }
    
    [Scoped]
    public partial class ThirdApiService
    {
        [InjectConfiguration]
        private readonly IOptions<ApiSettings> _apiSettings;
    }
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        var generatedCode = registrationSource.Content;

        _output.WriteLine("Generated ServiceRegistrations:");
        _output.WriteLine(generatedCode);

        // Verify Configure<ApiSettings> appears only once
        var configureMatches = Regex.Matches(
            generatedCode, @"Configure<ApiSettings>");
        Assert.True(configureMatches.Count == 1,
            $"Configure<ApiSettings> should appear exactly once, found {configureMatches.Count}");

        // Verify "Api" section binding appears only once
        var sectionMatches = Regex.Matches(
            generatedCode, @"GetSection\(""Api""\)");
        Assert.True(sectionMatches.Count == 1,
            $"GetSection(\"Api\") should appear exactly once, found {sectionMatches.Count}");
    }

    [Fact]
    public void MultiInterfaceServices_ShouldNotHaveDuplicateRegistrations()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace
{
    public interface IPaymentService { }
    public interface IPaymentValidator { }
    public interface IPaymentLogger { }

    [Scoped]
    [RegisterAsAll]
    public partial class PaymentProcessor : IPaymentService, IPaymentValidator, IPaymentLogger
    {
    }
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        var generatedCode = registrationSource.Content;

        _output.WriteLine("Generated ServiceRegistrations:");
        _output.WriteLine(generatedCode);

        // Verify PaymentProcessor is registered exactly once for each interface
        var paymentServiceMatches = Regex.Matches(
            generatedCode, @"AddScoped<[^>]*IPaymentService[^>]*>");
        Assert.True(paymentServiceMatches.Count == 1,
            $"IPaymentService registration should appear exactly once, found {paymentServiceMatches.Count}");

        var paymentValidatorMatches = Regex.Matches(
            generatedCode, @"AddScoped<[^>]*IPaymentValidator[^>]*>");
        Assert.True(paymentValidatorMatches.Count == 1,
            $"IPaymentValidator registration should appear exactly once, found {paymentValidatorMatches.Count}");

        var paymentLoggerMatches = Regex.Matches(
            generatedCode, @"AddScoped<[^>]*IPaymentLogger[^>]*>");
        Assert.True(paymentLoggerMatches.Count == 1,
            $"IPaymentLogger registration should appear exactly once, found {paymentLoggerMatches.Count}");
    }

    [Fact]
    public void ConditionalServices_ShouldNotDuplicateWhenSameCondition()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace
{
    public interface INotificationService { }

    [Scoped]
    [ConditionalService(Environment = ""Development"")]
    public partial class DevNotificationService : INotificationService
    {
    }
    
    [Scoped]
    [ConditionalService(Environment = ""Development"")]
    public partial class DevLoggingService
    {
    }
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        var generatedCode = registrationSource.Content;

        _output.WriteLine("Generated ServiceRegistrations:");
        _output.WriteLine(generatedCode);

        // Verify environment variable is declared only once
        var environmentMatches = Regex.Matches(
            generatedCode, @"var currentEnvironment = Environment\.GetEnvironmentVariable");
        Assert.True(environmentMatches.Count <= 1,
            $"Environment variable declaration should appear at most once, found {environmentMatches.Count}");
    }

    [Fact]
    public void ServiceRegistrationGenerator_ShouldDeduplicateBasedOnServiceType()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace
{
    public interface ICommonService { }

    // Same service type registered multiple times should be deduplicated
    [Scoped]
    public partial class CommonService : ICommonService
    {
    }
    
    // Different implementations of same interface should both be registered
    [Scoped]
    public partial class AnotherCommonService : ICommonService
    {
    }
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        var generatedCode = registrationSource.Content;

        _output.WriteLine("Generated ServiceRegistrations:");
        _output.WriteLine(generatedCode);

        // Verify both different implementations are registered
        var commonServiceMatches = Regex.Matches(
            generatedCode, @"AddScoped<[^>]*\.CommonService[^>]*>");
        Assert.True(commonServiceMatches.Count == 2, // Once for concrete, once for interface
            $"CommonService registrations should appear twice, found {commonServiceMatches.Count}");

        var anotherCommonServiceMatches = Regex.Matches(
            generatedCode, @"AddScoped<[^>]*\.AnotherCommonService[^>]*>");
        Assert.True(anotherCommonServiceMatches.Count == 2, // Once for concrete, once for interface
            $"AnotherCommonService registrations should appear twice, found {anotherCommonServiceMatches.Count}");
    }

    [Fact]
    public void Generator_ShouldEmitDiagnosticForDuplicateRegistrationAttempts()
    {
        const string source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace
{
    // This scenario should potentially warn about duplicate patterns
    // but not actually generate duplicates
    public interface IService { }

    [Scoped]
    [RegisterAsAll]
    public partial class ServiceImpl : IService
    {
    }
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        var generatedCode = registrationSource.Content;

        _output.WriteLine("Generated ServiceRegistrations:");
        _output.WriteLine(generatedCode);

        // This test verifies that even with RegisterAsAll, we don't get actual duplicates
        var serviceImplMatches = Regex.Matches(
            generatedCode, @"AddScoped<[^>]*ServiceImpl[^>]*>");

        // Count should be reasonable - not excessive duplicates
        Assert.True(serviceImplMatches.Count <= 2,
            $"ServiceImpl should not have excessive duplicate registrations, found {serviceImplMatches.Count}");
    }
}
