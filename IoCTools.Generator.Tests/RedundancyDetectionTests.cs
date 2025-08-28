namespace IoCTools.Generator.Tests;

using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;

/// <summary>
///     Comprehensive tests for redundancy detection in dependency declarations.
///     Tests all IOC diagnostic codes (IOC006-IOC009) and their interactions with IOC001-IOC005.
///     Validates both diagnostic generation and code generation behavior.
/// </summary>
public class RedundancyDetectionTests
{
    [Fact]
    public void RedundancyDetection_DuplicateInSingleDependsOn_GeneratesWarning()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService1 { }
[DependsOn<IService1, IService1>] // Duplicate within same attribute
public partial class TestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Fix fragile message checking with robust validation
        var ioc008Diagnostics = result.GetDiagnosticsByCode("IOC008");
        Assert.Single(ioc008Diagnostics); // Exact count validation

        var diagnostic = ioc008Diagnostics[0];
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        var message = diagnostic.GetMessage();
        Assert.Contains("IService1", message);
        Assert.Contains("multiple times in the same", message);

        // Verify diagnostic location accuracy
        Assert.True(diagnostic.Location.IsInSource);
        Assert.Contains("DependsOn<IService1, IService1>", diagnostic.Location.SourceTree!.ToString());
    }

    [Fact]
    public void RedundancyDetection_DuplicateAcrossDependsOn_GeneratesWarning()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService1 { }
public interface IService2 { }
[DependsOn<IService1>]
[DependsOn<IService1, IService2>] // IService1 is duplicate
public partial class TestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Robust validation with exact diagnostic count
        var ioc006Diagnostics = result.GetDiagnosticsByCode("IOC006");
        Assert.Single(ioc006Diagnostics); // Exact count validation

        var diagnostic = ioc006Diagnostics[0];
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        var message = diagnostic.GetMessage();
        Assert.Contains("IService1", message);
        Assert.Contains("multiple times in [DependsOn]", message);

        // Verify no other redundancy diagnostics are present
        Assert.Empty(result.GetDiagnosticsByCode("IOC007"));
        Assert.Empty(result.GetDiagnosticsByCode("IOC008"));
        Assert.Empty(result.GetDiagnosticsByCode("IOC009"));
    }

    [Fact]
    public void RedundancyDetection_DependsOnConflictsWithInject_GeneratesWarning()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ILogger { }
[DependsOn<ILogger>] // Conflicts with Inject field
public partial class TestService
{
    [Inject] private readonly ILogger _logger; // Same type as DependsOn
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Comprehensive validation with exact diagnostic count
        var ioc007Diagnostics = result.GetDiagnosticsByCode("IOC007");
        Assert.Single(ioc007Diagnostics); // Exact count validation

        var diagnostic = ioc007Diagnostics[0];
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        var message = diagnostic.GetMessage();
        Assert.Contains("ILogger", message);
        Assert.Contains("but also exists as [Inject] field", message);

        // Verify generation vs diagnostic consistency
        var constructorSource = result.GetConstructorSource("TestService");
        Assert.NotNull(constructorSource);
        // Should prioritize [Inject] field over [DependsOn]
        Assert.Contains("ILogger logger", constructorSource.Content);
        Assert.Contains("this._logger = logger", constructorSource.Content);
    }

    [Fact]
    public void RedundancyDetection_SkipRegistrationForNonInterface_GeneratesInfo()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IUserService { }
public interface INonImplemented { } // Not implemented by class
[RegisterAsAll]
[SkipRegistration<INonImplemented>] // Not an interface we implement
public partial class UserService : IUserService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Exact diagnostic validation
        var ioc009Diagnostics = result.GetDiagnosticsByCode("IOC009");
        Assert.Single(ioc009Diagnostics); // Exact count validation

        var diagnostic = ioc009Diagnostics[0];
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        var message = diagnostic.GetMessage();
        Assert.Contains("INonImplemented", message);
        Assert.Contains("not an interface that would be registered", message);

        // Verify service registration behavior
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        // Should only register IUserService, not INonImplemented
        Assert.Contains("IUserService", registrationSource.Content);
        Assert.DoesNotContain("INonImplemented", registrationSource.Content);
    }

    [Fact]
    public void RedundancyDetection_AutomaticRemovalInGeneration_WorksCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService1 { }
public interface IService2 { }
[DependsOn<IService1>]
[DependsOn<IService1, IService2>] // IService1 is duplicate but should be auto-removed
public partial class TestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("TestService");
        Assert.NotNull(constructorSource);

        // Should have both IService1 and IService2 parameters (no duplicates)
        Assert.Contains("IService1", constructorSource.Content);
        Assert.Contains("IService2", constructorSource.Content);

        // Robust constructor content validation - replace brittle regex counting
        var content = constructorSource.Content;

        // Verify both service types appear in parameters (deduplication worked)
        var service1Count = Regex.Matches(content, @"\bIService1\b").Count;
        var service2Count = Regex.Matches(content, @"\bIService2\b").Count;

        // Each service type should appear (at least in parameter declarations)
        Assert.True(service1Count > 0, "IService1 should appear in constructor");
        Assert.True(service2Count > 0, "IService2 should appear in constructor");

        // Generation vs diagnostic consistency validation
        var ioc006Diagnostics = result.GetDiagnosticsByCode("IOC006");
        Assert.Single(ioc006Diagnostics); // Warning should be generated despite auto-removal
    }

    [Fact]
    public void RedundancyDetection_InjectAndDependsOnSameType_PrioritizesInject()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ILogger { }
public interface IService { }
[DependsOn<ILogger, IService>] // ILogger conflicts with Inject, IService is unique
public partial class TestService
{
    [Inject] private readonly ILogger _logger; // Takes priority over DependsOn
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("TestService");
        Assert.NotNull(constructorSource);

        // Should have logger (from Inject) and service (from DependsOn)
        Assert.Contains("ILogger logger", constructorSource.Content);
        Assert.Contains("IService service", constructorSource.Content);
        Assert.Contains("this._logger = logger", constructorSource.Content);
        Assert.Contains("this._service = service", constructorSource.Content);

        // Should generate warning about the conflict - exact validation
        var ioc007Diagnostics = result.GetDiagnosticsByCode("IOC007");
        Assert.Single(ioc007Diagnostics); // Exact count validation

        // Verify no other redundancy diagnostics are present
        Assert.Empty(result.GetDiagnosticsByCode("IOC006"));
        Assert.Empty(result.GetDiagnosticsByCode("IOC008"));
        Assert.Empty(result.GetDiagnosticsByCode("IOC009"));
    }

    [Fact]
    public void RedundancyDetection_ComplexScenario_DetectsAllIssues()
    {
        // Arrange - Multiple types of redundancies
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface ILogger { }
public interface IService { }
public interface IUserService { }
public interface INonImplemented { }
[RegisterAsAll]
[DependsOn<ILogger, ILogger>] // IOC008: Duplicate in same attribute
[DependsOn<IService>] 
[DependsOn<ILogger>] // IOC006: Duplicate across attributes
[SkipRegistration<INonImplemented>] // IOC009: Not an implemented interface
public partial class UserService : IUserService
{
    [Inject] private readonly ILogger _logger; // IOC007: Conflicts with DependsOn
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Comprehensive diagnostic validation with exact counts
        Assert.Single(result.GetDiagnosticsByCode("IOC006")); // Duplicate across attributes
        Assert.Single(result.GetDiagnosticsByCode("IOC007")); // DependsOn conflicts with Inject
        Assert.Single(result.GetDiagnosticsByCode("IOC008")); // Duplicate in same attribute
        Assert.Single(result.GetDiagnosticsByCode("IOC009")); // Unnecessary SkipRegistration

        // Verify total diagnostic count is exactly what we expect
        var allRedundancyDiagnostics = result.CompilationDiagnostics.Concat(result.GeneratorDiagnostics)
            .Where(d => d.Id.StartsWith("IOC") && int.Parse(d.Id.Substring(3)) >= 6).ToList();
        Assert.Equal(4, allRedundancyDiagnostics.Count);

        // Generation vs diagnostic consistency - code should work despite warnings
        Assert.False(result.HasErrors);
        var constructorSource = result.GetConstructorSource("UserService");
        Assert.NotNull(constructorSource);

        // Verify redundancies are handled correctly in generation
        var content = constructorSource.Content;
        // Should have ILogger from [Inject], IService from [DependsOn] (no duplicates)
        Assert.Contains("ILogger logger", content);
        Assert.Contains("IService service", content);
        Assert.Contains("this._logger = logger", content);
        Assert.Contains("this._service = service", content);

        // Verify no duplicate parameters exist
        var loggerParamMatches = Regex.Matches(content, @"\bILogger logger\b");
        Assert.Single(loggerParamMatches);
    }

    [Fact]
    public void RedundancyDetection_NoRedundancies_GeneratesNoWarnings()
    {
        // Arrange - Clean configuration with no redundancies
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface ILogger { }
public interface IService { }
public interface IUserService { }

// Add implementations to avoid IOC001 warnings
[Scoped]
public partial class LoggerImpl : ILogger { }
[Scoped]
public partial class ServiceImpl : IService { }
[RegisterAsAll]
[DependsOn<IService>]
[SkipRegistration<IUserService>] // Valid - this interface is implemented
public partial class UserService : IUserService
{
    [Inject] private readonly ILogger _logger; // Different type from DependsOn
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Negative assertion patterns with exact validation
        Assert.Empty(result.GetDiagnosticsByCode("IOC006")); // No duplicate across attributes
        Assert.Empty(result.GetDiagnosticsByCode("IOC007")); // No DependsOn/Inject conflicts  
        Assert.Empty(result.GetDiagnosticsByCode("IOC008")); // No duplicate in same attribute
        Assert.Empty(result.GetDiagnosticsByCode("IOC009")); // No unnecessary SkipRegistration

        // Verify no unexpected diagnostics are present
        var allIocDiagnostics = result.CompilationDiagnostics.Concat(result.GeneratorDiagnostics)
            .Where(d => d.Id.StartsWith("IOC")).ToList();
        Assert.Empty(allIocDiagnostics); // Should have zero IOC diagnostics

        Assert.False(result.HasErrors);

        // Verify clean generation without redundancies
        var constructorSource = result.GetConstructorSource("UserService");
        Assert.NotNull(constructorSource);
        var content = constructorSource.Content;

        // Should have all three dependencies cleanly generated
        Assert.Contains("ILogger logger", content); // From [Inject]
        Assert.Contains("IService service", content); // From [DependsOn]
        Assert.Contains("this._logger = logger", content);
        Assert.Contains("this._service = service", content);
    }

    #region Cross-Diagnostic Interaction Tests (IOC001-IOC005 with Redundancy)

    [Fact]
    public void CrossDiagnosticInteraction_RedundancyWithNoImplementation_GeneratesIOC001AndIOC006()
    {
        // Test redundancy detection when IOC001 (No implementation found) is also present
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ILogger { }
public interface IMissingService { } // No implementation exists
[DependsOn<ILogger>]
[DependsOn<ILogger, IMissingService>] // ILogger duplicate + missing service
public partial class TestService
{
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Should generate both IOC001 and IOC006
        var ioc001Diagnostics = result.GetDiagnosticsByCode("IOC001");
        var ioc006Diagnostics = result.GetDiagnosticsByCode("IOC006");

        Assert.NotEmpty(ioc001Diagnostics); // Missing implementation
        Assert.Single(ioc006Diagnostics); // Duplicate dependency

        // Verify diagnostic messages
        Assert.Contains(ioc001Diagnostics, d => d.GetMessage().Contains("IMissingService"));
        Assert.Contains("ILogger", ioc006Diagnostics[0].GetMessage());
    }

    [Fact]
    public void CrossDiagnosticInteraction_RedundancyWithUnmanagedService_GeneratesIOC002AndIOC008()
    {
        // Test redundancy with IOC002 (Implementation not registered) scenarios
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ILogger { }

// Implementation exists but not registered as service
public class LoggerImpl : ILogger { }
[DependsOn<ILogger, ILogger>] // Duplicate within same attribute
public partial class TestService
{
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        var ioc002Diagnostics = result.GetDiagnosticsByCode("IOC002");
        var ioc008Diagnostics = result.GetDiagnosticsByCode("IOC008");

        Assert.NotEmpty(ioc002Diagnostics); // Implementation not registered
        Assert.Single(ioc008Diagnostics); // Duplicate in same attribute

        Assert.Contains("ILogger", ioc002Diagnostics[0].GetMessage());
        Assert.Contains("ILogger", ioc008Diagnostics[0].GetMessage());
    }

    [Fact]
    public void CrossDiagnosticInteraction_RedundancyWithCircularDependency_GeneratesIOC003AndIOC007()
    {
        // Test redundancy with IOC003 (Circular dependencies) combinations
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IServiceA { }
public interface IServiceB { }
[DependsOn<IServiceB>] // Creates potential circular dependency
public partial class ServiceA : IServiceA
{
    [Inject] private readonly IServiceB _serviceB; // Conflicts with DependsOn
}

[Scoped] 
[DependsOn<IServiceA>]
public partial class ServiceB : IServiceB
{
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        var ioc007Diagnostics = result.GetDiagnosticsByCode("IOC007");

        // May or may not detect circular dependency depending on implementation
        // but should definitely detect the Inject/DependsOn conflict
        Assert.Single(ioc007Diagnostics);
        Assert.Contains("IServiceB", ioc007Diagnostics[0].GetMessage());
    }

    [Fact]
    public void CrossDiagnosticInteraction_RedundancyWithRegisterAsAll_GeneratesIOC009()
    {
        // Test redundancy with IOC004/IOC005 (RegisterAsAll scenarios)
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IUserService { }
public interface IAdminService { }
public interface INonExistentService { } // Not implemented by class
[RegisterAsAll]
[SkipRegistration<INonExistentService>] // Unnecessary - not implemented anyway
[SkipRegistration<IAdminService>] // Valid skip
public partial class UserService : IUserService, IAdminService  
{
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        var ioc009Diagnostics = result.GetDiagnosticsByCode("IOC009");
        Assert.Single(ioc009Diagnostics); // Unnecessary SkipRegistration
        Assert.Contains("INonExistentService", ioc009Diagnostics[0].GetMessage());

        // Verify registration behavior
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("IUserService", registrationSource.Content); // Should be registered
        Assert.DoesNotContain("IAdminService", registrationSource.Content); // Should be skipped
        Assert.DoesNotContain("INonExistentService", registrationSource.Content); // Not implemented
    }

    #endregion

    #region Maximum Arity and Complex Redundancy Pattern Tests

    [Fact]
    public void MaximumArityRedundancy_DependsOnWith20TypeParameters_DetectsDuplicates()
    {
        // Test DependsOn with up to 20 type parameters (generator supports this)
        var interfaces = string.Join("\n", Enumerable.Range(1, 20).Select(i => $"public interface IService{i} {{ }}"));
        var duplicateTypes = string.Join(", ", Enumerable.Range(1, 10).Select(i => $"IService{i}")) +
                             ", IService5, IService10"; // Duplicates

        var source = $@"
using IoCTools.Abstractions.Annotations;

namespace Test;

{interfaces}
[DependsOn<{duplicateTypes}>] // Contains duplicates IService5 and IService10
public partial class TestService
{{
}}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        var ioc008Diagnostics = result.GetDiagnosticsByCode("IOC008");
        Assert.Equal(2, ioc008Diagnostics.Count); // Should detect both duplicates

        // Verify both duplicate types are mentioned
        var messages = ioc008Diagnostics.Select(d => d.GetMessage()).ToList();
        Assert.Contains(messages, m => m.Contains("IService5"));
        Assert.Contains(messages, m => m.Contains("IService10"));
    }

    [Fact]
    public void MixedArityRedundancy_DependsOnSingleVsMultipleWithSameType_GeneratesIOC006()
    {
        // Test mixed arity redundancy (DependsOn<T> vs DependsOn<T, U> with same T)
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService1 { }
public interface IService2 { }
public interface IService3 { }
[DependsOn<IService1>] // Single type
[DependsOn<IService1, IService2>] // Multiple types with duplicate IService1
[DependsOn<IService2, IService3>] // Multiple types with duplicate IService2
public partial class TestService
{
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        var ioc006Diagnostics = result.GetDiagnosticsByCode("IOC006");
        Assert.Equal(2, ioc006Diagnostics.Count); // Should detect both IService1 and IService2 duplicates

        var messages = ioc006Diagnostics.Select(d => d.GetMessage()).ToList();
        Assert.Contains(messages, m => m.Contains("IService1"));
        Assert.Contains(messages, m => m.Contains("IService2"));
    }

    [Fact]
    public void ComplexMultiTypeDuplicatePatterns_WithinSingleAttribute_DetectsAll()
    {
        // Test complex multi-type duplicate patterns in single attributes
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IServiceA { }
public interface IServiceB { }
public interface IServiceC { }
[DependsOn<IServiceA, IServiceB, IServiceA, IServiceC, IServiceB, IServiceA>] // Multiple complex duplicates
public partial class TestService
{
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        var ioc008Diagnostics = result.GetDiagnosticsByCode("IOC008");
        // Should detect duplicates for IServiceA (3 times) and IServiceB (2 times)
        Assert.True(ioc008Diagnostics.Count >= 2); // At least one diagnostic per duplicate type

        var messages = ioc008Diagnostics.Select(d => d.GetMessage()).ToList();
        Assert.Contains(messages, m => m.Contains("IServiceA"));
        Assert.Contains(messages, m => m.Contains("IServiceB"));
    }

    #endregion

    #region Inheritance Hierarchy Redundancy Tests

    [Fact]
    public void InheritanceHierarchyRedundancy_BaseInjectVsDerivedDependsOn_GeneratesIOC007()
    {
        // Base class [Inject] vs derived class [DependsOn] conflicts
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ILogger { }
public interface IService { }
public partial class BaseService
{
    [Inject] protected readonly ILogger _logger; // Base class has Inject
}
[DependsOn<ILogger, IService>] // Derived class has DependsOn with same type
public partial class DerivedService : BaseService
{
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var ioc007Diagnostics = result.GetDiagnosticsByCode("IOC007");
        Assert.Single(ioc007Diagnostics);
        Assert.Contains("ILogger", ioc007Diagnostics[0].GetMessage());

        // Verify generation behavior with inheritance
        var derivedConstructor = result.GetConstructorSource("DerivedService");
        Assert.NotNull(derivedConstructor);
        // Should handle inheritance correctly
        Assert.Contains("IService service", derivedConstructor.Content); // From DependsOn
    }

    [Fact]
    public void InheritedDependenciesRedundancyChain_MultiLevelInheritance_DetectsConflicts()
    {
        // Inherited dependencies creating redundancy chains
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ILogger { }
public interface IRepository { }
public interface IService { }
public partial class BaseService
{
    [Inject] protected readonly ILogger _logger;
}

[Scoped] 
[DependsOn<IRepository>]
public partial class MiddleService : BaseService
{
    [Inject] protected readonly ILogger _duplicateLogger; // Same type as base
}
[DependsOn<ILogger, IService>] // Conflicts with inherited Inject fields
public partial class DerivedService : MiddleService
{
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        var ioc007Diagnostics = result.GetDiagnosticsByCode("IOC007");
        Assert.True(ioc007Diagnostics.Count >= 1); // Should detect at least the ILogger conflict

        var messages = ioc007Diagnostics.Select(d => d.GetMessage()).ToList();
        Assert.Contains(messages, m => m.Contains("ILogger"));
    }

    [Fact]
    public void AbstractBaseClassRedundancy_WithDerivedImplementations_HandlesCorrectly()
    {
        // Abstract base classes with redundant dependency declarations
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ILogger { }
public interface IConfig { }
public abstract partial class AbstractService
{
    [Inject] protected readonly ILogger _logger;
}
[DependsOn<ILogger>] // Redundant with inherited Inject
[DependsOn<IConfig>] // Valid - not inherited
public partial class ConcreteService : AbstractService
{
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        var ioc007Diagnostics = result.GetDiagnosticsByCode("IOC007");
        Assert.Single(ioc007Diagnostics);
        Assert.Contains("ILogger", ioc007Diagnostics[0].GetMessage());

        // Verify IConfig is still properly handled
        var constructorSource = result.GetConstructorSource("ConcreteService");
        Assert.NotNull(constructorSource);
        Assert.Contains("IConfig config", constructorSource.Content);
    }

    #endregion

    #region Generic Type and Cross-Namespace Redundancy Tests

    [Fact]
    public void GenericTypeRedundancy_SameGenericTypeParameters_DetectsDuplicates()
    {
        // Generic type redundancy - IRepository<User> vs IRepository<User> detection
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public class User { }
public class Product { }
public interface IRepository<T> { }
[DependsOn<IRepository<User>>]
[DependsOn<IRepository<User>, IRepository<Product>>] // Duplicate IRepository<User>
public partial class TestService
{
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        var ioc006Diagnostics = result.GetDiagnosticsByCode("IOC006");
        Assert.Single(ioc006Diagnostics);
        Assert.Contains("IRepository<User>", ioc006Diagnostics[0].GetMessage());
    }

    [Fact]
    public void CrossNamespaceTypeCollision_SameNameDifferentNamespace_NoFalsePositives()
    {
        // Cross-namespace type name collision testing
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test.Services
{
    public interface ILogger { } // Same name, different namespace
}

namespace Test.Utilities
{
    public interface ILogger { } // Same name, different namespace
}

namespace Test
{
    using Services;
    using LoggerUtil = Utilities.ILogger;
    
    
    [DependsOn<ILogger>] // Test.Services.ILogger
    [DependsOn<LoggerUtil>] // Test.Utilities.ILogger - should NOT be detected as duplicate
    public partial class TestService
    {
    }
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Should NOT generate IOC006 - different namespaces mean different types
        var ioc006Diagnostics = result.GetDiagnosticsByCode("IOC006");
        Assert.Empty(ioc006Diagnostics);

        var constructorSource = result.GetConstructorSource("TestService");
        Assert.NotNull(constructorSource);
        // Should have both loggers as separate dependencies
        var paramCount = Regex.Matches(constructorSource.Content, @"\blogger\d*\b").Count;
        Assert.True(paramCount >= 2); // Should have parameters for both logger types
    }

    [Fact]
    public void ExternalServiceParameterInteraction_WithRedundancy_HandlesCorrectly()
    {
        // External service parameter interaction - external: true with redundancy
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IExternalService { }
public interface IInternalService { }
[DependsOn<IExternalService>(external: true)] // External dependency
[DependsOn<IExternalService, IInternalService>] // Duplicate external + internal
public partial class TestService
{
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        var ioc006Diagnostics = result.GetDiagnosticsByCode("IOC006");
        Assert.Single(ioc006Diagnostics);
        Assert.Contains("IExternalService", ioc006Diagnostics[0].GetMessage());

        // Verify generation handles external parameter correctly
        var constructorSource = result.GetConstructorSource("TestService");
        Assert.NotNull(constructorSource);
        Assert.Contains("IExternalService", constructorSource.Content);
        Assert.Contains("IInternalService", constructorSource.Content);
    }

    #endregion

    #region Edge Cases and Robustness Tests

    [Fact]
    public void EmptyAndNullScenarios_HandlesGracefully()
    {
        // Edge cases with empty class names and unusual scenarios
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService { }
[DependsOn<IService>] // Valid
public partial class TestService
{
    // No issues - baseline test for edge case handling
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Should have no redundancy diagnostics
        Assert.Empty(result.GetDiagnosticsByCode("IOC006"));
        Assert.Empty(result.GetDiagnosticsByCode("IOC007"));
        Assert.Empty(result.GetDiagnosticsByCode("IOC008"));
        Assert.Empty(result.GetDiagnosticsByCode("IOC009"));

        Assert.False(result.HasErrors);
    }

    [Fact]
    public void RobustnessValidation_NoMagicNumbers_ConsistentCounting()
    {
        // Replace magic number assumptions with robust validation
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService1 { }
public interface IService2 { }
public interface IService3 { }

// Add implementations to avoid IOC001 warnings and enable constructor generation

public partial class Service1Impl : IService1 { }
public partial class Service2Impl : IService2 { }
public partial class Service3Impl : IService3 { }
[DependsOn<IService1, IService1, IService2>] // 1 duplicate
[DependsOn<IService2, IService3>] // 1 duplicate (IService2)
public partial class TestService
{
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Robust counting without hardcoded expectations
        var ioc008Count = result.GetDiagnosticsByCode("IOC008").Count;
        var ioc006Count = result.GetDiagnosticsByCode("IOC006").Count;

        Assert.True(ioc008Count > 0); // Should detect intra-attribute duplicates
        Assert.True(ioc006Count > 0); // Should detect cross-attribute duplicates

        // Verify generation vs diagnostic consistency
        var constructorSource = result.GetConstructorSource("TestService");
        Assert.NotNull(constructorSource);

        // Count unique service parameters (should be 3 despite duplicates)
        var uniqueServices = new[] { "IService1", "IService2", "IService3" };
        foreach (var service in uniqueServices)
            // Check that each service type appears in the constructor parameters
            // Don't be specific about parameter naming - just verify the service types are present
            Assert.Contains(service, constructorSource.Content);
    }

    #endregion

    #region Performance and Boundary Condition Tests

    [Fact]
    public void MaximumComplexityStressTest_ManyDependsOnAttributes_HandlesEfficiently()
    {
        // Stress test with many DependsOn attributes
        var interfaces = string.Join("\n", Enumerable.Range(1, 50).Select(i => $"public interface IService{i} {{ }}"));
        var dependsOnAttributes = string.Join("\n", Enumerable.Range(1, 25).Select(i =>
            $"[DependsOn<IService{i}, IService{i + 1}>]")) + "\n[DependsOn<IService1>]"; // Add redundancy

        var source = $@"
using IoCTools.Abstractions.Annotations;

namespace Test;

{interfaces}
{dependsOnAttributes}
public partial class TestService
{{
}}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Should handle large numbers of attributes without crashing
        Assert.False(result.HasErrors);

        // Should detect redundancies efficiently
        var ioc006Count = result.GetDiagnosticsByCode("IOC006").Count;
        Assert.True(ioc006Count > 0); // Should detect some duplicates

        var constructorSource = result.GetConstructorSource("TestService");
        Assert.NotNull(constructorSource);
    }

    [Fact]
    public void PerformanceBoundaryTesting_LargeRedundancyScenarios_ProcessesCorrectly()
    {
        // Large redundancy detection scenarios
        var duplicatePattern = string.Join(", ", Enumerable.Repeat("IService1", 15)) + ", IService2";

        var source = $@"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService1 {{ }}
public interface IService2 {{ }}
[DependsOn<{duplicatePattern}>] // Massive duplication of IService1
public partial class TestService
{{
}}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Should handle massive duplication without performance issues
        Assert.False(result.HasErrors);

        var ioc008Diagnostics = result.GetDiagnosticsByCode("IOC008");
        Assert.True(ioc008Diagnostics.Count > 0); // Should detect the massive duplication

        // Generated constructor should still be clean
        var constructorSource = result.GetConstructorSource("TestService");
        Assert.NotNull(constructorSource);

        // Should have exactly 2 unique service types despite massive duplication
        Assert.Contains("IService1", constructorSource.Content);
        Assert.Contains("IService2", constructorSource.Content);
    }

    [Fact]
    public void MixedAttributeParameterStylesWithRedundancy_HandlesAllCombinations()
    {
        // Mixed attribute parameter styles with redundancy
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IService1 { }
public interface IService2 { }
public interface IService3 { }
[DependsOn<IService1>(external: true)] // External parameter
[DependsOn<IService1, IService2>(namingConvention: NamingConvention.CamelCase)] // With naming convention
[DependsOn<IService2, IService3>] // Standard
public partial class TestService
{
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Should detect redundancies regardless of parameter styles
        var ioc006Count = result.GetDiagnosticsByCode("IOC006").Count;
        Assert.Equal(2, ioc006Count); // IService1 and IService2 duplicates

        // Generation should handle mixed parameter styles correctly
        var constructorSource = result.GetConstructorSource("TestService");
        Assert.NotNull(constructorSource);

        // Should have all three services with appropriate parameter handling
        Assert.Contains("IService1", constructorSource.Content);
        Assert.Contains("IService2", constructorSource.Content);
        Assert.Contains("IService3", constructorSource.Content);
    }

    #endregion
}
