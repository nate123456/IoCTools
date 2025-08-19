using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace IoCTools.Generator.Tests;

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

[Service]
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
        Assert.False(result.HasErrors,
            $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        // Get the generated constructor
        var constructorSource = result.GetConstructorSource("MixedService");
        Assert.NotNull(constructorSource);

        // Verify the exact constructor signature matches README claim
        var expectedSignature =
            "public MixedService(IMediator mediator, IOtherService otherService, IAnotherService anotherService)";

        // Extract actual constructor signature
        var constructorMatch = Regex.Match(
            constructorSource.Content,
            @"public MixedService\(\s*([^)]+)\s*\)");
        Assert.True(constructorMatch.Success, $"Constructor signature not found in: {constructorSource.Content}");

        var actualSignature = $"public MixedService({constructorMatch.Groups[1].Value.Trim()})";

        // Verify DependsOn parameters come before Inject parameters
        var parameters = constructorMatch.Groups[1].Value
            .Split(',')
            .Select(p => p.Trim())
            .ToArray();

        Assert.Equal(3, parameters.Length);

        // CRITICAL: Verify parameter ordering - DependsOn before Inject
        Assert.Contains("IMediator", parameters[0]); // DependsOn parameter first
        Assert.Contains("IOtherService", parameters[1]); // Inject parameter second  
        Assert.Contains("IAnotherService", parameters[2]); // Inject parameter third

        // Verify field assignments in constructor body
        var content = constructorSource.Content;
        Assert.Contains("this._mediator = mediator;", content);
        Assert.Contains("this._otherService = otherService;", content);
        Assert.Contains("this._anotherService = anotherService;", content);
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

[Service]
[DependsOn<IBaseService>]
public abstract partial class BaseService
{
}

[Service]
[DependsOn<IDerivedService>]
public partial class DerivedService : BaseService
{
    [Inject] private readonly IInjectService _injectService;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var derivedConstructor = result.GetConstructorSource("DerivedService");
        Assert.NotNull(derivedConstructor);

        // Verify inheritance mixing - base DependsOn + derived DependsOn + derived Inject
        var content = derivedConstructor.Content;

        // Should have all three dependencies: base DependsOn, derived DependsOn, derived Inject
        Assert.Contains("IBaseService", content);
        Assert.Contains("IDerivedService", content);
        Assert.Contains("IInjectService", content);

        // Verify proper base constructor call
        Assert.Contains("base(", content);
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

[Service]
[DependsOn<IFirst, ISecond>]
public partial class OrderingTestService
{
    [Inject] private readonly IThird _third;
    [Inject] private readonly IFourth _fourth;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("OrderingTestService");
        Assert.NotNull(constructorSource);

        // Extract parameters
        var constructorMatch = Regex.Match(
            constructorSource.Content,
            @"public OrderingTestService\(\s*([^)]+)\s*\)");
        Assert.True(constructorMatch.Success);

        var parameters = constructorMatch.Groups[1].Value
            .Split(',')
            .Select(p => p.Trim())
            .ToArray();

        Assert.Equal(4, parameters.Length);

        // Verify DependsOn parameters come first, then Inject parameters
        Assert.Contains("IFirst", parameters[0]); // DependsOn parameter 1
        Assert.Contains("ISecond", parameters[1]); // DependsOn parameter 2
        Assert.Contains("IThird", parameters[2]); // Inject parameter 1
        Assert.Contains("IFourth", parameters[3]); // Inject parameter 2
    }

    [Fact]
    public void MixedPatterns_RedundancyDetection_HandlesCorrectly()
    {
        // Arrange - Test IOC007 diagnostic for redundant dependencies
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IConflictService { }

[Service]
[DependsOn<IConflictService>]
public partial class ConflictService
{
    [Inject] private readonly IConflictService _conflictService;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - should generate IOC007 diagnostic
        var ioc007Diagnostics = result.GetDiagnosticsByCode("IOC007");
        Assert.Single(ioc007Diagnostics);

        var diagnostic = ioc007Diagnostics[0];
        Assert.Contains("IConflictService", diagnostic.GetMessage());
        Assert.Contains("declared in [DependsOn] attribute", diagnostic.GetMessage());
    }
}