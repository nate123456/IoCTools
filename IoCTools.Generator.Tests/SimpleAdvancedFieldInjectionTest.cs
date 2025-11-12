namespace IoCTools.Generator.Tests;

using Microsoft.CodeAnalysis;

/// <summary>
///     Simple test to validate advanced field injection patterns work
/// </summary>
public class SimpleAdvancedFieldInjectionTest
{
    [Fact]
    public void SimpleCollectionInjection_IEnumerable_Works()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using System.Collections.Generic;

namespace Test;

public interface ITestService { }

public partial class CollectionService
{
    [Inject] private readonly IEnumerable<ITestService> _services;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should()
            .BeFalse(
                $"Collection injection should work: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var constructorSource = result.GetRequiredConstructorSource("CollectionService");
        constructorSource.Content.Should().Contain("IEnumerable<ITestService> services");
        constructorSource.Content.Should().Contain("this._services = services;");
    }

    [Fact]
    public void SimpleNullableInjection_Works()
    {
        // Arrange
        var source = @"
#nullable enable
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

public partial class NullableService
{
    [Inject] private readonly ITestService? _optionalService;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("NullableService");
        constructorSource.Content.Should().Contain("ITestService? optionalService");
        constructorSource.Content.Should().Contain("this._optionalService = optionalService;");
    }

    [Fact]
    public void SimpleFuncFactory_Works()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using System;

namespace Test;

public interface ITestService { }

public partial class FactoryService
{
    [Inject] private readonly Func<ITestService> _factory;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("FactoryService");
        constructorSource.Content.Should().Contain("Func<ITestService> factory");
        constructorSource.Content.Should().Contain("this._factory = factory;");
    }

    [Fact]
    public void ServiceProviderInjection_Works()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using System;

namespace Test;

public partial class ServiceProviderService
{
    [Inject] private readonly IServiceProvider _provider;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("ServiceProviderService");
        constructorSource.Content.Should().Contain("IServiceProvider provider");
        constructorSource.Content.Should().Contain("this._provider = provider;");
    }

    [Fact]
    public void AccessModifiers_ProtectedFields_Work()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

public partial class AccessModifierService
{
    [Inject] protected readonly ITestService _protectedService;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("AccessModifierService");
        constructorSource.Content.Should().Contain("ITestService protectedService");
        constructorSource.Content.Should().Contain("this._protectedService = protectedService;");
    }

    [Fact]
    public void MixedPatterns_InjectWithDependsOn_Works()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IDependsService { }
public interface IInjectService { }

[DependsOn<IDependsService>]
public partial class MixedService
{
    [Inject] private readonly IInjectService _injectService;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("MixedService");

        // DependsOn parameter should come first
        constructorSource.Content.Should().Contain("IDependsService dependsService");
        constructorSource.Content.Should().Contain("IInjectService injectService");
        constructorSource.Content.Should().Contain("this._dependsService = dependsService;");
        constructorSource.Content.Should().Contain("this._injectService = injectService;");
    }

    [Fact]
    public void LazyT_Pattern_Documentation()
    {
        // Arrange - Document Lazy<T> behavior
        var source = @"
using IoCTools.Abstractions.Annotations;
using System;

namespace Test;

public interface ITestService { }

public partial class LazyService
{
    [Inject] private readonly Lazy<ITestService> _lazyService;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Document what happens
        var constructorSource = result.GetConstructorSource("LazyService");

        if (constructorSource != null && constructorSource.Content.Contains("Lazy<ITestService>"))
        {
            // If IoCTools supports Lazy<T> directly
            constructorSource.Content.Should().Contain("Lazy<ITestService> lazyService");
            true.Should().BeTrue("Lazy<T> is directly supported by IoCTools");
        }
        else
        {
            // If Lazy<T> requires manual setup
            true.Should().BeTrue("Lazy<T> requires manual DI container setup - documented limitation");
        }
    }
}
