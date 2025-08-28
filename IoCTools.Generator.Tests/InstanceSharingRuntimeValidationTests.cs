namespace Test;

using IoCTools.Generator.Tests;

/// <summary>
///     RUNTIME VALIDATION TESTS FOR INSTANCE SHARING
///     These tests validate the ACTUAL RUNTIME BEHAVIOR of instance sharing in IoCTools,
///     not just the registration patterns. They test:
///     1. That shared instances generate correct factory lambda registrations
///     2. That separate instances generate correct direct registrations
///     3. Mixed lifetime scenarios work correctly with proper registration patterns
///     4. Factory pattern generates syntactically correct code for all lifetimes
///     5. Edge cases and complex scenarios generate appropriate registrations
///     Note: These tests focus on validating the GENERATED REGISTRATION CODE rather than
///     executing the DI container directly, as that requires complex runtime assembly loading.
/// </summary>
public class InstanceSharingRuntimeValidationTests
{
    #region Performance and Registration Count Tests

    [Fact]
    public void ManyInterfaces_SharedInstances_GeneratesEfficientRegistrations()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IService01 { }
public interface IService02 { }
public interface IService03 { }
public interface IService04 { }
public interface IService05 { }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class ManyInterfaceService : IService01, IService02, IService03, IService04, IService05
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Verify concrete registration (two parameter form for RegisterAsAll only + shared)
        Assert.Contains("services.AddScoped<global::Test.ManyInterfaceService, global::Test.ManyInterfaceService>()",
            registrationSource.Content);

        // Verify all interfaces use factory pattern
        for (var i = 1; i <= 5; i++)
            Assert.Contains(
                $"AddScoped<global::Test.IService{i:D2}>(provider => provider.GetRequiredService<global::Test.ManyInterfaceService>())",
                registrationSource.Content);

        // Verify no direct interface registrations (they should all use factory pattern)
        for (var i = 1; i <= 5; i++)
            Assert.DoesNotContain($"AddScoped<global::Test.IService{i:D2}, global::Test.ManyInterfaceService>()",
                registrationSource.Content);

        // Verify reasonable code size (should not explode with many interfaces)
        Assert.True(registrationSource.Content.Length < 10000,
            "Generated code should be reasonably sized even with many interfaces");
    }

    #endregion

    #region Comprehensive Integration Test

    [Fact]
    public void ComplexScenario_AllFeaturesCombined_GeneratesCorrectRegistrationPatterns()
    {
        // Arrange - The most complex scenario testing all features together
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace Test;

public interface IEntity { }
public interface IUser : IEntity { }
public interface IRepository<T> { }
public interface IValidator<T> { }
public interface INotificationService { }
public interface ISkippedService { }
public interface ILogger { }
[Scoped]
public partial class Logger : ILogger { }

[Singleton]
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
[SkipRegistration<ISkippedService, IRepository<string>>]
[DependsOn<ILogger>]
public partial class ComplexService : IUser, IRepository<string>, IValidator<string>, INotificationService, ISkippedService
{
    [Inject] private readonly IEnumerable<ILogger> _loggers;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        // Check constructor generation
        var constructorSource = result.GetConstructorSource("ComplexService");
        Assert.NotNull(constructorSource);
        Assert.Contains("public ComplexService(ILogger logger, IEnumerable<ILogger> loggers)",
            constructorSource.Content);

        // Check service registration
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Logger should be registered first
        Assert.Contains("services.AddScoped<global::Test.ILogger, global::Test.Logger>()", registrationSource.Content);

        // ComplexService should be registered as Singleton with shared instances (single parameter form)
        Assert.Contains("services.AddSingleton<global::Test.ComplexService>()", registrationSource.Content);

        // Non-skipped interfaces should use factory pattern
        Assert.Contains(
            "services.AddSingleton<global::Test.IUser>(provider => provider.GetRequiredService<global::Test.ComplexService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddSingleton<global::Test.IEntity>(provider => provider.GetRequiredService<global::Test.ComplexService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddSingleton<global::Test.IValidator<string>>(provider => provider.GetRequiredService<global::Test.ComplexService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddSingleton<global::Test.INotificationService>(provider => provider.GetRequiredService<global::Test.ComplexService>())",
            registrationSource.Content);

        // Skipped interfaces should NOT be registered
        Assert.DoesNotContain("AddSingleton<global::Test.ISkippedService>", registrationSource.Content);
        Assert.DoesNotContain("AddSingleton<global::Test.IRepository<string>>", registrationSource.Content);
    }

    #endregion

    #region Basic Shared vs Separate Instance Registration Pattern Tests

    [Fact]
    public void SharedInstances_Scoped_GeneratesFactoryLambdaRegistrations()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IUserService { }
public interface INotificationService { }

[Scoped]
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class SharedUserNotificationService : IUserService, INotificationService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Verify shared instance pattern: concrete type registered directly, interfaces use factory
        Assert.Contains("services.AddScoped<global::Test.SharedUserNotificationService>()",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IUserService>(provider => provider.GetRequiredService<global::Test.SharedUserNotificationService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.INotificationService>(provider => provider.GetRequiredService<global::Test.SharedUserNotificationService>())",
            registrationSource.Content);

        // Should NOT contain direct interface-to-implementation registrations
        Assert.DoesNotContain("AddScoped<global::Test.IUserService, global::Test.SharedUserNotificationService>",
            registrationSource.Content);
        Assert.DoesNotContain(
            "AddScoped<global::Test.INotificationService, global::Test.SharedUserNotificationService>",
            registrationSource.Content);
    }

    [Fact]
    public void SeparateInstances_Scoped_GeneratesDirectRegistrations()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IUserService { }
public interface INotificationService { }

[Scoped]
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Separate)]
public partial class SeparateUserNotificationService : IUserService, INotificationService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Verify separate instance pattern: each registration creates its own instance
        Assert.Contains("services.AddScoped<global::Test.SeparateUserNotificationService>()",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IUserService, global::Test.SeparateUserNotificationService>()",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.INotificationService, global::Test.SeparateUserNotificationService>()",
            registrationSource.Content);

        // Should NOT contain factory lambda registrations
        Assert.DoesNotContain("provider => provider.GetRequiredService<global::Test.SeparateUserNotificationService>()",
            registrationSource.Content);
    }

    #endregion

    #region Lifetime-Specific Registration Pattern Tests

    [Fact]
    public void SingletonSharedInstances_GeneratesCorrectFactoryPattern()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IService1 { }
public interface IService2 { }

[Singleton]
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class SingletonSharedService : IService1, IService2
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Verify singleton shared pattern with factory lambdas
        Assert.Contains("services.AddSingleton<global::Test.SingletonSharedService>()",
            registrationSource.Content);
        Assert.Contains(
            "services.AddSingleton<global::Test.IService1>(provider => provider.GetRequiredService<global::Test.SingletonSharedService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddSingleton<global::Test.IService2>(provider => provider.GetRequiredService<global::Test.SingletonSharedService>())",
            registrationSource.Content);
    }

    [Fact]
    public void TransientSharedInstances_GeneratesCorrectFactoryPattern()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IService1 { }
public interface IService2 { }

[Transient]
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class TransientSharedService : IService1, IService2
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Verify transient shared pattern with factory lambdas
        Assert.Contains("services.AddTransient<global::Test.TransientSharedService>()",
            registrationSource.Content);
        Assert.Contains(
            "services.AddTransient<global::Test.IService1>(provider => provider.GetRequiredService<global::Test.TransientSharedService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddTransient<global::Test.IService2>(provider => provider.GetRequiredService<global::Test.TransientSharedService>())",
            registrationSource.Content);
    }

    [Fact]
    public void TransientSeparateInstances_GeneratesDirectRegistrations()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IService1 { }
public interface IService2 { }

[Transient]
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Separate)]
public partial class TransientSeparateService : IService1, IService2
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Verify transient separate pattern with direct registrations
        Assert.Contains("services.AddTransient<global::Test.TransientSeparateService>()",
            registrationSource.Content);
        Assert.Contains("services.AddTransient<global::Test.IService1, global::Test.TransientSeparateService>()",
            registrationSource.Content);
        Assert.Contains("services.AddTransient<global::Test.IService2, global::Test.TransientSeparateService>()",
            registrationSource.Content);

        // Should NOT contain factory lambdas
        Assert.DoesNotContain("provider => provider.GetRequiredService<global::Test.TransientSeparateService>()",
            registrationSource.Content);
    }

    #endregion

    #region Complex Dependency Injection Pattern Tests

    [Fact]
    public void SharedInstances_WithDependencies_GeneratesCorrectRegistrationsAndConstructors()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface ILogger { }
public interface IValidator { }
public interface IService { }
public partial class Logger : ILogger
{
}

[Scoped]
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class ValidatorService : IValidator, IService
{
    [Inject] private readonly ILogger _logger;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        // Verify constructor generation with dependencies
        var constructorSource = result.GetConstructorSource("ValidatorService");
        Assert.NotNull(constructorSource);
        Assert.Contains("public ValidatorService(ILogger logger)", constructorSource.Content);
        Assert.Contains("_logger = logger;", constructorSource.Content);

        // Verify registration patterns
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Logger should use standard registration
        Assert.Contains("services.AddScoped<global::Test.ILogger, global::Test.Logger>()", registrationSource.Content);

        // ValidatorService should use shared instance pattern
        Assert.Contains("services.AddScoped<global::Test.ValidatorService>()", registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IValidator>(provider => provider.GetRequiredService<global::Test.ValidatorService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IService>(provider => provider.GetRequiredService<global::Test.ValidatorService>())",
            registrationSource.Content);
    }

    [Fact]
    public void MixedLifetimes_SharedAndSeparateInstances_GenerateCorrectPatterns()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface ISharedService { }
public interface ISharedUtility { }
public interface ISeparateService { }
public interface ISeparateUtility { }

[Singleton]
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class SharedService : ISharedService, ISharedUtility
{
}

[Scoped]
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Separate)]
public partial class SeparateService : ISeparateService, ISeparateUtility
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Verify shared service uses factory pattern
        Assert.Contains("services.AddSingleton<global::Test.SharedService>()", registrationSource.Content);
        Assert.Contains(
            "services.AddSingleton<global::Test.ISharedService>(provider => provider.GetRequiredService<global::Test.SharedService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddSingleton<global::Test.ISharedUtility>(provider => provider.GetRequiredService<global::Test.SharedService>())",
            registrationSource.Content);

        // Verify separate service uses direct pattern
        Assert.Contains("services.AddScoped<global::Test.SeparateService>()", registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.ISeparateService, global::Test.SeparateService>()",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.ISeparateUtility, global::Test.SeparateService>()",
            registrationSource.Content);
    }

    #endregion

    #region Generic Services Pattern Tests

    [Fact]
    public void GenericSharedInstances_GeneratesCorrectFactoryPattern()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IRepository<T> { }
public interface IValidator<T> { }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class StringDataService : IRepository<string>, IValidator<string>
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Verify generic shared instance pattern
        Assert.Contains("services.AddScoped<global::Test.StringDataService, global::Test.StringDataService>()",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IRepository<string>>(provider => provider.GetRequiredService<global::Test.StringDataService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IValidator<string>>(provider => provider.GetRequiredService<global::Test.StringDataService>())",
            registrationSource.Content);
    }

    [Fact]
    public void OpenGenericSharedInstances_GeneratesCorrectFactoryPattern()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IRepository<T> { }
public interface IValidator<T> { }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class GenericDataService<T> : IRepository<T>, IValidator<T>
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // DEBUG: Print the actual generated code
        Console.WriteLine("=== GENERATED REGISTRATION CODE ===");
        Console.WriteLine(registrationSource.Content);
        Console.WriteLine("=== END GENERATED CODE ===");

        // Verify open generic shared instance pattern uses typeof syntax
        Assert.Contains("services.AddScoped(typeof(global::Test.GenericDataService<>));", registrationSource.Content);
        Assert.Contains(
            "services.AddScoped(typeof(global::Test.IRepository<>), provider => provider.GetRequiredService(typeof(global::Test.GenericDataService<>)));",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped(typeof(global::Test.IValidator<>), provider => provider.GetRequiredService(typeof(global::Test.GenericDataService<>)));",
            registrationSource.Content);
    }

    #endregion

    #region Registration Mode Pattern Tests

    [Fact]
    public void ExclusionaryMode_SharedInstances_OnlyRegistersInterfacesWithFactoryPattern()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IService1 { }
public interface IService2 { }
[RegisterAsAll(RegistrationMode.Exclusionary, InstanceSharing.Shared)]
public partial class ExclusionarySharedService : IService1, IService2
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // In Exclusionary mode with Shared instances, we need the concrete type registered
        // for the factory lambdas to work, even though it's not exposed
        Assert.Contains(
            "services.AddScoped<global::Test.ExclusionarySharedService, global::Test.ExclusionarySharedService>()",
            registrationSource.Content);

        // Interfaces should use factory pattern
        Assert.Contains(
            "services.AddScoped<global::Test.IService1>(provider => provider.GetRequiredService<global::Test.ExclusionarySharedService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IService2>(provider => provider.GetRequiredService<global::Test.ExclusionarySharedService>())",
            registrationSource.Content);
    }

    [Fact]
    public void DirectOnlyMode_SharedInstances_OnlyRegistersConcreteType()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IService1 { }
public interface IService2 { }
[RegisterAsAll(RegistrationMode.DirectOnly, InstanceSharing.Shared)]
public partial class DirectOnlyService : IService1, IService2
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Only concrete type should be registered
        Assert.Contains("services.AddScoped<global::Test.DirectOnlyService, global::Test.DirectOnlyService>()",
            registrationSource.Content);

        // Interfaces should NOT be registered at all
        Assert.DoesNotContain("AddScoped<global::Test.IService1>", registrationSource.Content);
        Assert.DoesNotContain("AddScoped<global::Test.IService2>", registrationSource.Content);
    }

    #endregion

    #region Factory Pattern Syntactic Validation Tests

    [Fact]
    public void FactoryPattern_AllLifetimes_GeneratesSyntacticallyCorrectLambdas()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IService { }

[Singleton]
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class SingletonService : IService { }

[Scoped] 
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class ScopedService : IService { }

[Transient]
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class TransientService : IService { }";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Verify each lifetime generates syntactically correct factory lambdas
        Assert.Contains(
            "services.AddSingleton<global::Test.IService>(provider => provider.GetRequiredService<global::Test.SingletonService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IService>(provider => provider.GetRequiredService<global::Test.ScopedService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddTransient<global::Test.IService>(provider => provider.GetRequiredService<global::Test.TransientService>())",
            registrationSource.Content);

        // Verify no malformed registrations
        Assert.DoesNotContain("provider => provider.GetRequiredService<",
            registrationSource.Content
                .Replace("provider => provider.GetRequiredService<global::Test.SingletonService>()", "")
                .Replace("provider => provider.GetRequiredService<global::Test.ScopedService>()", "")
                .Replace("provider => provider.GetRequiredService<global::Test.TransientService>()", ""));
    }

    [Fact]
    public void FactoryPattern_WithComplexTypes_GeneratesCorrectTypeNames()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace Test.Complex.Namespace;

public interface IComplexService<T> where T : class { }
public interface IRepository<TEntity, TKey> { }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class ComplexGenericService : IComplexService<string>, IRepository<object, int>
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Verify complex generic types are handled correctly in factory lambdas
        Assert.Contains(
            "services.AddScoped<global::Test.Complex.Namespace.ComplexGenericService, global::Test.Complex.Namespace.ComplexGenericService>()",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.Complex.Namespace.IComplexService<string>>(provider => provider.GetRequiredService<global::Test.Complex.Namespace.ComplexGenericService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.Complex.Namespace.IRepository<object, int>>(provider => provider.GetRequiredService<global::Test.Complex.Namespace.ComplexGenericService>())",
            registrationSource.Content);
    }

    #endregion

    #region Edge Cases and Error Scenarios

    [Fact]
    public void CircularDependency_SharedInstances_StillGeneratesCorrectRegistrations()
    {
        // Arrange - This should generate warnings but still produce valid registration code
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IServiceA { }
public interface IServiceB { }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class ServiceA : IServiceA
{
    [Inject] private readonly IServiceB _serviceB;
}
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class ServiceB : IServiceB
{
    [Inject] private readonly IServiceA _serviceA;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        // Should detect circular dependency warning
        var circularDiagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(circularDiagnostics);

        // But should still generate correct registration code
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        Assert.Contains("services.AddScoped<global::Test.ServiceA, global::Test.ServiceA>()",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IServiceA>(provider => provider.GetRequiredService<global::Test.ServiceA>())",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.ServiceB, global::Test.ServiceB>()",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IServiceB>(provider => provider.GetRequiredService<global::Test.ServiceB>())",
            registrationSource.Content);
    }

    [Fact]
    public void ComplexHierarchy_SharedInstances_GeneratesCorrectRegistrationForAllInterfaceLevels()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IEntity { }
public interface IUser : IEntity { }
public interface IAdminUser : IUser { }
public interface INotificationService { }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class AdminUserService : IAdminUser, INotificationService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Verify all interface levels are registered with factory pattern
        Assert.Contains("services.AddScoped<global::Test.AdminUserService, global::Test.AdminUserService>()",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IEntity>(provider => provider.GetRequiredService<global::Test.AdminUserService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IUser>(provider => provider.GetRequiredService<global::Test.AdminUserService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IAdminUser>(provider => provider.GetRequiredService<global::Test.AdminUserService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.INotificationService>(provider => provider.GetRequiredService<global::Test.AdminUserService>())",
            registrationSource.Content);
    }

    #endregion

    #region Integration with Other Features Tests

    [Fact]
    public void SkipRegistration_WithSharedInstances_ExcludesFromFactoryRegistrations()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IService1 { }
public interface IService2 { }
public interface IService3 { }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
[SkipRegistration<IService2>]
public partial class SkippedSharedService : IService1, IService2, IService3
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Concrete type should still be registered
        Assert.Contains("services.AddScoped<global::Test.SkippedSharedService, global::Test.SkippedSharedService>()",
            registrationSource.Content);

        // Non-skipped interfaces should use factory pattern
        Assert.Contains(
            "services.AddScoped<global::Test.IService1>(provider => provider.GetRequiredService<global::Test.SkippedSharedService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IService3>(provider => provider.GetRequiredService<global::Test.SkippedSharedService>())",
            registrationSource.Content);

        // Skipped interface should not be registered at all
        Assert.DoesNotContain("AddScoped<global::Test.IService2>", registrationSource.Content);
    }

    [Fact]
    public void DependsOn_WithSharedInstances_GeneratesCorrectConstructorAndRegistrations()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IService1 { }
public interface IService2 { }
public interface ILogger { }
public interface IValidator { }
[Scoped]
public partial class Logger : ILogger { }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
[DependsOn<ILogger, IValidator>]
public partial class SharedServiceWithDeps : IService1, IService2
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        // Verify constructor generation
        var constructorSource = result.GetConstructorSource("SharedServiceWithDeps");
        Assert.NotNull(constructorSource);
        Assert.Contains("public SharedServiceWithDeps(ILogger logger, IValidator validator)",
            constructorSource.Content);

        // Verify registration patterns
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Logger should be registered normally
        Assert.Contains("services.AddScoped<global::Test.ILogger, global::Test.Logger>()", registrationSource.Content);

        // Shared service should use factory pattern
        Assert.Contains("services.AddScoped<global::Test.SharedServiceWithDeps, global::Test.SharedServiceWithDeps>()",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IService1>(provider => provider.GetRequiredService<global::Test.SharedServiceWithDeps>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IService2>(provider => provider.GetRequiredService<global::Test.SharedServiceWithDeps>())",
            registrationSource.Content);
    }

    #endregion
}
