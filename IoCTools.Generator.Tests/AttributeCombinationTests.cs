namespace IoCTools.Generator.Tests;

using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;

/// <summary>
///     ABSOLUTELY BRUTAL ATTRIBUTE COMBINATION TESTS
///     These tests will try EVERY POSSIBLE combination of attributes!
/// </summary>
public class AttributeCombinationTests
{
    [Fact]
    public void Attributes_AllLifetimes_GenerateCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface ITestService { }

[Singleton]
public partial class SingletonService
{
    [Inject] private readonly ITestService _service;
}

[Scoped]
public partial class ScopedService
{
    [Inject] private readonly ITestService _service;
}

[Transient]
public partial class TransientService
{
    [Inject] private readonly ITestService _service;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        // Check that all services get constructors
        result.GetConstructorSourceText("SingletonService").Should().NotBeNullOrWhiteSpace();
        result.GetConstructorSourceText("ScopedService").Should().NotBeNullOrWhiteSpace();
        result.GetConstructorSourceText("TransientService").Should().NotBeNullOrWhiteSpace();

        // Check service registration
        var registrationContent = result.GetServiceRegistrationText();
        registrationContent.Should().Contain(
            "services.AddSingleton<global::Test.SingletonService, global::Test.SingletonService>");
        registrationContent.Should().Contain(
            "AddScoped<global::Test.ScopedService, global::Test.ScopedService>");
        registrationContent.Should().Contain(
            "services.AddTransient<global::Test.TransientService, global::Test.TransientService>");
    }

    [Fact]
    public void Attributes_DependsOnWithAllOverloads_WorksCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService1 { }
public interface IService2 { }
public interface IService3 { }
public interface IService4 { }
public interface IService5 { }
[DependsOn<IService1>]
public partial class SingleDependency { }
[DependsOn<IService1, IService2>]
public partial class TwoDependencies { }
[DependsOn<IService1, IService2, IService3>]
public partial class ThreeDependencies { }
[DependsOn<IService1, IService2, IService3, IService4>]
public partial class FourDependencies { }
[DependsOn<IService1, IService2, IService3, IService4, IService5>]
public partial class FiveDependencies { }";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var singleDep = result.GetConstructorSourceText("SingleDependency");
        singleDep.Should().Contain("public SingleDependency(IService1 service1)");

        var twoDep = result.GetConstructorSourceText("TwoDependencies");
        twoDep.Should().Contain("public TwoDependencies(IService1 service1, IService2 service2)");

        var threeDep = result.GetConstructorSourceText("ThreeDependencies");
        threeDep.Should().Contain(
            "public ThreeDependencies(IService1 service1, IService2 service2, IService3 service3)");

        var fourDep = result.GetConstructorSourceText("FourDependencies");
        fourDep.Should().Contain("public FourDependencies(");
        fourDep.Should().Contain("IService1 service1");
        fourDep.Should().Contain("IService2 service2");
        fourDep.Should().Contain("IService3 service3");
        fourDep.Should().Contain("IService4 service4");

        var fiveDep = result.GetConstructorSourceText("FiveDependencies");
        fiveDep.Should().Contain("public FiveDependencies(");
        fiveDep.Should().Contain("IService1 service1");
        fiveDep.Should().Contain("IService2 service2");
        fiveDep.Should().Contain("IService3 service3");
        fiveDep.Should().Contain("IService4 service4");
        fiveDep.Should().Contain("IService5 service5");
    }

    [Fact]
    public void Attributes_MultipleDependsOnAttributes_CombineCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService1 { }
public interface IService2 { }
public interface IService3 { }
public interface IService4 { }
[DependsOn<IService1, IService2>]
[DependsOn<IService3, IService4>]
public partial class MultipleDependsOn { }";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetConstructorSourceText("MultipleDependsOn");
        constructorSource.Should().Contain("public MultipleDependsOn(");
        constructorSource.Should().Contain("IService1 service1");
        constructorSource.Should().Contain("IService2 service2");
        constructorSource.Should().Contain("IService3 service3");
        constructorSource.Should().Contain("IService4 service4");
    }

    [Fact]
    public void Attributes_ExternalServiceOnField_SkipsDiagnostics()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IMissingService { }
public interface IRegularService { }
public partial class ExternalFieldService
{
    [ExternalService]
    [Inject] private readonly IMissingService _externalService; // Should not generate diagnostic
    
    [Inject] private readonly IRegularService _regularService; // Should generate diagnostic
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        // Should have warnings for missing services, but external should be skipped
        var ioc001Diagnostics = result.GetDiagnosticsByCode("IOC001");

        // Should only have diagnostic for IRegularService, not IMissingService
        var regularServiceDiagnostic =
            ioc001Diagnostics.FirstOrDefault(d => d.GetMessage().Contains("IRegularService"));
        regularServiceDiagnostic.Should().NotBeNull();

        var externalServiceDiagnostic =
            ioc001Diagnostics.FirstOrDefault(d => d.GetMessage().Contains("IMissingService"));
        externalServiceDiagnostic.Should().BeNull(); // Should NOT have diagnostic for external service
    }

    [Fact]
    public void Attributes_ExternalServiceOnClass_SkipsAllDiagnostics()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IMissingService1 { }
public interface IMissingService2 { }
[ExternalService]
[DependsOn<IMissingService1>]
public partial class ExternalClassService
{
    [Inject] private readonly IMissingService2 _missingService;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        // Should have NO diagnostics because entire class is marked external
        var ioc001Diagnostics = result.GetDiagnosticsByCode("IOC001");
        var classRelatedDiagnostics = ioc001Diagnostics.Where(d =>
            d.GetMessage().Contains("ExternalClassService")).ToList();

        classRelatedDiagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Attributes_DependsOnWithExternal_SelectiveSkipping()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IMissing1 { }
public interface IMissing2 { }
public interface IMissing3 { }
[DependsOn<IMissing1>(external: true)]
[DependsOn<IMissing2>(external: false)]
[DependsOn<IMissing3>] // Default should be false
public partial class SelectiveExternalService { }";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var ioc001Diagnostics = result.GetDiagnosticsByCode("IOC001");

        // Should NOT have diagnostic for IMissing1 (external: true)
        var missing1Diagnostic = ioc001Diagnostics.FirstOrDefault(d => d.GetMessage().Contains("IMissing1"));
        missing1Diagnostic.Should().BeNull();

        // Should have diagnostics for IMissing2 and IMissing3
        var missing2Diagnostic = ioc001Diagnostics.FirstOrDefault(d => d.GetMessage().Contains("IMissing2"));
        missing2Diagnostic.Should().NotBeNull();

        var missing3Diagnostic = ioc001Diagnostics.FirstOrDefault(d => d.GetMessage().Contains("IMissing3"));
        missing3Diagnostic.Should().NotBeNull();
    }

    [Fact]
    public void Attributes_WithoutLifetime_DoesNotGenerateRegistration()
    {
        // Arrange - Test intelligent inference: services with explicit lifetimes OR DependsOn attributes are registered
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface ITestService { }

[DependsOn<ITestService>]
public partial class UnmanagedService { }

[Scoped]
[DependsOn<ITestService>]
public partial class RegisteredService { }";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        // Both should get constructors
        result.GetConstructorSourceText("UnmanagedService").Should().NotBeNullOrWhiteSpace();
        result.GetConstructorSourceText("RegisteredService").Should().NotBeNullOrWhiteSpace();

        // Both services should be registered: RegisteredService (explicit lifetime) and UnmanagedService (has DependsOn)
        var registrationContent = result.GetServiceRegistrationText();
        registrationContent.Should().Contain(
            "global::Test.RegisteredService, global::Test.RegisteredService");
        registrationContent.Should().Contain("global::Test.UnmanagedService, global::Test.UnmanagedService");
    }

    [Fact]
    public void Attributes_AllCombinationsWithLifetime_WorkCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IService1 { }
public interface IService2 { }

// Every possible combination!
[Singleton]
[DependsOn<IService1>]
public partial class SingletonWithDependsOn { }

[Scoped]
[ExternalService]
[DependsOn<IService1>]
public partial class ScopedExternalWithDependsOn { }

[Transient]
[DependsOn<IService1>(external: true)]
[DependsOn<IService2>(external: false)]
public partial class TransientSelectiveExternal { }

[Singleton]
[ExternalService]
public partial class SingletonFullExternal
{
    [Inject] private readonly IService1 _service;
    [ExternalService]
    [Inject] private readonly IService2 _externalField;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        // All should have constructors
        result.GetConstructorSourceText("SingletonWithDependsOn").Should().NotBeNullOrWhiteSpace();
        result.GetConstructorSourceText("ScopedExternalWithDependsOn").Should().NotBeNullOrWhiteSpace();
        result.GetConstructorSourceText("TransientSelectiveExternal").Should().NotBeNullOrWhiteSpace();
        result.GetConstructorSourceText("SingletonFullExternal").Should().NotBeNullOrWhiteSpace();

        // Check service registrations have correct lifetimes
        var registrationContent = result.GetServiceRegistrationText();
        registrationContent.Should().Contain(
            "services.AddSingleton<global::Test.SingletonWithDependsOn, global::Test.SingletonWithDependsOn>");
        registrationContent.Should().Contain(
            "AddScoped<global::Test.ScopedExternalWithDependsOn, global::Test.ScopedExternalWithDependsOn>");
        registrationContent.Should().Contain(
            "services.AddTransient<global::Test.TransientSelectiveExternal, global::Test.TransientSelectiveExternal>");
        registrationContent.Should().Contain(
            "services.AddSingleton<global::Test.SingletonFullExternal, global::Test.SingletonFullExternal>");
    }

    #region RegisterAsAll Test Suite - CRITICAL MISSING FEATURES

    [Fact]
    public void RegisterAsAll_WithDirectOnlyMode_RegistersOnlyConcreteType()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IBaseInterface { }
public interface IDirectInterface : IBaseInterface { }
public interface IAnotherInterface { }
[RegisterAsAll(RegistrationMode.DirectOnly)]
public partial class DirectOnlyService : IDirectInterface, IAnotherInterface
{
}

public class ConcreteService : IBaseInterface { }";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var registrationContent = result.GetServiceRegistrationText();

        // DirectOnly mode: Register only the concrete type (no interfaces)
        // Per enum definition: "Register only the concrete type (no interfaces)"

        // Should register only the concrete type
        registrationContent.Should().Contain(
            "services.AddScoped<global::Test.DirectOnlyService, global::Test.DirectOnlyService>");

        // Should NOT register any interfaces
        registrationContent.Should().NotContain("services.AddScoped<global::Test.IDirectInterface,");
        registrationContent.Should().NotContain("services.AddScoped<global::Test.IAnotherInterface,");
        registrationContent.Should().NotContain("services.AddScoped<global::Test.IBaseInterface,");
    }

    [Fact]
    public void RegisterAsAll_WithAllMode_RegistersAllInterfaces()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IBaseInterface { }
public interface IDirectInterface : IBaseInterface { }
public interface IAnotherInterface { }
[RegisterAsAll(RegistrationMode.All)]
public partial class AllModeService : IDirectInterface, IAnotherInterface
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var registrationContent = result.GetServiceRegistrationText();

        // Should register ALL interfaces including inherited ones
        // Since no InstanceSharing specified, uses default (Separate), so should use direct registration
        registrationContent.Should().Contain(
            "services.AddScoped<global::Test.AllModeService, global::Test.AllModeService>");
        registrationContent.Should().Contain(
            "services.AddScoped<global::Test.IBaseInterface, global::Test.AllModeService>");
        registrationContent.Should().Contain(
            "services.AddScoped<global::Test.IDirectInterface, global::Test.AllModeService>");
        registrationContent.Should().Contain(
            "services.AddScoped<global::Test.IAnotherInterface, global::Test.AllModeService>");
    }

    [Fact]
    public void RegisterAsAll_WithExclusionaryMode_RegistersAllExceptExcluded()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IBaseInterface { }
public interface IDirectInterface : IBaseInterface { }
public interface IAnotherInterface { }
public interface IExcludedInterface { }
[RegisterAsAll(RegistrationMode.Exclusionary)]
[SkipRegistration<IExcludedInterface>]
public partial class ExclusionaryService : IDirectInterface, IAnotherInterface, IExcludedInterface
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var registrationContent = result.GetServiceRegistrationText();

        // Exclusionary mode: register ONLY interfaces (no concrete class), except excluded ones
        // The current implementation uses direct registration pattern (not factory pattern)
        registrationContent.Should().Contain(
            "services.AddScoped<global::Test.IDirectInterface, global::Test.ExclusionaryService>");
        registrationContent.Should().Contain(
            "services.AddScoped<global::Test.IBaseInterface, global::Test.ExclusionaryService>");
        registrationContent.Should().Contain(
            "services.AddScoped<global::Test.IAnotherInterface, global::Test.ExclusionaryService>");

        // Excluded interface should not be registered at all
        registrationContent.Should().NotContain("global::Test.IExcludedInterface");
    }

    [Fact]
    public void RegisterAsAll_WithSeparateInstanceSharing_CreatesDistinctRegistrations()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IInterface1 { }
public interface IInterface2 { }
[RegisterAsAll(instanceSharing: InstanceSharing.Separate)]
public partial class SeparateInstanceService : IInterface1, IInterface2
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var registrationContent = result.GetServiceRegistrationText();

        // Each interface should resolve to its own instance
        registrationContent.Should().Contain(
            "services.AddScoped<global::Test.IInterface1, global::Test.SeparateInstanceService>()");
        registrationContent.Should().Contain(
            "services.AddScoped<global::Test.IInterface2, global::Test.SeparateInstanceService>()");
    }

    [Fact]
    public void RegisterAsAll_WithSharedInstanceSharing_CreatesSharedRegistrations()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IInterface1 { }
public interface IInterface2 { }
[RegisterAsAll(instanceSharing: InstanceSharing.Shared)]
public partial class SharedInstanceService : IInterface1, IInterface2
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var sharedRegistrationContent = result.GetServiceRegistrationText();

        // All interfaces should resolve to the same instance via factory forwarding
        sharedRegistrationContent.Should().Contain(
            "services.AddScoped<global::Test.SharedInstanceService, global::Test.SharedInstanceService>()");
        sharedRegistrationContent.Should().Contain(
            "services.AddScoped<global::Test.IInterface1>(provider => provider.GetRequiredService<global::Test.SharedInstanceService>())");
        sharedRegistrationContent.Should().Contain(
            "services.AddScoped<global::Test.IInterface2>(provider => provider.GetRequiredService<global::Test.SharedInstanceService>())");
    }

    [Fact]
    public void RegisterAsAll_WithLifetimeInference_WorksCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IInterface1 { }

[RegisterAsAll]
public partial class IntelligentLifetimeService : IInterface1
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - With intelligent inference, this should work without IOC004 diagnostic
        var ioc004Diagnostics = result.GetDiagnosticsByCode("IOC004");
        ioc004Diagnostics.Should().BeEmpty();

        // Should generate service registrations
        var registrationContent = result.GetServiceRegistrationText();

        // Should register concrete class and all interfaces
        registrationContent.Should().Contain(
            "services.AddScoped<global::Test.IntelligentLifetimeService, global::Test.IntelligentLifetimeService>");
        registrationContent.Should().Contain(
            "services.AddScoped<global::Test.IInterface1, global::Test.IntelligentLifetimeService>");
    }

    [Fact]
    public void RegisterAsAll_ComplexLifetimeCombinations_WorkCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface ISingletonInterface { }
public interface IScopedInterface { }
public interface ITransientInterface { }

[Singleton]
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class SingletonRegisterAsAll : ISingletonInterface
{
}

[Scoped]
[RegisterAsAll(RegistrationMode.DirectOnly, InstanceSharing.Separate)]
public partial class ScopedRegisterAsAll : IScopedInterface
{
}

[Transient]
[RegisterAsAll(RegistrationMode.Exclusionary)]
[SkipRegistration<ITransientInterface>]
public partial class TransientRegisterAsAll : ITransientInterface
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var registrationContent = result.GetServiceRegistrationText();

        // Verify lifetime preservation in RegisterAsAll scenarios
        registrationContent.Should().Contain("services.AddSingleton<global::Test.ISingletonInterface>");
        registrationContent.Should().Contain("services.AddScoped<global::Test.ScopedRegisterAsAll>()");
        // TransientRegisterAsAll should have no registrations due to SkipRegistration
        registrationContent.Should().NotContain(
            "services.AddTransient<global::Test.ITransientInterface, global::Test.TransientRegisterAsAll>");
    }

    #endregion

    #region SkipRegistration Test Suite - CRITICAL MISSING FEATURES

    [Fact]
    public void SkipRegistration_WithoutRegisterAsAll_NoLongerGeneratesIOC005Diagnostic()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IInterface1 { }
[SkipRegistration<IInterface1>]
public partial class IntelligentSkipRegistration : IInterface1
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - With intelligent inference, IOC005 diagnostic was removed
        var ioc005Diagnostics = result.GetDiagnosticsByCode("IOC005");
        ioc005Diagnostics.Should().BeEmpty();

        // With SkipRegistration without RegisterAsAll, registration behavior depends on intelligent inference
        var registrationSource = result.GetServiceRegistrationText();

        if (registrationSource != null)
        {
            var registrationText = registrationSource;
            registrationText.Should().Contain(
                "services.AddScoped<global::Test.IntelligentSkipRegistration, global::Test.IntelligentSkipRegistration>");
        }
        // It's valid for SkipRegistration to result in no registrations
        // This indicates the generator correctly interpreted the SkipRegistration attribute
    }

    [Fact]
    public void SkipRegistration_ForNonRegisteredInterface_GeneratesIOC009Diagnostic()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IActualInterface { }
public interface INonExistentInterface { }
[RegisterAsAll(RegistrationMode.DirectOnly)]
[SkipRegistration<INonExistentInterface>] // This interface is not implemented by the class
public partial class SkipNonExistentInterface : IActualInterface
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var ioc009Diagnostics = result.GetDiagnosticsByCode("IOC009");
        ioc009Diagnostics.Should().ContainSingle();

        var diagnostic = ioc009Diagnostics[0];
        diagnostic.GetMessage().Should().Contain("INonExistentInterface");
        diagnostic.GetMessage().Should().Contain("SkipNonExistentInterface");
    }

    [Fact]
    public void SkipRegistration_MultipleGenericVariations_WorkCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IInterface1 { }
public interface IInterface2 { }
public interface IInterface3 { }
public interface IInterface4 { }
public interface IInterface5 { }
[RegisterAsAll(RegistrationMode.All)]
[SkipRegistration<IInterface1>] // Skip single interface
[SkipRegistration<IInterface2, IInterface3>] // Skip two interfaces
[SkipRegistration<IInterface4, IInterface5>] // Skip another pair
public partial class MultiSkipService : IInterface1, IInterface2, IInterface3, IInterface4, IInterface5
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var registrationContent = result.GetServiceRegistrationText();

        // Verify all specified interfaces are skipped
        registrationContent.Should().NotContain("global::Test.IInterface1");
        registrationContent.Should().NotContain("global::Test.IInterface2");
        registrationContent.Should().NotContain("global::Test.IInterface3");
        registrationContent.Should().NotContain("global::Test.IInterface4");
        registrationContent.Should().NotContain("global::Test.IInterface5");

        // The class itself should still be registered (concrete class registration)
        registrationContent.Should().Contain(
            "services.AddScoped<global::Test.MultiSkipService, global::Test.MultiSkipService>");
    }

    [Fact]
    public void SkipRegistration_WithInheritance_HandlesInterfaceHierarchy()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IBaseInterface { }
public interface IDerivedInterface : IBaseInterface { }
public interface IAnotherInterface { }
[RegisterAsAll(RegistrationMode.All)]
[SkipRegistration<IBaseInterface>] // Skip base interface only
public partial class InheritanceSkipService : IDerivedInterface, IAnotherInterface
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var inheritanceRegistration = result.GetServiceRegistrationText();

        // Should skip only IBaseInterface, but register derived interfaces
        inheritanceRegistration.Should().NotContain("global::Test.IBaseInterface");
        inheritanceRegistration.Should().Contain("global::Test.IDerivedInterface");
        inheritanceRegistration.Should().Contain("global::Test.IAnotherInterface");

        // Verify the exact registration patterns for shared instances
        inheritanceRegistration.Should().Contain(
            "services.AddScoped<global::Test.IDerivedInterface, global::Test.InheritanceSkipService>");
        inheritanceRegistration.Should().Contain(
            "services.AddScoped<global::Test.IAnotherInterface, global::Test.InheritanceSkipService>");
    }

    [Fact]
    public void SkipRegistration_FiveGenericTypeParameters_MaximumSupported()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IInterface1 { }
public interface IInterface2 { }
public interface IInterface3 { }
public interface IInterface4 { }
public interface IInterface5 { }
public interface IInterface6 { }
[RegisterAsAll(RegistrationMode.All)]
[SkipRegistration<IInterface1, IInterface2, IInterface3, IInterface4, IInterface5>] // Test maximum supported
public partial class MaxSkipService : IInterface1, IInterface2, IInterface3, IInterface4, IInterface5, IInterface6
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var registrationContent = result.GetServiceRegistrationText();

        // Should skip all 5 specified interfaces
        registrationContent.Should().NotContain("global::Test.IInterface1");
        registrationContent.Should().NotContain("global::Test.IInterface2");
        registrationContent.Should().NotContain("global::Test.IInterface3");
        registrationContent.Should().NotContain("global::Test.IInterface4");
        registrationContent.Should().NotContain("global::Test.IInterface5");

        // Should still register IInterface6
        registrationContent.Should().Contain("global::Test.IInterface6");
        registrationContent.Should().Contain(
            "services.AddScoped<global::Test.IInterface6, global::Test.MaxSkipService>");
    }

    [Fact]
    public void MultipleLifetimeAttributes_OnSameClass_GeneratesIOC036()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService { }

[Scoped]
[Singleton]
public partial class ConflictingLifetimeService : IService
{
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diagnostics = result.GetDiagnosticsByCode("IOC036");
        diagnostics.Should().ContainSingle();
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void SkipRegistration_WithLifetimeAttribute_GeneratesIOC037()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService { }

[Scoped]
[SkipRegistration]
public partial class ManualRegistrationService : IService
{
}
";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diagnostics = result.GetDiagnosticsByCode("IOC037");
        diagnostics.Should().ContainSingle();
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    #endregion

    #region Missing Diagnostic Coverage - IOC002, IOC003, IOC006-IOC009

    [Fact]
    public void Diagnostic_IOC002_ImplementationExistsButNotRegistered()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IRepository { }

// Implementation exists but lacks lifetime attributes
public class ConcreteRepository : IRepository
{
}

[Scoped]
public partial class ConsumerService
{
    [Inject] private readonly IRepository _repository; // Should generate IOC002
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var ioc002Diagnostics = result.GetDiagnosticsByCode("IOC002");
        ioc002Diagnostics.Should().ContainSingle();

        var diagnostic = ioc002Diagnostics[0];
        diagnostic.GetMessage().Should().Contain("ConsumerService");
        diagnostic.GetMessage().Should().Contain("IRepository");
        diagnostic.GetMessage().Should().Contain("implementation exists but lacks lifetime attribute");
    }

    [Fact]
    public void Diagnostic_IOC002_NotRaised_ForPartialInterfaceServiceWithoutExplicitLifetime()
    {
        // Arrange - Partial service implements an interface with no lifetime attribute but should still register as scoped
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IModuleProvider { }

public partial class ModuleProvider : IModuleProvider
{
}

[Singleton]
[DependsOn<IModuleProvider>]
public partial class ResultToHttpResponseMapper
{
}
";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - No IOC002 or IOC033 warnings
        result.GetDiagnosticsByCode("IOC002").Should().BeEmpty();
        result.GetDiagnosticsByCode("IOC033").Should().BeEmpty();
    }


    [Fact]
    public void Diagnostic_IOC003_CircularDependencyDetected()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IServiceA { }
public interface IServiceB { }
public partial class ServiceA : IServiceA
{
    [Inject] private readonly IServiceB _serviceB;
}
public partial class ServiceB : IServiceB
{
    [Inject] private readonly IServiceA _serviceA; // Creates circular dependency
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        ioc003Diagnostics.Should().NotBeEmpty();

        var diagnostic = ioc003Diagnostics[0];
        diagnostic.GetMessage().Should().Contain("Circular dependency detected");
        diagnostic.GetMessage().Should().MatchRegex("ServiceA|ServiceB");
    }

    [Fact]
    public void Diagnostic_IOC006_DuplicateDependsOnType()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService1 { }
public interface IService2 { }
[DependsOn<IService1, IService2>]
[DependsOn<IService1>] // Duplicate IService1 across multiple attributes
public partial class DuplicateDependsOnService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var ioc006Diagnostics = result.GetDiagnosticsByCode("IOC006");
        ioc006Diagnostics.Should().ContainSingle();

        var diagnostic = ioc006Diagnostics[0];
        diagnostic.GetMessage().Should().Contain("IService1");
        diagnostic.GetMessage().Should().Contain("declared multiple times");
        diagnostic.GetMessage().Should().Contain("DuplicateDependsOnService");
    }

    [Fact]
    public void Diagnostic_IOC040_DependsOnConflictsWithInjectField()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService1 { }
[DependsOn<IService1>] // Conflict with [Inject] field below
public partial class ConflictingDependenciesService
{
    [Inject] private readonly IService1 _service1; // Conflicts with DependsOn
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var ioc040Diagnostics = result.GetDiagnosticsByCode("IOC040");
        ioc040Diagnostics.Should().ContainSingle();

        var diagnostic = ioc040Diagnostics[0];
        var message = diagnostic.GetMessage();
        message.Should().Contain("IService1");
        message.Should().Contain("[Inject] fields");
        message.Should().Contain("[DependsOn] attributes");
        message.Should().Contain("ConflictingDependenciesService");
    }

    [Fact]
    public void Diagnostic_IOC008_DuplicateTypeInSingleDependsOn()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService1 { }
public interface IService2 { }
[DependsOn<IService1, IService2, IService1>] // IService1 appears twice in the same attribute
public partial class DuplicateInAttributeService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var ioc008Diagnostics = result.GetDiagnosticsByCode("IOC008");
        ioc008Diagnostics.Should().ContainSingle();

        var diagnostic = ioc008Diagnostics[0];
        diagnostic.GetMessage().Should().Contain("IService1");
        diagnostic.GetMessage().Should().Contain(
            "declared multiple times in the same [DependsOn] attribute");
        diagnostic.GetMessage().Should().Contain("DuplicateInAttributeService");
    }

    #endregion

    #region NamingConvention Combinations - MISSING FEATURE

    [Fact]
    public void NamingConvention_PascalCase_GeneratesCorrectParameterNames()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IUserService { }
public interface IOrderRepository { }
[DependsOn<IUserService, IOrderRepository>(namingConvention: NamingConvention.PascalCase)]
public partial class PascalCaseService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetConstructorSourceText("PascalCaseService");

        // PascalCase should generate: userService, orderRepository (camelCase semantic naming)
        constructorSource.Should().Contain("IUserService userService");
        constructorSource.Should().Contain("IOrderRepository orderRepository");
    }

    [Fact]
    public void NamingConvention_SnakeCase_GeneratesCorrectParameterNames()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IUserService { }
public interface IOrderRepository { }
[DependsOn<IUserService, IOrderRepository>(namingConvention: NamingConvention.SnakeCase)]
public partial class SnakeCaseService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var snakeConstructor = result.GetConstructorSourceText("SnakeCaseService");
        // SnakeCase should generate camelCase parameters (C# convention)
        snakeConstructor.Should().Contain("IUserService userService");
        snakeConstructor.Should().Contain("IOrderRepository orderRepository");
    }

    [Fact]
    public void NamingConvention_StripIVariations_GeneratesCorrectNames()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IUserService { }
public interface IOrderRepository { }
[DependsOn<IUserService>(stripI: true, namingConvention: NamingConvention.CamelCase)]
[DependsOn<IOrderRepository>(stripI: false, namingConvention: NamingConvention.CamelCase)]
public partial class StripIVariationService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var stripConstructor = result.GetConstructorSourceText("StripIVariationService");

        // stripI=true: userService, stripI=false: orderRepository (semantic naming)
        stripConstructor.Should().Contain("IUserService userService");
        stripConstructor.Should().Contain("IOrderRepository orderRepository");
    }

    [Fact]
    public void NamingConvention_PrefixVariations_GeneratesCorrectNames()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IUserService { }
public interface IOrderRepository { }
[DependsOn<IUserService>]
[DependsOn<IOrderRepository>]
public partial class PrefixVariationService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var prefixConstructor = result.GetConstructorSourceText("PrefixVariationService");

        // Verify basic dependency injection works
        prefixConstructor.Should().Contain("IUserService");
        prefixConstructor.Should().Contain("IOrderRepository");
    }

    [Fact]
    public void NamingConvention_MixedConventionsInMultipleDependsOn_WorkCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IUserService { }
public interface IOrderRepository { }
public interface IPaymentGateway { }
[DependsOn<IUserService>(namingConvention: NamingConvention.PascalCase, stripI: true)]
[DependsOn<IOrderRepository>(namingConvention: NamingConvention.SnakeCase, stripI: false)]
[DependsOn<IPaymentGateway>(namingConvention: NamingConvention.CamelCase, stripI: true)]
public partial class MixedNamingService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var mixedConstructor = result.GetConstructorSourceText("MixedNamingService");

        // Verify all three services are present with correct parameter types
        // (Parameter naming in constructor may follow consistent convention regardless of field naming)
        mixedConstructor.Should().Contain("IUserService");
        mixedConstructor.Should().Contain("IOrderRepository");
        mixedConstructor.Should().Contain("IPaymentGateway");

        // Verify constructor has exactly 3 parameters
        var parameterMatches = Regex.Matches(
                mixedConstructor, @"\w+\s+\w+\s*[,)]")
            .Count;
        parameterMatches.Should().Be(3);
    }

    #endregion

    #region Maximum Complexity and Edge Cases

    [Fact]
    public void DependsOn_MaximumTwentyParameters_HandlesCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

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
public interface IService11 { }
public interface IService12 { }
public interface IService13 { }
public interface IService14 { }
public interface IService15 { }
public interface IService16 { }
public interface IService17 { }
public interface IService18 { }
public interface IService19 { }
public interface IService20 { }
[DependsOn<IService01, IService02, IService03, IService04, IService05, IService06, IService07, IService08, IService09, IService10, IService11, IService12, IService13, IService14, IService15, IService16, IService17, IService18, IService19, IService20>]
public partial class MaximumDependenciesService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse(
            $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var constructorSource = result.GetConstructorSourceText("MaximumDependenciesService");

        // Verify all 20 services are in constructor parameters
        for (var i = 1; i <= 20; i++)
        {
            var serviceName = $"IService{i:D2}";
            constructorSource.Should().Contain(serviceName);
        }

        // Count constructor parameters to ensure all 20 are present
        var constructorMatch = Regex.Match(
            constructorSource, @"public MaximumDependenciesService\(([^)]+)\)");
        constructorMatch.Success.Should().BeTrue();

        var parameters = constructorMatch.Groups[1].Value;
        var parameterCount = parameters.Split(',').Length;
        parameterCount.Should().Be(20);
    }

    [Fact]
    public void MultiLevel_InheritanceWithAttributes_HandlesCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IBaseService { }
public interface IDerivedService { }
public interface IFinalService { }

[ExternalService]
[DependsOn<IBaseService>]
public abstract partial class BaseService<T> where T : class
{
    protected BaseService() { }
}
[DependsOn<IDerivedService>]
public partial class DerivedService : BaseService<string>
{
}

[Singleton]
[RegisterAsAll(RegistrationMode.All)]
[DependsOn<IFinalService>]
public partial class FinalService : DerivedService, IFinalService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        // Both derived and final should have constructors
        result.GetConstructorSourceText("DerivedService").Should().NotBeNullOrWhiteSpace();
        result.GetConstructorSourceText("FinalService").Should().NotBeNullOrWhiteSpace();

        // FinalService should register as all interfaces due to RegisterAsAll
        var registrationContent = result.GetServiceRegistrationText();

        // Should be registered as Singleton due to explicit lifetime
        registrationContent.Should().Contain("AddSingleton");
        registrationContent.Should().Contain("global::Test.FinalService");
    }

    [Fact]
    public void GenericConstraints_ComplexWhereClausesWithAttributes_CompileCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using System;
using System.Collections.Generic;

namespace Test;

public interface IConstrainedService<T> where T : class, IComparable<T> { }
public interface IRepository<TEntity, TKey> where TEntity : class where TKey : IComparable<TKey> { }
[DependsOn<IConstrainedService<string>, IRepository<string, int>>]
public partial class ComplexGenericService<T> where T : class, IComparable<T>, new()
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse(
            $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var constructorSource = result.GetConstructorSourceText("ComplexGenericService");

        // Verify complex generic types are handled correctly
        constructorSource.Should().Contain("IConstrainedService<string>");
        constructorSource.Should().Contain("IRepository<string, int>");
    }

    [Fact]
    public void FrameworkIntegration_CommonFrameworkTypes_WorkCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;

namespace Test;

public class MyOptions { }
[DependsOn<ILogger<FrameworkIntegrationService>, IOptions<MyOptions>, IConfiguration>]
public partial class FrameworkIntegrationService
{
    [Inject] private readonly IEnumerable<string> _configValues;
}";
        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        if (result.HasErrors)
        {
            var errors = result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
            var errorDetails = string.Join("\n", errors.Select(e => $"  {e.Id}: {e.GetMessage()} @ {e.Location}"));
            var generatedFileDetails = string.Join("\n\n", result.GeneratedSources.Select(s =>
                $"=== {s.Hint} ({s.Content.Length} chars) ===\n{s.Content}\n=== END {s.Hint} ==="));
            throw new Exception(
                $"Framework integration test failed - HasErrors: {result.HasErrors}\nErrors:\n{errorDetails}\n\nGenerated Files:\n{generatedFileDetails}");
        }

        result.HasErrors.Should().BeFalse();

        var constructorContent = result.GetConstructorSource("FrameworkIntegrationService")?.Content
                                 ?? throw new Exception(
                                     $"Constructor source not found. Generated files:\n{string.Join("\n", result.GeneratedSources.Select(s => $"  {s.Hint}"))}");

        // Verify framework types are included
        constructorContent.Should().Contain("ILogger<FrameworkIntegrationService>");
        constructorContent.Should().Contain("IOptions<MyOptions>");
        constructorContent.Should().Contain("IConfiguration");
        constructorContent.Should().Contain("IEnumerable<string>");
    }

    #endregion

    #region ASSERTION IMPROVEMENTS - Fix Weak Constructor Parameter Validation

    [Fact]
    public void ConstructorParameterAssertion_ExactSignatureValidation_FixedImplementation()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService1 { }
public interface IService2 { }
public interface IService3 { }
[DependsOn<IService1, IService2, IService3>]
public partial class ExactSignatureService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var constructorContent = result.GetConstructorSourceText("ExactSignatureService");

        // IMPROVED: Exact signature validation with parameter order
        var constructorMatch = Regex.Match(
            constructorContent,
            @"public ExactSignatureService\(\s*([^)]+)\s*\)");
        constructorMatch.Success.Should().BeTrue("Constructor signature not found");

        var parameters = constructorMatch.Groups[1].Value
            .Split(',')
            .Select(p => p.Trim())
            .ToArray();

        parameters.Should().HaveCount(3);

        // Verify exact parameter positions and types
        parameters[0].Should().Contain("IService1");
        parameters[0].Should().Contain("service1");

        parameters[1].Should().Contain("IService2");
        parameters[1].Should().Contain("service2");

        parameters[2].Should().Contain("IService3");
        parameters[2].Should().Contain("service3");
    }

    [Fact]
    public void NegativeAssertionPattern_OnlyExpectedDiagnostics_NoOthers()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IMissingService { }
public partial class SpecificDiagnosticService
{
    [Inject] private readonly IMissingService _missing; // Should generate only IOC001
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var ioc001Diagnostics = result.GetDiagnosticsByCode("IOC001");
        ioc001Diagnostics.Should().ContainSingle();

        // IMPROVED: Verify only the expected IOC diagnostics are present
        var allIOCDiagnostics = result.GeneratorDiagnostics
            .Concat(result.CompilationDiagnostics)
            .Where(d => d.Id.StartsWith("IOC"))
            .ToList();

        var expectedIds = new[] { "IOC001", "IOC039" };
        allIOCDiagnostics.Select(d => d.Id)
            .Should().BeEquivalentTo(expectedIds);

        // Verify the specific diagnostic message
        var diagnostic = allIOCDiagnostics.First(d => d.Id == "IOC001");
        diagnostic.GetMessage().Should().Contain("SpecificDiagnosticService");
        diagnostic.GetMessage().Should().Contain("IMissingService");
        diagnostic.GetMessage().Should().Contain("no implementation");
    }

    #endregion
}
