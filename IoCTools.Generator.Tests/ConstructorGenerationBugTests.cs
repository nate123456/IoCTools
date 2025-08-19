using Microsoft.CodeAnalysis;
using System.Linq;

namespace IoCTools.Generator.Tests;

/// <summary>
/// COMPREHENSIVE BUG COVERAGE: Constructor Generation Bugs
/// 
/// These tests explicitly reproduce and prevent regression of discovered bugs:
/// - Empty Constructor Bug: Services with [Inject] fields generating empty constructors
/// - Template Replacement Bug: Field placeholders not being replaced in generated code
/// - Field Detection Failure: HasInjectFieldsAcrossPartialClasses() not detecting fields
/// 
/// Each test reproduces the exact bug condition and validates the fix.
/// </summary>
public class ConstructorGenerationBugTests
{
    #region BUG: Empty Constructor Generation
    
    /// <summary>
    /// BUG REPRODUCTION: Services with [Inject] fields were generating empty constructors
    /// instead of constructors with the required parameters.
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

[Service]
public partial class GreetingService : IGreetingService
{
    [Inject] private readonly ILogger<GreetingService> _logger;
}";

        // Act - Generate constructor
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        
        // Assert - Should NOT be empty (the bug was empty constructor)
        Assert.False(result.HasErrors, 
            $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");
            
        var constructorSource = result.GetConstructorSource("GreetingService");
        Assert.NotNull(constructorSource);
        
        // CRITICAL: Should NOT contain empty constructor
        Assert.DoesNotContain("public GreetingService() { }", constructorSource.Content);
        Assert.DoesNotContain("public GreetingService()\n    {", constructorSource.Content);
        
        // CRITICAL: Should contain constructor with parameter
        var hasParameterizedConstructor = 
            constructorSource.Content.Contains("public GreetingService(ILogger<GreetingService> logger)") ||
            constructorSource.Content.Contains("public GreetingService(Microsoft.Extensions.Logging.ILogger<GreetingService> logger)");
            
        Assert.True(hasParameterizedConstructor, 
            $"Should generate constructor with parameter. Generated content: {constructorSource.Content}");
            
        // CRITICAL: Should contain field assignment
        Assert.Contains("this._logger = logger;", constructorSource.Content);
    }
    
    /// <summary>
    /// BUG REPRODUCTION: Multiple [Inject] fields should generate constructor with all parameters,
    /// not an empty constructor.
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

[Service]
public partial class ComplexService
{
    [Inject] private readonly ILogger<ComplexService> _logger;
    [Inject] private readonly IRepository _repository;
    [Inject] private readonly ICache _cache;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        
        // Assert
        Assert.False(result.HasErrors);
        
        var constructorSource = result.GetConstructorSource("ComplexService");
        Assert.NotNull(constructorSource);
        
        // CRITICAL: Should NOT be empty constructor
        Assert.DoesNotContain("public ComplexService() { }", constructorSource.Content);
        
        // CRITICAL: Should contain all three parameters
        Assert.Contains("ILogger<ComplexService>", constructorSource.Content);
        Assert.Contains("IRepository", constructorSource.Content);
        Assert.Contains("ICache", constructorSource.Content);
        
        // CRITICAL: Should contain all field assignments
        Assert.Contains("this._logger = logger;", constructorSource.Content);
        Assert.Contains("this._repository = repository;", constructorSource.Content);
        Assert.Contains("this._cache = cache;", constructorSource.Content);
    }
    
    #endregion
    
    #region BUG: Template Replacement Failures
    
    /// <summary>
    /// BUG REPRODUCTION: Field placeholders were not being replaced in generated code,
    /// leaving template variables in the final output.
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

[Service]
public partial class TemplateTestService
{
    [Inject] private readonly IEnumerable<IDataService> _services;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        
        // Assert
        Assert.False(result.HasErrors);
        
        var constructorSource = result.GetConstructorSource("TemplateTestService");
        Assert.NotNull(constructorSource);
        
        // CRITICAL: Should not contain template placeholders or empty constructors
        Assert.DoesNotContain("{PARAMETERS}", constructorSource.Content);
        Assert.DoesNotContain("{ASSIGNMENTS}", constructorSource.Content);
        Assert.DoesNotContain("{BASE_CALL}", constructorSource.Content);
        Assert.DoesNotContain("public TemplateTestService() { }", constructorSource.Content);
        
        // CRITICAL: Should contain properly resolved template
        Assert.Contains("IEnumerable<IDataService> services", constructorSource.Content);
        Assert.Contains("this._services = services;", constructorSource.Content);
    }
    
    /// <summary>
    /// BUG REPRODUCTION: Template replacement should work with inheritance scenarios.
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

[Service]
public partial class BaseService
{
    [Inject] private readonly ILogger<BaseService> _logger;
}

[Service]
public partial class DerivedService : BaseService
{
    [Inject] private readonly IDerivedService _derivedService;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        
        // Assert
        Assert.False(result.HasErrors);
        
        var derivedConstructorSource = result.GetConstructorSource("DerivedService");
        Assert.NotNull(derivedConstructorSource);
        
        // CRITICAL: Should not contain template placeholders
        Assert.DoesNotContain("{PARAMETERS}", derivedConstructorSource.Content);
        Assert.DoesNotContain("{ASSIGNMENTS}", derivedConstructorSource.Content);
        Assert.DoesNotContain("{BASE_CALL}", derivedConstructorSource.Content);
        
        // CRITICAL: Should contain base constructor call
        Assert.Contains("base(", derivedConstructorSource.Content);
        
        // CRITICAL: Should contain derived field assignment
        Assert.Contains("this._derivedService = derivedService;", derivedConstructorSource.Content);
    }
    
    #endregion
    
    #region BUG: Field Detection Across Partial Classes
    
    /// <summary>
    /// BUG REPRODUCTION: HasInjectFieldsAcrossPartialClasses() was not detecting fields
    /// across multiple partial class declarations.
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
[Service]
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
        Assert.False(result.HasErrors);
        
        // CRITICAL: Should detect fields across partial declarations and generate constructor
        var constructorSource = result.GetConstructorSource("PartialService");
        Assert.NotNull(constructorSource);
        
        // CRITICAL: Should contain both injected dependencies
        Assert.Contains("ILogger<PartialService>", constructorSource.Content);
        Assert.Contains("IRepository", constructorSource.Content);
        
        // CRITICAL: Should contain both field assignments
        Assert.Contains("this._logger = logger;", constructorSource.Content);
        Assert.Contains("this._repository = repository;", constructorSource.Content);
    }
    
    /// <summary>
    /// BUG REPRODUCTION: Field detection should work when [Service] attribute is on one part
    /// and [Inject] fields are on another part.
    /// </summary>
    [Fact]
    public void Test_FieldDetection_ServiceAttributeAndInjectFieldsInDifferentParts()
    {
        // Arrange - [Service] on one part, [Inject] on another
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace TestNamespace;

public interface IDataAccess { }

// Part with [Service] attribute
[Service]
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
        Assert.False(result.HasErrors);
        
        // CRITICAL: Should generate constructor despite split across parts
        var constructorSource = result.GetConstructorSource("SplitService");
        Assert.NotNull(constructorSource);
        
        // CRITICAL: Should detect and inject both dependencies
        Assert.Contains("ILogger<SplitService>", constructorSource.Content);
        Assert.Contains("IDataAccess", constructorSource.Content);
        
        // CRITICAL: Should assign to private fields correctly
        Assert.Contains("this._logger = logger;", constructorSource.Content);
        Assert.Contains("this._dataAccess = dataAccess;", constructorSource.Content);
    }
    
    #endregion
    
    #region BUG: Constructor Generation With Complex Scenarios
    
    /// <summary>
    /// BUG REPRODUCTION: Complex scenarios with generic types and inheritance
    /// should not generate empty constructors.
    /// </summary>
    [Fact]
    public void Test_ComplexGenericInheritance_GeneratesCorrectConstructor()
    {
        // Arrange - Complex generic inheritance scenario
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace TestNamespace;

public interface IRepository<T> { }
public interface ICache<T> { }
public class Entity { }

[Service]
public partial class GenericBase<T>
{
    [Inject] private readonly ILogger<GenericBase<T>> _logger;
}

[Service] 
public partial class EntityService : GenericBase<Entity>
{
    [Inject] private readonly IRepository<Entity> _repository;
    [Inject] private readonly ICache<Entity> _cache;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        
        // Assert
        Assert.False(result.HasErrors);
        
        // Check EntityService constructor
        var entityConstructorSource = result.GetConstructorSource("EntityService");
        Assert.NotNull(entityConstructorSource);
        
        // CRITICAL: Should NOT be empty
        Assert.DoesNotContain("public EntityService() { }", entityConstructorSource.Content);
        
        // CRITICAL: Should contain base constructor call
        Assert.Contains("base(", entityConstructorSource.Content);
        
        // CRITICAL: Should contain derived dependencies
        Assert.Contains("IRepository<Entity>", entityConstructorSource.Content);
        Assert.Contains("ICache<Entity>", entityConstructorSource.Content);
    }
    
    #endregion
    
    #region REGRESSION PREVENTION: Edge Cases
    
    /// <summary>
    /// REGRESSION PREVENTION: Ensure constructor generation works with nullable types.
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

[Service]
public partial class NullableService
{
    [Inject] private readonly IOptionalService? _optionalService;
    [Inject] private readonly string? _optionalString;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        
        // Assert
        Assert.False(result.HasErrors);
        
        var constructorSource = result.GetConstructorSource("NullableService");
        Assert.NotNull(constructorSource);
        
        // CRITICAL: Should handle nullable types correctly
        Assert.DoesNotContain("public NullableService() { }", constructorSource.Content);
        Assert.Contains("IOptionalService?", constructorSource.Content);
        Assert.Contains("string?", constructorSource.Content);
    }
    
    /// <summary>
    /// REGRESSION PREVENTION: Static fields should be ignored in constructor generation.
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

[Service]
public partial class ServiceWithStatics
{
    [Inject] private static readonly ILogger<ServiceWithStatics>? _staticLogger; // Should be ignored
    [Inject] private readonly IRepository _repository; // Should be included
    private static readonly string StaticField = ""test""; // Should be ignored
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        
        // Assert
        Assert.False(result.HasErrors);
        
        var constructorSource = result.GetConstructorSource("ServiceWithStatics");
        Assert.NotNull(constructorSource);
        
        // CRITICAL: Should only include non-static [Inject] fields
        Assert.Contains("IRepository", constructorSource.Content);
        Assert.DoesNotContain("ILogger<ServiceWithStatics>", constructorSource.Content); // Static field ignored
        
        // Should contain assignment for non-static field only
        Assert.Contains("this._repository = repository;", constructorSource.Content);
        Assert.DoesNotContain("_staticLogger", constructorSource.Content);
    }
    
    #endregion
}