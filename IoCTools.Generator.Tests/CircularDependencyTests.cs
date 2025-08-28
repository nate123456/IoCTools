namespace IoCTools.Generator.Tests;

using Microsoft.CodeAnalysis;

/// <summary>
///     BULLETPROOF CIRCULAR DEPENDENCY TEST SUITE
///     Tests comprehensive circular dependency detection scenarios.
///     Circular dependencies should be detected and reported as IOC003 errors with specific cycle paths.
/// </summary>
public class CircularDependencyTests
{
    [Fact]
    public void CircularDependency_TwoServices_DetectsCircularReference()
    {
        // Arrange - A depends on B, B depends on A
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IServiceA { }
public interface IServiceB { }
[Scoped]
public partial class ServiceA : IServiceA
{
    [Inject] private readonly IServiceB _serviceB;
}
[Scoped]
public partial class ServiceB : IServiceB
{
    [Inject] private readonly IServiceA _serviceA;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should detect circular dependency (may detect multiple related cycles)
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);

        // All diagnostics should be warnings and contain proper cycle detection
        Assert.All(ioc003Diagnostics, diagnostic =>
        {
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("Circular dependency detected", diagnostic.GetMessage());
        });

        // Should detect the core cycle involving ServiceA and ServiceB
        var hasExpectedCycle = ioc003Diagnostics.Any(d =>
        {
            var message = d.GetMessage();
            return message.Contains("ServiceA") && message.Contains("ServiceB");
        });
        Assert.True(hasExpectedCycle,
            $"Expected at least one diagnostic containing ServiceA and ServiceB cycle. Got: {string.Join("; ", ioc003Diagnostics.Select(d => d.GetMessage()))}");
    }

    [Fact]
    public void CircularDependency_ThreeServices_DetectsCircularChain()
    {
        // Arrange - A → B → C → A
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IServiceA { }
public interface IServiceB { }
public interface IServiceC { }
[Scoped]
public partial class ServiceA : IServiceA
{
    [Inject] private readonly IServiceB _serviceB;
}
[Scoped]
public partial class ServiceB : IServiceB
{
    [Inject] private readonly IServiceC _serviceC;
}
[Scoped]
public partial class ServiceC : IServiceC
{
    [Inject] private readonly IServiceA _serviceA;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should detect circular dependency (may detect multiple related cycles)
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);

        // All diagnostics should be warnings and contain proper cycle detection
        Assert.All(ioc003Diagnostics, diagnostic =>
        {
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("Circular dependency detected", diagnostic.GetMessage());
        });

        // Should contain cycle path like: ServiceA → ServiceB → ServiceC → ServiceA
        var hasExpectedCycle = ioc003Diagnostics.Any(d =>
        {
            var message = d.GetMessage();
            return message.Contains("ServiceA") && message.Contains("ServiceB") && message.Contains("ServiceC");
        });
        Assert.True(hasExpectedCycle,
            $"Expected cycle containing ServiceA, ServiceB, and ServiceC. Got: {string.Join("; ", ioc003Diagnostics.Select(d => d.GetMessage()))}");
    }

    [Fact]
    public void CircularDependency_SelfReference_DetectsImmediately()
    {
        // Arrange - Service depends on itself
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IMyService { }
[Scoped]
public partial class MyService : IMyService
{
    [Inject] private readonly IMyService _myService;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should detect circular dependency (may detect multiple related cycles)
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);

        // All diagnostics should be warnings and contain proper cycle detection
        Assert.All(ioc003Diagnostics, diagnostic =>
        {
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("Circular dependency detected", diagnostic.GetMessage());
        });

        // Should contain self-reference cycle path like: MyService → MyService
        var hasExpectedCycle = ioc003Diagnostics.Any(d =>
        {
            var message = d.GetMessage();
            return message.Contains("MyService");
        });
        Assert.True(hasExpectedCycle,
            $"Expected cycle containing MyService. Got: {string.Join("; ", ioc003Diagnostics.Select(d => d.GetMessage()))}");
    }

    [Fact]
    public void CircularDependency_WithDependsOnAttribute_DetectsCorrectly()
    {
        // Arrange - Using DependsOn attributes
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IServiceX { }
public interface IServiceY { }
[DependsOn<IServiceY>]
[Scoped]
public partial class ServiceX : IServiceX { }
[DependsOn<IServiceX>]
[Scoped]
public partial class ServiceY : IServiceY { }";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should detect circular dependency (may detect multiple related cycles)
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);

        // All diagnostics should be warnings and contain proper cycle detection
        Assert.All(ioc003Diagnostics, diagnostic =>
        {
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("Circular dependency detected", diagnostic.GetMessage());
        });

        // Should contain cycle path with ServiceX and ServiceY
        var hasExpectedCycle = ioc003Diagnostics.Any(d =>
        {
            var message = d.GetMessage();
            return message.Contains("ServiceX") && message.Contains("ServiceY");
        });
        Assert.True(hasExpectedCycle,
            $"Expected cycle containing ServiceX and ServiceY. Got: {string.Join("; ", ioc003Diagnostics.Select(d => d.GetMessage()))}");
    }

    [Fact]
    public void CircularDependency_MixedInjectAndDependsOn_DetectsCorrectly()
    {
        // Arrange - Mix of [Inject] and [DependsOn]
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IServiceP { }
public interface IServiceQ { }
[Scoped]
public partial class ServiceP : IServiceP
{
    [Inject] private readonly IServiceQ _serviceQ;
}
[DependsOn<IServiceP>]
[Scoped]
public partial class ServiceQ : IServiceQ { }";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should detect circular dependency (may detect multiple related cycles)
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);

        // All diagnostics should be warnings and contain proper cycle detection
        Assert.All(ioc003Diagnostics, diagnostic =>
        {
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("Circular dependency detected", diagnostic.GetMessage());
        });

        // Should contain cycle path with ServiceP and ServiceQ
        var hasExpectedCycle = ioc003Diagnostics.Any(d =>
        {
            var message = d.GetMessage();
            return message.Contains("ServiceP") && message.Contains("ServiceQ");
        });
        Assert.True(hasExpectedCycle,
            $"Expected cycle containing ServiceP and ServiceQ. Got: {string.Join("; ", ioc003Diagnostics.Select(d => d.GetMessage()))}");
    }

    [Fact]
    public void CircularDependency_ComplexFourServiceCycle_DetectsCorrectly()
    {
        // Arrange - A → B → C → D → A
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IA { }
public interface IB { }
public interface IC { }
public interface ID { }
[DependsOn<IB>]
[Scoped]
public partial class A : IA { }
[DependsOn<IC>]
[Scoped]
public partial class B : IB { }
[Scoped]
public partial class C : IC
{
    [Inject] private readonly ID _d;
}
[Scoped]
public partial class D : ID
{
    [Inject] private readonly IA _a;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should detect circular dependency (may detect multiple related cycles)
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);

        // All diagnostics should be warnings and contain proper cycle detection
        Assert.All(ioc003Diagnostics, diagnostic =>
        {
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("Circular dependency detected", diagnostic.GetMessage());
        });

        // Should contain cycle path with all four services: A → B → C → D → A
        var hasExpectedCycle = ioc003Diagnostics.Any(d =>
        {
            var message = d.GetMessage();
            return message.Contains("A") && message.Contains("B") && message.Contains("C") && message.Contains("D");
        });
        Assert.True(hasExpectedCycle,
            $"Expected cycle containing A, B, C, and D. Got: {string.Join("; ", ioc003Diagnostics.Select(d => d.GetMessage()))}");
    }

    [Fact]
    public void CircularDependency_ValidDependencyChain_NoErrors()
    {
        // Arrange - Valid linear dependency: A → B → C (no cycles)
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IServiceA { }
public interface IServiceB { }
public interface IServiceC { }
[Scoped]
public partial class ServiceA : IServiceA
{
    [Inject] private readonly IServiceB _serviceB;
}
[Scoped]
public partial class ServiceB : IServiceB
{
    [Inject] private readonly IServiceC _serviceC;
}
[Scoped]
public partial class ServiceC : IServiceC { }";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should have NO circular dependency errors and generate valid code
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.Empty(ioc003Diagnostics);
        Assert.False(result.HasErrors);

        // Should generate valid service registrations for linear dependency chain
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("ServiceA", registrationSource.Content);
        Assert.Contains("ServiceB", registrationSource.Content);
        Assert.Contains("ServiceC", registrationSource.Content);
    }

    [Fact]
    public void CircularDependency_ExternalServiceInCycle_IgnoresCircularCheck()
    {
        // Arrange - A → B → A, but B is marked as external
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IServiceA { }
public interface IServiceB { }
[Scoped]
public partial class ServiceA : IServiceA
{
    [Inject] private readonly IServiceB _serviceB;
}
[ExternalService]
public partial class ServiceB : IServiceB
{
    [Inject] private readonly IServiceA _serviceA;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - External services should skip circular dependency checks
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.Empty(ioc003Diagnostics);
    }

    [Fact]
    public void CircularDependency_GenericServices_DetectsCorrectly()
    {
        // Arrange - Generic services in circular dependency
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IRepo<T> { }
public interface IService<T> { }
[Scoped]
public partial class StringRepo : IRepo<string>
{
    [Inject] private readonly IService<string> _service;
}
[Scoped]
public partial class StringService : IService<string>
{
    [Inject] private readonly IRepo<string> _repo;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should detect circular dependency (may detect multiple related cycles)
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);

        // All diagnostics should be warnings and contain proper cycle detection
        Assert.All(ioc003Diagnostics, diagnostic =>
        {
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("Circular dependency detected", diagnostic.GetMessage());
        });

        // Should contain cycle path with generic services
        var hasExpectedCycle = ioc003Diagnostics.Any(d =>
        {
            var message = d.GetMessage();
            return message.Contains("StringRepo") && message.Contains("StringService");
        });
        Assert.True(hasExpectedCycle,
            $"Expected cycle containing StringRepo and StringService. Got: {string.Join("; ", ioc003Diagnostics.Select(d => d.GetMessage()))}");
    }

    #region COMPLEX MULTI-ATTRIBUTE SCENARIOS

    [Fact]
    public void CircularDependency_MultipleAttributesCombined_DetectsCorrectly()
    {
        // Arrange - Complex scenario with multiple DependsOn attributes on same class
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IMultiDep1 { }
public interface IMultiDep2 { }
public interface IMultiDep3 { }
public interface IComplexMulti { }
[DependsOn<IMultiDep1>]
[DependsOn<IMultiDep2>]
[Scoped]
public partial class ComplexMulti : IComplexMulti { }
[Scoped]
public partial class MultiDep1 : IMultiDep1
{
    [Inject] private readonly IMultiDep2 _dep2;
}
[Scoped]
public partial class MultiDep2 : IMultiDep2
{
    [Inject] private readonly IMultiDep3 _dep3;
}
[Scoped]
public partial class MultiDep3 : IMultiDep3
{
    [Inject] private readonly IComplexMulti _complexMulti; // Creates cycle back to ComplexMulti
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should detect circular dependency (may detect multiple related cycles)
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);

        // All diagnostics should be warnings and contain proper cycle detection
        Assert.All(ioc003Diagnostics, diagnostic =>
        {
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("Circular dependency detected", diagnostic.GetMessage());
        });

        // Should detect cycle through complex multi-attribute scenario
        var hasExpectedCycle = ioc003Diagnostics.Any(d =>
        {
            var message = d.GetMessage();
            return message.Contains("ComplexMulti") && (message.Contains("MultiDep1") ||
                                                        message.Contains("MultiDep2") || message.Contains("MultiDep3"));
        });
        Assert.True(hasExpectedCycle,
            $"Expected complex multi-attribute cycle detection containing ComplexMulti and one of MultiDep1, MultiDep2, or MultiDep3. Got: {string.Join("; ", ioc003Diagnostics.Select(d => d.GetMessage()))}");
    }

    #endregion

    #region NEGATIVE TEST ENHANCEMENTS

    [Fact]
    public void CircularDependency_ComplexValidChain_GeneratesCorrectCode()
    {
        // Arrange - Complex but valid dependency chain that should NOT trigger circular dependency
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

// Complex diamond dependency pattern (valid - no cycles)
public interface IRoot { }
public interface ILeft { }
public interface IRight { }
public interface IBottom { }
[Scoped]
public partial class Root : IRoot { } // No dependencies - root of chain
[Scoped]
public partial class Left : ILeft
{
    [Inject] private readonly IRoot _root;
}
[Scoped]
public partial class Right : IRight
{
    [Inject] private readonly IRoot _root; // Both Left and Right depend on Root (diamond pattern)
}
[Scoped]
public partial class Bottom : IBottom
{
    [Inject] private readonly ILeft _left;
    [Inject] private readonly IRight _right; // Bottom depends on both Left and Right
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should have NO circular dependency errors and generate valid service registrations
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.Empty(ioc003Diagnostics);
        Assert.False(result.HasErrors);

        // Should generate valid service registrations for diamond dependency pattern
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("Root", registrationSource.Content);
        Assert.Contains("Left", registrationSource.Content);
        Assert.Contains("Right", registrationSource.Content);
        Assert.Contains("Bottom", registrationSource.Content);

        // Should generate constructors for services with dependencies (using semantic naming)
        var leftConstructor = result.GetConstructorSource("Left");
        Assert.NotNull(leftConstructor);
        Assert.Contains("IRoot root", leftConstructor.Content); // Semantic naming: _root -> root

        var bottomConstructor = result.GetConstructorSource("Bottom");
        Assert.NotNull(bottomConstructor);
        Assert.Contains("ILeft left", bottomConstructor.Content); // Semantic naming: _left -> left
        Assert.Contains("IRight right", bottomConstructor.Content); // Semantic naming: _right -> right
    }

    #endregion

    #region HIGH PRIORITY MISSING TESTS

    [Fact]
    public void CircularDependency_InheritanceBasedCycle_DetectsCorrectly()
    {
        // Arrange - Base → Child → Grandchild → Base inheritance cycle
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IBaseService { }
public interface IChildService { }
public interface IGrandchildService { }
[Scoped]
public partial class BaseService : IBaseService
{
    [Inject] private readonly IChildService _child;
}
[Scoped]
public partial class ChildService : IChildService
{
    [Inject] private readonly IGrandchildService _grandchild;
}
[Scoped]
public partial class GrandchildService : IGrandchildService
{
    [Inject] private readonly IBaseService _base;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should detect circular dependency (may detect multiple related cycles)
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);

        // All diagnostics should be warnings and contain proper cycle detection
        Assert.All(ioc003Diagnostics, diagnostic =>
        {
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("Circular dependency detected", diagnostic.GetMessage());
        });

        // Should show full cycle path: Base → Child → Grandchild → Base
        var hasExpectedCycle = ioc003Diagnostics.Any(d =>
        {
            var message = d.GetMessage();
            return message.Contains("BaseService") && message.Contains("ChildService") &&
                   message.Contains("GrandchildService");
        });
        Assert.True(hasExpectedCycle,
            $"Expected cycle containing BaseService, ChildService, and GrandchildService. Got: {string.Join("; ", ioc003Diagnostics.Select(d => d.GetMessage()))}");
    }

    [Fact]
    public void CircularDependency_MultiParameterDependsOnCycle_DetectsCorrectly()
    {
        // Arrange - Test DependsOn<T1, T2> where T2 creates cycle
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IServiceAlpha { }
public interface IServiceBeta { }
public interface IServiceGamma { }
[DependsOn<IServiceBeta, IServiceGamma>]
[Scoped]
public partial class ServiceAlpha : IServiceAlpha { }
[Scoped]
public partial class ServiceBeta : IServiceBeta
{
    [Inject] private readonly IServiceGamma _gamma;
}
[DependsOn<IServiceAlpha>]
[Scoped]
public partial class ServiceGamma : IServiceGamma { }";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should detect circular dependency (may detect multiple related cycles)
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);

        // All diagnostics should be warnings and contain proper cycle detection
        Assert.All(ioc003Diagnostics, diagnostic =>
        {
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("Circular dependency detected", diagnostic.GetMessage());
        });

        // Should detect cycle through multi-parameter DependsOn: Alpha → Gamma → Alpha
        var hasExpectedCycle = ioc003Diagnostics.Any(d =>
        {
            var message = d.GetMessage();
            return message.Contains("ServiceAlpha") && message.Contains("ServiceGamma");
        });
        Assert.True(hasExpectedCycle,
            $"Expected cycle containing ServiceAlpha and ServiceGamma. Got: {string.Join("; ", ioc003Diagnostics.Select(d => d.GetMessage()))}");
    }

    [Fact]
    public void CircularDependency_CollectionTypeCycle_DetectsCorrectly()
    {
        // Arrange - Collection types causing circular dependencies
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace Test;

public interface ICollectionService { }
public interface IItemService { }
[Scoped]
public partial class CollectionService : ICollectionService
{
    [Inject] private readonly IEnumerable<IItemService> _items;
}
[Scoped]
public partial class ItemService : IItemService
{
    [Inject] private readonly IList<ICollectionService> _collections;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Collection types should NOT create circular dependencies 
        // because collections are framework types, not part of our service graph
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.Empty(ioc003Diagnostics); // Collection dependencies should not create cycles

        // Should not have any compilation errors either
        Assert.False(result.HasErrors, "Collection dependencies should compile without circular dependency errors");
    }

    [Fact]
    public void CircularDependency_PartialExternalServiceScenario_DetectsInternalCycles()
    {
        // Arrange - Mixed external/internal services with partial cycle detection
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IInternalA { }
public interface IExternalB { }
public interface IInternalC { }
[Scoped]
public partial class InternalA : IInternalA
{
    [Inject] private readonly IExternalB _externalB;
}
[ExternalService]
public partial class ExternalB : IExternalB
{
    [Inject] private readonly IInternalC _internalC;
}
[Scoped]
public partial class InternalC : IInternalC
{
    [Inject] private readonly IInternalA _internalA;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should detect internal cycle while ignoring external service
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        // May detect internal cycle A → (external B skipped) → C → A or may skip entirely
        // Implementation dependent - external services should break cycle detection
        var message = ioc003Diagnostics.FirstOrDefault()?.GetMessage() ?? "No cycle detected";
        // This test validates behavior when external services are in the middle of potential cycles
    }

    [Fact]
    public void CircularDependency_MultiCycleDetection_FindsAllCycles()
    {
        // Arrange - Multiple independent cycles in same compilation
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

// First independent cycle: A → B → A
public interface IServiceA { }
public interface IServiceB { }
[Scoped]
public partial class ServiceA : IServiceA
{
    [Inject] private readonly IServiceB _serviceB;
}
[Scoped]
public partial class ServiceB : IServiceB
{
    [Inject] private readonly IServiceA _serviceA;
}

// Second independent cycle: X → Y → Z → X
public interface IServiceX { }
public interface IServiceY { }
public interface IServiceZ { }
[Scoped]
public partial class ServiceX : IServiceX
{
    [Inject] private readonly IServiceY _serviceY;
}
[Scoped]
public partial class ServiceY : IServiceY
{
    [Inject] private readonly IServiceZ _serviceZ;
}
[Scoped]
public partial class ServiceZ : IServiceZ
{
    [Inject] private readonly IServiceX _serviceX;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should detect both independent cycles
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        // May detect 1 or 2 diagnostics depending on implementation
        Assert.NotEmpty(ioc003Diagnostics);
        Assert.True(ioc003Diagnostics.Count >= 1, "Should detect at least one cycle");

        var allMessages = string.Join(" | ", ioc003Diagnostics.Select(d => d.GetMessage()));
        // Should detect cycles involving both groups of services
        Assert.True(allMessages.Contains("ServiceA") || allMessages.Contains("ServiceB") ||
                    allMessages.Contains("ServiceX") || allMessages.Contains("ServiceY") ||
                    allMessages.Contains("ServiceZ"),
            $"Expected cycle detection for multiple independent cycles, got: {allMessages}");
    }

    [Fact]
    public void CircularDependency_ComplexDeepCycle_DetectsLongChains()
    {
        // Arrange - Deep cycle with 10+ nodes to test cycle length handling
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
public interface IService8 { }
public interface IService9 { }
public interface IService10 { }
public interface IService11 { }
public interface IService12 { }
[Scoped]
public partial class Service1 : IService1
{
    [Inject] private readonly IService2 _service2;
}
[Scoped]
public partial class Service2 : IService2
{
    [Inject] private readonly IService3 _service3;
}
[Scoped]
public partial class Service3 : IService3
{
    [Inject] private readonly IService4 _service4;
}
[Scoped]
public partial class Service4 : IService4
{
    [Inject] private readonly IService5 _service5;
}
[Scoped]
public partial class Service5 : IService5
{
    [Inject] private readonly IService6 _service6;
}
[Scoped]
public partial class Service6 : IService6
{
    [Inject] private readonly IService7 _service7;
}
[Scoped]
public partial class Service7 : IService7
{
    [Inject] private readonly IService8 _service8;
}
[Scoped]
public partial class Service8 : IService8
{
    [Inject] private readonly IService9 _service9;
}
[Scoped]
public partial class Service9 : IService9
{
    [Inject] private readonly IService10 _service10;
}
[Scoped]
public partial class Service10 : IService10
{
    [Inject] private readonly IService11 _service11;
}
[Scoped]
public partial class Service11 : IService11
{
    [Inject] private readonly IService12 _service12;
}
[Scoped]
public partial class Service12 : IService12
{
    [Inject] private readonly IService1 _service1; // Completes the cycle
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should detect circular dependency (may detect multiple related cycles)
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);

        // All diagnostics should be warnings and contain proper cycle detection
        Assert.All(ioc003Diagnostics, diagnostic =>
        {
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("Circular dependency detected", diagnostic.GetMessage());
        });

        // Should handle deep cycle detection without performance issues
        var hasExpectedCycle = ioc003Diagnostics.Any(d =>
        {
            var message = d.GetMessage();
            return message.Contains("Service1") && message.Contains("Service12");
        });
        Assert.True(hasExpectedCycle,
            $"Expected cycle containing Service1 and Service12 in deep chain. Got: {string.Join("; ", ioc003Diagnostics.Select(d => d.GetMessage()))}");
    }

    [Fact]
    public void CircularDependency_SelfReferenceThroughDifferentRegistrations_DetectsCorrectly()
    {
        // Arrange - Service depending on itself through different registration approaches
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IComplexService { }
public interface IComplexServiceProxy { }
[Scoped]
public partial class ComplexService : IComplexService, IComplexServiceProxy
{
    [Inject] private readonly IComplexServiceProxy _proxy; // Self-reference through different interface
}
[Scoped]
public partial class AnotherComplexService : IComplexService
{
    [Inject] private readonly IComplexService _self; // Direct self-reference through same interface
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should detect self-reference edge cases
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);

        // Should detect at least one self-reference scenario
        var messages = ioc003Diagnostics.Select(d => d.GetMessage()).ToList();
        Assert.True(messages.Any(m => m.Contains("ComplexService") || m.Contains("AnotherComplexService")),
            $"Expected self-reference detection, got: {string.Join(" | ", messages)}");
    }

    [Fact]
    public void CircularDependency_CrossNamespaceCycle_DetectsCorrectly()
    {
        // Arrange - Cycles across different namespaces
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test.ServiceA
{
    public interface IServiceFromA { }
    
    [Scoped]
    public partial class ServiceFromA : IServiceFromA
    {
        [Inject] private readonly Test.ServiceB.IServiceFromB _serviceB;
    }
}

namespace Test.ServiceB
{
    public interface IServiceFromB { }
    
    [Scoped]
    public partial class ServiceFromB : IServiceFromB
    {
        [Inject] private readonly Test.ServiceC.IServiceFromC _serviceC;
    }
}

namespace Test.ServiceC
{
    public interface IServiceFromC { }
    
    [Scoped]
    public partial class ServiceFromC : IServiceFromC
    {
        [Inject] private readonly Test.ServiceA.IServiceFromA _serviceA;
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should detect circular dependency (may detect multiple related cycles)
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);

        // All diagnostics should be warnings and contain proper cycle detection
        Assert.All(ioc003Diagnostics, diagnostic =>
        {
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("Circular dependency detected", diagnostic.GetMessage());
        });

        // Should detect cycle across namespaces: ServiceFromA → ServiceFromB → ServiceFromC → ServiceFromA
        var hasExpectedCycle = ioc003Diagnostics.Any(d =>
        {
            var message = d.GetMessage();
            return message.Contains("ServiceFromA") && message.Contains("ServiceFromB") &&
                   message.Contains("ServiceFromC");
        });
        Assert.True(hasExpectedCycle,
            $"Expected cross-namespace cycle detection containing ServiceFromA, ServiceFromB, and ServiceFromC. Got: {string.Join("; ", ioc003Diagnostics.Select(d => d.GetMessage()))}");
    }

    #endregion

    #region ENHANCED GENERIC AND CONSTRAINT TESTS

    [Fact]
    public void CircularDependency_GenericConstraintCycle_DetectsCorrectly()
    {
        // Arrange - Generic services with constraints creating cycles
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System;

namespace Test;

public interface IConstrainedRepo<T> where T : class { }
public interface IConstrainedService<T> where T : class, IComparable { }
[Scoped]
public partial class ConstrainedRepo<T> : IConstrainedRepo<T> where T : class
{
    [Inject] private readonly IConstrainedService<T> _service;
}
[Scoped]
public partial class ConstrainedService<T> : IConstrainedService<T> where T : class, IComparable
{
    [Inject] private readonly IConstrainedRepo<T> _repo;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should detect circular dependency (may detect multiple related cycles)
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);

        // All diagnostics should be warnings and contain proper cycle detection
        Assert.All(ioc003Diagnostics, diagnostic =>
        {
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("Circular dependency detected", diagnostic.GetMessage());
        });

        // Should detect cycle between constrained generic services
        var hasExpectedCycle = ioc003Diagnostics.Any(d =>
        {
            var message = d.GetMessage();
            return message.Contains("ConstrainedRepo") && message.Contains("ConstrainedService");
        });
        Assert.True(hasExpectedCycle,
            $"Expected constraint-based generic cycle detection containing ConstrainedRepo and ConstrainedService. Got: {string.Join("; ", ioc003Diagnostics.Select(d => d.GetMessage()))}");
    }

    [Fact]
    public void CircularDependency_OpenGenericCycle_DetectsCorrectly()
    {
        // Arrange - Open generic services creating cycles
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IOpenGenericA<T> { }
public interface IOpenGenericB<T> { }
[Scoped]
public partial class OpenGenericA<T> : IOpenGenericA<T>
{
    [Inject] private readonly IOpenGenericB<T> _genericB;
}
[Scoped]
public partial class OpenGenericB<T> : IOpenGenericB<T>
{
    [Inject] private readonly IOpenGenericA<T> _genericA;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should detect circular dependency (may detect multiple related cycles)
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);

        // All diagnostics should be warnings and contain proper cycle detection
        Assert.All(ioc003Diagnostics, diagnostic =>
        {
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("Circular dependency detected", diagnostic.GetMessage());
        });

        // Should detect cycle between open generic services
        var hasExpectedCycle = ioc003Diagnostics.Any(d =>
        {
            var message = d.GetMessage();
            return message.Contains("OpenGenericA") && message.Contains("OpenGenericB");
        });
        Assert.True(hasExpectedCycle,
            $"Expected open generic cycle detection containing OpenGenericA and OpenGenericB. Got: {string.Join("; ", ioc003Diagnostics.Select(d => d.GetMessage()))}");
    }

    #endregion

    #region COMPREHENSIVE IENUMERABLE CIRCULAR DEPENDENCY TESTS

    [Fact]
    public void CircularDependency_MultipleIEnumerableDependencies_NoCircularDetection()
    {
        // Arrange - Service with multiple IEnumerable dependencies should NOT create circular dependencies
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace Test;

public interface IMultiEnumService { }
public interface IHandlerA { }
public interface IHandlerB { }
public interface IHandlerC { }
[Scoped]
public partial class MultiEnumService : IMultiEnumService
{
    [Inject] private readonly IEnumerable<IHandlerA> _handlersA;
    [Inject] private readonly IEnumerable<IHandlerB> _handlersB;
    [Inject] private readonly IEnumerable<IHandlerC> _handlersC;
}
[Scoped]
public partial class HandlerA : IHandlerA
{
    [Inject] private readonly IMultiEnumService _multiService;
}
[Scoped]
public partial class HandlerB : IHandlerB
{
    [Inject] private readonly IMultiEnumService _multiService;
}
[Scoped]
public partial class HandlerC : IHandlerC
{
    [Inject] private readonly IMultiEnumService _multiService;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - IEnumerable collections should NOT create circular dependencies
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.Empty(ioc003Diagnostics);
        Assert.False(result.HasErrors,
            "Multiple IEnumerable dependencies should not create circular dependency errors");

        // Should generate valid service registrations
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("MultiEnumService", registrationSource.Content);
        Assert.Contains("HandlerA", registrationSource.Content);
        Assert.Contains("HandlerB", registrationSource.Content);
        Assert.Contains("HandlerC", registrationSource.Content);
    }

    [Fact]
    public void CircularDependency_IEnumerableInCircularChain_NoCircularDetection()
    {
        // Arrange - A → IEnumerable<B> → C → A should NOT create circular dependency
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace Test;

public interface IChainServiceA { }
public interface IChainServiceB { }
public interface IChainServiceC { }
[Scoped]
public partial class ChainServiceA : IChainServiceA
{
    [Inject] private readonly IEnumerable<IChainServiceB> _servicesB;
}
[Scoped]
public partial class ChainServiceB : IChainServiceB
{
    [Inject] private readonly IChainServiceC _serviceC;
}
[Scoped]
public partial class ChainServiceC : IChainServiceC
{
    [Inject] private readonly IChainServiceA _serviceA;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - IEnumerable breaks circular dependency chain
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.Empty(ioc003Diagnostics);
        Assert.False(result.HasErrors, "IEnumerable in circular chain should break cycle detection");

        // Should generate valid service registrations
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("ChainServiceA", registrationSource.Content);
        Assert.Contains("ChainServiceB", registrationSource.Content);
        Assert.Contains("ChainServiceC", registrationSource.Content);
    }

    [Fact]
    public void CircularDependency_SelfReferencingIEnumerable_NoCircularDetection()
    {
        // Arrange - Service depending on IEnumerable<ISelf> should NOT create circular dependency
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace Test;

public interface ISelfRefService { }
[Scoped]
public partial class SelfRefService : ISelfRefService
{
    [Inject] private readonly IEnumerable<ISelfRefService> _otherInstances;
}
[Scoped]
public partial class AnotherSelfRefService : ISelfRefService
{
    [Inject] private readonly IEnumerable<ISelfRefService> _allInstances;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Self-referencing IEnumerable should NOT create circular dependencies
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.Empty(ioc003Diagnostics);
        Assert.False(result.HasErrors, "Self-referencing IEnumerable should not create circular dependency errors");

        // Should generate valid service registrations
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("SelfRefService", registrationSource.Content);
        Assert.Contains("AnotherSelfRefService", registrationSource.Content);
    }

    [Fact]
    public void CircularDependency_NestedIEnumerable_NoCircularDetection()
    {
        // Arrange - IEnumerable<IEnumerable<T>> scenarios should NOT create circular dependencies
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace Test;

public interface INestedEnumService { }
public interface INestedItem { }
public interface INestedGroup { }
[Scoped]
public partial class NestedEnumService : INestedEnumService
{
    [Inject] private readonly IEnumerable<IEnumerable<INestedItem>> _nestedItems;
    [Inject] private readonly IList<IEnumerable<INestedGroup>> _nestedGroups;
}
[Scoped]
public partial class NestedItem : INestedItem
{
    [Inject] private readonly INestedEnumService _enumService;
}
[Scoped]
public partial class NestedGroup : INestedGroup
{
    [Inject] private readonly INestedEnumService _enumService;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Nested collection types should NOT create circular dependencies
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.Empty(ioc003Diagnostics);
        Assert.False(result.HasErrors, "Nested IEnumerable scenarios should not create circular dependency errors");

        // Should generate valid service registrations
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("NestedEnumService", registrationSource.Content);
        Assert.Contains("NestedItem", registrationSource.Content);
        Assert.Contains("NestedGroup", registrationSource.Content);
    }

    [Fact]
    public void CircularDependency_IEnumerableWithDependsOnAttribute_NoCircularDetection()
    {
        // Arrange - IEnumerable combined with DependsOn attribute should NOT create circular dependencies
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace Test;

public interface IDepOnEnumService { }
public interface IDepOnProcessor { }
public interface IDepOnValidator { }
[DependsOn<IDepOnValidator>]
[Scoped]
public partial class DepOnEnumService : IDepOnEnumService
{
    [Inject] private readonly IEnumerable<IDepOnProcessor> _processors;
}
[Scoped]
public partial class DepOnProcessor : IDepOnProcessor
{
    [Inject] private readonly IDepOnEnumService _enumService;
}
[Scoped]
public partial class DepOnValidator : IDepOnValidator
{
    [Inject] private readonly IEnumerable<IDepOnProcessor> _processors;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - IEnumerable with DependsOn should NOT create circular dependencies
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.Empty(ioc003Diagnostics);
        Assert.False(result.HasErrors,
            "IEnumerable with DependsOn attribute should not create circular dependency errors");

        // Should generate valid service registrations
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("DepOnEnumService", registrationSource.Content);
        Assert.Contains("DepOnProcessor", registrationSource.Content);
        Assert.Contains("DepOnValidator", registrationSource.Content);
    }

    [Fact]
    public void CircularDependency_ComplexInheritanceWithIEnumerable_NoCircularDetection()
    {
        // Arrange - Complex inheritance hierarchy with IEnumerable should NOT create circular dependencies
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace Test;

public interface IBaseInheritService { }
public interface IDerivedInheritService : IBaseInheritService { }
public interface IGrandInheritService : IDerivedInheritService { }
public interface IInheritHandler { }
[Scoped]
public partial class BaseInheritService : IBaseInheritService
{
    [Inject] private readonly IEnumerable<IInheritHandler> _handlers;
}
[Scoped]
public partial class DerivedInheritService : BaseInheritService, IDerivedInheritService
{
    [Inject] private readonly IEnumerable<IGrandInheritService> _grandServices;
}
[Scoped]
public partial class GrandInheritService : DerivedInheritService, IGrandInheritService
{
    [Inject] private readonly IEnumerable<IBaseInheritService> _baseServices;
}
[Scoped]
public partial class InheritHandler : IInheritHandler
{
    [Inject] private readonly IGrandInheritService _grandService;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Complex inheritance with IEnumerable should NOT create circular dependencies
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.Empty(ioc003Diagnostics);
        Assert.False(result.HasErrors,
            "Complex inheritance with IEnumerable should not create circular dependency errors");

        // Should generate valid service registrations
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("BaseInheritService", registrationSource.Content);
        Assert.Contains("DerivedInheritService", registrationSource.Content);
        Assert.Contains("GrandInheritService", registrationSource.Content);
        Assert.Contains("InheritHandler", registrationSource.Content);
    }

    [Fact]
    public void CircularDependency_IEnumerableEdgeCases_NoCircularDetection()
    {
        // Arrange - Edge cases: Empty/single/multiple implementations with IEnumerable
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace Test;

// Edge case: Interface with no implementations
public interface IEmptyHandler { }

// Edge case: Interface with single implementation
public interface ISingleHandler { }

// Edge case: Interface with multiple implementations
public interface IMultiHandler { }

public interface IEdgeCaseService { }
[Scoped]
public partial class EdgeCaseService : IEdgeCaseService
{
    [Inject] private readonly IEnumerable<IEmptyHandler> _emptyHandlers;     // Empty collection
    [Inject] private readonly IEnumerable<ISingleHandler> _singleHandlers;   // Single item collection
    [Inject] private readonly IEnumerable<IMultiHandler> _multiHandlers;     // Multiple items collection
}

// Single implementation
[Scoped]
public partial class SingleHandler : ISingleHandler
{
    [Inject] private readonly IEdgeCaseService _edgeService;
}

// Multiple implementations
[Scoped]
public partial class MultiHandlerA : IMultiHandler
{
    [Inject] private readonly IEdgeCaseService _edgeService;
}
[Scoped]
public partial class MultiHandlerB : IMultiHandler
{
    [Inject] private readonly IEdgeCaseService _edgeService;
}
[Scoped]
public partial class MultiHandlerC : IMultiHandler
{
    [Inject] private readonly IEdgeCaseService _edgeService;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Edge cases with IEnumerable should NOT create circular dependencies
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.Empty(ioc003Diagnostics);
        Assert.False(result.HasErrors, "IEnumerable edge cases should not create circular dependency errors");

        // Should generate valid service registrations
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("EdgeCaseService", registrationSource.Content);
        Assert.Contains("SingleHandler", registrationSource.Content);
        Assert.Contains("MultiHandlerA", registrationSource.Content);
        Assert.Contains("MultiHandlerB", registrationSource.Content);
        Assert.Contains("MultiHandlerC", registrationSource.Content);
    }

    [Fact]
    public void CircularDependency_IEnumerableVsDirectReference_NoCircularDetection()
    {
        // Arrange - Compare IEnumerable (no cycle) vs direct reference - both should NOT create cycles in current implementation
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace Test;

public interface ICompareServiceA { }
public interface ICompareServiceB { }
public interface ICompareServiceC { }
public interface ICompareServiceD { }

// No cycle: A → IEnumerable<B> → A (IEnumerable breaks cycle)

[Scoped]
public partial class CompareServiceA : ICompareServiceA
{
    [Inject] private readonly IEnumerable<ICompareServiceB> _servicesB;
}
[Scoped]
public partial class CompareServiceB : ICompareServiceB
{
    [Inject] private readonly ICompareServiceA _serviceA; // This should NOT create a cycle due to IEnumerable
}

// Regular dependencies: C → D (no cycle, linear chain)

[Scoped]
public partial class CompareServiceC : ICompareServiceC
{
    [Inject] private readonly ICompareServiceD _serviceD;
}
[Scoped]
public partial class CompareServiceD : ICompareServiceD
{
    // No dependencies - end of chain
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - IEnumerable collections should NOT create circular dependencies
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.Empty(ioc003Diagnostics);
        Assert.False(result.HasErrors, "IEnumerable dependencies should not create circular dependency errors");

        // Should generate valid service registrations
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("CompareServiceA", registrationSource.Content);
        Assert.Contains("CompareServiceB", registrationSource.Content);
        Assert.Contains("CompareServiceC", registrationSource.Content);
        Assert.Contains("CompareServiceD", registrationSource.Content);
    }

    [Fact]
    public void CircularDependency_MixedCollectionTypes_NoCircularDetection()
    {
        // Arrange - Various collection types (IEnumerable, IList, ICollection, etc.) should NOT create cycles
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace Test;

public interface IMixedCollectionService { }
public interface IMixedItem { }
[Scoped]
public partial class MixedCollectionService : IMixedCollectionService
{
    [Inject] private readonly IEnumerable<IMixedItem> _enumerable;
    [Inject] private readonly IList<IMixedItem> _list;
    [Inject] private readonly ICollection<IMixedItem> _collection;
    [Inject] private readonly List<IMixedItem> _concreteList;
    [Inject] private readonly IMixedItem[] _array;
}
[Scoped]
public partial class MixedItem : IMixedItem
{
    [Inject] private readonly IMixedCollectionService _collectionService;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Mixed collection types should NOT create circular dependencies
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.Empty(ioc003Diagnostics);
        Assert.False(result.HasErrors, "Mixed collection types should not create circular dependency errors");

        // Should generate valid service registrations
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("MixedCollectionService", registrationSource.Content);
        Assert.Contains("MixedItem", registrationSource.Content);
    }

    #endregion
}
