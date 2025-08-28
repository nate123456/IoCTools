namespace IoCTools.Generator.Tests;

using Microsoft.CodeAnalysis;

/// <summary>
///     COMPREHENSIVE BUG COVERAGE: Lifetime Diagnostic Bugs
///     These tests explicitly reproduce and prevent regression of discovered bugs:
///     - IOC012 Not Triggering: Singleton→Scoped dependencies not generating errors
///     - IOC013 Not Triggering: Singleton→Transient dependencies not generating warnings
///     - Dependency Relationship Analysis: Cross-service dependencies not being detected
///     Each test reproduces the exact bug condition and validates the fix.
/// </summary>
public class LifetimeDiagnosticBugTests
{
    #region BUG: IOC012 Not Triggering (Singleton→Scoped)

    /// <summary>
    ///     BUG REPRODUCTION: IOC012 was not triggering for Singleton services depending on Scoped services.
    ///     This should always generate an ERROR diagnostic.
    /// </summary>
    [Fact]
    public void Test_IOC012_SingletonDependsOnScoped_TriggersError()
    {
        // Arrange - Exact bug reproduction scenario
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

[Scoped]
public partial class DatabaseContext
{
    public void SaveChanges() { }
}

[Singleton]  
public partial class CacheService
{
    [Inject] private readonly DatabaseContext _context; // BUG: This should trigger IOC012 ERROR
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - CRITICAL: IOC012 must be triggered
        var ioc012Diagnostics = result.GetDiagnosticsByCode("IOC012");

        Assert.Single(ioc012Diagnostics); // Must have exactly one IOC012 error
        Assert.Equal(DiagnosticSeverity.Error, ioc012Diagnostics[0].Severity); // Must be ERROR severity

        var message = ioc012Diagnostics[0].GetMessage();
        Assert.Contains("CacheService", message); // Should mention the singleton service
        Assert.Contains("DatabaseContext", message); // Should mention the scoped dependency  
        Assert.Contains("Singleton", message); // Should mention lifetime conflict
        Assert.Contains("Scoped", message); // Should mention lifetime conflict
    }

    /// <summary>
    ///     BUG REPRODUCTION: IOC012 should trigger with DependsOn attribute as well.
    /// </summary>
    [Fact]
    public void Test_IOC012_SingletonDependsOnScoped_DependsOnAttribute_TriggersError()
    {
        // Arrange - DependsOn variation of the bug
        var source = @"
using IoCTools.Abstractions.Annotations;  
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

[Scoped]
public partial class DatabaseContext
{
}

[Singleton]
[DependsOn<DatabaseContext>] // BUG: This should trigger IOC012 ERROR
public partial class SingletonService  
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var ioc012Diagnostics = result.GetDiagnosticsByCode("IOC012");

        Assert.Single(ioc012Diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, ioc012Diagnostics[0].Severity);

        var message = ioc012Diagnostics[0].GetMessage();
        Assert.Contains("SingletonService", message);
        Assert.Contains("DatabaseContext", message);
    }

    /// <summary>
    ///     BUG REPRODUCTION: Complex inheritance scenario should still trigger IOC012.
    /// </summary>
    [Fact]
    public void Test_IOC012_InheritanceChain_SingletonDependsOnScoped_TriggersError()
    {
        // Arrange - Inheritance scenario that might miss diagnostic  
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

[Scoped]
public partial class ScopedRepository
{
}

[Singleton]
public partial class BaseService
{
    [Inject] private readonly ScopedRepository _repo; // BUG: Should trigger IOC012
}

[Singleton]
public partial class DerivedService : BaseService
{
    // Inherits dependency relationship bug
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Both services should trigger IOC012
        var ioc012Diagnostics = result.GetDiagnosticsByCode("IOC012");

        Assert.True(ioc012Diagnostics.Count >= 1, "Should have at least one IOC012 diagnostic");
        Assert.All(ioc012Diagnostics, d => Assert.Equal(DiagnosticSeverity.Error, d.Severity));
    }

    #endregion

    #region BUG: IOC013 Not Triggering (Singleton→Transient)

    /// <summary>
    ///     BUG REPRODUCTION: IOC013 was not triggering for Singleton services depending on Transient services.
    ///     This should always generate a WARNING diagnostic.
    /// </summary>
    [Fact]
    public void Test_IOC013_SingletonDependsOnTransient_TriggersWarning()
    {
        // Arrange - Exact bug reproduction scenario
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

[Transient]
public partial class TransientLogger
{
    public void Log(string message) { }
}

[Singleton]
public partial class CacheService
{
    [Inject] private readonly TransientLogger _logger; // BUG: This should trigger IOC013 WARNING
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - CRITICAL: IOC013 must be triggered
        var ioc013Diagnostics = result.GetDiagnosticsByCode("IOC013");

        Assert.Single(ioc013Diagnostics); // Must have exactly one IOC013 warning
        Assert.Equal(DiagnosticSeverity.Warning, ioc013Diagnostics[0].Severity); // Must be WARNING severity

        var message = ioc013Diagnostics[0].GetMessage();
        Assert.Contains("CacheService", message); // Should mention the singleton service
        Assert.Contains("TransientLogger", message); // Should mention the transient dependency
        Assert.Contains("Singleton", message); // Should mention lifetime conflict
        Assert.Contains("Transient", message); // Should mention lifetime conflict
    }

    /// <summary>
    ///     BUG REPRODUCTION: IOC013 should trigger with multiple transient dependencies.
    /// </summary>
    [Fact]
    public void Test_IOC013_SingletonMultipleTransientDependencies_TriggersWarnings()
    {
        // Arrange - Multiple transient dependencies
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

[Transient]
public partial class TransientServiceA
{
}

[Transient]
public partial class TransientServiceB
{
}

[Singleton]
public partial class SingletonService
{
    [Inject] private readonly TransientServiceA _serviceA; // Should trigger IOC013
    [Inject] private readonly TransientServiceB _serviceB; // Should trigger IOC013
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should have IOC013 for both dependencies
        var ioc013Diagnostics = result.GetDiagnosticsByCode("IOC013");

        Assert.Equal(2, ioc013Diagnostics.Count); // Should have two warnings
        Assert.All(ioc013Diagnostics, d => Assert.Equal(DiagnosticSeverity.Warning, d.Severity));
    }

    /// <summary>
    ///     BUG REPRODUCTION: IOC013 with DependsOn attribute should also trigger.
    /// </summary>
    [Fact]
    public void Test_IOC013_SingletonDependsOnTransient_DependsOnAttribute_TriggersWarning()
    {
        // Arrange - DependsOn variation
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

[Transient]
public partial class TransientHelper
{
}

[Singleton]
[DependsOn<TransientHelper>] // BUG: This should trigger IOC013 WARNING
public partial class SingletonCache
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var ioc013Diagnostics = result.GetDiagnosticsByCode("IOC013");

        Assert.Single(ioc013Diagnostics);
        Assert.Equal(DiagnosticSeverity.Warning, ioc013Diagnostics[0].Severity);
    }

    #endregion

    #region BUG: Dependency Relationship Analysis

    /// <summary>
    ///     BUG REPRODUCTION: Cross-service dependency relationships were not being detected properly,
    ///     causing lifetime diagnostics to be missed.
    /// </summary>
    [Fact]
    public void Test_DependencyRelationship_DetectsCrossServiceDependencies()
    {
        // Arrange - Complex cross-service dependency chain
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

[Transient]
public partial class TransientService
{
}

[Scoped]
public partial class ScopedService  
{
    [Inject] private readonly TransientService _transient; // Valid: Scoped→Transient
}

[Singleton]
public partial class SingletonService
{
    [Inject] private readonly ScopedService _scoped;     // BUG: Should trigger IOC012 ERROR
    [Inject] private readonly TransientService _transient; // BUG: Should trigger IOC013 WARNING
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - CRITICAL: Should detect both lifetime violations
        var ioc012Diagnostics = result.GetDiagnosticsByCode("IOC012"); // Singleton→Scoped ERROR
        var ioc013Diagnostics = result.GetDiagnosticsByCode("IOC013"); // Singleton→Transient WARNING

        Assert.Single(ioc012Diagnostics); // Must detect Singleton→Scoped violation
        Assert.Single(ioc013Diagnostics); // Must detect Singleton→Transient violation

        Assert.Equal(DiagnosticSeverity.Error, ioc012Diagnostics[0].Severity);
        Assert.Equal(DiagnosticSeverity.Warning, ioc013Diagnostics[0].Severity);
    }

    /// <summary>
    ///     BUG REPRODUCTION: Indirect dependency chains should be analyzed for lifetime violations.
    /// </summary>
    [Fact]
    public void Test_IndirectDependencyChain_DetectsLifetimeViolations()
    {
        // Arrange - Indirect dependency chain A→B→C where A is Singleton, C is Scoped
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

[Scoped]
public partial class ScopedDataAccess
{
}

[Singleton]  
public partial class IntermediateService
{
    [Inject] private readonly ScopedDataAccess _data; // Direct violation: Singleton→Scoped
}

[Singleton]
public partial class TopLevelService
{
    [Inject] private readonly IntermediateService _intermediate; // Valid: Singleton→Singleton
    // But IntermediateService has Scoped dependency - should still be detected
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should detect the direct Singleton→Scoped violation
        var ioc012Diagnostics = result.GetDiagnosticsByCode("IOC012");

        Assert.Single(ioc012Diagnostics); // IntermediateService → ScopedDataAccess violation
        Assert.Contains("IntermediateService", ioc012Diagnostics[0].GetMessage());
        Assert.Contains("ScopedDataAccess", ioc012Diagnostics[0].GetMessage());
    }

    #endregion

    #region BUG: Lifetime Validation in Complex Scenarios

    /// <summary>
    ///     BUG REPRODUCTION: Lifetime validation should work with generic services.
    /// </summary>
    [Fact]
    public void Test_GenericServices_LifetimeValidation_TriggersCorrectly()
    {
        // Arrange - Generic services with lifetime violations
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

[Scoped]
public partial class ScopedRepository<T>
{
}

[Singleton]
public partial class SingletonCache<T>
{
    [Inject] private readonly ScopedRepository<T> _repo; // BUG: Should trigger IOC012
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var ioc012Diagnostics = result.GetDiagnosticsByCode("IOC012");

        Assert.Single(ioc012Diagnostics);
        Assert.Equal(DiagnosticSeverity.Error, ioc012Diagnostics[0].Severity);
    }

    /// <summary>
    ///     BUG REPRODUCTION: Mixed [Inject] and [DependsOn] in same service should both be validated.
    /// </summary>
    [Fact]
    public void Test_MixedDependencyTypes_BothValidated()
    {
        // Arrange - Service with both [Inject] and [DependsOn] dependencies
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

[Scoped]
public partial class ScopedServiceA
{
}

[Transient]
public partial class TransientServiceB
{
}

[Singleton]
[DependsOn<ScopedServiceA>] // BUG: Should trigger IOC012 ERROR
public partial class MixedService
{
    [Inject] private readonly TransientServiceB _serviceB; // BUG: Should trigger IOC013 WARNING
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should detect both violations
        var ioc012Diagnostics = result.GetDiagnosticsByCode("IOC012");
        var ioc013Diagnostics = result.GetDiagnosticsByCode("IOC013");

        Assert.Single(ioc012Diagnostics); // DependsOn violation
        Assert.Single(ioc013Diagnostics); // Inject violation

        Assert.Equal(DiagnosticSeverity.Error, ioc012Diagnostics[0].Severity);
        Assert.Equal(DiagnosticSeverity.Warning, ioc013Diagnostics[0].Severity);
    }

    #endregion

    #region REGRESSION PREVENTION: Edge Cases

    /// <summary>
    ///     REGRESSION PREVENTION: Ensure diagnostics work with inheritance hierarchies.
    /// </summary>
    [Fact]
    public void Test_InheritanceHierarchy_LifetimeDiagnostics_WorkCorrectly()
    {
        // Arrange - Deep inheritance with lifetime conflicts
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

[Scoped]
public partial class ScopedBase
{
}

[Singleton]
public partial class MiddleLayer : ScopedBase
{
}

[Singleton]
public partial class TopLayer : MiddleLayer
{
    [Inject] private readonly ScopedBase _base; // Should trigger diagnostic
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var ioc012Diagnostics = result.GetDiagnosticsByCode("IOC012");

        // Should detect lifetime violation despite inheritance
        Assert.True(ioc012Diagnostics.Count >= 1, "Should detect lifetime violations in inheritance hierarchy");
    }

    /// <summary>
    ///     REGRESSION PREVENTION: Self-dependencies should not trigger false positives.
    /// </summary>
    [Fact]
    public void Test_SelfDependencies_DoNotTriggerFalsePositives()
    {
        // Arrange - Service depending on itself (valid scenario)
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace TestNamespace;

[Singleton]
public partial class SingletonService
{
    [Inject] private readonly IEnumerable<SingletonService> _allInstances; // Self-dependency, should be valid
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should not have any lifetime violations for self-dependencies
        var ioc012Diagnostics = result.GetDiagnosticsByCode("IOC012");
        var ioc013Diagnostics = result.GetDiagnosticsByCode("IOC013");

        // Filter out any diagnostics that mention the self-dependency
        var selfReferencingDiagnostics = ioc012Diagnostics.Concat(ioc013Diagnostics)
            .Where(d => d.GetMessage().Contains("SingletonService") &&
                        d.GetMessage().Count(x => x.ToString().Contains("SingletonService")) > 1);

        Assert.Empty(selfReferencingDiagnostics); // Should not trigger false positives for self-dependencies
    }

    #endregion
}
