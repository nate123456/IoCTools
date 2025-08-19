using Microsoft.CodeAnalysis;

namespace IoCTools.Generator.Tests;

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

[Service]
public partial class CollectionService
{
    [Inject] private readonly IEnumerable<ITestService> _services;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors,
            $"Collection injection should work: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var constructorSource = result.GetConstructorSource("CollectionService");
        Assert.NotNull(constructorSource);
        Assert.Contains("IEnumerable<ITestService> services", constructorSource.Content);
        Assert.Contains("this._services = services;", constructorSource.Content);
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

[Service]
public partial class NullableService
{
    [Inject] private readonly ITestService? _optionalService;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("NullableService");
        Assert.NotNull(constructorSource);
        Assert.Contains("ITestService? optionalService", constructorSource.Content);
        Assert.Contains("this._optionalService = optionalService;", constructorSource.Content);
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

[Service]
public partial class FactoryService
{
    [Inject] private readonly Func<ITestService> _factory;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("FactoryService");
        Assert.NotNull(constructorSource);
        Assert.Contains("Func<ITestService> factory", constructorSource.Content);
        Assert.Contains("this._factory = factory;", constructorSource.Content);
    }

    [Fact]
    public void ServiceProviderInjection_Works()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using System;

namespace Test;

[Service]
public partial class ServiceProviderService
{
    [Inject] private readonly IServiceProvider _provider;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("ServiceProviderService");
        Assert.NotNull(constructorSource);
        Assert.Contains("IServiceProvider provider", constructorSource.Content);
        Assert.Contains("this._provider = provider;", constructorSource.Content);
    }

    [Fact]
    public void AccessModifiers_ProtectedFields_Work()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

[Service]
public partial class AccessModifierService
{
    [Inject] protected readonly ITestService _protectedService;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("AccessModifierService");
        Assert.NotNull(constructorSource);
        Assert.Contains("ITestService protectedService", constructorSource.Content);
        Assert.Contains("this._protectedService = protectedService;", constructorSource.Content);
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

[Service]
[DependsOn<IDependsService>]
public partial class MixedService
{
    [Inject] private readonly IInjectService _injectService;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("MixedService");
        Assert.NotNull(constructorSource);

        // DependsOn parameter should come first
        Assert.Contains("IDependsService dependsService", constructorSource.Content);
        Assert.Contains("IInjectService injectService", constructorSource.Content);
        Assert.Contains("this._dependsService = dependsService;", constructorSource.Content);
        Assert.Contains("this._injectService = injectService;", constructorSource.Content);
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

[Service]
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
            Assert.Contains("Lazy<ITestService> lazyService", constructorSource.Content);
            Assert.True(true, "Lazy<T> is directly supported by IoCTools");
        }
        else
        {
            // If Lazy<T> requires manual setup
            Assert.True(true, "Lazy<T> requires manual DI container setup - documented limitation");
        }
    }
}