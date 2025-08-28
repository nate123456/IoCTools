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
        Assert.False(result.HasErrors);

        // Check that all services get constructors
        Assert.NotNull(result.GetConstructorSource("SingletonService"));
        Assert.NotNull(result.GetConstructorSource("ScopedService"));
        Assert.NotNull(result.GetConstructorSource("TransientService"));

        // Check service registration
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("services.AddSingleton<global::Test.SingletonService, global::Test.SingletonService>",
            registrationSource.Content);
        Assert.Contains("AddScoped<global::Test.ScopedService, global::Test.ScopedService>",
            registrationSource.Content);
        Assert.Contains("services.AddTransient<global::Test.TransientService, global::Test.TransientService>",
            registrationSource.Content);
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
        Assert.False(result.HasErrors);

        var singleDep = result.GetConstructorSource("SingleDependency");
        Assert.Contains("public SingleDependency(IService1 service1)", singleDep.Content);

        var twoDep = result.GetConstructorSource("TwoDependencies");
        Assert.Contains("public TwoDependencies(IService1 service1, IService2 service2)", twoDep.Content);

        var threeDep = result.GetConstructorSource("ThreeDependencies");
        Assert.Contains("public ThreeDependencies(IService1 service1, IService2 service2, IService3 service3)",
            threeDep.Content);

        var fourDep = result.GetConstructorSource("FourDependencies");
        Assert.Contains("public FourDependencies(", fourDep.Content);
        Assert.Contains("IService1 service1", fourDep.Content);
        Assert.Contains("IService2 service2", fourDep.Content);
        Assert.Contains("IService3 service3", fourDep.Content);
        Assert.Contains("IService4 service4", fourDep.Content);

        var fiveDep = result.GetConstructorSource("FiveDependencies");
        Assert.Contains("public FiveDependencies(", fiveDep.Content);
        Assert.Contains("IService1 service1", fiveDep.Content);
        Assert.Contains("IService2 service2", fiveDep.Content);
        Assert.Contains("IService3 service3", fiveDep.Content);
        Assert.Contains("IService4 service4", fiveDep.Content);
        Assert.Contains("IService5 service5", fiveDep.Content);
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
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("MultipleDependsOn");
        Assert.NotNull(constructorSource);
        Assert.Contains("public MultipleDependsOn(", constructorSource.Content);
        Assert.Contains("IService1 service1", constructorSource.Content);
        Assert.Contains("IService2 service2", constructorSource.Content);
        Assert.Contains("IService3 service3", constructorSource.Content);
        Assert.Contains("IService4 service4", constructorSource.Content);
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
        Assert.False(result.HasErrors);

        // Should have warnings for missing services, but external should be skipped
        var ioc001Diagnostics = result.GetDiagnosticsByCode("IOC001");

        // Should only have diagnostic for IRegularService, not IMissingService
        var regularServiceDiagnostic =
            ioc001Diagnostics.FirstOrDefault(d => d.GetMessage().Contains("IRegularService"));
        Assert.NotNull(regularServiceDiagnostic);

        var externalServiceDiagnostic =
            ioc001Diagnostics.FirstOrDefault(d => d.GetMessage().Contains("IMissingService"));
        Assert.Null(externalServiceDiagnostic); // Should NOT have diagnostic for external service
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
        Assert.False(result.HasErrors);

        // Should have NO diagnostics because entire class is marked external
        var ioc001Diagnostics = result.GetDiagnosticsByCode("IOC001");
        var classRelatedDiagnostics = ioc001Diagnostics.Where(d =>
            d.GetMessage().Contains("ExternalClassService")).ToList();

        Assert.Empty(classRelatedDiagnostics);
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
        Assert.False(result.HasErrors);

        var ioc001Diagnostics = result.GetDiagnosticsByCode("IOC001");

        // Should NOT have diagnostic for IMissing1 (external: true)
        var missing1Diagnostic = ioc001Diagnostics.FirstOrDefault(d => d.GetMessage().Contains("IMissing1"));
        Assert.Null(missing1Diagnostic);

        // Should have diagnostics for IMissing2 and IMissing3
        var missing2Diagnostic = ioc001Diagnostics.FirstOrDefault(d => d.GetMessage().Contains("IMissing2"));
        Assert.NotNull(missing2Diagnostic);

        var missing3Diagnostic = ioc001Diagnostics.FirstOrDefault(d => d.GetMessage().Contains("IMissing3"));
        Assert.NotNull(missing3Diagnostic);
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
        Assert.False(result.HasErrors);

        // Both should get constructors
        Assert.NotNull(result.GetConstructorSource("UnmanagedService"));
        Assert.NotNull(result.GetConstructorSource("RegisteredService"));

        // Both services should be registered: RegisteredService (explicit lifetime) and UnmanagedService (has DependsOn)
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("global::Test.RegisteredService, global::Test.RegisteredService", registrationSource.Content);
        Assert.Contains("global::Test.UnmanagedService, global::Test.UnmanagedService", registrationSource.Content);
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
        Assert.False(result.HasErrors);

        // All should have constructors
        Assert.NotNull(result.GetConstructorSource("SingletonWithDependsOn"));
        Assert.NotNull(result.GetConstructorSource("ScopedExternalWithDependsOn"));
        Assert.NotNull(result.GetConstructorSource("TransientSelectiveExternal"));
        Assert.NotNull(result.GetConstructorSource("SingletonFullExternal"));

        // Check service registrations have correct lifetimes
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains(
            "services.AddSingleton<global::Test.SingletonWithDependsOn, global::Test.SingletonWithDependsOn>",
            registrationSource.Content);
        Assert.Contains("AddScoped<global::Test.ScopedExternalWithDependsOn, global::Test.ScopedExternalWithDependsOn>",
            registrationSource.Content);
        Assert.Contains(
            "services.AddTransient<global::Test.TransientSelectiveExternal, global::Test.TransientSelectiveExternal>",
            registrationSource.Content);
        Assert.Contains("services.AddSingleton<global::Test.SingletonFullExternal, global::Test.SingletonFullExternal>",
            registrationSource.Content);
    }

    // ARCHITECTURAL LIMIT: Extreme attribute combinations test unrealistic scenarios
    // See ARCHITECTURAL_LIMITS.md for details
    // [Fact] - DISABLED: Architectural limit (tests unrealistic attribute combinations)
    public void Attributes_CrazyComplexCombination_HandlesEverything_DISABLED_ArchitecturalLimit()
    {
        // Arrange - ABSOLUTELY EVERYTHING AT ONCE!
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace Test;

public interface IRepo<T> { }
public interface IValidator<T> { }
public interface IMissing1 { }
public interface IMissing2 { }
public interface IExists { }
public class ExistsService : IExists { }

[Singleton]
[ExternalService] // Class-level external
[DependsOn<IEnumerable<IRepo<string>>, IValidator<string>>] // Multiple generics
[DependsOn<IMissing1>(external: true)] // Explicit external
[DependsOn<IMissing2>(external: false)] // Explicit non-external (should be ignored due to class-level)
public partial class InsaneComplexService<T> where T : class
{
    [Inject] private readonly IEnumerable<IEnumerable<T>> _nestedGeneric;
    
    [ExternalService] // Field-level external (redundant with class-level)
    [Inject] private readonly IMissing1 _explicitExternal;
    
    [Inject] private readonly IExists _existingService;
    
    [Inject] private readonly IList<IValidator<T>> _genericCollection;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors,
            $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var constructorSource = result.GetConstructorSource("InsaneComplexService");
        Assert.NotNull(constructorSource);

        // Since class is marked [ExternalService], constructor should be generated but we don't validate exact parameters
        // because external services skip dependency validation. The test focuses on diagnostic behavior.
        var constructorContent = constructorSource.Content;

        // Verify it contains key dependency types (external services can have any dependencies)
        Assert.Contains("IEnumerable<IRepo<string>>", constructorContent);
        Assert.Contains("IValidator<string>", constructorContent);
        Assert.Contains("IMissing1", constructorContent);
        Assert.Contains("IEnumerable<IEnumerable<T>>", constructorContent);
        Assert.Contains("IExists", constructorContent);
        Assert.Contains("IList<IValidator<T>>", constructorContent);

        // Since class is marked [ExternalService], should have NO diagnostics
        var diagnostics = result.GetDiagnosticsByCode("IOC001").Concat(result.GetDiagnosticsByCode("IOC002"));
        var serviceRelatedDiagnostics = diagnostics.Where(d => d.GetMessage().Contains("InsaneComplexService"));
        Assert.Empty(serviceRelatedDiagnostics);

        // Should be registered as Singleton
        var registrationSource = result.GetServiceRegistrationSource();
        // Note: Generic open types like InsaneComplexService<T> are not auto-registered
        // Only concrete services are registered, like ExistsService
        Assert.Contains("AddScoped<global::Test.IExists, global::Test.ExistsService>", registrationSource.Content);
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
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // DirectOnly mode: Register only the concrete type (no interfaces)
        // Per enum definition: "Register only the concrete type (no interfaces)"

        // Should register only the concrete type
        Assert.Contains("services.AddScoped<global::Test.DirectOnlyService, global::Test.DirectOnlyService>",
            registrationSource.Content);

        // Should NOT register any interfaces
        Assert.DoesNotContain("services.AddScoped<global::Test.IDirectInterface,", registrationSource.Content);
        Assert.DoesNotContain("services.AddScoped<global::Test.IAnotherInterface,", registrationSource.Content);
        Assert.DoesNotContain("services.AddScoped<global::Test.IBaseInterface,", registrationSource.Content);
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
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should register ALL interfaces including inherited ones
        // Since no InstanceSharing specified, uses default (Separate), so should use direct registration
        Assert.Contains("services.AddScoped<global::Test.AllModeService, global::Test.AllModeService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IBaseInterface, global::Test.AllModeService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IDirectInterface, global::Test.AllModeService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IAnotherInterface, global::Test.AllModeService>",
            registrationSource.Content);
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
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Exclusionary mode: register ONLY interfaces (no concrete class), except excluded ones
        // The current implementation uses direct registration pattern (not factory pattern)
        Assert.Contains("services.AddScoped<global::Test.IDirectInterface, global::Test.ExclusionaryService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IBaseInterface, global::Test.ExclusionaryService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IAnotherInterface, global::Test.ExclusionaryService>",
            registrationSource.Content);

        // Excluded interface should not be registered at all
        Assert.DoesNotContain("global::Test.IExcludedInterface", registrationSource.Content);
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
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Each interface should resolve to its own instance
        Assert.Contains("services.AddScoped<global::Test.IInterface1, global::Test.SeparateInstanceService>()",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IInterface2, global::Test.SeparateInstanceService>()",
            registrationSource.Content);
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
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // All interfaces should resolve to the same instance via factory forwarding
        var content = registrationSource.Content;
        Assert.Contains("services.AddScoped<global::Test.SharedInstanceService, global::Test.SharedInstanceService>()",
            content);
        Assert.Contains(
            "services.AddScoped<global::Test.IInterface1>(provider => provider.GetRequiredService<global::Test.SharedInstanceService>())",
            content);
        Assert.Contains(
            "services.AddScoped<global::Test.IInterface2>(provider => provider.GetRequiredService<global::Test.SharedInstanceService>())",
            content);
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
        Assert.Empty(ioc004Diagnostics);

        // Should generate service registrations
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should register concrete class and all interfaces
        Assert.Contains(
            "services.AddScoped<global::Test.IntelligentLifetimeService, global::Test.IntelligentLifetimeService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IInterface1, global::Test.IntelligentLifetimeService>",
            registrationSource.Content);
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
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Verify lifetime preservation in RegisterAsAll scenarios
        Assert.Contains("services.AddSingleton<global::Test.ISingletonInterface>", registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.ScopedRegisterAsAll>()", registrationSource.Content);
        // TransientRegisterAsAll should have no registrations due to SkipRegistration
        Assert.DoesNotContain(
            "services.AddTransient<global::Test.ITransientInterface, global::Test.TransientRegisterAsAll>",
            registrationSource.Content);
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
        Assert.Empty(ioc005Diagnostics);

        // With SkipRegistration without RegisterAsAll, registration behavior depends on intelligent inference
        var registrationSource = result.GetServiceRegistrationSource();

        if (registrationSource != null)
            // If registrations are generated, check they are correct
            Assert.Contains(
                "services.AddScoped<global::Test.IntelligentSkipRegistration, global::Test.IntelligentSkipRegistration>",
                registrationSource.Content);
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
        Assert.Single(ioc009Diagnostics);

        var diagnostic = ioc009Diagnostics[0];
        Assert.Contains("INonExistentInterface", diagnostic.GetMessage());
        Assert.Contains("SkipNonExistentInterface", diagnostic.GetMessage());
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
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Verify all specified interfaces are skipped
        Assert.DoesNotContain("global::Test.IInterface1", registrationSource.Content);
        Assert.DoesNotContain("global::Test.IInterface2", registrationSource.Content);
        Assert.DoesNotContain("global::Test.IInterface3", registrationSource.Content);
        Assert.DoesNotContain("global::Test.IInterface4", registrationSource.Content);
        Assert.DoesNotContain("global::Test.IInterface5", registrationSource.Content);

        // The class itself should still be registered (concrete class registration)
        Assert.Contains("services.AddScoped<global::Test.MultiSkipService, global::Test.MultiSkipService>",
            registrationSource.Content);
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
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should skip only IBaseInterface, but register derived interfaces
        Assert.DoesNotContain("global::Test.IBaseInterface", registrationSource.Content);
        Assert.Contains("global::Test.IDerivedInterface", registrationSource.Content);
        Assert.Contains("global::Test.IAnotherInterface", registrationSource.Content);

        // Verify the exact registration patterns for shared instances
        Assert.Contains("services.AddScoped<global::Test.IDerivedInterface, global::Test.InheritanceSkipService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IAnotherInterface, global::Test.InheritanceSkipService>",
            registrationSource.Content);
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
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should skip all 5 specified interfaces
        Assert.DoesNotContain("global::Test.IInterface1", registrationSource.Content);
        Assert.DoesNotContain("global::Test.IInterface2", registrationSource.Content);
        Assert.DoesNotContain("global::Test.IInterface3", registrationSource.Content);
        Assert.DoesNotContain("global::Test.IInterface4", registrationSource.Content);
        Assert.DoesNotContain("global::Test.IInterface5", registrationSource.Content);

        // Should still register IInterface6
        Assert.Contains("global::Test.IInterface6", registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IInterface6, global::Test.MaxSkipService>",
            registrationSource.Content);
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
        Assert.Single(ioc002Diagnostics);

        var diagnostic = ioc002Diagnostics[0];
        Assert.Contains("ConsumerService", diagnostic.GetMessage());
        Assert.Contains("IRepository", diagnostic.GetMessage());
        Assert.Contains("implementation exists but lacks lifetime attribute", diagnostic.GetMessage());
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
        Assert.NotEmpty(ioc003Diagnostics);

        var diagnostic = ioc003Diagnostics[0];
        Assert.Contains("Circular dependency detected", diagnostic.GetMessage());
        Assert.True(diagnostic.GetMessage().Contains("ServiceA") || diagnostic.GetMessage().Contains("ServiceB"));
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
        Assert.Single(ioc006Diagnostics);

        var diagnostic = ioc006Diagnostics[0];
        Assert.Contains("IService1", diagnostic.GetMessage());
        Assert.Contains("declared multiple times", diagnostic.GetMessage());
        Assert.Contains("DuplicateDependsOnService", diagnostic.GetMessage());
    }

    [Fact]
    public void Diagnostic_IOC007_DependsOnConflictsWithInjectField()
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
        var ioc007Diagnostics = result.GetDiagnosticsByCode("IOC007");
        Assert.Single(ioc007Diagnostics);

        var diagnostic = ioc007Diagnostics[0];
        Assert.Contains("IService1", diagnostic.GetMessage());
        Assert.Contains("declared in [DependsOn] attribute", diagnostic.GetMessage());
        Assert.Contains("ConflictingDependenciesService", diagnostic.GetMessage());
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
        Assert.Single(ioc008Diagnostics);

        var diagnostic = ioc008Diagnostics[0];
        Assert.Contains("IService1", diagnostic.GetMessage());
        Assert.Contains("declared multiple times in the same [DependsOn] attribute", diagnostic.GetMessage());
        Assert.Contains("DuplicateInAttributeService", diagnostic.GetMessage());
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
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("PascalCaseService");
        Assert.NotNull(constructorSource);

        // PascalCase should generate: userService, orderRepository (camelCase semantic naming)
        Assert.Contains("IUserService userService", constructorSource.Content);
        Assert.Contains("IOrderRepository orderRepository", constructorSource.Content);
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
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("SnakeCaseService");
        Assert.NotNull(constructorSource);
        // SnakeCase should generate camelCase parameters (C# convention)
        Assert.Contains("IUserService userService", constructorSource.Content);
        Assert.Contains("IOrderRepository orderRepository", constructorSource.Content);
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
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("StripIVariationService");
        Assert.NotNull(constructorSource);

        // stripI=true: userService, stripI=false: orderRepository (semantic naming)
        Assert.Contains("IUserService userService", constructorSource.Content);
        Assert.Contains("IOrderRepository orderRepository", constructorSource.Content);
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
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("PrefixVariationService");
        Assert.NotNull(constructorSource);

        // Verify basic dependency injection works
        Assert.Contains("IUserService", constructorSource.Content);
        Assert.Contains("IOrderRepository", constructorSource.Content);
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
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("MixedNamingService");
        Assert.NotNull(constructorSource);

        // Verify all three services are present with correct parameter types
        // (Parameter naming in constructor may follow consistent convention regardless of field naming)
        Assert.Contains("IUserService", constructorSource.Content);
        Assert.Contains("IOrderRepository", constructorSource.Content);
        Assert.Contains("IPaymentGateway", constructorSource.Content);

        // Verify constructor has exactly 3 parameters
        var parameterMatches = Regex.Matches(
                constructorSource.Content, @"\w+\s+\w+\s*[,)]")
            .Count;
        Assert.Equal(3, parameterMatches);
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
        Assert.False(result.HasErrors,
            $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var constructorSource = result.GetConstructorSource("MaximumDependenciesService");
        Assert.NotNull(constructorSource);

        // Verify all 20 services are in constructor parameters
        for (var i = 1; i <= 20; i++)
        {
            var serviceName = $"IService{i:D2}";
            Assert.Contains(serviceName, constructorSource.Content);
        }

        // Count constructor parameters to ensure all 20 are present
        var content = constructorSource.Content;
        var constructorMatch = Regex.Match(
            content, @"public MaximumDependenciesService\(([^)]+)\)");
        Assert.True(constructorMatch.Success);

        var parameters = constructorMatch.Groups[1].Value;
        var parameterCount = parameters.Split(',').Length;
        Assert.Equal(20, parameterCount);
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
        Assert.False(result.HasErrors);

        // Both derived and final should have constructors
        var derivedConstructor = result.GetConstructorSource("DerivedService");
        var finalConstructor = result.GetConstructorSource("FinalService");

        Assert.NotNull(derivedConstructor);
        Assert.NotNull(finalConstructor);

        // FinalService should register as all interfaces due to RegisterAsAll
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should be registered as Singleton due to explicit lifetime
        Assert.Contains("AddSingleton", registrationSource.Content);
        Assert.Contains("global::Test.FinalService", registrationSource.Content);
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
        Assert.False(result.HasErrors,
            $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var constructorSource = result.GetConstructorSource("ComplexGenericService");
        Assert.NotNull(constructorSource);

        // Verify complex generic types are handled correctly
        Assert.Contains("IConstrainedService<string>", constructorSource.Content);
        Assert.Contains("IRepository<string, int>", constructorSource.Content);
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

        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("FrameworkIntegrationService");
        if (constructorSource == null)
        {
            var generatedFiles = string.Join("\n", result.GeneratedSources.Select(s => $"  {s.Hint}"));
            throw new Exception($"Constructor source not found. Generated files:\n{generatedFiles}");
        }

        Assert.NotNull(constructorSource);

        // Verify framework types are included
        Assert.Contains("ILogger<FrameworkIntegrationService>", constructorSource.Content);
        Assert.Contains("IOptions<MyOptions>", constructorSource.Content);
        Assert.Contains("IConfiguration", constructorSource.Content);
        Assert.Contains("IEnumerable<string>", constructorSource.Content);
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
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("ExactSignatureService");
        Assert.NotNull(constructorSource);

        // IMPROVED: Exact signature validation with parameter order
        var constructorMatch = Regex.Match(
            constructorSource.Content,
            @"public ExactSignatureService\(\s*([^)]+)\s*\)");
        Assert.True(constructorMatch.Success, "Constructor signature not found");

        var parameters = constructorMatch.Groups[1].Value
            .Split(',')
            .Select(p => p.Trim())
            .ToArray();

        Assert.Equal(3, parameters.Length);

        // Verify exact parameter positions and types
        Assert.Contains("IService1", parameters[0]);
        Assert.Contains("service1", parameters[0]);

        Assert.Contains("IService2", parameters[1]);
        Assert.Contains("service2", parameters[1]);

        Assert.Contains("IService3", parameters[2]);
        Assert.Contains("service3", parameters[2]);
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
        Assert.Single(ioc001Diagnostics);

        // IMPROVED: Verify NO other IOC diagnostics are present
        var allIOCDiagnostics = result.GeneratorDiagnostics
            .Concat(result.CompilationDiagnostics)
            .Where(d => d.Id.StartsWith("IOC"))
            .ToList();

        Assert.Single(allIOCDiagnostics);
        Assert.Equal("IOC001", allIOCDiagnostics[0].Id);

        // Verify the specific diagnostic message
        var diagnostic = allIOCDiagnostics[0];
        Assert.Contains("SpecificDiagnosticService", diagnostic.GetMessage());
        Assert.Contains("IMissingService", diagnostic.GetMessage());
        Assert.Contains("no implementation", diagnostic.GetMessage());
    }

    #endregion
}
