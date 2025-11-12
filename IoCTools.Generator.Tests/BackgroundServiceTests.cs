namespace IoCTools.Generator.Tests;

using Microsoft.CodeAnalysis;

public class BackgroundServiceTests
{
    /// <summary>
    ///     Test that classes inheriting from BackgroundService are automatically registered as IHostedService
    /// </summary>
    [Fact]
    public void BackgroundService_AutoDetection_RegistersAsHostedService()
    {
        var sourceCode = @"
using Microsoft.Extensions.Hosting;
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Threading;
using System.Threading.Tasks;

namespace Test;

[Scoped]
public partial class EmailBackgroundService : BackgroundService
{
    [Inject] private readonly IEmailService _emailService;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Background work
    }
}

public interface IEmailService { }
[Scoped] public partial class EmailService : IEmailService { }
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        result.HasErrors.Should().BeFalse();

        // Should generate constructor
        var constructorSource = result.GetRequiredConstructorSource("EmailBackgroundService");
        constructorSource.Content.Should().Contain("EmailBackgroundService(IEmailService emailService)");

        // Should register as IHostedService 
        var registrationSource = result.GetRequiredServiceRegistrationSource();
        registrationSource.Content.Should().Contain("services.AddHostedService<global::Test.EmailBackgroundService>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.EmailService, global::Test.EmailService>");
    }

    /// <summary>
    ///     Test explicit BackgroundService attribute with default settings
    /// </summary>
    [Fact]
    public void BackgroundService_ExplicitAttribute_RegistersCorrectly()
    {
        var sourceCode = @"
using Microsoft.Extensions.Hosting;
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Threading;
using System.Threading.Tasks;

namespace Test;

[Scoped]
public partial class DataProcessingService : BackgroundService
{
    [Inject] private readonly IDataProcessor _processor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Background work
    }
}

public interface IDataProcessor { }
[Scoped] public partial class DataProcessor : IDataProcessor { }
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();
        registrationSource.Content.Should().Contain("services.AddHostedService<global::Test.DataProcessingService>");
    }

    /// <summary>
    ///     Test BackgroundService with AutoRegister = false should not register as IHostedService
    /// </summary>
    [Fact]
    public void BackgroundService_AutoRegisterFalse_DoesNotRegister()
    {
        var sourceCode = @"
using Microsoft.Extensions.Hosting;
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Threading;
using System.Threading.Tasks;

namespace Test;

[SkipRegistration]
public partial class ManualBackgroundService : BackgroundService
{
    [Inject] private readonly IProcessor _processor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Background work
    }
}

public interface IProcessor { }
[Scoped] public partial class Processor : IProcessor { }
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        result.HasErrors.Should().BeFalse();

        // Should still generate constructor
        _ = result.GetRequiredConstructorSource("ManualBackgroundService");

        // Should NOT register as IHostedService
        var registrationSource = result.GetRequiredServiceRegistrationSource();
        registrationSource.Content.Should().NotContain("ManualBackgroundService");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.Processor, global::Test.Processor>");
    }

    /// <summary>
    ///     Test BackgroundService with Service attribute - no IOC014 warnings for hosted services
    /// </summary>
    [Fact]
    public void BackgroundService_WithLifetimeAttributes_NoLifetimeWarning()
    {
        var sourceCode = @"
using Microsoft.Extensions.Hosting;
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

[Transient]
public partial class TransientBackgroundService : BackgroundService
{
    [Inject] private readonly IWorker _worker;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Background work
    }
}

public interface IWorker { }
[Scoped] public partial class Worker : IWorker { }
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // No IOC014 errors for hosted services - their lifetime is managed by AddHostedService()
        var warnings = result.GetDiagnosticsByCode("IOC014");
        warnings.Should().BeEmpty();

        // Should still register as IHostedService (not as regular service)
        var registrationSource = result.GetRequiredServiceRegistrationSource();
        registrationSource.Content.Should()
            .Contain("services.AddHostedService<global::Test.TransientBackgroundService>");
        // Should NOT contain regular service registration
        registrationSource.Content.Should().NotContain("services.AddTransient<TransientBackgroundService>");
    }

    /// <summary>
    ///     Test BackgroundService with Singleton lifetime has no warnings
    /// </summary>
    [Fact]
    public void BackgroundService_SingletonLifetime_NoWarning()
    {
        var sourceCode = @"
using Microsoft.Extensions.Hosting;
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using IoCTools.Abstractions.Enumerations;
using System.Threading;
using System.Threading.Tasks;

namespace Test;

[Singleton]
public partial class SingletonBackgroundService : BackgroundService
{
    [Inject] private readonly IHandler _handler;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Background work
    }
}

public interface IHandler { }
[Scoped] public partial class Handler : IHandler { }
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Should NOT have IOC014 warning (Singleton is recommended)
        var warnings = result.GetDiagnosticsByCode("IOC014");
        warnings.Should().BeEmpty();

        // Should still register as IHostedService
        var registrationSource = result.GetRequiredServiceRegistrationSource();
        registrationSource.Content.Should()
            .Contain("services.AddHostedService<global::Test.SingletonBackgroundService>");
    }

    /// <summary>
    ///     Test BackgroundService class that is not partial should produce error
    /// </summary>
    [Fact]
    public void BackgroundService_NotPartial_ProducesError()
    {
        var sourceCode = @"
using Microsoft.Extensions.Hosting;
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Threading;
using System.Threading.Tasks;

namespace Test;

public class NonPartialBackgroundService : BackgroundService
{
    [Inject] private readonly IService _service;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Background work
    }
}

public interface IService { }
[Scoped] public partial class ServiceImpl : IService { }
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Should have IOC011 error about not being partial
        var errors = result.GetDiagnosticsByCode("IOC011");
        errors.Should().ContainSingle();
        errors[0].GetMessage().Should().Contain("NonPartialBackgroundService");
        errors[0].Severity.Should().Be(DiagnosticSeverity.Error);
    }

    /// <summary>
    ///     Test BackgroundService with multiple dependencies and inheritance
    /// </summary>
    [Fact]
    public void BackgroundService_ComplexDependencies_GeneratesCorrectly()
    {
        var sourceCode = @"
using Microsoft.Extensions.Hosting;
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Threading;
using System.Threading.Tasks;

namespace Test;

[Scoped]
public partial class ComplexBackgroundService : BackgroundService
{
    [Inject] private readonly ILogger _logger;
    [Inject] private readonly IEmailService _emailService;
    [Inject] private readonly IDataRepository _repository;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Complex background work
    }
}

public interface ILogger { }
public interface IEmailService { }
public interface IDataRepository { }

[Scoped] public partial class Logger : ILogger { }
[Scoped] public partial class EmailService : IEmailService { }
[Scoped] public partial class DataRepository : IDataRepository { }
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        result.HasErrors.Should().BeFalse();

        // Should generate constructor with all dependencies
        var constructorSource = result.GetRequiredConstructorSource("ComplexBackgroundService");
        constructorSource.Content.Should()
            .Contain(
                "ComplexBackgroundService(ILogger logger, IEmailService emailService, IDataRepository repository)");

        // Should register as IHostedService
        var registrationSource = result.GetRequiredServiceRegistrationSource();
        registrationSource.Content.Should().Contain("services.AddHostedService<global::Test.ComplexBackgroundService>");
    }

    /// <summary>
    ///     Test BackgroundService with DependsOn attribute
    /// </summary>
    [Fact]
    public void BackgroundService_WithDependsOn_GeneratesCorrectly()
    {
        var sourceCode = @"
using Microsoft.Extensions.Hosting;
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Threading;
using System.Threading.Tasks;

namespace Test;

[Scoped]
[DependsOn<IConfigurationService, IMetrics>]
public partial class MonitoringBackgroundService : BackgroundService
{
    [Inject] private readonly ILogger _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Monitoring work
    }
}

public interface ILogger { }
public interface IConfigurationService { }
public interface IMetrics { }

[Scoped] public partial class Logger : ILogger { }
[Scoped] public partial class ConfigurationService : IConfigurationService { }
[Scoped] public partial class Metrics : IMetrics { }
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        result.HasErrors.Should().BeFalse();

        // Should generate constructor with DependsOn + Inject dependencies
        var constructorSource = result.GetRequiredConstructorSource("MonitoringBackgroundService");
        // DependsOn dependencies come first, then Inject dependencies
        constructorSource.Content.Should()
            .Contain(
                "MonitoringBackgroundService(IConfigurationService configurationService, IMetrics metrics, ILogger logger)");
    }

    /// <summary>
    ///     Test that BackgroundService registration appears in service registration
    /// </summary>
    [Fact]
    public void BackgroundService_RegistersInServiceCollection_CorrectFormat()
    {
        var sourceCode = @"
using Microsoft.Extensions.Hosting;
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Threading;
using System.Threading.Tasks;

namespace Test;

[Scoped]
public partial class SimpleBackgroundService : BackgroundService
{
    [Inject] private readonly IEmailService _emailService;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(1000, stoppingToken);
    }
}

public interface IEmailService { }
[Scoped] public partial class EmailService : IEmailService { }
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should contain the correct using statements
        registrationSource.Content.Should().Contain("using Microsoft.Extensions.Hosting;");

        // Should register as IHostedService with Singleton lifetime
        registrationSource.Content.Should().Contain("services.AddHostedService<global::Test.SimpleBackgroundService>");

        // Should also register the dependency service
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.EmailService, global::Test.EmailService>");
    }

    /// <summary>
    ///     Test BackgroundService with custom ServiceName - no IOC014 warnings for hosted services
    /// </summary>
    [Fact]
    public void BackgroundService_WithCustomServiceName_NoWarnings()
    {
        var sourceCode = @"
using Microsoft.Extensions.Hosting;
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using IoCTools.Abstractions.Enumerations;
using System.Threading;
using System.Threading.Tasks;

namespace Test;

[Transient]
public partial class EmailProcessorService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Process emails
    }
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // No IOC014 errors for hosted services - their lifetime is managed by AddHostedService()
        var warnings = result.GetDiagnosticsByCode("IOC014");
        warnings.Should().BeEmpty();
    }
}
