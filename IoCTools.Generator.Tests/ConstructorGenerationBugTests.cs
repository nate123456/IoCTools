namespace IoCTools.Generator.Tests;

using Microsoft.CodeAnalysis;

/// <summary>
///     COMPREHENSIVE BUG COVERAGE: Constructor Generation Bugs
///     These tests explicitly reproduce and prevent regression of discovered bugs:
///     - Empty Constructor Bug: Services with [Inject] fields generating empty constructors
///     - Template Replacement Bug: Field placeholders not being replaced in generated code
///     - Field Detection Failure: HasInjectFieldsAcrossPartialClasses() not detecting fields
///     Each test reproduces the exact bug condition and validates the fix.
/// </summary>
public class ConstructorGenerationBugTests
{
    #region BUG: Constructor Generation With Complex Scenarios

    /// <summary>
    ///     BUG REPRODUCTION: Complex scenarios with generic types and inheritance
    ///     should not generate empty constructors.
    /// </summary>
    [Fact]
    public void Test_ComplexGenericInheritance_GeneratesCorrectConstructor()
    {
        // Arrange - Complex generic inheritance scenario
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace TestNamespace;

public interface IRepository<T> { }
public interface ICache<T> { }
public class Entity { }
public partial class GenericBase<T>
{
    [Inject] private readonly ILogger<GenericBase<T>> _logger;
}

[Scoped] 
public partial class EntityService : GenericBase<Entity>
{
    [Inject] private readonly IRepository<Entity> _repository;
    [Inject] private readonly ICache<Entity> _cache;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        // Check EntityService constructor
        var entityConstructorSource = result.GetRequiredConstructorSource("EntityService");

        // CRITICAL: Should NOT be empty
        entityConstructorSource.Content.Should().NotContain("public EntityService() { }");

        // CRITICAL: Should contain base constructor call
        entityConstructorSource.Content.Should().Contain("base(");

        // CRITICAL: Should contain derived dependencies
        entityConstructorSource.Content.Should().Contain("IRepository<Entity>");
        entityConstructorSource.Content.Should().Contain("ICache<Entity>");
    }

    #endregion

    #region BUG: Empty Constructor Generation

    /// <summary>
    ///     BUG REPRODUCTION: Services with [Inject] fields were generating empty constructors
    ///     instead of constructors with the required parameters.
    /// </summary>
    [Fact]
    public void Test_BasicFieldInjection_DoesNotGenerateEmptyConstructor()
    {
        // Arrange - Service with [Inject] field (the bug condition)
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace TestNamespace;

public interface IGreetingService { }
public partial class GreetingService : IGreetingService
{
    [Inject] private readonly ILogger<GreetingService> _logger;
}";

        // Act - Generate constructor
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should NOT be empty (the bug was empty constructor)
        result.HasErrors.Should()
            .BeFalse(
                $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var constructorSource = result.GetRequiredConstructorSource("GreetingService");

        // CRITICAL: Should NOT contain empty constructor
        constructorSource.Content.Should().NotContain("public GreetingService() { }");
        constructorSource.Content.Should().NotContain("public GreetingService()\n    {");

        // CRITICAL: Should contain constructor with parameter
        var hasParameterizedConstructor =
            constructorSource.Content.Contains("public GreetingService(ILogger<GreetingService> logger)") ||
            constructorSource.Content.Contains(
                "public GreetingService(Microsoft.Extensions.Logging.ILogger<GreetingService> logger)");

        hasParameterizedConstructor.Should()
            .BeTrue($"Should generate constructor with parameter. Generated content: {constructorSource.Content}");

        // CRITICAL: Should contain field assignment
        constructorSource.Content.Should().Contain("this._logger = logger;");
    }

    /// <summary>
    ///     BUG REPRODUCTION: Multiple [Inject] fields should generate constructor with all parameters,
    ///     not an empty constructor.
    /// </summary>
    [Fact]
    public void Test_MultipleInjectFields_GeneratesConstructorWithAllParameters()
    {
        // Arrange - Service with multiple [Inject] fields
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace TestNamespace;

public interface IRepository { }
public interface ICache { }
public partial class ComplexService
{
    [Inject] private readonly ILogger<ComplexService> _logger;
    [Inject] private readonly IRepository _repository;
    [Inject] private readonly ICache _cache;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("ComplexService");

        // CRITICAL: Should NOT be empty constructor
        constructorSource.Content.Should().NotContain("public ComplexService() { }");

        // CRITICAL: Should contain all three parameters
        constructorSource.Content.Should().Contain("ILogger<ComplexService>");
        constructorSource.Content.Should().Contain("IRepository");
        constructorSource.Content.Should().Contain("ICache");

        // CRITICAL: Should contain all field assignments
        constructorSource.Content.Should().Contain("this._logger = logger;");
        constructorSource.Content.Should().Contain("this._repository = repository;");
        constructorSource.Content.Should().Contain("this._cache = cache;");
    }

    #endregion

    #region BUG: Template Replacement Failures

    /// <summary>
    ///     BUG REPRODUCTION: Field placeholders were not being replaced in generated code,
    ///     leaving template variables in the final output.
    /// </summary>
    [Fact]
    public void Test_TemplateReplacement_DoesNotLeaveEmptyConstructors()
    {
        // Arrange - Service that could trigger template replacement bug
        var source = @"
using IoCTools.Abstractions.Annotations;
using System.Collections.Generic;

namespace TestNamespace;

public interface IDataService { }
public partial class TemplateTestService
{
    [Inject] private readonly IEnumerable<IDataService> _services;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("TemplateTestService");

        // CRITICAL: Should not contain template placeholders or empty constructors
        constructorSource.Content.Should().NotContain("{PARAMETERS}");
        constructorSource.Content.Should().NotContain("{ASSIGNMENTS}");
        constructorSource.Content.Should().NotContain("{BASE_CALL}");
        constructorSource.Content.Should().NotContain("public TemplateTestService() { }");

        // CRITICAL: Should contain properly resolved template
        constructorSource.Content.Should().Contain("IEnumerable<IDataService> services");
        constructorSource.Content.Should().Contain("this._services = services;");
    }

    /// <summary>
    ///     BUG REPRODUCTION: Template replacement should work with inheritance scenarios.
    /// </summary>
    [Fact]
    public void Test_InheritanceTemplateReplacement_GeneratesCorrectConstructor()
    {
        // Arrange - Inheritance scenario that could expose template bugs
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace TestNamespace;

public interface IBaseService { }
public interface IDerivedService { }
public partial class BaseService
{
    [Inject] private readonly ILogger<BaseService> _logger;
}
public partial class DerivedService : BaseService
{
    [Inject] private readonly IDerivedService _derivedService;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var derivedConstructorSource = result.GetRequiredConstructorSource("DerivedService");

        // CRITICAL: Should not contain template placeholders
        derivedConstructorSource.Content.Should().NotContain("{PARAMETERS}");
        derivedConstructorSource.Content.Should().NotContain("{ASSIGNMENTS}");
        derivedConstructorSource.Content.Should().NotContain("{BASE_CALL}");

        // CRITICAL: Should contain base constructor call
        derivedConstructorSource.Content.Should().Contain("base(");

        // CRITICAL: Should contain derived field assignment
        derivedConstructorSource.Content.Should().Contain("this._derivedService = derivedService;");
    }

    #endregion

    #region BUG: Field Detection Across Partial Classes

    /// <summary>
    ///     BUG REPRODUCTION: HasInjectFieldsAcrossPartialClasses() was not detecting fields
    ///     across multiple partial class declarations.
    /// </summary>
    [Fact]
    public void Test_FieldDetection_FindsInjectFieldsAcrossPartialClasses()
    {
        // Arrange - Partial class with [Inject] field in different part
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace TestNamespace;

public interface IRepository { }

// First partial declaration

public partial class PartialService
{
    public void DoSomething() { }
}

// Second partial declaration with [Inject] field
public partial class PartialService
{
    [Inject] private readonly ILogger<PartialService> _logger;
    [Inject] private readonly IRepository _repository;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        // CRITICAL: Should detect fields across partial declarations and generate constructor
        var constructorSource = result.GetRequiredConstructorSource("PartialService");

        // CRITICAL: Should contain both injected dependencies
        constructorSource.Content.Should().Contain("ILogger<PartialService>");
        constructorSource.Content.Should().Contain("IRepository");

        // CRITICAL: Should contain both field assignments
        constructorSource.Content.Should().Contain("this._logger = logger;");
        constructorSource.Content.Should().Contain("this._repository = repository;");
    }

    /// <summary>
    ///     BUG REPRODUCTION: Field detection should work when [Scoped] attribute is on one part
    ///     and [Inject] fields are on another part.
    /// </summary>
    [Fact]
    public void Test_FieldDetection_LifetimeAttributeAndInjectFieldsInDifferentParts()
    {
        // Arrange - [Scoped] on one part, [Inject] on another
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace TestNamespace;

public interface IDataAccess { }

// Part with [Scoped] attribute

public partial class SplitService
{
    public string Name { get; set; } = ""Test"";
}

// Part with [Inject] fields
public partial class SplitService
{
    [Inject] private readonly ILogger<SplitService> _logger;
    [Inject] private readonly IDataAccess _dataAccess;
    
    public void LogData()
    {
        _logger?.LogInformation(""Accessing data via {Service}"", _dataAccess?.GetType().Name);
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert  
        result.HasErrors.Should().BeFalse();

        // CRITICAL: Should generate constructor despite split across parts
        var constructorSource = result.GetRequiredConstructorSource("SplitService");

        // CRITICAL: Should detect and inject both dependencies
        constructorSource.Content.Should().Contain("ILogger<SplitService>");
        constructorSource.Content.Should().Contain("IDataAccess");

        // CRITICAL: Should assign to private fields correctly
        constructorSource.Content.Should().Contain("this._logger = logger;");
        constructorSource.Content.Should().Contain("this._dataAccess = dataAccess;");
    }

    #endregion

    #region REGRESSION PREVENTION: Edge Cases

    /// <summary>
    ///     REGRESSION PREVENTION: Ensure constructor generation works with nullable types.
    /// </summary>
    [Fact]
    public void Test_NullableTypes_GeneratesCorrectConstructor()
    {
        // Arrange
        var source = @"
#nullable enable
using IoCTools.Abstractions.Annotations;
using System;

namespace TestNamespace;

public interface IOptionalService { }
public partial class NullableService
{
    [Inject] private readonly IOptionalService? _optionalService;
    [Inject] private readonly string? _optionalString;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("NullableService");

        // CRITICAL: Should handle nullable types correctly
        constructorSource.Content.Should().NotContain("public NullableService() { }");
        constructorSource.Content.Should().Contain("IOptionalService?");
        constructorSource.Content.Should().Contain("string?");
    }

    /// <summary>
    ///     REGRESSION PREVENTION: Static fields should be ignored in constructor generation.
    /// </summary>
    [Fact]
    public void Test_StaticFields_AreIgnoredInConstructorGeneration()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace TestNamespace;

public interface IRepository { }
public partial class ServiceWithStatics
{
    [Inject] private static readonly ILogger<ServiceWithStatics>? _staticLogger; // Should be ignored
    [Inject] private readonly IRepository _repository; // Should be included
    private static readonly string StaticField = ""test""; // Should be ignored
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("ServiceWithStatics");

        // CRITICAL: Should only include non-static [Inject] fields
        constructorSource.Content.Should().Contain("IRepository");
        constructorSource.Content.Should().NotContain("ILogger<ServiceWithStatics>"); // Static field ignored

        // Should contain assignment for non-static field only
        constructorSource.Content.Should().Contain("this._repository = repository;");
        constructorSource.Content.Should().NotContain("_staticLogger");
    }

    #endregion
}
