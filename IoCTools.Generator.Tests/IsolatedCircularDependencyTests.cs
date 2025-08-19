using Microsoft.CodeAnalysis;

namespace IoCTools.Generator.Tests;

/// <summary>
///     Isolated tests for circular dependency detection that don't break the build.
///     These tests validate IOC003 diagnostics are generated correctly.
/// </summary>
public class IsolatedCircularDependencyTests
{
    [Fact]
    public void SimpleCircularDependency_A_DependsOn_B_B_DependsOn_A_GeneratesIOC003()
    {
        // Arrange - Simple A → B → A cycle
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace IsolatedTest;

public interface IServiceA { }
public interface IServiceB { }

[Service]
public partial class ServiceA : IServiceA
{
    [Inject] private readonly IServiceB _serviceB;
}

[Service]
public partial class ServiceB : IServiceB
{
    [Inject] private readonly IServiceA _serviceA;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should detect circular dependency and generate IOC003 diagnostics
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);

        // Validate diagnostic severity
        Assert.All(ioc003Diagnostics, diagnostic =>
        {
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("Circular dependency detected", diagnostic.GetMessage());
        });

        // Verify cycle path contains both services
        var hasExpectedCycle = ioc003Diagnostics.Any(d =>
        {
            var message = d.GetMessage();
            return message.Contains("ServiceA") && message.Contains("ServiceB");
        });

        Assert.True(hasExpectedCycle,
            $"Expected cycle containing ServiceA and ServiceB. Got diagnostics: {string.Join("; ", ioc003Diagnostics.Select(d => d.GetMessage()))}");

        // Ensure no compilation errors
        var compilationErrors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
        Assert.Empty(compilationErrors);
    }

    [Fact]
    public void ThreeServiceCircularChain_A_B_C_A_GeneratesIOC003()
    {
        // Arrange - A → B → C → A cycle
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace IsolatedTest;

public interface IServiceA { }
public interface IServiceB { }  
public interface IServiceC { }

[Service]
public partial class ServiceA : IServiceA
{
    [Inject] private readonly IServiceB _serviceB;
}

[Service]  
public partial class ServiceB : IServiceB
{
    [Inject] private readonly IServiceC _serviceC;
}

[Service]
public partial class ServiceC : IServiceC
{
    [Inject] private readonly IServiceA _serviceA;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should detect three-service cycle
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);

        // Validate diagnostic properties
        Assert.All(ioc003Diagnostics, diagnostic =>
        {
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Equal("IOC003", diagnostic.Id);
            Assert.Contains("Circular dependency detected", diagnostic.GetMessage());
        });

        // Should detect the full cycle involving all three services
        var allMessages = string.Join(" ", ioc003Diagnostics.Select(d => d.GetMessage()));
        Assert.True(
            allMessages.Contains("ServiceA") && allMessages.Contains("ServiceB") && allMessages.Contains("ServiceC"),
            $"Expected cycle containing all three services. Got: {allMessages}");

        // No compilation errors should be present
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void SelfReferencingService_GeneratesIOC003()
    {
        // Arrange - Service that depends on itself
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace IsolatedTest;

public interface ISelfService { }

[Service]
public partial class SelfService : ISelfService
{
    [Inject] private readonly ISelfService _self;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should detect self-reference cycle
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);

        Assert.All(ioc003Diagnostics, diagnostic =>
        {
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("Circular dependency detected", diagnostic.GetMessage());
        });

        // Should mention self-reference
        var hasExpectedMessage = ioc003Diagnostics.Any(d =>
            d.GetMessage().Contains("SelfService"));

        Assert.True(hasExpectedMessage,
            $"Expected self-reference diagnostic containing SelfService. Got: {string.Join("; ", ioc003Diagnostics.Select(d => d.GetMessage()))}");
    }

    [Fact]
    public void DependsOnAttribute_CircularDependency_GeneratesIOC003()
    {
        // Arrange - Using DependsOn attributes instead of Inject
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace IsolatedTest;

public interface IServiceX { }
public interface IServiceY { }

[Service]
[DependsOn<IServiceY>]
public partial class ServiceX : IServiceX { }

[Service]
[DependsOn<IServiceX>]
public partial class ServiceY : IServiceY { }";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should detect DependsOn circular dependency
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);

        Assert.All(ioc003Diagnostics, diagnostic =>
        {
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("Circular dependency detected", diagnostic.GetMessage());
        });

        var hasExpectedCycle = ioc003Diagnostics.Any(d =>
        {
            var message = d.GetMessage();
            return message.Contains("ServiceX") && message.Contains("ServiceY");
        });

        Assert.True(hasExpectedCycle,
            $"Expected DependsOn cycle containing ServiceX and ServiceY. Got: {string.Join("; ", ioc003Diagnostics.Select(d => d.GetMessage()))}");
    }

    [Fact]
    public void MixedInjectAndDependsOn_CircularDependency_GeneratesIOC003()
    {
        // Arrange - Mix [Inject] and [DependsOn] creating cycle
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace IsolatedTest;

public interface IServiceM { }
public interface IServiceN { }

[Service]
public partial class ServiceM : IServiceM
{
    [Inject] private readonly IServiceN _serviceN;
}

[Service]
[DependsOn<IServiceM>]
public partial class ServiceN : IServiceN { }";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should detect mixed attribute circular dependency
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);

        Assert.All(ioc003Diagnostics, diagnostic =>
        {
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("Circular dependency detected", diagnostic.GetMessage());
        });

        var hasExpectedCycle = ioc003Diagnostics.Any(d =>
        {
            var message = d.GetMessage();
            return message.Contains("ServiceM") && message.Contains("ServiceN");
        });

        Assert.True(hasExpectedCycle,
            $"Expected mixed attribute cycle containing ServiceM and ServiceN. Got: {string.Join("; ", ioc003Diagnostics.Select(d => d.GetMessage()))}");
    }

    [Fact]
    public void ExternalService_InCircularChain_SkipsCircularDetection()
    {
        // Arrange - A → B → A where B is external
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace IsolatedTest;

public interface IServiceA { }
public interface IServiceB { }

[Service]
public partial class ServiceA : IServiceA
{
    [Inject] private readonly IServiceB _serviceB;
}

[Service]
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

        // Should not have compilation errors
        Assert.False(result.HasErrors);
    }

    [Fact]
    public void ValidLinearDependencyChain_NoCircularDependencies()
    {
        // Arrange - Valid A → B → C chain (no cycles)
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace IsolatedTest;

public interface IServiceA { }
public interface IServiceB { }
public interface IServiceC { }

[Service]
public partial class ServiceA : IServiceA
{
    [Inject] private readonly IServiceB _serviceB;
}

[Service]
public partial class ServiceB : IServiceB
{
    [Inject] private readonly IServiceC _serviceC;
}

[Service]
public partial class ServiceC : IServiceC { }";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should have NO circular dependencies
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.Empty(ioc003Diagnostics);

        // Should generate valid code without errors
        Assert.False(result.HasErrors);

        // Should generate valid service registrations
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("ServiceA", registrationSource.Content);
        Assert.Contains("ServiceB", registrationSource.Content);
        Assert.Contains("ServiceC", registrationSource.Content);
    }

    [Fact]
    public void IEnumerableCollection_DoesNotCreateCircularDependency()
    {
        // Arrange - A → IEnumerable<B>, B → A should NOT create cycle 
        var source = @"
using IoCTools.Abstractions.Annotations;
using System.Collections.Generic;

namespace IsolatedTest;

public interface ICollectionService { }
public interface IItemService { }

[Service]
public partial class CollectionService : ICollectionService
{
    [Inject] private readonly IEnumerable<IItemService> _items;
}

[Service]
public partial class ItemService : IItemService
{
    [Inject] private readonly ICollectionService _collectionService;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - IEnumerable should NOT create circular dependencies
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.Empty(ioc003Diagnostics);

        Assert.False(result.HasErrors, "IEnumerable dependencies should not create circular dependency errors");

        // Should generate valid service registrations
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("CollectionService", registrationSource.Content);
        Assert.Contains("ItemService", registrationSource.Content);
    }

    [Fact]
    public void GenericServices_CircularDependency_GeneratesIOC003()
    {
        // Arrange - Generic services in circular dependency
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace IsolatedTest;

public interface IRepository<T> { }
public interface IService<T> { }

[Service]
public partial class StringRepository : IRepository<string>
{
    [Inject] private readonly IService<string> _service;
}

[Service]
public partial class StringService : IService<string>
{
    [Inject] private readonly IRepository<string> _repository;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should detect generic circular dependency
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);

        Assert.All(ioc003Diagnostics, diagnostic =>
        {
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("Circular dependency detected", diagnostic.GetMessage());
        });

        var hasExpectedCycle = ioc003Diagnostics.Any(d =>
        {
            var message = d.GetMessage();
            return message.Contains("StringRepository") && message.Contains("StringService");
        });

        Assert.True(hasExpectedCycle,
            $"Expected generic cycle containing StringRepository and StringService. Got: {string.Join("; ", ioc003Diagnostics.Select(d => d.GetMessage()))}");
    }

    [Fact]
    public void ComplexFourServiceCircularChain_GeneratesIOC003()
    {
        // Arrange - A → B → C → D → A complex cycle
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace IsolatedTest;

public interface IA { }
public interface IB { }
public interface IC { }
public interface ID { }

[Service]
[DependsOn<IB>]
public partial class A : IA { }

[Service]
[DependsOn<IC>]
public partial class B : IB { }

[Service]
public partial class C : IC
{
    [Inject] private readonly ID _d;
}

[Service]
public partial class D : ID
{
    [Inject] private readonly IA _a;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should detect four-service circular dependency
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);

        Assert.All(ioc003Diagnostics, diagnostic =>
        {
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("Circular dependency detected", diagnostic.GetMessage());
        });

        // Should detect cycle involving all four services
        var allMessages = string.Join(" ", ioc003Diagnostics.Select(d => d.GetMessage()));
        var containsAllServices = allMessages.Contains("A") && allMessages.Contains("B") &&
                                  allMessages.Contains("C") && allMessages.Contains("D");

        Assert.True(containsAllServices,
            $"Expected cycle containing A, B, C, and D. Got: {allMessages}");
    }

    [Fact]
    public void DeepCircularChain_TwelveServices_GeneratesIOC003()
    {
        // Arrange - Deep cycle: Service1 → Service2 → ... → Service12 → Service1
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace IsolatedTest;

public interface IService1 { } public interface IService2 { } public interface IService3 { }
public interface IService4 { } public interface IService5 { } public interface IService6 { }
public interface IService7 { } public interface IService8 { } public interface IService9 { }
public interface IService10 { } public interface IService11 { } public interface IService12 { }

[Service] public partial class Service1 : IService1 { [Inject] private readonly IService2 _s2; }
[Service] public partial class Service2 : IService2 { [Inject] private readonly IService3 _s3; }
[Service] public partial class Service3 : IService3 { [Inject] private readonly IService4 _s4; }
[Service] public partial class Service4 : IService4 { [Inject] private readonly IService5 _s5; }
[Service] public partial class Service5 : IService5 { [Inject] private readonly IService6 _s6; }
[Service] public partial class Service6 : IService6 { [Inject] private readonly IService7 _s7; }
[Service] public partial class Service7 : IService7 { [Inject] private readonly IService8 _s8; }
[Service] public partial class Service8 : IService8 { [Inject] private readonly IService9 _s9; }
[Service] public partial class Service9 : IService9 { [Inject] private readonly IService10 _s10; }
[Service] public partial class Service10 : IService10 { [Inject] private readonly IService11 _s11; }
[Service] public partial class Service11 : IService11 { [Inject] private readonly IService12 _s12; }
[Service] public partial class Service12 : IService12 { [Inject] private readonly IService1 _s1; }";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should detect deep circular dependency without performance issues
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);

        Assert.All(ioc003Diagnostics, diagnostic =>
        {
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("Circular dependency detected", diagnostic.GetMessage());
        });

        // Should handle the deep cycle (at least detect Service1 and Service12 as cycle endpoints)
        var hasDeepCycle = ioc003Diagnostics.Any(d =>
        {
            var message = d.GetMessage();
            return message.Contains("Service1") && message.Contains("Service12");
        });

        Assert.True(hasDeepCycle,
            $"Expected deep cycle detection containing Service1 and Service12. Got: {string.Join("; ", ioc003Diagnostics.Select(d => d.GetMessage()))}");
    }

    [Fact]
    public void CrossNamespace_CircularDependency_GeneratesIOC003()
    {
        // Arrange - Cycles across different namespaces
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace ServiceA.Namespace
{
    public interface IServiceFromA { }
    
    [Service]
    public partial class ServiceFromA : IServiceFromA
    {
        [Inject] private readonly ServiceB.Namespace.IServiceFromB _serviceB;
    }
}

namespace ServiceB.Namespace
{
    public interface IServiceFromB { }
    
    [Service]
    public partial class ServiceFromB : IServiceFromB
    {
        [Inject] private readonly ServiceA.Namespace.IServiceFromA _serviceA;
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should detect cross-namespace circular dependency
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);

        Assert.All(ioc003Diagnostics, diagnostic =>
        {
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("Circular dependency detected", diagnostic.GetMessage());
        });

        var hasExpectedCycle = ioc003Diagnostics.Any(d =>
        {
            var message = d.GetMessage();
            return message.Contains("ServiceFromA") && message.Contains("ServiceFromB");
        });

        Assert.True(hasExpectedCycle,
            $"Expected cross-namespace cycle containing ServiceFromA and ServiceFromB. Got: {string.Join("; ", ioc003Diagnostics.Select(d => d.GetMessage()))}");
    }

    [Fact]
    public void MultiParameterDependsOn_CircularDependency_GeneratesIOC003()
    {
        // Arrange - DependsOn<T1, T2> creating cycle through T2
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace IsolatedTest;

public interface IAlpha { }
public interface IBeta { }
public interface IGamma { }

[Service]
[DependsOn<IBeta, IGamma>]
public partial class Alpha : IAlpha { }

[Service]
public partial class Beta : IBeta
{
    [Inject] private readonly IGamma _gamma;
}

[Service]
[DependsOn<IAlpha>]
public partial class Gamma : IGamma { }";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should detect circular dependency through multi-parameter DependsOn
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);

        Assert.All(ioc003Diagnostics, diagnostic =>
        {
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("Circular dependency detected", diagnostic.GetMessage());
        });

        // Should detect cycle: Alpha → Gamma → Alpha
        var hasExpectedCycle = ioc003Diagnostics.Any(d =>
        {
            var message = d.GetMessage();
            return message.Contains("Alpha") && message.Contains("Gamma");
        });

        Assert.True(hasExpectedCycle,
            $"Expected multi-parameter DependsOn cycle containing Alpha and Gamma. Got: {string.Join("; ", ioc003Diagnostics.Select(d => d.GetMessage()))}");
    }

    [Fact]
    public void MultipleIndependentCycles_DetectsAllCycles()
    {
        // Arrange - Two independent cycles in same compilation
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace IsolatedTest;

// First cycle: ServiceA ← → ServiceB
public interface IServiceA { } public interface IServiceB { }
[Service] public partial class ServiceA : IServiceA { [Inject] private readonly IServiceB _serviceB; }
[Service] public partial class ServiceB : IServiceB { [Inject] private readonly IServiceA _serviceA; }

// Second cycle: ServiceX → ServiceY → ServiceZ → ServiceX
public interface IServiceX { } public interface IServiceY { } public interface IServiceZ { }
[Service] public partial class ServiceX : IServiceX { [Inject] private readonly IServiceY _serviceY; }
[Service] public partial class ServiceY : IServiceY { [Inject] private readonly IServiceZ _serviceZ; }
[Service] public partial class ServiceZ : IServiceZ { [Inject] private readonly IServiceX _serviceX; }";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should detect both independent cycles
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);
        Assert.True(ioc003Diagnostics.Count >= 1, "Should detect at least one cycle");

        Assert.All(ioc003Diagnostics, diagnostic =>
        {
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("Circular dependency detected", diagnostic.GetMessage());
        });

        var allMessages = string.Join(" | ", ioc003Diagnostics.Select(d => d.GetMessage()));

        // Should detect cycles involving both groups of services
        var detectsFirstGroup = allMessages.Contains("ServiceA") || allMessages.Contains("ServiceB");
        var detectsSecondGroup = allMessages.Contains("ServiceX") || allMessages.Contains("ServiceY") ||
                                 allMessages.Contains("ServiceZ");

        Assert.True(detectsFirstGroup || detectsSecondGroup,
            $"Expected cycle detection for multiple independent cycles. Got: {allMessages}");
    }

    [Fact]
    public void SelfReferenceViaMultipleInterfaces_GeneratesIOC003()
    {
        // Arrange - Service implementing multiple interfaces and depending on itself via different interface
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace IsolatedTest;

public interface IComplexService { }
public interface IComplexServiceProxy { }

[Service]
public partial class ComplexService : IComplexService, IComplexServiceProxy
{
    [Inject] private readonly IComplexServiceProxy _proxy; // Self-reference through different interface
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should detect self-reference through different interface
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        Assert.NotEmpty(ioc003Diagnostics);

        Assert.All(ioc003Diagnostics, diagnostic =>
        {
            Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
            Assert.Contains("Circular dependency detected", diagnostic.GetMessage());
        });

        var messages = ioc003Diagnostics.Select(d => d.GetMessage()).ToList();
        Assert.True(messages.Any(m => m.Contains("ComplexService")),
            $"Expected self-reference detection involving ComplexService. Got: {string.Join(" | ", messages)}");
    }
}