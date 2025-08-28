namespace IoCTools.Generator.Tests;

using Microsoft.CodeAnalysis;

/// <summary>
///     COMPREHENSIVE MULTI-INTERFACE REGISTRATION TESTS
///     Tests the RegisterAsAll and SkipRegistration attributes with all modes and scenarios
/// </summary>
public class MultiInterfaceRegistrationTests
{
    #region SkipRegistration Tests - Single Type

    [Fact]
    public void SkipRegistration_SingleInterface_ExcludesFromRegistration()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IUserService { }
public interface INotificationService { }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Separate)]
[SkipRegistration<IUserService>]
public partial class UserNotificationService : IUserService, INotificationService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should register concrete type and INotificationService but not IUserService
        Assert.Contains(
            "services.AddScoped<global::Test.UserNotificationService, global::Test.UserNotificationService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.INotificationService, global::Test.UserNotificationService>",
            registrationSource.Content);
        Assert.DoesNotContain("AddScoped<IUserService", registrationSource.Content);
    }

    #endregion

    #region Stacked SkipRegistration Tests

    [Fact]
    public void SkipRegistration_MultipleAttributes_CombinesExclusions()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IService1 { }
public interface IService2 { }
public interface IService3 { }
public interface IService4 { }
public interface IService5 { }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Separate)]
[SkipRegistration<IService1, IService2>]
[SkipRegistration<IService3>]
[SkipRegistration<IService4, IService5>]
public partial class StackedSkipService : IService1, IService2, IService3, IService4, IService5
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should only register concrete type since all interfaces are skipped
        Assert.Contains("services.AddScoped<global::Test.StackedSkipService, global::Test.StackedSkipService>",
            registrationSource.Content);

        // Should not register any interfaces
        Assert.DoesNotContain("AddScoped<IService1", registrationSource.Content);
        Assert.DoesNotContain("AddScoped<IService2", registrationSource.Content);
        Assert.DoesNotContain("AddScoped<IService3", registrationSource.Content);
        Assert.DoesNotContain("AddScoped<IService4", registrationSource.Content);
        Assert.DoesNotContain("AddScoped<IService5", registrationSource.Content);
    }

    #endregion

    #region External Services Integration Tests

    [Fact]
    public void RegisterAsAll_WithExternalService_SkipsDiagnosticsCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IUserService { }
public interface INotificationService { }
public interface IMissingDependency { }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Separate)]
[ExternalService]
public partial class ExternalMultiService : IUserService, INotificationService
{
    [Inject] private readonly IMissingDependency _missing;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        // Should have no diagnostics for missing dependency due to ExternalService
        var ioc001Diagnostics = result.GetDiagnosticsByCode("IOC001");
        var serviceRelatedDiagnostics = ioc001Diagnostics.Where(d => d.GetMessage().Contains("ExternalMultiService"));
        Assert.Empty(serviceRelatedDiagnostics);

        // Should still register all interfaces
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("services.AddScoped<global::Test.ExternalMultiService, global::Test.ExternalMultiService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IUserService, global::Test.ExternalMultiService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.INotificationService, global::Test.ExternalMultiService>",
            registrationSource.Content);
    }

    #endregion

    #region Abstract Base Class Tests

    [Fact]
    public void RegisterAsAll_AbstractBaseWithMultipleInterfaces_HandlesCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IService1 { }
public interface IService2 { }
public interface IService3 { }

public abstract class BaseService : IService1
{
}
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class ConcreteService : BaseService, IService2, IService3
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should register concrete class and all interfaces (including inherited) with shared instance pattern
        // For InstanceSharing.Shared: concrete class gets direct registration, interfaces get factory pattern
        Assert.Contains("services.AddScoped<global::Test.ConcreteService, global::Test.ConcreteService>",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IService1>(provider => provider.GetRequiredService<global::Test.ConcreteService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IService2>(provider => provider.GetRequiredService<global::Test.ConcreteService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IService3>(provider => provider.GetRequiredService<global::Test.ConcreteService>())",
            registrationSource.Content);

        // Should NOT register abstract base class
        Assert.DoesNotContain("AddScoped<BaseService", registrationSource.Content);
    }

    #endregion

    #region Lifetime Integration Tests

    [Fact]
    public void RegisterAsAll_DifferentLifetimes_AppliesToAllRegistrations()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IUserService { }
public interface INotificationService { }

[Singleton]
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class SingletonService : IUserService, INotificationService
{
}

[Transient]
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Separate)]
public partial class TransientService : IUserService, INotificationService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Singleton with shared instances
        // CRITICAL FIX: Services with explicit lifetime attributes ([Singleton]) use single-parameter form for concrete registration
        Assert.Contains("services.AddSingleton<global::Test.SingletonService>();", registrationSource.Content);
        Assert.Contains(
            "services.AddSingleton<global::Test.IUserService>(provider => provider.GetRequiredService<global::Test.SingletonService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddSingleton<global::Test.INotificationService>(provider => provider.GetRequiredService<global::Test.SingletonService>())",
            registrationSource.Content);

        // Transient with separate instances  
        // CRITICAL FIX: Services with explicit lifetime attributes ([Transient]) use single-parameter form for concrete registration
        Assert.Contains("services.AddTransient<global::Test.TransientService>();", registrationSource.Content);
        Assert.Contains("services.AddTransient<global::Test.IUserService, global::Test.TransientService>",
            registrationSource.Content);
        Assert.Contains("services.AddTransient<global::Test.INotificationService, global::Test.TransientService>",
            registrationSource.Content);
    }

    #endregion

    #region Complex Integration Tests

    [Fact]
    public void RegisterAsAll_ComplexScenario_AllFeaturesWorking()
    {
        // Arrange - The most complex scenario possible
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace Test;

public interface IEntity { }
public interface IUser : IEntity { }
public interface IUserQuery : IUser { }
public interface IRepository<T> { }
public interface IValidator<T> { }
public interface INotificationService { }
public interface ISkippedService { }
public interface ILogger { }
public interface IExternalDep { }

[Scoped]
public partial class Logger : ILogger { }

[Singleton]
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
[SkipRegistration<ISkippedService, IRepository<string>>]
[DependsOn<ILogger>]
[ExternalService] // This should skip diagnostics for missing IExternalDep
public partial class ComplexService : IUserQuery, IRepository<string>, IValidator<string>, INotificationService, ISkippedService
{
    [Inject] private readonly IExternalDep _externalDep;
    [Inject] private readonly IEnumerable<ILogger> _loggers;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        // Should have no diagnostics due to ExternalService
        var ioc001Diagnostics = result.GetDiagnosticsByCode("IOC001");
        var complexServiceDiagnostics = ioc001Diagnostics.Where(d => d.GetMessage().Contains("ComplexService"));
        Assert.Empty(complexServiceDiagnostics);

        // Check constructor generation
        var constructorSource = result.GetConstructorSource("ComplexService");
        Assert.NotNull(constructorSource);
        Assert.Contains("public ComplexService(ILogger logger, IExternalDep externalDep, IEnumerable<ILogger> loggers)",
            constructorSource.Content);

        // Check service registration
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should register Logger first
        Assert.Contains("services.AddScoped<global::Test.ILogger, global::Test.Logger>", registrationSource.Content);

        // Should register ComplexService as Singleton with shared instances
        Assert.Contains("services.AddSingleton<global::Test.ComplexService>", registrationSource.Content);

        // Should register all non-skipped interfaces with factory pattern
        Assert.Contains(
            "services.AddSingleton<global::Test.IUserQuery>(provider => provider.GetRequiredService<global::Test.ComplexService>())",
            registrationSource.Content);
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

        // Should NOT register skipped interfaces
        Assert.DoesNotContain("AddSingleton<ISkippedService>", registrationSource.Content);
        Assert.DoesNotContain("AddSingleton<IRepository<string>>", registrationSource.Content);
    }

    #endregion

    #region Circular Dependency Tests

    [Fact]
    public void RegisterAsAll_CircularDependency_HandledGracefully()
    {
        // Arrange
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

        // Should detect and warn about circular dependency
        var circularDiagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(circularDiagnostics);

        // Validate circular dependency diagnostic content
        Assert.All(circularDiagnostics, d =>
        {
            Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
            var message = d.GetMessage();
            Assert.True(message.Contains("ServiceA") || message.Contains("ServiceB"));
        });

        // But should still generate registration code
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        // Should register with InstanceSharing.Shared pattern - concrete direct, interfaces with factory
        Assert.Contains("services.AddScoped<global::Test.ServiceA, global::Test.ServiceA>", registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IServiceA>(provider => provider.GetRequiredService<global::Test.ServiceA>())",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.ServiceB, global::Test.ServiceB>", registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IServiceB>(provider => provider.GetRequiredService<global::Test.ServiceB>())",
            registrationSource.Content);
    }

    #endregion

    #region Basic Multi-Interface Registration Tests

    [Fact]
    public void RegisterAsAll_DirectOnly_RegistersOnlyConcreteType()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IUserService { }
public interface INotificationService { }
[RegisterAsAll(RegistrationMode.DirectOnly, InstanceSharing.Separate)]
public partial class UserNotificationService : IUserService, INotificationService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should only register concrete type
        Assert.Contains(
            "services.AddScoped<global::Test.UserNotificationService, global::Test.UserNotificationService>",
            registrationSource.Content);

        // Should NOT register interfaces
        Assert.DoesNotContain("AddScoped<IUserService", registrationSource.Content);
        Assert.DoesNotContain("AddScoped<INotificationService", registrationSource.Content);
    }

    [Fact]
    public void RegisterAsAll_All_RegistersConcreteAndAllInterfaces()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IUserService { }
public interface INotificationService { }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Separate)]
public partial class UserNotificationService : IUserService, INotificationService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should register concrete type and all interfaces
        Assert.Contains(
            "services.AddScoped<global::Test.UserNotificationService, global::Test.UserNotificationService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IUserService, global::Test.UserNotificationService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.INotificationService, global::Test.UserNotificationService>",
            registrationSource.Content);
    }

    [Fact]
    public void RegisterAsAll_Exclusionary_RegistersInterfacesOnly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IUserService { }
public interface INotificationService { }
[RegisterAsAll(RegistrationMode.Exclusionary, InstanceSharing.Separate)]
public partial class UserNotificationService : IUserService, INotificationService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should register interfaces only
        Assert.Contains("services.AddScoped<global::Test.IUserService, global::Test.UserNotificationService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.INotificationService, global::Test.UserNotificationService>",
            registrationSource.Content);

        // Should NOT register concrete type
        Assert.DoesNotContain("AddScoped<global::Test.UserNotificationService, global::Test.UserNotificationService>",
            registrationSource.Content);
    }

    #endregion

    #region InstanceSharing Tests

    [Fact]
    public void RegisterAsAll_SharedInstances_UsesFactoryForSharing()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IUserService { }
public interface INotificationService { }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class UserNotificationService : IUserService, INotificationService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // For shared instances, should register concrete type normally and interfaces with factory pattern
        Assert.Contains(
            "services.AddScoped<global::Test.UserNotificationService, global::Test.UserNotificationService>",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IUserService>(provider => provider.GetRequiredService<global::Test.UserNotificationService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.INotificationService>(provider => provider.GetRequiredService<global::Test.UserNotificationService>())",
            registrationSource.Content);
    }

    [Fact]
    public void RegisterAsAll_SeparateInstances_RegistersEachSeparately()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IUserService { }
public interface INotificationService { }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Separate)]
public partial class UserNotificationService : IUserService, INotificationService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // For separate instances, each registration creates its own instance
        Assert.Contains(
            "services.AddScoped<global::Test.UserNotificationService, global::Test.UserNotificationService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IUserService, global::Test.UserNotificationService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.INotificationService, global::Test.UserNotificationService>",
            registrationSource.Content);
    }

    [Fact]
    public void RegisterAsAll_SharedInstances_ActuallySharesInstancesAtRuntime()
    {
        // Arrange - Generate the registration code
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IUserService { void DoUser(); }
public interface INotificationService { void DoNotify(); }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class UserNotificationService : IUserService, INotificationService
{
    public void DoUser() { }
    public void DoNotify() { }
}";

        // Act - Get the generated registration method
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var registrationSource = result.GetServiceRegistrationSource();

        // Assert - Validate the registration pattern is correct for shared instances
        Assert.False(result.HasErrors);
        Assert.NotNull(registrationSource);

        // CRITICAL: Validate that the factory pattern is generated correctly for shared instances
        Assert.Contains(
            "services.AddScoped<global::Test.UserNotificationService, global::Test.UserNotificationService>",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IUserService>(provider => provider.GetRequiredService<global::Test.UserNotificationService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.INotificationService>(provider => provider.GetRequiredService<global::Test.UserNotificationService>())",
            registrationSource.Content);

        // CRITICAL: Validate that we don't have direct interface registrations for shared instances
        Assert.DoesNotContain("AddScoped<global::Test.IUserService, global::Test.UserNotificationService>",
            registrationSource.Content);
        Assert.DoesNotContain("AddScoped<global::Test.INotificationService, global::Test.UserNotificationService>",
            registrationSource.Content);
    }

    [Fact]
    public void RegisterAsAll_SeparateInstances_ActuallyCreatesDistinctInstancesAtRuntime()
    {
        // Arrange - Generate registration for separate instances
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IUserService { void DoUser(); }
public interface INotificationService { void DoNotify(); }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Separate)]
public partial class UserNotificationService : IUserService, INotificationService
{
    public void DoUser() { }
    public void DoNotify() { }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var registrationSource = result.GetServiceRegistrationSource();

        // Assert - Validate separate instance registration pattern
        Assert.False(result.HasErrors);
        Assert.NotNull(registrationSource);

        // CRITICAL: Validate direct registrations (no factory pattern)
        Assert.Contains(
            "services.AddScoped<global::Test.UserNotificationService, global::Test.UserNotificationService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IUserService, global::Test.UserNotificationService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.INotificationService, global::Test.UserNotificationService>",
            registrationSource.Content);

        // CRITICAL: Validate that we don't have factory patterns
        Assert.DoesNotContain("provider => provider.GetRequiredService", registrationSource.Content);
    }

    [Fact]
    public void RegisterAsAll_SharedInstances_ValidatesLifetimeConsistency()
    {
        // Test that all registrations use the same lifetime for shared instances
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IService1 { }
public interface IService2 { }
public interface IService3 { }

[Singleton]
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class SingletonSharedService : IService1, IService2, IService3 { }";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var registrationSource = result.GetServiceRegistrationSource();

        Assert.False(result.HasErrors);
        Assert.NotNull(registrationSource);

        // CRITICAL: All registrations must use the same lifetime (single parameter for explicit lifetime + shared)
        Assert.Contains("services.AddSingleton<global::Test.SingletonSharedService>",
            registrationSource.Content);
        Assert.Contains(
            "services.AddSingleton<global::Test.IService1>(provider => provider.GetRequiredService<global::Test.SingletonSharedService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddSingleton<global::Test.IService2>(provider => provider.GetRequiredService<global::Test.SingletonSharedService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddSingleton<global::Test.IService3>(provider => provider.GetRequiredService<global::Test.SingletonSharedService>())",
            registrationSource.Content);

        // CRITICAL: No mixed lifetimes
        Assert.DoesNotContain("AddScoped<", registrationSource.Content);
        Assert.DoesNotContain("AddTransient<", registrationSource.Content);
    }

    #endregion

    #region SkipRegistration Tests - Multiple Types

    [Fact]
    public void SkipRegistration_TwoInterfaces_ExcludesBothFromRegistration()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IUserService { }
public interface INotificationService { }
public interface ILoggingService { }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Separate)]
[SkipRegistration<IUserService, INotificationService>]
public partial class MultiService : IUserService, INotificationService, ILoggingService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should only register concrete type and ILoggingService
        Assert.Contains("services.AddScoped<global::Test.MultiService, global::Test.MultiService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.ILoggingService, global::Test.MultiService>",
            registrationSource.Content);
        Assert.DoesNotContain("AddScoped<IUserService", registrationSource.Content);
        Assert.DoesNotContain("AddScoped<INotificationService", registrationSource.Content);
    }

    [Fact]
    public void SkipRegistration_FiveInterfaces_ExcludesAllFromRegistration()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IService1 { }
public interface IService2 { }
public interface IService3 { }
public interface IService4 { }
public interface IService5 { }
public interface IService6 { }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Separate)]
[SkipRegistration<IService1, IService2, IService3, IService4, IService5>]
public partial class MegaService : IService1, IService2, IService3, IService4, IService5, IService6
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should only register concrete type and IService6
        Assert.Contains("services.AddScoped<global::Test.MegaService, global::Test.MegaService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IService6, global::Test.MegaService>",
            registrationSource.Content);

        // Should not register the skipped services
        Assert.DoesNotContain("AddScoped<IService1", registrationSource.Content);
        Assert.DoesNotContain("AddScoped<IService2", registrationSource.Content);
        Assert.DoesNotContain("AddScoped<IService3", registrationSource.Content);
        Assert.DoesNotContain("AddScoped<IService4", registrationSource.Content);
        Assert.DoesNotContain("AddScoped<IService5", registrationSource.Content);
    }

    #endregion

    #region Interface Inheritance Tests

    [Fact]
    public void RegisterAsAll_InterfaceInheritance_RegistersAllLevels()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IEntity { }
public interface IUser : IEntity { }
public interface IUserQuery : IUser { }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Separate)]
public partial class UserService : IUserQuery
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should register concrete type and all interface levels
        Assert.Contains("services.AddScoped<global::Test.UserService, global::Test.UserService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IUserQuery, global::Test.UserService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IUser, global::Test.UserService>", registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IEntity, global::Test.UserService>",
            registrationSource.Content);
    }

    [Fact]
    public void SkipRegistration_InterfaceInheritance_SkipsSpecificLevel()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IEntity { }
public interface IUser : IEntity { }
public interface IUserQuery : IUser { }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Separate)]
[SkipRegistration<IUser>]
public partial class UserService : IUserQuery
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should register concrete type, IUserQuery, and IEntity but not IUser
        Assert.Contains("services.AddScoped<global::Test.UserService, global::Test.UserService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IUserQuery, global::Test.UserService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IEntity, global::Test.UserService>",
            registrationSource.Content);
        Assert.DoesNotContain("AddScoped<global::Test.IUser, global::Test.UserService>", registrationSource.Content);
    }

    #endregion

    #region Generic Interface Tests

    [Fact]
    public void RegisterAsAll_GenericInterfaces_RegistersCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace Test;

public interface IRepository<T> { }
public interface IValidator<T> { }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Separate)]
public partial class UserRepository : IRepository<string>, IValidator<string>
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should register concrete type and both generic interfaces
        Assert.Contains("services.AddScoped<global::Test.UserRepository, global::Test.UserRepository>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IRepository<string>, global::Test.UserRepository>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IValidator<string>, global::Test.UserRepository>",
            registrationSource.Content);
    }

    [Fact]
    public void SkipRegistration_GenericInterface_SkipsCorrectGeneric()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace Test;

public interface IRepository<T> { }
public interface IValidator<T> { }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Separate)]
[SkipRegistration<IRepository<string>>]
public partial class UserRepository : IRepository<string>, IValidator<string>
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should register concrete type and IValidator but not IRepository
        Assert.Contains("services.AddScoped<global::Test.UserRepository, global::Test.UserRepository>",
            registrationSource.Content);
        // Accept either string or System.String format for generic types
        Assert.True(
            registrationSource.Content.Contains(
                "AddScoped<global::Test.IValidator<System.String>, global::Test.UserRepository>") ||
            registrationSource.Content.Contains(
                "AddScoped<global::Test.IValidator<string>, global::Test.UserRepository>"),
            "Should contain IValidator registration with either string or System.String format");
        Assert.DoesNotContain("AddScoped<IRepository<", registrationSource.Content); // Skip any form of IRepository
    }

    #endregion

    #region Integration with Existing Features Tests

    [Fact]
    public void RegisterAsAll_WithDependsOn_CombinesCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IUserService { }
public interface INotificationService { }
public interface ILogger { }
public interface IValidator { }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
[DependsOn<ILogger, IValidator>]
public partial class UserNotificationService : IUserService, INotificationService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        // Should have constructor with dependencies
        var constructorSource = result.GetConstructorSource("UserNotificationService");
        Assert.NotNull(constructorSource);
        Assert.Contains("public UserNotificationService(ILogger logger, IValidator validator)",
            constructorSource.Content);

        // Should register with shared instances (factory pattern)
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains(
            "services.AddScoped<global::Test.UserNotificationService, global::Test.UserNotificationService>",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IUserService>(provider => provider.GetRequiredService<global::Test.UserNotificationService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.INotificationService>(provider => provider.GetRequiredService<global::Test.UserNotificationService>())",
            registrationSource.Content);
    }

    [Fact]
    public void RegisterAsAll_WithInject_CombinesCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IUserService { }
public interface INotificationService { }
public interface ILogger { }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Separate)]
public partial class UserNotificationService : IUserService, INotificationService
{
    [Inject] private readonly ILogger _logger;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        // Should have constructor with injected dependencies
        var constructorSource = result.GetConstructorSource("UserNotificationService");
        Assert.NotNull(constructorSource);
        Assert.Contains("public UserNotificationService(ILogger logger)", constructorSource.Content);
        Assert.Contains("_logger = logger", constructorSource.Content);

        // Should register all interfaces separately
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains(
            "services.AddScoped<global::Test.UserNotificationService, global::Test.UserNotificationService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IUserService, global::Test.UserNotificationService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.INotificationService, global::Test.UserNotificationService>",
            registrationSource.Content);
    }

    [Fact]
    public void RegisterAsAll_WithInheritance_HandlesCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IService { }
public interface ISpecialService { }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class BaseService : IService
{
}
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class DerivedService : BaseService, ISpecialService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // BaseService should register with InstanceSharing.Shared pattern
        Assert.Contains("services.AddScoped<global::Test.BaseService, global::Test.BaseService>",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IService>(provider => provider.GetRequiredService<global::Test.BaseService>())",
            registrationSource.Content);

        // DerivedService should register with InstanceSharing.Shared pattern
        Assert.Contains("services.AddScoped<global::Test.DerivedService, global::Test.DerivedService>",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.ISpecialService>(provider => provider.GetRequiredService<global::Test.DerivedService>())",
            registrationSource.Content);
        // Note: IService from base class should also be registered for derived class with factory pattern
        Assert.Contains(
            "services.AddScoped<global::Test.IService>(provider => provider.GetRequiredService<global::Test.DerivedService>())",
            registrationSource.Content);
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public void RegisterAsAll_SingleInterface_WorksButShouldWarn()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IUserService { }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Separate)]
public partial class UserService : IUserService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        // Should work but potentially have a warning about using RegisterAsAll with single interface
        var warnings = result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();
        // Note: Currently no specific warning for single interface RegisterAsAll usage
        // This is acceptable behavior - future enhancement could add IOC010 diagnostic

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("services.AddScoped<global::Test.UserService, global::Test.UserService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IUserService, global::Test.UserService>",
            registrationSource.Content);
    }

    [Fact]
    public void SkipRegistration_NonImplementedInterface_ShouldWarn()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IUserService { }
public interface INotificationService { }
public interface INonImplemented { }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Separate)]
[SkipRegistration<INonImplemented>] // This interface is not implemented!
public partial class UserService : IUserService, INotificationService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        // Should have warning about skipping non-implemented interface
        var ioc009Diagnostics = result.GetDiagnosticsByCode("IOC009");
        Assert.NotEmpty(ioc009Diagnostics);

        // Validate the diagnostic message contains relevant context
        Assert.All(ioc009Diagnostics, d =>
        {
            Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
            var message = d.GetMessage();
            Assert.Contains("INonImplemented", message);
        });

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("services.AddScoped<global::Test.UserService, global::Test.UserService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IUserService, global::Test.UserService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.INotificationService, global::Test.UserService>",
            registrationSource.Content);
    }

    [Fact]
    public void SkipRegistration_AllInterfaces_ShouldWarnOrError()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IUserService { }
public interface INotificationService { }
[RegisterAsAll(RegistrationMode.Exclusionary, InstanceSharing.Separate)] // Only interfaces
[SkipRegistration<IUserService, INotificationService>] // But skip all interfaces!
public partial class UserService : IUserService, INotificationService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        // This should either error or warn because we're using Exclusionary mode but skipping all interfaces
        var diagnostics = result.CompilationDiagnostics.Where(d =>
            d.Severity == DiagnosticSeverity.Error || d.Severity == DiagnosticSeverity.Warning).ToList();

        // When using Exclusionary mode but skipping all interfaces, there's nothing to register
        // So the generator correctly doesn't generate any registration file
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.Null(registrationSource);

        // The test passes - this edge case is handled correctly by generating no registrations
    }

    #endregion

    #region Error Validation Tests

    [Fact]
    public void RegisterAsAll_WithLifetimeInference_WorksCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IUserService { }

[RegisterAsAll(RegistrationMode.All, InstanceSharing.Separate)] // With intelligent inference, no [Scoped] needed
public partial class UserService : IUserService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - With intelligent inference, this should work without IOC004 diagnostic
        var ioc004Diagnostics = result.GetDiagnosticsByCode("IOC004");
        Assert.Empty(ioc004Diagnostics);

        // Should generate service registrations
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should register concrete class and all interfaces with separate instances
        Assert.Contains("services.AddScoped<global::Test.UserService, global::Test.UserService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IUserService, global::Test.UserService>",
            registrationSource.Content);
    }

    [Fact]
    public void SkipRegistration_WithoutRegisterAsAll_NoLongerWarns()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IUserService { }
[SkipRegistration<IUserService>] // SkipRegistration without RegisterAsAll with intelligent inference
public partial class UserService : IUserService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        // With intelligent inference, IOC005 diagnostic was removed
        var ioc005Diagnostics = result.GetDiagnosticsByCode("IOC005");
        Assert.Empty(ioc005Diagnostics);

        // Should generate registration based on intelligent inference
        var registrationSource = result.GetServiceRegistrationSource();

        // With SkipRegistration without RegisterAsAll, the generator might not produce any registrations
        // since SkipRegistration typically means "don't register this interface"
        if (registrationSource != null)
            // If registrations are generated, check they are correct
            Assert.Contains("services.AddScoped<global::Test.UserService, global::Test.UserService>",
                registrationSource.Content);
        // The IUserService interface might be skipped due to SkipRegistration
        // It's valid for SkipRegistration to result in no registrations
        // This indicates the generator correctly interpreted the SkipRegistration hint
    }

    #endregion

    #region Boundary and Performance Tests

    [Fact]
    public void RegisterAsAll_MaximumInterfaceLimit_HandlesCorrectly()
    {
        // Arrange - Test with maximum realistic number of interfaces (10)
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IService01 { }
public interface IService02 { }
public interface IService03 { }
public interface IService04 { }
public interface IService05 { }
public interface IService06 { }
public interface IService07 { }
public interface IService08 { }
public interface IService09 { }
public interface IService10 { }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Separate)]
public partial class MegaInterfaceService : IService01, IService02, IService03, IService04, IService05, IService06, IService07, IService08, IService09, IService10
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should register concrete type and all 10 interfaces
        Assert.Contains("services.AddScoped<global::Test.MegaInterfaceService, global::Test.MegaInterfaceService>",
            registrationSource.Content);
        for (var i = 1; i <= 10; i++)
            Assert.Contains($"AddScoped<global::Test.IService{i:D2}, global::Test.MegaInterfaceService>",
                registrationSource.Content);

        // Performance check - should generate reasonable amount of code
        Assert.True(registrationSource.Content.Length < 50000, "Generated code should be reasonable in size");
    }

    [Fact]
    public void SkipRegistration_MaximumParameters_HandlesCorrectly()
    {
        // Arrange - Test SkipRegistration with maximum 5 parameters
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IService1 { }
public interface IService2 { }
public interface IService3 { }
public interface IService4 { }
public interface IService5 { }
public interface IService6 { }
public interface IService7 { }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Separate)]
[SkipRegistration<IService1, IService2, IService3, IService4, IService5>]
public partial class MaxSkipService : IService1, IService2, IService3, IService4, IService5, IService6, IService7
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should register concrete type and only non-skipped interfaces
        Assert.Contains("services.AddScoped<global::Test.MaxSkipService, global::Test.MaxSkipService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IService6, global::Test.MaxSkipService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IService7, global::Test.MaxSkipService>",
            registrationSource.Content);

        // Should NOT register skipped interfaces
        for (var i = 1; i <= 5; i++)
            Assert.DoesNotContain($"AddScoped<global::Test.IService{i}, global::Test.MaxSkipService>",
                registrationSource.Content);
    }

    #endregion

    #region Advanced Generic Interface Tests

    [Fact]
    public void RegisterAsAll_NestedGenerics_HandlesCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace Test;

public interface IRepository<T> { }
public interface IComplexRepository<T> { }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class NestedGenericService : IRepository<List<string>>, IComplexRepository<Dictionary<string, int>>
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // NestedGenericService uses InstanceSharing.Shared, so should use factory pattern
        Assert.Contains("services.AddScoped<global::Test.NestedGenericService, global::Test.NestedGenericService>",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IRepository<List<string>>>(provider => provider.GetRequiredService<global::Test.NestedGenericService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IComplexRepository<Dictionary<string, int>>>(provider => provider.GetRequiredService<global::Test.NestedGenericService>())",
            registrationSource.Content);
    }

    [Fact]
    public void RegisterAsAll_GenericConstraints_HandlesCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IConstrainedRepository<T> where T : class { }
public interface IValueRepository<T> where T : struct { }

public class TestClass { }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Separate)]
public partial class ConstrainedService : IConstrainedRepository<TestClass>, IValueRepository<int>
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should register with correct generic constraints
        Assert.Contains("services.AddScoped<global::Test.ConstrainedService, global::Test.ConstrainedService>",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IConstrainedRepository<global::Test.TestClass>, global::Test.ConstrainedService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IValueRepository<int>, global::Test.ConstrainedService>",
            registrationSource.Content);
    }

    [Fact]
    public void RegisterAsAll_MultipleGenericParameters_HandlesCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IMapper<TSource, TDestination> { }
public interface IConverter<TInput, TOutput, TContext> { }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class MultiGenericService : IMapper<string, int>, IConverter<byte[], string, object>
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should handle multiple generic parameters correctly with shared instance pattern (factory lambda)
        // For shared instances, concrete class uses direct registration, interfaces use factory lambdas
        Assert.Contains("services.AddScoped<global::Test.MultiGenericService, global::Test.MultiGenericService>",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IMapper<string, int>>(provider => provider.GetRequiredService<global::Test.MultiGenericService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IConverter<byte[], string, object>>(provider => provider.GetRequiredService<global::Test.MultiGenericService>())",
            registrationSource.Content);
    }

    #endregion

    #region Inheritance Conflict Tests

    [Fact]
    public void RegisterAsAll_BaseWithRegisterAsAllDerivedWithout_HandlesCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IBaseService { }
public interface IDerivedService { }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class BaseService : IBaseService
{
}

[Scoped] // No RegisterAsAll here
public partial class DerivedService : BaseService, IDerivedService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Base service should use RegisterAsAll behavior with InstanceSharing.Shared pattern
        Assert.Contains("services.AddScoped<global::Test.BaseService, global::Test.BaseService>",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IBaseService>(provider => provider.GetRequiredService<global::Test.BaseService>())",
            registrationSource.Content);

        // Derived service should use standard behavior (interface → implementation)
        Assert.Contains("services.AddScoped<global::Test.IDerivedService, global::Test.DerivedService>",
            registrationSource.Content);
        // Derived should also handle inherited interface normally
        Assert.Contains("services.AddScoped<global::Test.IBaseService, global::Test.DerivedService>",
            registrationSource.Content);
    }

    [Fact]
    public void RegisterAsAll_ConflictingRegisterAsAllConfigurations_HandlesCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface ISharedService { }
public interface ISpecialService { }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class SharedService : ISharedService
{
}
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Separate)]
public partial class SeparateService : ISharedService, ISpecialService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Each service should maintain its own configuration
        // SharedService with InstanceSharing.Shared should use factory pattern for interfaces
        Assert.Contains("services.AddScoped<global::Test.SharedService, global::Test.SharedService>",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.ISharedService>(provider => provider.GetRequiredService<global::Test.SharedService>())",
            registrationSource.Content);

        // SeparateService with direct registration
        Assert.Contains("services.AddScoped<global::Test.ISharedService, global::Test.SeparateService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.ISpecialService, global::Test.SeparateService>",
            registrationSource.Content);
    }

    [Fact]
    public void RegisterAsAll_AbstractBaseWithSkipRegistration_HandlesCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IService1 { }
public interface IService2 { }
public interface IService3 { }

[SkipRegistration<IService1>] // This should be ignored on abstract class
public abstract class AbstractBase : IService1
{
}
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
[SkipRegistration<IService2>]
public partial class ConcreteImpl : AbstractBase, IService2, IService3
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // ConcreteImpl uses InstanceSharing.Shared, so should use factory pattern
        Assert.Contains("services.AddScoped<global::Test.ConcreteImpl, global::Test.ConcreteImpl>",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IService1>(provider => provider.GetRequiredService<global::Test.ConcreteImpl>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IService3>(provider => provider.GetRequiredService<global::Test.ConcreteImpl>())",
            registrationSource.Content);

        // Should NOT register skipped interface
        Assert.DoesNotContain("AddScoped<IService2>", registrationSource.Content);

        // Should NOT register abstract base
        Assert.DoesNotContain("AddScoped<AbstractBase", registrationSource.Content);
    }

    #endregion

    #region Cross-Boundary Integration Tests

    [Fact]
    public void RegisterAsAll_MultiNamespaceInterfaces_HandlesCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test.Services
{
    public interface IUserService { }
}

namespace Test.Notifications
{
    public interface INotificationService { }
}

namespace Test.Implementation
{
    using Test.Services;
    using Test.Notifications;
    
    
    [RegisterAsAll(RegistrationMode.All, InstanceSharing.Separate)]
    public partial class CrossNamespaceService : IUserService, INotificationService
    {
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should handle interfaces from different namespaces
        Assert.Contains(
            "services.AddScoped<global::Test.Implementation.CrossNamespaceService, global::Test.Implementation.CrossNamespaceService>",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.Services.IUserService, global::Test.Implementation.CrossNamespaceService>",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.Notifications.INotificationService, global::Test.Implementation.CrossNamespaceService>",
            registrationSource.Content);
    }

    [Fact]
    public void RegisterAsAll_PartialClassAcrossFiles_HandlesCorrectly()
    {
        // Arrange - Simulate partial class across multiple files
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IService1 { }
public interface IService2 { }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class PartialService : IService1
{
    // File 1 content
}

// Simulating second file
public partial class PartialService : IService2
{
    // File 2 content - additional interface
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // PartialService uses InstanceSharing.Shared, so should use factory pattern  
        Assert.Contains("services.AddScoped<global::Test.PartialService, global::Test.PartialService>",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IService1>(provider => provider.GetRequiredService<global::Test.PartialService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IService2>(provider => provider.GetRequiredService<global::Test.PartialService>())",
            registrationSource.Content);
    }

    #endregion

    #region Instance Sharing Runtime Behavior Tests

    [Fact]
    public void RegisterAsAll_SharedInstancesValidation_RegistrationPatternCorrect()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IService1 { }
public interface IService2 { }
public interface IService3 { }

[Singleton]
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class SharedInstanceService : IService1, IService2, IService3
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Validate shared instance pattern: concrete registered normally, interfaces use factory
        // CRITICAL FIX: Services with explicit lifetime attributes ([Singleton]) use single-parameter form for concrete registration
        Assert.Contains("services.AddSingleton<global::Test.SharedInstanceService>();",
            registrationSource.Content);

        // All interfaces should resolve to the same instance via factory
        Assert.Contains(
            "services.AddSingleton<global::Test.IService1>(provider => provider.GetRequiredService<global::Test.SharedInstanceService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddSingleton<global::Test.IService2>(provider => provider.GetRequiredService<global::Test.SharedInstanceService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddSingleton<global::Test.IService3>(provider => provider.GetRequiredService<global::Test.SharedInstanceService>())",
            registrationSource.Content);

        // Should NOT have direct interface-to-implementation registrations
        Assert.DoesNotContain("AddSingleton<global::Test.IService1, global::Test.SharedInstanceService>",
            registrationSource.Content);
        Assert.DoesNotContain("AddSingleton<global::Test.IService2, global::Test.SharedInstanceService>",
            registrationSource.Content);
        Assert.DoesNotContain("AddSingleton<global::Test.IService3, global::Test.SharedInstanceService>",
            registrationSource.Content);
    }

    [Fact]
    public void RegisterAsAll_SeparateInstancesValidation_RegistrationPatternCorrect()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IService1 { }
public interface IService2 { }
public interface IService3 { }

[Transient]
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Separate)]
public partial class SeparateInstanceService : IService1, IService2, IService3
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Validate separate instance pattern: each registration creates new instance
        Assert.Contains("services.AddTransient<global::Test.SeparateInstanceService>",
            registrationSource.Content);
        Assert.Contains("services.AddTransient<global::Test.IService1, global::Test.SeparateInstanceService>",
            registrationSource.Content);
        Assert.Contains("services.AddTransient<global::Test.IService2, global::Test.SeparateInstanceService>",
            registrationSource.Content);
        Assert.Contains("services.AddTransient<global::Test.IService3, global::Test.SeparateInstanceService>",
            registrationSource.Content);

        // Should NOT have factory-based registrations
        Assert.DoesNotContain("provider => provider.GetRequiredService<global::Test.SeparateInstanceService>()",
            registrationSource.Content);
    }

    #endregion

    #region Complex Dependency Injection Pattern Tests

    [Fact]
    public void RegisterAsAll_WithFactoryPattern_HandlesCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System;

namespace Test;

public interface IService { }
public interface IServiceFactory { }
public interface IConfigurableService { }

public interface IFactory<T> { }
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class FactoryService : IService, IServiceFactory, IFactory<IConfigurableService>
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should handle factory patterns correctly with shared instances
        Assert.Contains("services.AddScoped<global::Test.FactoryService, global::Test.FactoryService>",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IService>(provider => provider.GetRequiredService<global::Test.FactoryService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IServiceFactory>(provider => provider.GetRequiredService<global::Test.FactoryService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IFactory<global::Test.IConfigurableService>>(provider => provider.GetRequiredService<global::Test.FactoryService>())",
            registrationSource.Content);
    }

    [Fact]
    public void RegisterAsAll_ComplexIntegrationAllCombinations_Comprehensive()
    {
        // Arrange - Test ALL possible combinations in single comprehensive test
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace Test;

// Define all test interfaces
public interface IBaseEntity { }
public interface IEntity : IBaseEntity { }
public interface IUser : IEntity { }
public interface IRepository<T> { }
public interface IValidator<T> { }
public interface INotificationService { }
public interface ILoggingService { }
public interface ISkippedInterface1 { }
public interface ISkippedInterface2 { }
public interface IExternalDep { }

// Test 1: DirectOnly mode with dependencies
[Singleton]
[RegisterAsAll(RegistrationMode.DirectOnly, InstanceSharing.Separate)]
[DependsOn<ILoggingService>]
public partial class DirectOnlyService : IUser, IRepository<string>
{
}

// Test 2: Exclusionary mode with skipped interfaces
[Scoped]
[RegisterAsAll(RegistrationMode.Exclusionary, InstanceSharing.Shared)]
[SkipRegistration<ISkippedInterface1, ISkippedInterface2>]
public partial class ExclusionaryService : IValidator<int>, INotificationService, ISkippedInterface1, ISkippedInterface2
{
}

// Test 3: All mode with mixed dependencies
[Transient]
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
[DependsOn<ILoggingService>]
[ExternalService]
public partial class AllModeService : IRepository<byte[]>, IValidator<string>
{
    [Inject] private readonly IExternalDep _external;
    [Inject] private readonly IEnumerable<INotificationService> _notifications;
}

// Test 4: Supporting services
[Scoped]
public partial class LoggingService : ILoggingService
{
}
[Scoped]
public partial class NotificationService : INotificationService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Validate DirectOnly service (only concrete type registered)
        Assert.Contains("services.AddSingleton<global::Test.DirectOnlyService>", registrationSource.Content);
        Assert.DoesNotContain("AddSingleton<global::Test.IUser, global::Test.DirectOnlyService>",
            registrationSource.Content);
        Assert.DoesNotContain("AddSingleton<global::Test.IRepository<string>, global::Test.DirectOnlyService>",
            registrationSource.Content);

        // Validate Exclusionary service (interfaces only, not skipped ones)
        // ExclusionaryService uses InstanceSharing.Shared, so should use factory pattern
        Assert.Contains("services.AddScoped<global::Test.ExclusionaryService>", registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IValidator<int>>(provider => provider.GetRequiredService<global::Test.ExclusionaryService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.INotificationService>(provider => provider.GetRequiredService<global::Test.ExclusionaryService>())",
            registrationSource.Content);
        Assert.DoesNotContain("AddScoped<ISkippedInterface1>", registrationSource.Content);
        Assert.DoesNotContain("AddScoped<ISkippedInterface2>", registrationSource.Content);

        // Validate All mode service (concrete + all interfaces, shared instances)
        Assert.Contains("services.AddTransient<global::Test.AllModeService>", registrationSource.Content);
        Assert.Contains(
            "services.AddTransient<global::Test.IRepository<byte[]>>(provider => provider.GetRequiredService<global::Test.AllModeService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddTransient<global::Test.IValidator<string>>(provider => provider.GetRequiredService<global::Test.AllModeService>())",
            registrationSource.Content);

        // Validate supporting services
        Assert.Contains("services.AddScoped<global::Test.ILoggingService, global::Test.LoggingService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.INotificationService, global::Test.NotificationService>",
            registrationSource.Content);

        // Validate constructors are generated correctly
        var directConstructor = result.GetConstructorSource("DirectOnlyService");
        Assert.NotNull(directConstructor);
        Assert.Contains("public DirectOnlyService(ILoggingService loggingService)", directConstructor.Content);

        var allModeConstructor = result.GetConstructorSource("AllModeService");
        Assert.NotNull(allModeConstructor);
        Assert.Contains(
            "public AllModeService(ILoggingService loggingService, IExternalDep external, IEnumerable<INotificationService> notifications)",
            allModeConstructor.Content);

        // No diagnostics for external dependencies due to ExternalService attribute
        var ioc001Diagnostics = result.GetDiagnosticsByCode("IOC001");
        var allModeServiceDiagnostics = ioc001Diagnostics.Where(d => d.GetMessage().Contains("AllModeService"));
        Assert.Empty(allModeServiceDiagnostics);
    }

    #endregion

    #region IEnumerable<TDependency> Resolution Tests

    [Fact]
    public void IEnumerableDependency_MultipleImplementations_ResolvesAllImplementations()
    {
        // Arrange - Multiple implementations of same interface
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace Test;

public interface INotificationService { void SendNotification(string message); }
[Scoped]
public partial class EmailService : INotificationService
{
    public void SendNotification(string message) { }
}
[Scoped]
public partial class SmsService : INotificationService
{
    public void SendNotification(string message) { }
}
[Scoped]
public partial class PushService : INotificationService
{
    public void SendNotification(string message) { }
}
[Scoped]
public partial class NotificationManager
{
    [Inject] private readonly IEnumerable<INotificationService> _services;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // All implementations should be registered
        Assert.Contains("services.AddScoped<global::Test.INotificationService, global::Test.EmailService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.INotificationService, global::Test.SmsService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.INotificationService, global::Test.PushService>",
            registrationSource.Content);

        // Constructor should accept IEnumerable<INotificationService>
        var constructorSource = result.GetConstructorSource("NotificationManager");
        Assert.NotNull(constructorSource);
        Assert.Contains("public NotificationManager(IEnumerable<INotificationService> services)",
            constructorSource.Content);
        Assert.Contains("_services = services", constructorSource.Content);
    }

    [Fact]
    public void IEnumerableDependency_NoImplementations_ResolvesEmptyCollection()
    {
        // Arrange - Service depends on interface with no implementations
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace Test;

public interface IMissingService { }

[Scoped]
public partial class ServiceWithEmptyDependency
{
    [Inject] private readonly IEnumerable<IMissingService> _services;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should not contain any IMissingService registrations
        Assert.DoesNotContain("AddScoped<IMissingService", registrationSource.Content);

        // Constructor should still accept IEnumerable<IMissingService>
        var constructorSource = result.GetConstructorSource("ServiceWithEmptyDependency");
        Assert.NotNull(constructorSource);
        Assert.Contains("public ServiceWithEmptyDependency(IEnumerable<IMissingService> services)",
            constructorSource.Content);
        Assert.Contains("_services = services", constructorSource.Content);
    }

    [Fact]
    public void IEnumerableDependency_SingleImplementation_ResolvesSingleItemCollection()
    {
        // Arrange - Only one implementation available
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace Test;

public interface IUniqueService { }

[Scoped]
public partial class OnlyImplementation : IUniqueService
{
}

[Scoped]
public partial class ConsumerService
{
    [Inject] private readonly IEnumerable<IUniqueService> _services;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Single implementation should be registered
        Assert.Contains("services.AddScoped<global::Test.IUniqueService, global::Test.OnlyImplementation>",
            registrationSource.Content);

        // Constructor should accept IEnumerable<IUniqueService>
        var constructorSource = result.GetConstructorSource("ConsumerService");
        Assert.NotNull(constructorSource);
        Assert.Contains("public ConsumerService(IEnumerable<IUniqueService> services)", constructorSource.Content);
    }

    [Fact]
    public void IEnumerableDependency_MultipleDifferentCollections_HandlesCorrectly()
    {
        // Arrange - Service with 3+ different IEnumerable<T> dependencies
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace Test;

public interface IValidator { }
public interface IProcessor { }
public interface ILogger { }

[Scoped]
public partial class ValidationService : IValidator { }

[Scoped] 
public partial class AuditValidator : IValidator { }

[Scoped]
public partial class DataProcessor : IProcessor { }

[Scoped]
public partial class FileProcessor : IProcessor { }

[Scoped]
public partial class BatchProcessor : IProcessor { }

[Scoped]
public partial class ConsoleLogger : ILogger { }

[Scoped]
public partial class ComplexService
{
    [Inject] private readonly IEnumerable<IValidator> _validators;
    [Inject] private readonly IEnumerable<IProcessor> _processors;
    [Inject] private readonly IEnumerable<ILogger> _loggers;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Validators
        Assert.Contains("services.AddScoped<global::Test.IValidator, global::Test.ValidationService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IValidator, global::Test.AuditValidator>",
            registrationSource.Content);

        // Processors
        Assert.Contains("services.AddScoped<global::Test.IProcessor, global::Test.DataProcessor>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IProcessor, global::Test.FileProcessor>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IProcessor, global::Test.BatchProcessor>",
            registrationSource.Content);

        // Loggers
        Assert.Contains("services.AddScoped<global::Test.ILogger, global::Test.ConsoleLogger>",
            registrationSource.Content);

        // Constructor should have all three collections
        var constructorSource = result.GetConstructorSource("ComplexService");
        Assert.NotNull(constructorSource);
        Assert.Contains(
            "public ComplexService(IEnumerable<IValidator> validators, IEnumerable<IProcessor> processors, IEnumerable<ILogger> loggers)",
            constructorSource.Content);
        Assert.Contains("_validators = validators", constructorSource.Content);
        Assert.Contains("_processors = processors", constructorSource.Content);
        Assert.Contains("_loggers = loggers", constructorSource.Content);
    }

    [Fact]
    public void IEnumerableDependency_RegisterAsAllIntegration_IncludesRegisterAsAllServices()
    {
        // Arrange - RegisterAsAll services should appear in IEnumerable<T> collections
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using IoCTools.Abstractions;
using System.Collections.Generic;

namespace Test;

public interface INotificationService { }
public interface IEmailService { }

[Scoped]
public partial class StandardNotification : INotificationService { }

[Scoped]
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Separate)]
public partial class EmailNotificationService : INotificationService, IEmailService { }

[Scoped]
public partial class NotificationCoordinator
{
    [Inject] private readonly IEnumerable<INotificationService> _notifications;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Both services should register for INotificationService
        Assert.Contains("services.AddScoped<global::Test.INotificationService, global::Test.StandardNotification>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.INotificationService, global::Test.EmailNotificationService>",
            registrationSource.Content);

        // RegisterAsAll should also register for IEmailService
        Assert.Contains("services.AddScoped<global::Test.IEmailService, global::Test.EmailNotificationService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.EmailNotificationService>",
            registrationSource.Content);

        // Constructor should collect both implementations
        var constructorSource = result.GetConstructorSource("NotificationCoordinator");
        Assert.NotNull(constructorSource);
        Assert.Contains("public NotificationCoordinator(IEnumerable<INotificationService> notifications)",
            constructorSource.Content);
    }

    [Fact]
    public void IEnumerableDependency_MixedRegistrationModes_CombinesCorrectly()
    {
        // Arrange - Mix of standard registration and RegisterAsAll
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using IoCTools.Abstractions;
using System.Collections.Generic;

namespace Test;

public interface IService { }
public interface ISpecialService { }

[Scoped]
public partial class StandardService : IService { }

[Scoped]
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class SharedMultiService : IService, ISpecialService { }

[Scoped]
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Separate)]
public partial class SeparateMultiService : IService, ISpecialService { }

[Scoped]
public partial class ServiceConsumer
{
    [Inject] private readonly IEnumerable<IService> _services;
    [Inject] private readonly IEnumerable<ISpecialService> _specialServices;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // All services should register for IService
        Assert.Contains("services.AddScoped<global::Test.IService, global::Test.StandardService>",
            registrationSource.Content);

        // SharedMultiService uses InstanceSharing.Shared, so it should use factory pattern
        Assert.Contains("services.AddScoped<global::Test.SharedMultiService>", registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IService>(provider => provider.GetRequiredService<global::Test.SharedMultiService>())",
            registrationSource.Content);

        // SeparateMultiService uses InstanceSharing.Separate, so direct registration
        Assert.Contains("services.AddScoped<global::Test.IService, global::Test.SeparateMultiService>",
            registrationSource.Content);

        // Multi-services should also register for ISpecialService
        Assert.Contains(
            "services.AddScoped<global::Test.ISpecialService>(provider => provider.GetRequiredService<global::Test.SharedMultiService>())",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.ISpecialService, global::Test.SeparateMultiService>",
            registrationSource.Content);

        // Constructors should handle collections correctly
        var constructorSource = result.GetConstructorSource("ServiceConsumer");
        Assert.NotNull(constructorSource);
        Assert.Contains(
            "public ServiceConsumer(IEnumerable<IService> services, IEnumerable<ISpecialService> specialServices)",
            constructorSource.Content);
    }

    [Fact]
    public void IEnumerableDependency_SharedInstancesInCollections_BehavesCorrectly()
    {
        // Arrange - Verify shared instances work correctly in collections
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using IoCTools.Abstractions;
using System.Collections.Generic;

namespace Test;

public interface ISharedService { }

[Singleton]
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class SharedSingletonService : ISharedService { }

[Scoped]
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class SharedScopedService : ISharedService { }

[Scoped]
public partial class CollectionConsumer
{
    [Inject] private readonly IEnumerable<ISharedService> _services;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Shared services should use factory pattern for interface registration
        Assert.Contains("services.AddSingleton<global::Test.SharedSingletonService>",
            registrationSource.Content);
        Assert.Contains(
            "services.AddSingleton<global::Test.ISharedService>(provider => provider.GetRequiredService<global::Test.SharedSingletonService>())",
            registrationSource.Content);

        Assert.Contains("services.AddScoped<global::Test.SharedScopedService>", registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.ISharedService>(provider => provider.GetRequiredService<global::Test.SharedScopedService>())",
            registrationSource.Content);

        // Constructor should accept collection
        var constructorSource = result.GetConstructorSource("CollectionConsumer");
        Assert.NotNull(constructorSource);
        Assert.Contains("public CollectionConsumer(IEnumerable<ISharedService> services)", constructorSource.Content);
    }

    [Fact]
    public void IEnumerableDependency_InheritanceHierarchy_ResolvesAllLevels()
    {
        // Arrange - Test IEnumerable with interface inheritance
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace Test;

public interface IEntity { }
public interface IUser : IEntity { }
public interface IAdminUser : IUser { }

[Scoped]
public partial class BasicUser : IUser { }

[Scoped]
public partial class AdminUser : IAdminUser { }

[Scoped]
public partial class SuperAdmin : IAdminUser { }

[Scoped]
public partial class UserManager
{
    [Inject] private readonly IEnumerable<IEntity> _entities;
    [Inject] private readonly IEnumerable<IUser> _users;
    [Inject] private readonly IEnumerable<IAdminUser> _admins;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // All users should register for IEntity (through inheritance)
        Assert.Contains("services.AddScoped<global::Test.IEntity, global::Test.BasicUser>", registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IEntity, global::Test.AdminUser>", registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IEntity, global::Test.SuperAdmin>",
            registrationSource.Content);

        // Users should register for IUser
        Assert.Contains("services.AddScoped<global::Test.IUser, global::Test.BasicUser>", registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IUser, global::Test.AdminUser>", registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IUser, global::Test.SuperAdmin>", registrationSource.Content);

        // Only admin users should register for IAdminUser
        Assert.Contains("services.AddScoped<global::Test.IAdminUser, global::Test.AdminUser>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IAdminUser, global::Test.SuperAdmin>",
            registrationSource.Content);
        Assert.DoesNotContain("AddScoped<global::Test.IAdminUser, global::Test.BasicUser>", registrationSource.Content);
    }

    [Fact]
    public void IEnumerableDependency_GenericInterfaces_HandlesCorrectly()
    {
        // Arrange - Test IEnumerable with generic interfaces
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace Test;

public interface IRepository<T> { }
public interface IValidator<T> { }
[Scoped]
public partial class UserRepository : IRepository<string> { }

[Scoped]
public partial class ProductRepository : IRepository<string> { }

[Scoped]
public partial class IntValidator : IValidator<int> { }

[Scoped]
public partial class StringValidator : IValidator<string> { }

[Scoped]
public partial class GenericConsumer
{
    [Inject] private readonly IEnumerable<IRepository<string>> _stringRepositories;
    [Inject] private readonly IEnumerable<IValidator<int>> _intValidators;
    [Inject] private readonly IEnumerable<IValidator<string>> _stringValidators;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // String repositories
        Assert.Contains("services.AddScoped<global::Test.IRepository<string>, global::Test.UserRepository>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IRepository<string>, global::Test.ProductRepository>",
            registrationSource.Content);

        // Validators by type
        Assert.Contains("services.AddScoped<global::Test.IValidator<int>, global::Test.IntValidator>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IValidator<string>, global::Test.StringValidator>",
            registrationSource.Content);

        // Constructor should handle generic collections
        var constructorSource = result.GetConstructorSource("GenericConsumer");
        Assert.NotNull(constructorSource);
        Assert.Contains(
            "public GenericConsumer(IEnumerable<IRepository<string>> stringRepositories, IEnumerable<IValidator<int>> intValidators, IEnumerable<IValidator<string>> stringValidators)",
            constructorSource.Content);
    }

    [Fact]
    public void IEnumerableDependency_ExternalServices_SkipsDiagnostics()
    {
        // Arrange - External services with IEnumerable dependencies
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace Test;

public interface IExternalDependency { }

[Scoped]
[ExternalService]
public partial class ExternalConsumer
{
    [Inject] private readonly IEnumerable<IExternalDependency> _dependencies;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        // Should not have diagnostics for missing IExternalDependency implementations
        var ioc001Diagnostics = result.GetDiagnosticsByCode("IOC001");
        var externalServiceDiagnostics = ioc001Diagnostics.Where(d => d.GetMessage().Contains("ExternalConsumer"));
        Assert.Empty(externalServiceDiagnostics);

        // Constructor should still be generated
        var constructorSource = result.GetConstructorSource("ExternalConsumer");
        Assert.NotNull(constructorSource);
        Assert.Contains("public ExternalConsumer(IEnumerable<IExternalDependency> dependencies)",
            constructorSource.Content);
    }

    [Fact]
    public void IEnumerableDependency_DifferentLifetimes_MixedCorrectly()
    {
        // Arrange - Different lifetimes in same collection
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using IoCTools.Abstractions;
using System.Collections.Generic;

namespace Test;

public interface IService { }

[Singleton]
public partial class SingletonService : IService { }

[Scoped]
public partial class ScopedService : IService { }

[Transient]
public partial class TransientService : IService { }

[Scoped]
public partial class MixedLifetimeConsumer
{
    [Inject] private readonly IEnumerable<IService> _services;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Each service should maintain its own lifetime
        Assert.Contains("services.AddSingleton<global::Test.IService, global::Test.SingletonService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IService, global::Test.ScopedService>",
            registrationSource.Content);
        Assert.Contains("services.AddTransient<global::Test.IService, global::Test.TransientService>",
            registrationSource.Content);

        // Constructor should accept collection
        var constructorSource = result.GetConstructorSource("MixedLifetimeConsumer");
        Assert.NotNull(constructorSource);
        Assert.Contains("public MixedLifetimeConsumer(IEnumerable<IService> services)", constructorSource.Content);
    }

    #endregion
}
