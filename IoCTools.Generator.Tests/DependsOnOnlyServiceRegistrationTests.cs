namespace IoCTools.Generator.Tests;

/// <summary>
///     Tests for the critical bug fix: Services with ONLY [DependsOn] attributes (no [Inject] fields)
///     should be automatically registered in the service container, not just get constructor generation.
///     This test class ensures the bug reported by external AI integration doesn't regress:
///     "Repository classes with proper IoC attributes not being detected/registered"
/// </summary>
public class DependsOnOnlyServiceRegistrationTests
{
    [Fact]
    public void DependsOnOnly_SingleAttribute_ShouldGenerateServiceRegistration()
    {
        // Arrange - Service with ONLY [DependsOn], no [Inject] fields
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace Test;

public interface ITestRepository { }

[DependsOn<ILogger<TestRepository>>]
public partial class TestRepository : ITestRepository
{
    public void DoWork()
    {
        _logger.LogInformation(""Repository working"");
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should()
            .BeFalse($"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Verify the service is registered (not just constructor generated)
        registrationSource.Content.Should()
            .Contain("AddScoped<global::Test.TestRepository, global::Test.TestRepository>");
        registrationSource.Content.Should()
            .Contain("AddScoped<global::Test.ITestRepository, global::Test.TestRepository>");

        // Verify constructor is still generated
        var constructorSource = result.GetRequiredConstructorSource("TestRepository");
        constructorSource.Content.Should().Contain("private readonly ILogger<TestRepository> _logger;");
    }

    [Fact]
    public void DependsOnOnly_MultipleAttributes_ShouldGenerateServiceRegistration()
    {
        // Arrange - Service with multiple [DependsOn] attributes, no [Inject]
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace Test;

[DependsOn<ILogger<MultiDependencyService>>]
[DependsOn<IConfiguration>]
public partial class MultiDependencyService
{
    public void ProcessData()
    {
        var setting = _configuration[""TestSetting""];
        _logger.LogInformation(""Processing with setting: {Setting}"", setting);
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should()
            .BeFalse($"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Verify the service is registered with default Scoped lifetime
        registrationSource.Content.Should()
            .Contain("AddScoped<global::Test.MultiDependencyService, global::Test.MultiDependencyService>");

        // Verify constructor generation for both dependencies
        var constructorSource = result.GetRequiredConstructorSource("MultiDependencyService");
        constructorSource.Content.Should().Contain("private readonly ILogger<MultiDependencyService> _logger;");
        constructorSource.Content.Should().Contain("private readonly IConfiguration _configuration;");
        constructorSource.Content.Should()
            .Contain(
                "public MultiDependencyService(ILogger<MultiDependencyService> logger, IConfiguration configuration)");
    }

    [Fact]
    public void DependsOnOnly_WithExternalFlag_ShouldGenerateServiceRegistration()
    {
        // Arrange - Service using [DependsOn] with external flag (like ExternalServiceDemoService)
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace Test;

[DependsOn<IConfiguration>(external: true)]
[DependsOn<ILogger<ExternalDemoService>>(external: true, prefix: ""ext_"", stripI: false)]
public partial class ExternalDemoService
{
    public void DemonstrateExternal()
    {
        var setting = _configuration[""DemoSetting""] ?? ""default"";
        ext_logger.LogInformation(""External service demo: {Setting}"", setting);
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should()
            .BeFalse($"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Verify the service is registered even with external dependencies
        registrationSource.Content.Should()
            .Contain("AddScoped<global::Test.ExternalDemoService, global::Test.ExternalDemoService>");

        // Verify constructor generation with proper naming
        var constructorSource = result.GetRequiredConstructorSource("ExternalDemoService");
        constructorSource.Content.Should().Contain("private readonly IConfiguration _configuration;");
        constructorSource.Content.Should().Contain("private readonly ILogger<ExternalDemoService> ext_logger;");
    }

    [Fact]
    public void DependsOnOnly_RepositoryPattern_ShouldGenerateServiceRegistration()
    {
        // Arrange - Typical repository pattern that was failing in external AI integration
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace Test;

public interface IUserRepository
{
    User GetUser(int id);
}

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

[DependsOn<ILogger<UserRepository>>]
public partial class UserRepository : IUserRepository
{
    public User GetUser(int id)
    {
        _logger.LogInformation(""Getting user {UserId}"", id);
        return new User { Id = id, Name = $""User{id}"" };
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should()
            .BeFalse($"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // This is the key test - repository should be auto-registered
        registrationSource.Content.Should()
            .Contain("AddScoped<global::Test.UserRepository, global::Test.UserRepository>");
        registrationSource.Content.Should()
            .Contain("AddScoped<global::Test.IUserRepository, global::Test.UserRepository>");
    }

    [Fact]
    public void DependsOnOnly_WithLifetimeAttribute_ShouldRespectLifetime()
    {
        // Arrange - [DependsOn]-only service with explicit lifetime
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace Test;

[Singleton]
[DependsOn<ILogger<SingletonRepositoryService>>]
public partial class SingletonRepositoryService
{
    public void ProcessData()
    {
        _logger.LogInformation(""Singleton repository processing"");
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should()
            .BeFalse($"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should respect the explicit [Singleton] lifetime, not default to Scoped
        registrationSource.Content.Should()
            .Contain("AddSingleton<global::Test.SingletonRepositoryService, global::Test.SingletonRepositoryService>");
        registrationSource.Content.Should().NotContain("AddScoped<global::Test.SingletonRepositoryService");
    }

    [Fact]
    public void DependsOnOnly_GenericService_ShouldGenerateServiceRegistration()
    {
        // Arrange - Generic service with [DependsOn] only
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace Test;

public interface IGenericRepository<T> where T : class { }

[DependsOn<ILogger<object>>] // Note: Cannot use type parameters in attributes in C#
public partial class GenericRepository<T> : IGenericRepository<T> where T : class
{
    public void ProcessEntity(T entity)
    {
        _logger.LogInformation(""Processing entity of type {Type}"", typeof(T).Name);
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should()
            .BeFalse($"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should register generic services properly
        registrationSource.Content.Should()
            .Contain("AddScoped(typeof(global::Test.GenericRepository<>), typeof(global::Test.GenericRepository<>))");
        registrationSource.Content.Should()
            .Contain("AddScoped(typeof(global::Test.IGenericRepository<>), typeof(global::Test.GenericRepository<>))");
    }
}
