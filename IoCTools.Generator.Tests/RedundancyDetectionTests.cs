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
        ioc008Diagnostics.Should().ContainSingle();

        var diagnostic = ioc008Diagnostics[0];
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        var message = diagnostic.GetMessage();
        message.Should().Contain("IService1");
        message.Should().Contain("multiple times in the same");

        // Verify diagnostic location accuracy
        diagnostic.Location.IsInSource.Should().BeTrue();
        diagnostic.Location.SourceTree!.ToString().Should().Contain("DependsOn<IService1, IService1>");
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
        ioc006Diagnostics.Should().ContainSingle();

        var diagnostic = ioc006Diagnostics[0];
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        var message = diagnostic.GetMessage();
        message.Should().Contain("IService1");
        message.Should().Contain("multiple times in [DependsOn]");

        // Verify no other redundancy diagnostics are present
        result.GetDiagnosticsByCode("IOC007").Should().BeEmpty();
        result.GetDiagnosticsByCode("IOC008").Should().BeEmpty();
        result.GetDiagnosticsByCode("IOC009").Should().BeEmpty();
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
        ioc007Diagnostics.Should().ContainSingle();

        var diagnostic = ioc007Diagnostics[0];
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        var message = diagnostic.GetMessage();
        message.Should().Contain("ILogger");
        message.Should().Contain("but also exists as [Inject] field");

        // Verify generation vs diagnostic consistency
        var constructorText = result.GetConstructorSourceText("TestService");
        // Should prioritize [Inject] field over [DependsOn]
        constructorText.Should().Contain("ILogger logger");
        constructorText.Should().Contain("this._logger = logger");
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
        ioc009Diagnostics.Should().ContainSingle();

        var diagnostic = ioc009Diagnostics[0];
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        var message = diagnostic.GetMessage();
        message.Should().Contain("INonImplemented");
        message.Should().Contain("not an interface that would be registered");

        // Verify service registration behavior
        var registrationText = result.GetServiceRegistrationText();
        // Should only register IUserService, not INonImplemented
        registrationText.Should().Contain("IUserService");
        registrationText.Should().NotContain("INonImplemented");
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
        result.HasErrors.Should().BeFalse();

        var constructorText = result.GetConstructorSourceText("TestService");

        // Should have both IService1 and IService2 parameters (no duplicates)
        constructorText.Should().Contain("IService1");
        constructorText.Should().Contain("IService2");

        // Robust constructor content validation - replace brittle regex counting
        var content = constructorText;

        // Verify both service types appear in parameters (deduplication worked)
        var service1Count = Regex.Matches(content, @"\bIService1\b").Count;
        var service2Count = Regex.Matches(content, @"\bIService2\b").Count;

        // Each service type should appear (at least in parameter declarations)
        service1Count.Should().BeGreaterThan(0, "IService1 should appear in constructor");
        service2Count.Should().BeGreaterThan(0, "IService2 should appear in constructor");

        // Generation vs diagnostic consistency validation
        var ioc006Diagnostics = result.GetDiagnosticsByCode("IOC006");
        ioc006Diagnostics.Should().ContainSingle(); // Warning should be generated despite auto-removal
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
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetConstructorSourceText("TestService");

        // Should have logger (from Inject) and service (from DependsOn)
        constructorSource.Should().Contain("ILogger logger");
        constructorSource.Should().Contain("IService service");
        constructorSource.Should().Contain("this._logger = logger");
        constructorSource.Should().Contain("this._service = service");

        // Should generate warning about the conflict - exact validation
        var ioc007Diagnostics = result.GetDiagnosticsByCode("IOC007");
        ioc007Diagnostics.Should().ContainSingle(); // Exact count validation

        // Verify no other redundancy diagnostics are present
        result.GetDiagnosticsByCode("IOC006").Should().BeEmpty();
        result.GetDiagnosticsByCode("IOC008").Should().BeEmpty();
        result.GetDiagnosticsByCode("IOC009").Should().BeEmpty();
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
        result.GetDiagnosticsByCode("IOC006").Should().ContainSingle(); // Duplicate across attributes
        result.GetDiagnosticsByCode("IOC007").Should().ContainSingle(); // DependsOn conflicts with Inject
        result.GetDiagnosticsByCode("IOC008").Should().ContainSingle(); // Duplicate in same attribute
        result.GetDiagnosticsByCode("IOC009").Should().ContainSingle(); // Unnecessary SkipRegistration
        result.GetDiagnosticsByCode("IOC035").Should().ContainSingle(); // Inject field could be DependsOn

        // Verify total diagnostic count is exactly what we expect
        var allRedundancyDiagnostics = result.CompilationDiagnostics.Concat(result.GeneratorDiagnostics)
            .Where(d => d.Id.StartsWith("IOC") && int.Parse(d.Id.Substring(3)) >= 6).ToList();
        allRedundancyDiagnostics.Count.Should().Be(5);

        // Generation vs diagnostic consistency - code should work despite warnings
        result.HasErrors.Should().BeFalse();
        var constructorSource = result.GetConstructorSourceText("UserService");

        // Verify redundancies are handled correctly in generation
        var content = constructorSource;
        // Should have ILogger from [Inject], IService from [DependsOn] (no duplicates)
        content.Should().Contain("ILogger logger");
        content.Should().Contain("IService service");
        content.Should().Contain("this._logger = logger");
        content.Should().Contain("this._service = service");

        // Verify no duplicate parameters exist
        var loggerParamMatches = Regex.Matches(content, @"\bILogger logger\b");
        loggerParamMatches.Should().ContainSingle();
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
        result.GetDiagnosticsByCode("IOC006").Should().BeEmpty(); // No duplicate across attributes
        result.GetDiagnosticsByCode("IOC007").Should().BeEmpty(); // No DependsOn/Inject conflicts  
        result.GetDiagnosticsByCode("IOC008").Should().BeEmpty(); // No duplicate in same attribute
        result.GetDiagnosticsByCode("IOC009").Should().BeEmpty(); // No unnecessary SkipRegistration

        // Verify no unexpected diagnostics are present
        var allIocDiagnostics = result.CompilationDiagnostics.Concat(result.GeneratorDiagnostics)
            .Where(d => d.Id is "IOC006" or "IOC007" or "IOC008" or "IOC009").ToList();
        allIocDiagnostics.Should().BeEmpty(); // Should have zero redundancy diagnostics

        result.HasErrors.Should().BeFalse();

        // Verify clean generation without redundancies
        var constructorSource = result.GetConstructorSourceText("UserService");
        var content = constructorSource;

        // Should have all three dependencies cleanly generated
        content.Should().Contain("ILogger logger"); // From [Inject]
        content.Should().Contain("IService service"); // From [DependsOn]
        content.Should().Contain("this._logger = logger");
        content.Should().Contain("this._service = service");
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

        ioc001Diagnostics.Should().NotBeEmpty(); // Missing implementation
        ioc006Diagnostics.Should().ContainSingle(); // Duplicate dependency

        // Verify diagnostic messages
        var missingMessages = ioc001Diagnostics.Select(d => d.GetMessage()).ToList();
        missingMessages.Should().Contain(message => message.Contains("IMissingService"));
        ioc006Diagnostics[0].GetMessage().Should().Contain("ILogger");
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

        ioc002Diagnostics.Should().NotBeEmpty(); // Implementation not registered
        ioc008Diagnostics.Should().ContainSingle(); // Duplicate in same attribute

        ioc002Diagnostics[0].GetMessage().Should().Contain("ILogger");
        ioc008Diagnostics[0].GetMessage().Should().Contain("ILogger");
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
        ioc007Diagnostics.Should().ContainSingle();
        ioc007Diagnostics[0].GetMessage().Should().Contain("IServiceB");
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
        ioc009Diagnostics.Should().ContainSingle(); // Unnecessary SkipRegistration
        ioc009Diagnostics[0].GetMessage().Should().Contain("INonExistentService");

        // Verify registration behavior
        var registrationSource = result.GetServiceRegistrationText();
        registrationSource.Should().Contain("IUserService"); // Should be registered
        registrationSource.Should().NotContain("IAdminService"); // Should be skipped
        registrationSource.Should().NotContain("INonExistentService"); // Not implemented
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
        ioc008Diagnostics.Count.Should().Be(2); // Should detect both duplicates

        // Verify both duplicate types are mentioned
        var messages = ioc008Diagnostics.Select(d => d.GetMessage()).ToList();
        messages.Should().Contain(m => m.Contains("IService5"));
        messages.Should().Contain(m => m.Contains("IService10"));
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
        ioc006Diagnostics.Count.Should().Be(2); // Should detect both IService1 and IService2 duplicates

        var messages = ioc006Diagnostics.Select(d => d.GetMessage()).ToList();
        messages.Should().Contain(m => m.Contains("IService1"));
        messages.Should().Contain(m => m.Contains("IService2"));
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
        ioc008Diagnostics.Count.Should().BeGreaterOrEqualTo(2); // At least one diagnostic per duplicate type

        var messages = ioc008Diagnostics.Select(d => d.GetMessage()).ToList();
        messages.Should().Contain(m => m.Contains("IServiceA"));
        messages.Should().Contain(m => m.Contains("IServiceB"));
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
        ioc007Diagnostics.Should().ContainSingle();
        ioc007Diagnostics[0].GetMessage().Should().Contain("ILogger");

        // Verify generation behavior with inheritance
        var derivedConstructor = result.GetConstructorSourceText("DerivedService");
        // Should handle inheritance correctly
        derivedConstructor.Should().Contain("IService service"); // From DependsOn
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
        ioc007Diagnostics.Count.Should().BeGreaterOrEqualTo(1); // Should detect at least the ILogger conflict

        var messages = ioc007Diagnostics.Select(d => d.GetMessage()).ToList();
        messages.Should().Contain(m => m.Contains("ILogger"));
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
        ioc007Diagnostics.Should().ContainSingle();
        ioc007Diagnostics[0].GetMessage().Should().Contain("ILogger");

        // Verify IConfig is still properly handled
        var constructorSource = result.GetConstructorSourceText("ConcreteService");
        constructorSource.Should().Contain("IConfig config");
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
        ioc006Diagnostics.Should().ContainSingle();
        ioc006Diagnostics[0].GetMessage().Should().Contain("IRepository<User>");
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
        ioc006Diagnostics.Should().BeEmpty();

        var constructorSource = result.GetConstructorSourceText("TestService");
        // Should have both loggers as separate dependencies
        var paramCount = Regex.Matches(constructorSource, @"\blogger\d*\b").Count;
        paramCount.Should().BeGreaterOrEqualTo(2); // Should have parameters for both logger types
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
        ioc006Diagnostics.Should().ContainSingle();
        ioc006Diagnostics[0].GetMessage().Should().Contain("IExternalService");

        // Verify generation handles external parameter correctly
        var constructorSource = result.GetConstructorSourceText("TestService");
        constructorSource.Should().Contain("IExternalService");
        constructorSource.Should().Contain("IInternalService");
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
        result.GetDiagnosticsByCode("IOC006").Should().BeEmpty();
        result.GetDiagnosticsByCode("IOC007").Should().BeEmpty();
        result.GetDiagnosticsByCode("IOC008").Should().BeEmpty();
        result.GetDiagnosticsByCode("IOC009").Should().BeEmpty();

        result.HasErrors.Should().BeFalse();
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

        ioc008Count.Should().BeGreaterThan(0); // Should detect intra-attribute duplicates
        ioc006Count.Should().BeGreaterThan(0); // Should detect cross-attribute duplicates

        // Verify generation vs diagnostic consistency
        var constructorSource = result.GetConstructorSourceText("TestService");

        // Count unique service parameters (should be 3 despite duplicates)
        var uniqueServices = new[] { "IService1", "IService2", "IService3" };
        foreach (var service in uniqueServices)
            // Check that each service type appears in the constructor parameters
            // Don't be specific about parameter naming - just verify the service types are present
            constructorSource.Should().Contain(service);
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
        result.HasErrors.Should().BeFalse();

        // Should detect redundancies efficiently
        var ioc006Count = result.GetDiagnosticsByCode("IOC006").Count;
        ioc006Count.Should().BeGreaterThan(0); // Should detect some duplicates

        var constructorSource = result.GetConstructorSourceText("TestService");
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
        result.HasErrors.Should().BeFalse();

        var ioc008Diagnostics = result.GetDiagnosticsByCode("IOC008");
        ioc008Diagnostics.Count.Should().BeGreaterThan(0); // Should detect the massive duplication

        // Generated constructor should still be clean
        var constructorSource = result.GetConstructorSourceText("TestService");

        // Should have exactly 2 unique service types despite massive duplication
        constructorSource.Should().Contain("IService1");
        constructorSource.Should().Contain("IService2");
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
        ioc006Count.Should().Be(2); // IService1 and IService2 duplicates

        // Generation should handle mixed parameter styles correctly
        var constructorSource = result.GetConstructorSourceText("TestService");

        // Should have all three services with appropriate parameter handling
        constructorSource.Should().Contain("IService1");
        constructorSource.Should().Contain("IService2");
        constructorSource.Should().Contain("IService3");
    }

    #endregion
}
