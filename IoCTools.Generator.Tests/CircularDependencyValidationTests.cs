namespace IoCTools.Generator.Tests;

using Microsoft.CodeAnalysis;

/// <summary>
///     Focused tests for validating circular dependency detection functionality.
///     Each test is isolated to ensure build stability.
/// </summary>
public class CircularDependencyValidationTests
{
    [Fact]
    public void BasicCircularDependency_DetectsIOC003()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;
public interface IA { }
public interface IB { }
public partial class A : IA
{
    [Inject] private readonly IB _b;
}
public partial class B : IB
{
    [Inject] private readonly IA _a;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        ioc003Diagnostics.Should().NotBeEmpty();
        ioc003Diagnostics.Should().AllSatisfy(d => d.Severity.Should().Be(DiagnosticSeverity.Warning));
    }

    [Fact]
    public void SelfReference_DetectsIOC003()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;
public interface ISelf { }
public partial class Self : ISelf
{
    [Inject] private readonly ISelf _self;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        ioc003Diagnostics.Should().NotBeEmpty();
        ioc003Diagnostics.Should().AllSatisfy(d => d.Severity.Should().Be(DiagnosticSeverity.Warning));
    }

    [Fact]
    public void LinearDependency_NoCircularError()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;
public interface IA { }
public interface IB { }
public interface IC { }
public partial class A : IA
{
    [Inject] private readonly IB _b;
}
public partial class B : IB
{
    [Inject] private readonly IC _c;
}
public partial class C : IC { }";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        ioc003Diagnostics.Should().BeEmpty();
        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void IEnumerable_DoesNotCreateCircularDependency()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using System.Collections.Generic;

namespace Test;
public interface IService { }
public interface IHandler { }
public partial class Service : IService
{
    [Inject] private readonly IEnumerable<IHandler> _handlers;
}
public partial class Handler : IHandler
{
    [Inject] private readonly IService _service;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        ioc003Diagnostics.Should().BeEmpty();
        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void DependsOnAttribute_CircularDependency_DetectsIOC003()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;
public interface IX { }
public interface IY { }
[DependsOn<IY>]
public partial class X : IX { }
[DependsOn<IX>]
public partial class Y : IY { }";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        ioc003Diagnostics.Should().NotBeEmpty();
        ioc003Diagnostics.Should().AllSatisfy(d => d.Severity.Should().Be(DiagnosticSeverity.Warning));
    }

    [Fact]
    public void ExternalService_SkipsCircularCheck()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;
public interface IA { }
public interface IB { }
public partial class A : IA
{
    [Inject] private readonly IB _b;
}
[ExternalService]
public partial class B : IB
{
    [Inject] private readonly IA _a;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        ioc003Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void ThreeServiceCycle_DetectsIOC003()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;
public interface IA { }
public interface IB { }
public interface IC { }
public partial class A : IA
{
    [Inject] private readonly IB _b;
}
public partial class B : IB
{
    [Inject] private readonly IC _c;
}
public partial class C : IC
{
    [Inject] private readonly IA _a;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var ioc003Diagnostics = result.GetDiagnosticsByCode("IOC003");
        ioc003Diagnostics.Should().NotBeEmpty();
        ioc003Diagnostics.Should().AllSatisfy(d => d.Severity.Should().Be(DiagnosticSeverity.Warning));
    }
}
