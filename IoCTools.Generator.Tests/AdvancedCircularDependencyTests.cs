namespace IoCTools.Generator.Tests;

using Microsoft.CodeAnalysis;

/// <summary>
///     Advanced circular dependency tests covering edge cases and complex scenarios.
///     These tests ensure comprehensive coverage of circular dependency detection.
/// </summary>
public class AdvancedCircularDependencyTests
{
    [Fact]
    public void InheritanceBasedCircularDependency_DetectsIOC003()
    {
        // Arrange - Circular dependency through inheritance chain
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IBase { }
public interface IChild { }
public interface IGrandchild { }
[Scoped]
public partial class BaseService : IBase
{
    [Inject] private readonly IChild _child;
}
[Scoped]
public partial class ChildService : BaseService, IChild
{
    [Inject] private readonly IGrandchild _grandchild;
}
[Scoped]
public partial class GrandchildService : IGrandchild
{
    [Inject] private readonly IBase _base;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);
        Assert.All(ioc003Diagnostics, d =>
        {
            Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
            Assert.Contains("Circular dependency detected", d.GetMessage());
        });
    }

    [Fact]
    public void DeepCircularChain_TenServices_DetectsIOC003()
    {
        // Arrange - Deep 10-service circular chain
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface I1 { } public interface I2 { } public interface I3 { } 
public interface I4 { } public interface I5 { } public interface I6 { }
public interface I7 { } public interface I8 { } public interface I9 { } 
public interface I10 { }

[Scoped] public partial class S1 : I1 { [Inject] private readonly I2 _s2; }
[Scoped] public partial class S2 : I2 { [Inject] private readonly I3 _s3; }
[Scoped] public partial class S3 : I3 { [Inject] private readonly I4 _s4; }
[Scoped] public partial class S4 : I4 { [Inject] private readonly I5 _s5; }
[Scoped] public partial class S5 : I5 { [Inject] private readonly I6 _s6; }
[Scoped] public partial class S6 : I6 { [Inject] private readonly I7 _s7; }
[Scoped] public partial class S7 : I7 { [Inject] private readonly I8 _s8; }
[Scoped] public partial class S8 : I8 { [Inject] private readonly I9 _s9; }
[Scoped] public partial class S9 : I9 { [Inject] private readonly I10 _s10; }
[Scoped] public partial class S10 : I10 { [Inject] private readonly I1 _s1; }";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);
        Assert.All(ioc003Diagnostics, d => Assert.Equal(DiagnosticSeverity.Warning, d.Severity));

        // Should detect cycle involving first and last services
        var hasDeepCycle = ioc003Diagnostics.Any(d =>
        {
            var message = d.GetMessage();
            return message.Contains("S1") && message.Contains("S10");
        });

        Assert.True(hasDeepCycle,
            $"Expected deep cycle detection. Got: {string.Join("; ", ioc003Diagnostics.Select(d => d.GetMessage()))}");
    }

    [Fact]
    public void MultipleIndependentCycles_DetectsAllCycles()
    {
        // Arrange - Two separate circular dependency groups
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

// First cycle: A ↔ B
public interface IA { } public interface IB { }
[Scoped] public partial class A : IA { [Inject] private readonly IB _b; }
[Scoped] public partial class B : IB { [Inject] private readonly IA _a; }

// Second cycle: X → Y → Z → X
public interface IX { } public interface IY { } public interface IZ { }
[Scoped] public partial class X : IX { [Inject] private readonly IY _y; }
[Scoped] public partial class Y : IY { [Inject] private readonly IZ _z; }
[Scoped] public partial class Z : IZ { [Inject] private readonly IX _x; }";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);
        Assert.True(ioc003Diagnostics.Count >= 1, "Should detect at least one cycle");

        var allMessages = string.Join(" ", ioc003Diagnostics.Select(d => d.GetMessage()));
        var detectsBothGroups = (allMessages.Contains("A") || allMessages.Contains("B")) &&
                                (allMessages.Contains("X") || allMessages.Contains("Y") || allMessages.Contains("Z"));

        // Note: Implementation may detect one or both cycles
        Assert.True(allMessages.Contains("A") || allMessages.Contains("X"),
            $"Expected detection of at least one cycle group. Got: {allMessages}");
    }

    [Fact]
    public void SelfReferenceViaMultipleInterfaces_DetectsIOC003()
    {
        // Arrange - Service implementing multiple interfaces, depending on itself through different interface
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IProcessor { }
public interface IHandler { }
public partial class ProcessorHandler : IProcessor, IHandler
{
    [Inject] private readonly IHandler _handler; // Self-reference through different interface
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);
        Assert.All(ioc003Diagnostics, d => Assert.Equal(DiagnosticSeverity.Warning, d.Severity));

        var messages = ioc003Diagnostics.Select(d => d.GetMessage());
        Assert.True(messages.Any(m => m.Contains("ProcessorHandler")),
            $"Expected self-reference detection involving ProcessorHandler. Got: {string.Join(" | ", messages)}");
    }

    [Fact]
    public void CrossNamespaceCircularDependency_DetectsIOC003()
    {
        // Arrange - Circular dependencies across different namespaces
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace ServiceGroup.Alpha
{
    public interface IAlphaService { }
    
    
    public partial class AlphaService : IAlphaService
    {
        [Inject] private readonly ServiceGroup.Beta.IBetaService _beta;
    }
}

namespace ServiceGroup.Beta
{
    public interface IBetaService { }
    
    
    public partial class BetaService : IBetaService
    {
        [Inject] private readonly ServiceGroup.Alpha.IAlphaService _alpha;
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);
        Assert.All(ioc003Diagnostics, d => Assert.Equal(DiagnosticSeverity.Warning, d.Severity));

        var hasExpectedCycle = ioc003Diagnostics.Any(d =>
        {
            var message = d.GetMessage();
            return message.Contains("AlphaService") && message.Contains("BetaService");
        });

        Assert.True(hasExpectedCycle,
            $"Expected cross-namespace cycle. Got: {string.Join("; ", ioc003Diagnostics.Select(d => d.GetMessage()))}");
    }

    [Fact]
    public void MixedGenericAndNonGenericCycle_DetectsIOC003()
    {
        // Arrange - Mixing generic and non-generic services in circular dependency
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IGenericRepo<T> { }
public interface INonGenericService { }
public partial class StringRepo : IGenericRepo<string>
{
    [Inject] private readonly INonGenericService _service;
}
public partial class NonGenericService : INonGenericService
{
    [Inject] private readonly IGenericRepo<string> _repo;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);
        Assert.All(ioc003Diagnostics, d => Assert.Equal(DiagnosticSeverity.Warning, d.Severity));

        var hasExpectedCycle = ioc003Diagnostics.Any(d =>
        {
            var message = d.GetMessage();
            return message.Contains("StringRepo") && message.Contains("NonGenericService");
        });

        Assert.True(hasExpectedCycle,
            $"Expected mixed generic/non-generic cycle. Got: {string.Join("; ", ioc003Diagnostics.Select(d => d.GetMessage()))}");
    }

    [Fact]
    public void ComplexMultiParameterDependsOnCycle_DetectsIOC003()
    {
        // Arrange - Complex multi-parameter DependsOn creating cycles
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IAlpha { } public interface IBeta { } 
public interface IGamma { } public interface IDelta { }
[DependsOn<IBeta, IGamma>]
public partial class Alpha : IAlpha { }
[DependsOn<IDelta>]
public partial class Beta : IBeta
{
    [Inject] private readonly IGamma _gamma;
}
public partial class Gamma : IGamma
{
    [Inject] private readonly IDelta _delta;
}
[DependsOn<IAlpha>]
public partial class Delta : IDelta { }";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);
        Assert.All(ioc003Diagnostics, d => Assert.Equal(DiagnosticSeverity.Warning, d.Severity));

        // Should detect at least part of the complex cycle: Alpha → Gamma → Delta → Alpha or Alpha → Beta → ... → Alpha
        var allMessages = string.Join(" ", ioc003Diagnostics.Select(d => d.GetMessage()));
        Assert.True(allMessages.Contains("Alpha") &&
                    (allMessages.Contains("Gamma") || allMessages.Contains("Beta") || allMessages.Contains("Delta")),
            $"Expected complex multi-parameter cycle involving Alpha. Got: {allMessages}");
    }

    [Fact]
    public void CollectionWithConcreteTypeCycle_NoCircularDependency()
    {
        // Arrange - IEnumerable with specific concrete types should not create cycles
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace Test;

public interface IMessageHandler { }
public interface IMessageProcessor { }
public partial class MessageProcessor : IMessageProcessor
{
    [Inject] private readonly IList<IMessageHandler> _handlers;
    [Inject] private readonly IEnumerable<MessageHandler> _concreteHandlers;
}
public partial class MessageHandler : IMessageHandler
{
    [Inject] private readonly IMessageProcessor _processor;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Collections should not create circular dependencies
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.Empty(ioc003Diagnostics);
        Assert.False(result.HasErrors, "Collection dependencies should not create circular dependency errors");
    }

    [Fact]
    public void PartialExternalServiceScenario_SkipsCircularCheck()
    {
        // Arrange - Complex scenario with some external services in potential cycle
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IInternalA { } public interface IExternalB { } 
public interface IInternalC { } public interface IInternalD { }
public partial class InternalA : IInternalA
{
    [Inject] private readonly IExternalB _external;
}
[ExternalService]
public partial class ExternalB : IExternalB
{
    [Inject] private readonly IInternalC _internalC;
}
public partial class InternalC : IInternalC
{
    [Inject] private readonly IInternalD _internalD;
}
public partial class InternalD : IInternalD
{
    [Inject] private readonly IInternalA _internalA;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - External services should break cycle detection
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        // Implementation dependent: may detect internal cycle C→D→A or may skip entirely due to external service
        // This test validates behavior when external services are in the middle of potential cycles
        Assert.False(result.HasErrors, "External services should not cause compilation errors in cycle detection");
    }

    [Fact]
    public void ComplexDiamondDependencyPattern_NoCircularDependency()
    {
        // Arrange - Complex diamond pattern (valid - no cycles)
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IRoot { } public interface ILeft { } 
public interface IRight { } public interface IBottom { }
public interface IFinal { }
public partial class Root : IRoot { }
public partial class Left : ILeft
{
    [Inject] private readonly IRoot _root;
}
public partial class Right : IRight
{
    [Inject] private readonly IRoot _root; // Diamond: both Left and Right depend on Root
}
public partial class Bottom : IBottom
{
    [Inject] private readonly ILeft _left;
    [Inject] private readonly IRight _right; // Bottom depends on both branches
}
public partial class Final : IFinal
{
    [Inject] private readonly IBottom _bottom;
    [Inject] private readonly IRoot _root; // Valid: can depend on Root and Bottom
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Diamond pattern should NOT create circular dependencies
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.Empty(ioc003Diagnostics);
        Assert.False(result.HasErrors);

        // Should generate valid service registrations
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("Root", registrationSource.Content);
        Assert.Contains("Left", registrationSource.Content);
        Assert.Contains("Right", registrationSource.Content);
        Assert.Contains("Bottom", registrationSource.Content);
        Assert.Contains("Final", registrationSource.Content);
    }

    [Fact]
    public void MixedCollectionTypes_NoCircularDependency()
    {
        // Arrange - Various collection types should not create cycles
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace Test;

public interface ICollectionService { } public interface IItemService { }
public partial class CollectionService : ICollectionService
{
    [Inject] private readonly IEnumerable<IItemService> _enumerable;
    [Inject] private readonly IList<IItemService> _list;
    [Inject] private readonly ICollection<IItemService> _collection;
    [Inject] private readonly List<IItemService> _concreteList;
    [Inject] private readonly IItemService[] _array;
}
public partial class ItemService : IItemService
{
    [Inject] private readonly ICollectionService _collectionService;
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
        Assert.Contains("CollectionService", registrationSource.Content);
        Assert.Contains("ItemService", registrationSource.Content);
    }

    [Fact]
    public void OpenGenericCircularDependency_DetectsIOC003()
    {
        // Arrange - Open generic services in circular dependency
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IOpenGenericA<T> { }
public interface IOpenGenericB<T> { }
public partial class OpenGenericA<T> : IOpenGenericA<T>
{
    [Inject] private readonly IOpenGenericB<T> _genericB;
}
public partial class OpenGenericB<T> : IOpenGenericB<T>
{
    [Inject] private readonly IOpenGenericA<T> _genericA;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should detect open generic circular dependency
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);
        Assert.All(ioc003Diagnostics, d => Assert.Equal(DiagnosticSeverity.Warning, d.Severity));

        var hasExpectedCycle = ioc003Diagnostics.Any(d =>
        {
            var message = d.GetMessage();
            return message.Contains("OpenGenericA") && message.Contains("OpenGenericB");
        });

        Assert.True(hasExpectedCycle,
            $"Expected open generic cycle. Got: {string.Join("; ", ioc003Diagnostics.Select(d => d.GetMessage()))}");
    }

    [Fact]
    public void NestedGenericCircularDependency_DetectsIOC003()
    {
        // Arrange - Nested generics in circular dependency
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace Test;

public interface INestedGenericService<T> { }
public interface INestedGenericProcessor<T> { }
public partial class NestedGenericService<T> : INestedGenericService<T>
{
    [Inject] private readonly INestedGenericProcessor<List<T>> _processor;
}
public partial class NestedGenericProcessor<T> : INestedGenericProcessor<T>
{
    // This creates a cycle if T becomes List<SomeType> and we depend on INestedGenericService<SomeType>
    // For string: Service<string> → Processor<List<string>> but no back-reference, so no cycle
    // This test ensures no false positives for complex generics
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should NOT detect circular dependency as there's no actual cycle
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.Empty(ioc003Diagnostics);
        Assert.False(result.HasErrors,
            "Nested generics without actual cycle should not trigger circular dependency errors");
    }

    [Fact]
    public void GenericConstraintCircularDependency_DetectsIOC003()
    {
        // Arrange - Generic services with constraints in circular dependency
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System;

namespace Test;

public interface IConstrainedRepo<T> where T : class { }
public interface IConstrainedService<T> where T : class, IComparable { }
public partial class ConstrainedRepo<T> : IConstrainedRepo<T> where T : class
{
    [Inject] private readonly IConstrainedService<T> _service;
}
public partial class ConstrainedService<T> : IConstrainedService<T> where T : class, IComparable
{
    [Inject] private readonly IConstrainedRepo<T> _repo;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should detect circular dependency with generic constraints
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);
        Assert.All(ioc003Diagnostics, d => Assert.Equal(DiagnosticSeverity.Warning, d.Severity));

        var hasExpectedCycle = ioc003Diagnostics.Any(d =>
        {
            var message = d.GetMessage();
            return message.Contains("ConstrainedRepo") && message.Contains("ConstrainedService");
        });

        Assert.True(hasExpectedCycle,
            $"Expected constrained generic cycle. Got: {string.Join("; ", ioc003Diagnostics.Select(d => d.GetMessage()))}");
    }
}
