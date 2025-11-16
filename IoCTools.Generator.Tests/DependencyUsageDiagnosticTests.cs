namespace IoCTools.Generator.Tests;

using Microsoft.CodeAnalysis;

public class DependencyUsageDiagnosticTests
{
    [Fact]
    public void UnusedDependency_InjectField_ProducesIOC039()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ILogger { }

public partial class UnusedInjectService
{
    [Inject] private readonly ILogger _logger;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diagnostics = result.GetDiagnosticsByCode("IOC039");
        diagnostics.Should().ContainSingle();
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostics[0].GetMessage().Should().Contain("_logger");
    }

    [Fact]
    public void UnusedDependency_DependsOnField_ProducesIOC039()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService { }

[DependsOn<IService>]
public partial class DependsOnService
{
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diagnostics = result.GetDiagnosticsByCode("IOC039");
        diagnostics.Should().ContainSingle();
        diagnostics[0].GetMessage().Should().Contain("_service");
    }

    [Fact]
    public void UnusedDependency_DependsOnField_Used_NoDiagnostic()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService { }

[DependsOn<IService>]
public partial class ActiveService
{
    public string Value => _service.ToString();
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC039").Should().BeEmpty();
    }

    [Fact]
    public void UnusedDependency_ProtectedInjectField_Skipped()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IClock { }

public abstract partial class BaseClockService
{
    [Inject] protected readonly IClock _clock;
}

public partial class ConcreteClockService : BaseClockService
{
    public string Now() => _clock.ToString();
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC039").Should().BeEmpty();
    }

    [Fact]
    public void RedundantDependency_MultipleInjectFields_ProducesIOC040()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IDataStore { }

public partial class DuplicateInjectService
{
    [Inject] private readonly IDataStore _primaryStore;
    [Inject] private readonly IDataStore _secondaryStore;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var diagnostics = result.GetDiagnosticsByCode("IOC040");
        diagnostics.Should().ContainSingle();
        diagnostics[0].GetMessage().Should().Contain("IDataStore");
        diagnostics[0].GetMessage().Should().Contain("[Inject]");
    }

    [Fact]
    public void RedundantDependency_InheritanceMixedSources_ProducesIOC040()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ILogger { }

public abstract partial class BaseService
{
    [Inject] protected readonly ILogger _logger;
}

[DependsOn<ILogger>]
public partial class DerivedService : BaseService
{
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        result.GetDiagnosticsByCode("IOC040").Should().ContainSingle();
    }
}
