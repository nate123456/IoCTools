namespace IoCTools.Generator.Tests;

using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;

/// <summary>
///     Tests for Mixed Dependency Patterns as documented in README.md lines 460-488
///     This validates that [DependsOn] attributes and [Inject] fields can coexist correctly
/// </summary>
public class MixedDependencyPatternsTests
{
    [Fact]
    public void MixedPatterns_DependsOnWithInjectFields_GeneratesCorrectConstructor()
    {
        // Arrange - Exact example from README
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IMediator { }
public interface IOtherService { }
public interface IAnotherService { }
public interface ISomeService { }
[DependsOn<IMediator>]
public partial class MixedService : ISomeService
{
    [Inject] private readonly IOtherService _otherService;
    [Inject] private readonly IAnotherService _anotherService;
    
    private readonly int _someValue = 1;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - should compile without errors
        result.HasErrors.Should()
            .BeFalse(
                $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        // Get the generated constructor
        var constructorSource = result.GetRequiredConstructorSource("MixedService");

        // Verify the exact constructor signature matches README claim
        var expectedSignature =
            "public MixedService(IMediator mediator, IOtherService otherService, IAnotherService anotherService)";

        // Extract actual constructor signature
        var constructorMatch = Regex.Match(
            constructorSource.Content,
            @"public MixedService\(\s*([^)]+)\s*\)");
        constructorMatch.Success.Should().BeTrue($"Constructor signature not found in: {constructorSource.Content}");

        var actualSignature = $"public MixedService({constructorMatch.Groups[1].Value.Trim()})";
        actualSignature.Should().Be(expectedSignature);

        // Verify DependsOn parameters come before Inject parameters
        var parameters = constructorMatch.Groups[1].Value
            .Split(',')
            .Select(p => p.Trim())
            .ToArray();

        parameters.Length.Should().Be(3);

        // CRITICAL: Verify parameter ordering - DependsOn before Inject
        parameters[0].Should().Contain("IMediator"); // DependsOn parameter first
        parameters[1].Should().Contain("IOtherService"); // Inject parameter second  
        parameters[2].Should().Contain("IAnotherService"); // Inject parameter third

        // Verify field assignments in constructor body
        var content = constructorSource.Content;
        content.Should().Contain("this._mediator = mediator;");
        content.Should().Contain("this._otherService = otherService;");
        content.Should().Contain("this._anotherService = anotherService;");
    }

    [Fact]
    public void MixedPatterns_InheritanceHierarchy_MixingWorksCorrectly()
    {
        // Arrange - Test mixed patterns across inheritance
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IBaseService { }
public interface IDerivedService { }
public interface IInjectService { }
[DependsOn<IBaseService>]
public abstract partial class BaseService
{
}
[DependsOn<IDerivedService>]
public partial class DerivedService : BaseService
{
    [Inject] private readonly IInjectService _injectService;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var derivedConstructor = result.GetRequiredConstructorSource("DerivedService");

        // Verify inheritance mixing - base DependsOn + derived DependsOn + derived Inject
        var content = derivedConstructor.Content;

        // Should have all three dependencies: base DependsOn, derived DependsOn, derived Inject
        content.Should().Contain("IBaseService");
        content.Should().Contain("IDerivedService");
        content.Should().Contain("IInjectService");

        // Verify proper base constructor call
        content.Should().Contain("base(");
    }

    [Fact]
    public void MixedPatterns_ParameterOrdering_DependsOnBeforeInject()
    {
        // Arrange - Test to verify parameter ordering according to README
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IFirst { }
public interface ISecond { }
public interface IThird { }
public interface IFourth { }
[DependsOn<IFirst, ISecond>]
public partial class OrderingTestService
{
    [Inject] private readonly IThird _third;
    [Inject] private readonly IFourth _fourth;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("OrderingTestService");

        // Extract parameters
        var constructorMatch = Regex.Match(
            constructorSource.Content,
            @"public OrderingTestService\(\s*([^)]+)\s*\)");
        constructorMatch.Success.Should().BeTrue();

        var parameters = constructorMatch.Groups[1].Value
            .Split(',')
            .Select(p => p.Trim())
            .ToArray();

        parameters.Length.Should().Be(4);

        // Verify DependsOn parameters come first, then Inject parameters
        parameters[0].Should().Contain("IFirst"); // DependsOn parameter 1
        parameters[1].Should().Contain("ISecond"); // DependsOn parameter 2
        parameters[2].Should().Contain("IThird"); // Inject parameter 1
        parameters[3].Should().Contain("IFourth"); // Inject parameter 2
    }

    [Fact]
    public void MixedPatterns_RedundancyDetection_HandlesCorrectly()
    {
        // Arrange - Test IOC007 diagnostic for redundant dependencies
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IConflictService { }
[DependsOn<IConflictService>]
public partial class ConflictService
{
    [Inject] private readonly IConflictService _conflictService;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - should generate IOC007 diagnostic
        var ioc007Diagnostics = result.GetDiagnosticsByCode("IOC007");
        ioc007Diagnostics.Should().ContainSingle();

        var diagnostic = ioc007Diagnostics[0];
        diagnostic.GetMessage().Should().Contain("IConflictService");
        diagnostic.GetMessage().Should().Contain("declared in [DependsOn] attribute");
    }
}
