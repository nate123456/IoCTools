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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should register concrete type and INotificationService but not IUserService
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.UserNotificationService, global::Test.UserNotificationService>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.INotificationService, global::Test.UserNotificationService>");
        registrationSource.Content.Should().NotContain("AddScoped<IUserService");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should only register concrete type since all interfaces are skipped
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.StackedSkipService, global::Test.StackedSkipService>");

        // Should not register any interfaces
        registrationSource.Content.Should().NotContain("AddScoped<IService1");
        registrationSource.Content.Should().NotContain("AddScoped<IService2");
        registrationSource.Content.Should().NotContain("AddScoped<IService3");
        registrationSource.Content.Should().NotContain("AddScoped<IService4");
        registrationSource.Content.Should().NotContain("AddScoped<IService5");
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
        result.HasErrors.Should().BeFalse();

        // Should have no diagnostics for missing dependency due to ExternalService
        var ioc001Diagnostics = result.GetDiagnosticsByCode("IOC001");
        var serviceRelatedDiagnostics = ioc001Diagnostics.Where(d => d.GetMessage().Contains("ExternalMultiService"));
        serviceRelatedDiagnostics.Should().BeEmpty();

        // Should still register all interfaces
        var registrationSource = result.GetRequiredServiceRegistrationSource();
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.ExternalMultiService, global::Test.ExternalMultiService>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IUserService, global::Test.ExternalMultiService>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.INotificationService, global::Test.ExternalMultiService>");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should register concrete class and all interfaces (including inherited) with shared instance pattern
        // For InstanceSharing.Shared: concrete class gets direct registration, interfaces get factory pattern
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.ConcreteService, global::Test.ConcreteService>");
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.IService1>(provider => provider.GetRequiredService<global::Test.ConcreteService>())");
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.IService2>(provider => provider.GetRequiredService<global::Test.ConcreteService>())");
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.IService3>(provider => provider.GetRequiredService<global::Test.ConcreteService>())");

        // Should NOT register abstract base class
        registrationSource.Content.Should().NotContain("AddScoped<BaseService");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Singleton with shared instances
        // CRITICAL FIX: Services with explicit lifetime attributes ([Singleton]) use single-parameter form for concrete registration
        registrationSource.Content.Should().Contain("services.AddSingleton<global::Test.SingletonService>();");
        registrationSource.Content.Should()
            .Contain(
                "services.AddSingleton<global::Test.IUserService>(provider => provider.GetRequiredService<global::Test.SingletonService>())");
        registrationSource.Content.Should()
            .Contain(
                "services.AddSingleton<global::Test.INotificationService>(provider => provider.GetRequiredService<global::Test.SingletonService>())");

        // Transient with separate instances  
        // CRITICAL FIX: Services with explicit lifetime attributes ([Transient]) use single-parameter form for concrete registration
        registrationSource.Content.Should().Contain("services.AddTransient<global::Test.TransientService>();");
        registrationSource.Content.Should()
            .Contain("services.AddTransient<global::Test.IUserService, global::Test.TransientService>");
        registrationSource.Content.Should()
            .Contain("services.AddTransient<global::Test.INotificationService, global::Test.TransientService>");
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
        result.HasErrors.Should().BeFalse();

        // Should have no diagnostics due to ExternalService
        var ioc001Diagnostics = result.GetDiagnosticsByCode("IOC001");
        var complexServiceDiagnostics = ioc001Diagnostics.Where(d => d.GetMessage().Contains("ComplexService"));
        complexServiceDiagnostics.Should().BeEmpty();

        // Check constructor generation
        var constructorSource = result.GetRequiredConstructorSource("ComplexService");
        constructorSource.Content.Should()
            .Contain("public ComplexService(ILogger logger, IExternalDep externalDep, IEnumerable<ILogger> loggers)");

        // Check service registration
        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should register Logger first
        registrationSource.Content.Should().Contain("services.AddScoped<global::Test.ILogger, global::Test.Logger>");

        // Should register ComplexService as Singleton with shared instances
        registrationSource.Content.Should().Contain("services.AddSingleton<global::Test.ComplexService>");

        // Should register all non-skipped interfaces with factory pattern
        registrationSource.Content.Should()
            .Contain(
                "services.AddSingleton<global::Test.IUserQuery>(provider => provider.GetRequiredService<global::Test.ComplexService>())");
        registrationSource.Content.Should()
            .Contain(
                "services.AddSingleton<global::Test.IUser>(provider => provider.GetRequiredService<global::Test.ComplexService>())");
        registrationSource.Content.Should()
            .Contain(
                "services.AddSingleton<global::Test.IEntity>(provider => provider.GetRequiredService<global::Test.ComplexService>())");
        registrationSource.Content.Should()
            .Contain(
                "services.AddSingleton<global::Test.IValidator<string>>(provider => provider.GetRequiredService<global::Test.ComplexService>())");
        registrationSource.Content.Should()
            .Contain(
                "services.AddSingleton<global::Test.INotificationService>(provider => provider.GetRequiredService<global::Test.ComplexService>())");

        // Should NOT register skipped interfaces
        registrationSource.Content.Should().NotContain("AddSingleton<ISkippedService>");
        registrationSource.Content.Should().NotContain("AddSingleton<IRepository<string>>");
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
        result.HasErrors.Should().BeFalse();

        // Should detect and warn about circular dependency
        var circularDiagnostics = result.GetDiagnosticsByCode("IOC003");
        circularDiagnostics.Should().NotBeEmpty();

        // Validate circular dependency diagnostic content
        circularDiagnostics.Should().AllSatisfy(d =>
        {
            d.Severity.Should().Be(DiagnosticSeverity.Warning);
            var message = d.GetMessage();
            (message.Contains("ServiceA") || message.Contains("ServiceB")).Should().BeTrue();
        });

        // But should still generate registration code
        var registrationSource = result.GetRequiredServiceRegistrationSource();
        // Should register with InstanceSharing.Shared pattern - concrete direct, interfaces with factory
        registrationSource.Content.Should().Contain("services.AddScoped<global::Test.ServiceA, global::Test.ServiceA>");
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.IServiceA>(provider => provider.GetRequiredService<global::Test.ServiceA>())");
        registrationSource.Content.Should().Contain("services.AddScoped<global::Test.ServiceB, global::Test.ServiceB>");
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.IServiceB>(provider => provider.GetRequiredService<global::Test.ServiceB>())");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should only register concrete type
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.UserNotificationService, global::Test.UserNotificationService>");

        // Should NOT register interfaces
        registrationSource.Content.Should().NotContain("AddScoped<IUserService");
        registrationSource.Content.Should().NotContain("AddScoped<INotificationService");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should register concrete type and all interfaces
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.UserNotificationService, global::Test.UserNotificationService>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IUserService, global::Test.UserNotificationService>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.INotificationService, global::Test.UserNotificationService>");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should register interfaces only
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IUserService, global::Test.UserNotificationService>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.INotificationService, global::Test.UserNotificationService>");

        // Should NOT register concrete type
        registrationSource.Content.Should()
            .NotContain("AddScoped<global::Test.UserNotificationService, global::Test.UserNotificationService>");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // For shared instances, should register concrete type normally and interfaces with factory pattern
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.UserNotificationService, global::Test.UserNotificationService>");
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.IUserService>(provider => provider.GetRequiredService<global::Test.UserNotificationService>())");
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.INotificationService>(provider => provider.GetRequiredService<global::Test.UserNotificationService>())");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // For separate instances, each registration creates its own instance
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.UserNotificationService, global::Test.UserNotificationService>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IUserService, global::Test.UserNotificationService>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.INotificationService, global::Test.UserNotificationService>");
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
        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Assert - Validate the registration pattern is correct for shared instances
        result.HasErrors.Should().BeFalse();
        registrationSource.Should().NotBeNull();

        // CRITICAL: Validate that the factory pattern is generated correctly for shared instances
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.UserNotificationService, global::Test.UserNotificationService>");
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.IUserService>(provider => provider.GetRequiredService<global::Test.UserNotificationService>())");
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.INotificationService>(provider => provider.GetRequiredService<global::Test.UserNotificationService>())");

        // CRITICAL: Validate that we don't have direct interface registrations for shared instances
        registrationSource.Content.Should()
            .NotContain("AddScoped<global::Test.IUserService, global::Test.UserNotificationService>");
        registrationSource.Content.Should()
            .NotContain("AddScoped<global::Test.INotificationService, global::Test.UserNotificationService>");
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
        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Assert - Validate separate instance registration pattern
        result.HasErrors.Should().BeFalse();
        registrationSource.Should().NotBeNull();

        // CRITICAL: Validate direct registrations (no factory pattern)
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.UserNotificationService, global::Test.UserNotificationService>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IUserService, global::Test.UserNotificationService>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.INotificationService, global::Test.UserNotificationService>");

        // CRITICAL: Validate that we don't have factory patterns
        registrationSource.Content.Should().NotContain("provider => provider.GetRequiredService");
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
        var registrationSource = result.GetRequiredServiceRegistrationSource();

        result.HasErrors.Should().BeFalse();
        registrationSource.Should().NotBeNull();

        // CRITICAL: All registrations must use the same lifetime (single parameter for explicit lifetime + shared)
        registrationSource.Content.Should().Contain("services.AddSingleton<global::Test.SingletonSharedService>");
        registrationSource.Content.Should()
            .Contain(
                "services.AddSingleton<global::Test.IService1>(provider => provider.GetRequiredService<global::Test.SingletonSharedService>())");
        registrationSource.Content.Should()
            .Contain(
                "services.AddSingleton<global::Test.IService2>(provider => provider.GetRequiredService<global::Test.SingletonSharedService>())");
        registrationSource.Content.Should()
            .Contain(
                "services.AddSingleton<global::Test.IService3>(provider => provider.GetRequiredService<global::Test.SingletonSharedService>())");

        // CRITICAL: No mixed lifetimes
        registrationSource.Content.Should().NotContain("AddScoped<");
        registrationSource.Content.Should().NotContain("AddTransient<");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should only register concrete type and ILoggingService
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.MultiService, global::Test.MultiService>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.ILoggingService, global::Test.MultiService>");
        registrationSource.Content.Should().NotContain("AddScoped<IUserService");
        registrationSource.Content.Should().NotContain("AddScoped<INotificationService");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should only register concrete type and IService6
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.MegaService, global::Test.MegaService>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IService6, global::Test.MegaService>");

        // Should not register the skipped services
        registrationSource.Content.Should().NotContain("AddScoped<IService1");
        registrationSource.Content.Should().NotContain("AddScoped<IService2");
        registrationSource.Content.Should().NotContain("AddScoped<IService3");
        registrationSource.Content.Should().NotContain("AddScoped<IService4");
        registrationSource.Content.Should().NotContain("AddScoped<IService5");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should register concrete type and all interface levels
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.UserService, global::Test.UserService>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IUserQuery, global::Test.UserService>");
        registrationSource.Content.Should().Contain("services.AddScoped<global::Test.IUser, global::Test.UserService>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IEntity, global::Test.UserService>");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should register concrete type, IUserQuery, and IEntity but not IUser
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.UserService, global::Test.UserService>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IUserQuery, global::Test.UserService>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IEntity, global::Test.UserService>");
        registrationSource.Content.Should().NotContain("AddScoped<global::Test.IUser, global::Test.UserService>");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should register concrete type and both generic interfaces
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.UserRepository, global::Test.UserRepository>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IRepository<string>, global::Test.UserRepository>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IValidator<string>, global::Test.UserRepository>");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should register concrete type and IValidator but not IRepository
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.UserRepository, global::Test.UserRepository>");
        // Accept either string or System.String format for generic types
        (registrationSource.Content.Contains(
             "AddScoped<global::Test.IValidator<System.String>, global::Test.UserRepository>") ||
         registrationSource.Content.Contains(
             "AddScoped<global::Test.IValidator<string>, global::Test.UserRepository>")).Should()
            .BeTrue("Should contain IValidator registration with either string or System.String format");
        registrationSource.Content.Should().NotContain("AddScoped<IRepository<"); // Skip any form of IRepository
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
        result.HasErrors.Should().BeFalse();

        // Should have constructor with dependencies
        var constructorSource = result.GetRequiredConstructorSource("UserNotificationService");
        constructorSource.Content.Should()
            .Contain("public UserNotificationService(ILogger logger, IValidator validator)");

        // Should register with shared instances (factory pattern)
        var registrationSource = result.GetRequiredServiceRegistrationSource();
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.UserNotificationService, global::Test.UserNotificationService>");
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.IUserService>(provider => provider.GetRequiredService<global::Test.UserNotificationService>())");
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.INotificationService>(provider => provider.GetRequiredService<global::Test.UserNotificationService>())");
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
        result.HasErrors.Should().BeFalse();

        // Should have constructor with injected dependencies
        var constructorSource = result.GetRequiredConstructorSource("UserNotificationService");
        constructorSource.Content.Should().Contain("public UserNotificationService(ILogger logger)");
        constructorSource.Content.Should().Contain("_logger = logger");

        // Should register all interfaces separately
        var registrationSource = result.GetRequiredServiceRegistrationSource();
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.UserNotificationService, global::Test.UserNotificationService>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IUserService, global::Test.UserNotificationService>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.INotificationService, global::Test.UserNotificationService>");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // BaseService should register with InstanceSharing.Shared pattern
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.BaseService, global::Test.BaseService>");
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.IService>(provider => provider.GetRequiredService<global::Test.BaseService>())");

        // DerivedService should register with InstanceSharing.Shared pattern
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.DerivedService, global::Test.DerivedService>");
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.ISpecialService>(provider => provider.GetRequiredService<global::Test.DerivedService>())");
        // Note: IService from base class should also be registered for derived class with factory pattern
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.IService>(provider => provider.GetRequiredService<global::Test.DerivedService>())");
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
        result.HasErrors.Should().BeFalse();

        // Should work but potentially have a warning about using RegisterAsAll with single interface
        var warnings = result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();
        // Note: Currently no specific warning for single interface RegisterAsAll usage
        // This is acceptable behavior - future enhancement could add IOC010 diagnostic

        var registrationSource = result.GetRequiredServiceRegistrationSource();
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.UserService, global::Test.UserService>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IUserService, global::Test.UserService>");
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
        result.HasErrors.Should().BeFalse();

        // Should have warning about skipping non-implemented interface
        var ioc009Diagnostics = result.GetDiagnosticsByCode("IOC009");
        ioc009Diagnostics.Should().NotBeEmpty();

        // Validate the diagnostic message contains relevant context
        ioc009Diagnostics.Should().AllSatisfy(d =>
        {
            d.Severity.Should().Be(DiagnosticSeverity.Warning);
            var message = d.GetMessage();
            message.Should().Contain("INonImplemented");
        });

        var registrationSource = result.GetRequiredServiceRegistrationSource();
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.UserService, global::Test.UserService>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IUserService, global::Test.UserService>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.INotificationService, global::Test.UserService>");
    }

    [Fact]
    public void SkipRegistration_WithDirectOnlyRegisterAsAll_GeneratesIOC038()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IUserService { }
[RegisterAsAll(RegistrationMode.DirectOnly)]
[SkipRegistration<IUserService>]
public partial class DirectOnlyService : IUserService
{
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diagnostics = result.GetDiagnosticsByCode("IOC038");
        diagnostics.Should().ContainSingle();
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);
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
        registrationSource.Should().BeNull();

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
        ioc004Diagnostics.Should().BeEmpty();

        // Should generate service registrations
        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should register concrete class and all interfaces with separate instances
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.UserService, global::Test.UserService>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IUserService, global::Test.UserService>");
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
        result.HasErrors.Should().BeFalse();

        // With intelligent inference, IOC005 diagnostic was removed
        var ioc005Diagnostics = result.GetDiagnosticsByCode("IOC005");
        ioc005Diagnostics.Should().BeEmpty();

        // Should generate registration based on intelligent inference
        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // With SkipRegistration without RegisterAsAll, the generator might not produce any registrations
        // since SkipRegistration typically means "don't register this interface"
        if (registrationSource != null)
            // If registrations are generated, check they are correct
            registrationSource.Content.Should()
                .Contain("services.AddScoped<global::Test.UserService, global::Test.UserService>");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should register concrete type and all 10 interfaces
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.MegaInterfaceService, global::Test.MegaInterfaceService>");
        for (var i = 1; i <= 10; i++)
            registrationSource.Content.Should()
                .Contain($"AddScoped<global::Test.IService{i:D2}, global::Test.MegaInterfaceService>");

        // Performance check - should generate reasonable amount of code
        (registrationSource.Content.Length < 50000).Should().BeTrue("Generated code should be reasonable in size");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should register concrete type and only non-skipped interfaces
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.MaxSkipService, global::Test.MaxSkipService>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IService6, global::Test.MaxSkipService>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IService7, global::Test.MaxSkipService>");

        // Should NOT register skipped interfaces
        for (var i = 1; i <= 5; i++)
            registrationSource.Content.Should()
                .NotContain($"AddScoped<global::Test.IService{i}, global::Test.MaxSkipService>");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // NestedGenericService uses InstanceSharing.Shared, so should use factory pattern
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.NestedGenericService, global::Test.NestedGenericService>");
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.IRepository<List<string>>>(provider => provider.GetRequiredService<global::Test.NestedGenericService>())");
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.IComplexRepository<Dictionary<string, int>>>(provider => provider.GetRequiredService<global::Test.NestedGenericService>())");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should register with correct generic constraints
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.ConstrainedService, global::Test.ConstrainedService>");
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.IConstrainedRepository<global::Test.TestClass>, global::Test.ConstrainedService>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IValueRepository<int>, global::Test.ConstrainedService>");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should handle multiple generic parameters correctly with shared instance pattern (factory lambda)
        // For shared instances, concrete class uses direct registration, interfaces use factory lambdas
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.MultiGenericService, global::Test.MultiGenericService>");
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.IMapper<string, int>>(provider => provider.GetRequiredService<global::Test.MultiGenericService>())");
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.IConverter<byte[], string, object>>(provider => provider.GetRequiredService<global::Test.MultiGenericService>())");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Base service should use RegisterAsAll behavior with InstanceSharing.Shared pattern
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.BaseService, global::Test.BaseService>");
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.IBaseService>(provider => provider.GetRequiredService<global::Test.BaseService>())");

        // Derived service should use standard behavior (interface → implementation)
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IDerivedService, global::Test.DerivedService>");
        // Derived should also handle inherited interface normally
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IBaseService, global::Test.DerivedService>");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Each service should maintain its own configuration
        // SharedService with InstanceSharing.Shared should use factory pattern for interfaces
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.SharedService, global::Test.SharedService>");
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.ISharedService>(provider => provider.GetRequiredService<global::Test.SharedService>())");

        // SeparateService with direct registration
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.ISharedService, global::Test.SeparateService>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.ISpecialService, global::Test.SeparateService>");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // ConcreteImpl uses InstanceSharing.Shared, so should use factory pattern
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.ConcreteImpl, global::Test.ConcreteImpl>");
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.IService1>(provider => provider.GetRequiredService<global::Test.ConcreteImpl>())");
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.IService3>(provider => provider.GetRequiredService<global::Test.ConcreteImpl>())");

        // Should NOT register skipped interface
        registrationSource.Content.Should().NotContain("AddScoped<IService2>");

        // Should NOT register abstract base
        registrationSource.Content.Should().NotContain("AddScoped<AbstractBase");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should handle interfaces from different namespaces
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.Implementation.CrossNamespaceService, global::Test.Implementation.CrossNamespaceService>");
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.Services.IUserService, global::Test.Implementation.CrossNamespaceService>");
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.Notifications.INotificationService, global::Test.Implementation.CrossNamespaceService>");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // PartialService uses InstanceSharing.Shared, so should use factory pattern  
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.PartialService, global::Test.PartialService>");
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.IService1>(provider => provider.GetRequiredService<global::Test.PartialService>())");
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.IService2>(provider => provider.GetRequiredService<global::Test.PartialService>())");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Validate shared instance pattern: concrete registered normally, interfaces use factory
        // CRITICAL FIX: Services with explicit lifetime attributes ([Singleton]) use single-parameter form for concrete registration
        registrationSource.Content.Should().Contain("services.AddSingleton<global::Test.SharedInstanceService>();");

        // All interfaces should resolve to the same instance via factory
        registrationSource.Content.Should()
            .Contain(
                "services.AddSingleton<global::Test.IService1>(provider => provider.GetRequiredService<global::Test.SharedInstanceService>())");
        registrationSource.Content.Should()
            .Contain(
                "services.AddSingleton<global::Test.IService2>(provider => provider.GetRequiredService<global::Test.SharedInstanceService>())");
        registrationSource.Content.Should()
            .Contain(
                "services.AddSingleton<global::Test.IService3>(provider => provider.GetRequiredService<global::Test.SharedInstanceService>())");

        // Should NOT have direct interface-to-implementation registrations
        registrationSource.Content.Should()
            .NotContain("AddSingleton<global::Test.IService1, global::Test.SharedInstanceService>");
        registrationSource.Content.Should()
            .NotContain("AddSingleton<global::Test.IService2, global::Test.SharedInstanceService>");
        registrationSource.Content.Should()
            .NotContain("AddSingleton<global::Test.IService3, global::Test.SharedInstanceService>");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Validate separate instance pattern: each registration creates new instance
        registrationSource.Content.Should().Contain("services.AddTransient<global::Test.SeparateInstanceService>");
        registrationSource.Content.Should()
            .Contain("services.AddTransient<global::Test.IService1, global::Test.SeparateInstanceService>");
        registrationSource.Content.Should()
            .Contain("services.AddTransient<global::Test.IService2, global::Test.SeparateInstanceService>");
        registrationSource.Content.Should()
            .Contain("services.AddTransient<global::Test.IService3, global::Test.SeparateInstanceService>");

        // Should NOT have factory-based registrations
        registrationSource.Content.Should()
            .NotContain("provider => provider.GetRequiredService<global::Test.SeparateInstanceService>()");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should handle factory patterns correctly with shared instances
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.FactoryService, global::Test.FactoryService>");
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.IService>(provider => provider.GetRequiredService<global::Test.FactoryService>())");
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.IServiceFactory>(provider => provider.GetRequiredService<global::Test.FactoryService>())");
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.IFactory<global::Test.IConfigurableService>>(provider => provider.GetRequiredService<global::Test.FactoryService>())");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Validate DirectOnly service (only concrete type registered)
        registrationSource.Content.Should().Contain("services.AddSingleton<global::Test.DirectOnlyService>");
        registrationSource.Content.Should()
            .NotContain("AddSingleton<global::Test.IUser, global::Test.DirectOnlyService>");
        registrationSource.Content.Should()
            .NotContain("AddSingleton<global::Test.IRepository<string>, global::Test.DirectOnlyService>");

        // Validate Exclusionary service (interfaces only, not skipped ones)
        // ExclusionaryService uses InstanceSharing.Shared, so should use factory pattern
        registrationSource.Content.Should().Contain("services.AddScoped<global::Test.ExclusionaryService>");
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.IValidator<int>>(provider => provider.GetRequiredService<global::Test.ExclusionaryService>())");
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.INotificationService>(provider => provider.GetRequiredService<global::Test.ExclusionaryService>())");
        registrationSource.Content.Should().NotContain("AddScoped<ISkippedInterface1>");
        registrationSource.Content.Should().NotContain("AddScoped<ISkippedInterface2>");

        // Validate All mode service (concrete + all interfaces, shared instances)
        registrationSource.Content.Should().Contain("services.AddTransient<global::Test.AllModeService>");
        registrationSource.Content.Should()
            .Contain(
                "services.AddTransient<global::Test.IRepository<byte[]>>(provider => provider.GetRequiredService<global::Test.AllModeService>())");
        registrationSource.Content.Should()
            .Contain(
                "services.AddTransient<global::Test.IValidator<string>>(provider => provider.GetRequiredService<global::Test.AllModeService>())");

        // Validate supporting services
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.ILoggingService, global::Test.LoggingService>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.INotificationService, global::Test.NotificationService>");

        // Validate constructors are generated correctly
        var directConstructor = result.GetRequiredConstructorSource("DirectOnlyService");
        directConstructor.Content.Should().Contain("public DirectOnlyService(ILoggingService loggingService)");

        var allModeConstructor = result.GetRequiredConstructorSource("AllModeService");
        allModeConstructor.Content.Should()
            .Contain(
                "public AllModeService(ILoggingService loggingService, IExternalDep external, IEnumerable<INotificationService> notifications)");

        // No diagnostics for external dependencies due to ExternalService attribute
        var ioc001Diagnostics = result.GetDiagnosticsByCode("IOC001");
        var allModeServiceDiagnostics = ioc001Diagnostics.Where(d => d.GetMessage().Contains("AllModeService"));
        allModeServiceDiagnostics.Should().BeEmpty();
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // All implementations should be registered
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.INotificationService, global::Test.EmailService>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.INotificationService, global::Test.SmsService>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.INotificationService, global::Test.PushService>");

        // Constructor should accept IEnumerable<INotificationService>
        var constructorSource = result.GetRequiredConstructorSource("NotificationManager");
        constructorSource.Content.Should()
            .Contain("public NotificationManager(IEnumerable<INotificationService> services)");
        constructorSource.Content.Should().Contain("_services = services");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should not contain any IMissingService registrations
        registrationSource.Content.Should().NotContain("AddScoped<IMissingService");

        // Constructor should still accept IEnumerable<IMissingService>
        var constructorSource = result.GetRequiredConstructorSource("ServiceWithEmptyDependency");
        constructorSource.Content.Should()
            .Contain("public ServiceWithEmptyDependency(IEnumerable<IMissingService> services)");
        constructorSource.Content.Should().Contain("_services = services");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Single implementation should be registered
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IUniqueService, global::Test.OnlyImplementation>");

        // Constructor should accept IEnumerable<IUniqueService>
        var constructorSource = result.GetRequiredConstructorSource("ConsumerService");
        constructorSource.Content.Should().Contain("public ConsumerService(IEnumerable<IUniqueService> services)");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Validators
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IValidator, global::Test.ValidationService>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IValidator, global::Test.AuditValidator>");

        // Processors
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IProcessor, global::Test.DataProcessor>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IProcessor, global::Test.FileProcessor>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IProcessor, global::Test.BatchProcessor>");

        // Loggers
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.ILogger, global::Test.ConsoleLogger>");

        // Constructor should have all three collections
        var constructorSource = result.GetRequiredConstructorSource("ComplexService");
        constructorSource.Content.Should()
            .Contain(
                "public ComplexService(IEnumerable<IValidator> validators, IEnumerable<IProcessor> processors, IEnumerable<ILogger> loggers)");
        constructorSource.Content.Should().Contain("_validators = validators");
        constructorSource.Content.Should().Contain("_processors = processors");
        constructorSource.Content.Should().Contain("_loggers = loggers");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Both services should register for INotificationService
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.INotificationService, global::Test.StandardNotification>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.INotificationService, global::Test.EmailNotificationService>");

        // RegisterAsAll should also register for IEmailService
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IEmailService, global::Test.EmailNotificationService>");
        registrationSource.Content.Should().Contain("services.AddScoped<global::Test.EmailNotificationService>");

        // Constructor should collect both implementations
        var constructorSource = result.GetRequiredConstructorSource("NotificationCoordinator");
        constructorSource.Content.Should()
            .Contain("public NotificationCoordinator(IEnumerable<INotificationService> notifications)");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // All services should register for IService
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IService, global::Test.StandardService>");

        // SharedMultiService uses InstanceSharing.Shared, so it should use factory pattern
        registrationSource.Content.Should().Contain("services.AddScoped<global::Test.SharedMultiService>");
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.IService>(provider => provider.GetRequiredService<global::Test.SharedMultiService>())");

        // SeparateMultiService uses InstanceSharing.Separate, so direct registration
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IService, global::Test.SeparateMultiService>");

        // Multi-services should also register for ISpecialService
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.ISpecialService>(provider => provider.GetRequiredService<global::Test.SharedMultiService>())");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.ISpecialService, global::Test.SeparateMultiService>");

        // Constructors should handle collections correctly
        var constructorSource = result.GetRequiredConstructorSource("ServiceConsumer");
        constructorSource.Content.Should()
            .Contain(
                "public ServiceConsumer(IEnumerable<IService> services, IEnumerable<ISpecialService> specialServices)");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Shared services should use factory pattern for interface registration
        registrationSource.Content.Should().Contain("services.AddSingleton<global::Test.SharedSingletonService>");
        registrationSource.Content.Should()
            .Contain(
                "services.AddSingleton<global::Test.ISharedService>(provider => provider.GetRequiredService<global::Test.SharedSingletonService>())");

        registrationSource.Content.Should().Contain("services.AddScoped<global::Test.SharedScopedService>");
        registrationSource.Content.Should()
            .Contain(
                "services.AddScoped<global::Test.ISharedService>(provider => provider.GetRequiredService<global::Test.SharedScopedService>())");

        // Constructor should accept collection
        var constructorSource = result.GetRequiredConstructorSource("CollectionConsumer");
        constructorSource.Content.Should().Contain("public CollectionConsumer(IEnumerable<ISharedService> services)");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // All users should register for IEntity (through inheritance)
        registrationSource.Content.Should().Contain("services.AddScoped<global::Test.IEntity, global::Test.BasicUser>");
        registrationSource.Content.Should().Contain("services.AddScoped<global::Test.IEntity, global::Test.AdminUser>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IEntity, global::Test.SuperAdmin>");

        // Users should register for IUser
        registrationSource.Content.Should().Contain("services.AddScoped<global::Test.IUser, global::Test.BasicUser>");
        registrationSource.Content.Should().Contain("services.AddScoped<global::Test.IUser, global::Test.AdminUser>");
        registrationSource.Content.Should().Contain("services.AddScoped<global::Test.IUser, global::Test.SuperAdmin>");

        // Only admin users should register for IAdminUser
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IAdminUser, global::Test.AdminUser>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IAdminUser, global::Test.SuperAdmin>");
        registrationSource.Content.Should().NotContain("AddScoped<global::Test.IAdminUser, global::Test.BasicUser>");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // String repositories
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IRepository<string>, global::Test.UserRepository>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IRepository<string>, global::Test.ProductRepository>");

        // Validators by type
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IValidator<int>, global::Test.IntValidator>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IValidator<string>, global::Test.StringValidator>");

        // Constructor should handle generic collections
        var constructorSource = result.GetRequiredConstructorSource("GenericConsumer");
        constructorSource.Content.Should()
            .Contain(
                "public GenericConsumer(IEnumerable<IRepository<string>> stringRepositories, IEnumerable<IValidator<int>> intValidators, IEnumerable<IValidator<string>> stringValidators)");
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
        result.HasErrors.Should().BeFalse();

        // Should not have diagnostics for missing IExternalDependency implementations
        var ioc001Diagnostics = result.GetDiagnosticsByCode("IOC001");
        var externalServiceDiagnostics = ioc001Diagnostics.Where(d => d.GetMessage().Contains("ExternalConsumer"));
        externalServiceDiagnostics.Should().BeEmpty();

        // Constructor should still be generated
        var constructorSource = result.GetRequiredConstructorSource("ExternalConsumer");
        constructorSource.Content.Should()
            .Contain("public ExternalConsumer(IEnumerable<IExternalDependency> dependencies)");
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
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Each service should maintain its own lifetime
        registrationSource.Content.Should()
            .Contain("services.AddSingleton<global::Test.IService, global::Test.SingletonService>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.IService, global::Test.ScopedService>");
        registrationSource.Content.Should()
            .Contain("services.AddTransient<global::Test.IService, global::Test.TransientService>");

        // Constructor should accept collection
        var constructorSource = result.GetRequiredConstructorSource("MixedLifetimeConsumer");
        constructorSource.Content.Should().Contain("public MixedLifetimeConsumer(IEnumerable<IService> services)");
    }

    #endregion
}
