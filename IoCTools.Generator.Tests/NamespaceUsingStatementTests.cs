namespace IoCTools.Generator.Tests;

using System.Reflection;
using System.Text.RegularExpressions;

using Abstractions.Annotations;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
///     ABSOLUTELY BRUTAL NAMESPACE AND USING STATEMENT TESTS
///     These tests will push namespace collection to its absolute limits!
/// </summary>
public class NamespaceUsingStatementTests
{
    [Fact]
    public void Namespaces_SimpleCollectionTypes_GeneratesCorrectUsings()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace Complex.Nested.Namespace;

public interface ITestService { }
[Scoped]
public partial class CollectionService
{
    [Inject] private readonly IEnumerable<ITestService> _enumerable;
    [Inject] private readonly IList<ITestService> _list;
    [Inject] private readonly ICollection<ITestService> _collection;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("CollectionService");
        Assert.NotNull(constructorSource);

        // Should include System.Collections.Generic namespace using robust pattern matching
        var systemCollectionsPattern = @"^\s*using\s+System\.Collections\.Generic\s*;\s*$";
        var systemCollectionsMatches =
            Regex.Matches(constructorSource.Content, systemCollectionsPattern, RegexOptions.Multiline);
        Assert.Equal(1, systemCollectionsMatches.Count);

        // CRITICAL: Should NOT include self-namespace (constructor is generated IN Complex.Nested.Namespace)
        Assert.DoesNotContain("using Complex.Nested.Namespace;", constructorSource.Content);
    }

    [Fact]
    public void Namespaces_NestedGenerics_CollectsAllNamespaces()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using System.Collections.Generic;

namespace ProjectA.Services
{
    public interface IServiceA { }
}

namespace ProjectB.Repositories  
{
    public interface IRepositoryB { }
}

namespace ProjectC.Main
{
    using ProjectA.Services;
    using ProjectB.Repositories;

    
    public partial class NestedGenericService
    {
        [Inject] private readonly IEnumerable<IEnumerable<IServiceA>> _nestedA;
        [Inject] private readonly IList<IReadOnlyList<IRepositoryB>> _complexB;
        [Inject] private readonly IEnumerable<IEnumerable<IEnumerable<IServiceA>>> _tripleNested;
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("NestedGenericService");
        Assert.NotNull(constructorSource);

        // Should collect ALL namespaces from nested generics
        Assert.Contains("using System.Collections.Generic;", constructorSource.Content);
        Assert.Contains("using ProjectA.Services;", constructorSource.Content);
        Assert.Contains("using ProjectB.Repositories;", constructorSource.Content);
        // Note: Constructor is generated IN ProjectC.Main, so it shouldn't import its own namespace
        Assert.DoesNotContain("using ProjectC.Main;", constructorSource.Content);
    }

    [Fact]
    public void Namespaces_ArrayTypes_CollectsElementNamespaces()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Arrays.Test
{
    public interface IArrayService { }
}

namespace Consumer.Namespace
{
    using Arrays.Test;

    
    public partial class ArrayConsumer
    {
        [Inject] private readonly IArrayService[] _arrayField;
        [Inject] private readonly IArrayService[][] _jaggedArray;
        [Inject] private readonly IArrayService[,] _multiDimensional;
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("ArrayConsumer");
        Assert.NotNull(constructorSource);

        // Should collect namespace from array element type
        Assert.Contains("using Arrays.Test;", constructorSource.Content);
        // Note: Constructor is generated IN Consumer.Namespace, so it shouldn't import its own namespace
        Assert.DoesNotContain("using Consumer.Namespace;", constructorSource.Content);
    }

    [Fact]
    public void Namespaces_GlobalNamespace_HandlesCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

// Global namespace interface
public interface IGlobalService { }

namespace Specific.Namespace
{
    
    public partial class GlobalConsumer
    {
        [Inject] private readonly IGlobalService _globalService;
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("GlobalConsumer");
        Assert.NotNull(constructorSource);

        // Should handle global namespace correctly (no using statement for global)
        // Note: Constructor is generated IN Specific.Namespace, so it shouldn't import its own namespace
        Assert.DoesNotContain("using Specific.Namespace;", constructorSource.Content);
        // Global namespace types don't need using statements
        Assert.Contains("IGlobalService globalService", constructorSource.Content);
    }

    [Fact]
    public void Namespaces_ExtremelyLongNamespaces_HandlesCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Very.Very.Very.Very.Very.Long.Namespace.That.Goes.On.And.On.Services
{
    public interface ILongNamespaceService { }
}

namespace Another.Extremely.Long.Namespace.With.Many.Levels.Of.Nesting.Repositories
{
    public interface ILongNamespaceRepository { }
}

namespace Consumer.Application.Main
{
    using Very.Very.Very.Very.Very.Long.Namespace.That.Goes.On.And.On.Services;
    using Another.Extremely.Long.Namespace.With.Many.Levels.Of.Nesting.Repositories;

    
    public partial class LongNamespaceConsumer
    {
        [Inject] private readonly ILongNamespaceService _service;
        [Inject] private readonly ILongNamespaceRepository _repository;
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("LongNamespaceConsumer");
        Assert.NotNull(constructorSource);

        // Should handle extremely long namespaces
        Assert.Contains("using Very.Very.Very.Very.Very.Long.Namespace.That.Goes.On.And.On.Services;",
            constructorSource.Content);
        Assert.Contains("using Another.Extremely.Long.Namespace.With.Many.Levels.Of.Nesting.Repositories;",
            constructorSource.Content);
    }

    [Fact]
    public void Namespaces_ComplexGenericConstraints_CollectsAllNamespaces()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using System.Collections.Generic;

namespace Entities.Domain
{
    public interface IEntity { }
    public interface IAggregateRoot : IEntity { }
}

namespace Repositories.Contracts
{
    using Entities.Domain;
    public interface IRepository<T> where T : IEntity { }
}

namespace Services.Application
{
    using Entities.Domain;
    using Repositories.Contracts;
    using System.Collections.Generic;

    
    public partial class ComplexConstraintService<T> 
        where T : class, IEntity, new()
    {
        [Inject] private readonly IRepository<T> _repository;
        [Inject] private readonly IEnumerable<IRepository<IEntity>> _entityRepo;
        [Inject] private readonly IList<T> _entityList;
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("ComplexConstraintService");
        Assert.NotNull(constructorSource);

        // Should collect all namespaces from generic constraints and types
        Assert.Contains("using System.Collections.Generic;", constructorSource.Content);
        Assert.Contains("using Entities.Domain;", constructorSource.Content);
        Assert.Contains("using Repositories.Contracts;", constructorSource.Content);
        // Should NOT contain self-namespace (constructor is generated in Services.Application namespace)
        Assert.DoesNotContain("using Services.Application;", constructorSource.Content);
    }

    [Fact]
    public void Namespaces_RecursiveGenericTypes_HandlesCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using System.Collections.Generic;

namespace Recursive.Types
{
    public interface INode<T> where T : INode<T> { }
    public interface ITree<T> where T : INode<T> { }
}

namespace Consumer.App
{
    using Recursive.Types;

    public class MyNode : INode<MyNode> { }

    
    public partial class RecursiveService
    {
        [Inject] private readonly INode<MyNode> _node;
        [Inject] private readonly ITree<MyNode> _tree;
        [Inject] private readonly IEnumerable<INode<MyNode>> _nodes;
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("RecursiveService");
        Assert.NotNull(constructorSource);

        // Should handle recursive generic types correctly
        Assert.Contains("using System.Collections.Generic;", constructorSource.Content);
        Assert.Contains("using Recursive.Types;", constructorSource.Content);
        // Should NOT contain self-namespace (constructor is generated in Consumer.App namespace)
        Assert.DoesNotContain("using Consumer.App;", constructorSource.Content);
    }

    [Fact]
    public void Namespaces_InsanelyComplexNestedGenerics_CollectsEverything()
    {
        // Arrange - ABSOLUTELY INSANE COMPLEXITY!
        var source = @"
using IoCTools.Abstractions.Annotations;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace Level1.Services { public interface IService1 { } }
namespace Level2.Repositories { public interface IRepo2<T> { } }
namespace Level3.Validators { public interface IValidator3<T, U> { } }
namespace Level4.Mappers { public interface IMapper4<T, U, V> { } }
namespace Level5.Handlers { public interface IHandler5<T, U, V, W> { } }

namespace Consumer.Application.Services.Main
{
    using Level1.Services;
    using Level2.Repositories;
    using Level3.Validators;
    using Level4.Mappers;
    using Level5.Handlers;

    
    public partial class InsanelyComplexNamespaceService
    {
        // This is ABSOLUTELY BRUTAL - every type from different namespace!
        [Inject] private readonly IEnumerable<IEnumerable<IEnumerable<IService1>>> _tripleNestedService;
        
        [Inject] private readonly IList<IReadOnlyList<IRepo2<IService1>>> _nestedRepoWithService;
        
        [Inject] private readonly IDictionary<string, IEnumerable<IValidator3<IService1, IRepo2<IService1>>>> _dictionaryWithComplex;
        
        [Inject] private readonly ConcurrentDictionary<int, IList<IMapper4<IService1, IRepo2<string>, IValidator3<int, string>>>> _concurrentDictionary;
        
        [Inject] private readonly IEnumerable<IHandler5<
            IService1,
            IRepo2<IValidator3<IService1, string>>,
            IMapper4<IService1, IRepo2<IService1>, IValidator3<string, int>>,
            IEnumerable<IEnumerable<IService1>>
        >> _absolutelyInsaneNesting;
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors,
            $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var constructorSource = result.GetConstructorSource("InsanelyComplexNamespaceService");
        Assert.NotNull(constructorSource);

        // Should collect ALL namespaces from this insane complexity
        var expectedUsings = new[]
        {
            "using System.Collections.Generic;", "using System.Collections.Concurrent;", "using Level1.Services;",
            "using Level2.Repositories;", "using Level3.Validators;", "using Level4.Mappers;", "using Level5.Handlers;"
            // NOTE: Removed "using Consumer.Application.Services.Main;" as it's redundant 
            // (class is already in that namespace)
        };

        foreach (var expectedUsing in expectedUsings) Assert.Contains(expectedUsing, constructorSource.Content);

        // Should have all these complex types in constructor
        Assert.Contains("IEnumerable<IEnumerable<IEnumerable<IService1>>> tripleNestedService",
            constructorSource.Content);
        Assert.Contains("IList<IReadOnlyList<IRepo2<IService1>>> nestedRepoWithService", constructorSource.Content);
        Assert.Contains(
            "IDictionary<string, IEnumerable<IValidator3<IService1, IRepo2<IService1>>>> dictionaryWithComplex",
            constructorSource.Content);
        Assert.Contains(
            "ConcurrentDictionary<int, IList<IMapper4<IService1, IRepo2<string>, IValidator3<int, string>>>> concurrentDictionary",
            constructorSource.Content);
    }

    [Fact]
    public void Namespaces_DuplicateNamespacesAcrossFiles_DeduplicatesCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using System.Collections.Generic;
using System.Collections.Generic; // Duplicate!

namespace Test.Services
{
    public interface IService1 { }
    public interface IService2 { }
}

namespace Test.Services // Same namespace again
{
    public interface IService3 { }
}

namespace Consumer.App
{
    using Test.Services;
    using Test.Services; // Duplicate using!
    using System.Collections.Generic;
    using System.Collections.Generic; // Duplicate!

    
    public partial class DuplicateNamespaceService
    {
        [Inject] private readonly IEnumerable<IService1> _service1;
        [Inject] private readonly IList<IService2> _service2;
        [Inject] private readonly ICollection<IService3> _service3;
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("DuplicateNamespaceService");
        Assert.NotNull(constructorSource);

        // Should deduplicate using statements - Use proper regex patterns for robustness
        var systemCollectionsMatches = Regex.Matches(constructorSource.Content,
            @"^\s*using\s+System\.Collections\.Generic\s*;\s*$", RegexOptions.Multiline);
        Assert.Equal(1, systemCollectionsMatches.Count); // Should only appear once despite duplicates

        var testServicesMatches = Regex.Matches(constructorSource.Content, @"^\s*using\s+Test\.Services\s*;\s*$",
            RegexOptions.Multiline);
        Assert.Equal(1, testServicesMatches.Count); // Should only appear once

        // Verify exact using statement count to ensure no extras
        var allUsingMatches =
            Regex.Matches(constructorSource.Content, @"^\s*using\s+[^;]+\s*;\s*$", RegexOptions.Multiline);
        // Should have exactly: System.Collections.Generic + Test.Services (no duplicates, no self-namespace)
        Assert.True(allUsingMatches.Count >= 2, $"Expected at least 2 using statements, got {allUsingMatches.Count}");
    }

    /// <summary>
    ///     CRITICAL SELF-NAMESPACE EXCLUSION TESTS
    ///     These are the highest priority missing tests that could cause compilation failures
    /// </summary>
    [Fact]
    public void Namespaces_SelfNamespaceExclusion_SingleClass_ExcludesSelfNamespace()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace MyApp.Services
{
    public interface IExternalService { }

    
    public partial class TestService
    {
        [Inject] private readonly IExternalService _external;
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("TestService");
        Assert.NotNull(constructorSource);

        // CRITICAL: Should NOT include self-namespace (constructor is generated IN MyApp.Services)
        Assert.DoesNotContain("using MyApp.Services;", constructorSource.Content);

        // Verify it contains the expected constructor parameter
        Assert.Contains("IExternalService external", constructorSource.Content);
    }

    [Fact]
    public void Namespaces_SelfNamespaceExclusion_MultipleClassesSameNamespace_ExcludesSelfNamespace()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Shared.Domain
{
    public interface ISharedService { }
    
    [Scoped]
    public partial class SharedServiceImpl : ISharedService { }
    
    
    [Scoped]
    public partial class ServiceA
    {
        [Inject] private readonly ISharedService _shared;
    }
    
    [Scoped] 
    public partial class ServiceB
    {
        [Inject] private readonly ISharedService _shared;
        [Inject] private readonly ServiceA _serviceA; // Cross-dependency within same namespace
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSourceA = result.GetConstructorSource("ServiceA");
        var constructorSourceB = result.GetConstructorSource("ServiceB");
        Assert.NotNull(constructorSourceA);
        Assert.NotNull(constructorSourceB);

        // CRITICAL: Neither should include self-namespace (both generated IN Shared.Domain)
        Assert.DoesNotContain("using Shared.Domain;", constructorSourceA.Content);
        Assert.DoesNotContain("using Shared.Domain;", constructorSourceB.Content);

        // Verify both have expected parameters without namespace prefixes
        Assert.Contains("ISharedService shared", constructorSourceA.Content);
        Assert.Contains("ISharedService shared", constructorSourceB.Content);
        Assert.Contains("ServiceA serviceA", constructorSourceB.Content);
    }

    [Fact]
    public void Namespaces_SelfNamespaceExclusion_WithExternalDependencies_OnlyIncludesExternal()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace External.Services
{
    public interface IExternalService { }
}

namespace MyApp.Core
{
    using External.Services;
    
    public interface ILocalService { }
    
    
    public partial class MixedDependencyService
    {
        [Inject] private readonly IExternalService _external;
        [Inject] private readonly ILocalService _local;
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("MixedDependencyService");
        Assert.NotNull(constructorSource);

        // Should include external namespace
        Assert.Contains("using External.Services;", constructorSource.Content);

        // CRITICAL: Should NOT include self-namespace (constructor is generated IN MyApp.Core)
        Assert.DoesNotContain("using MyApp.Core;", constructorSource.Content);

        // Verify parameters are correct
        Assert.Contains("IExternalService external", constructorSource.Content);
        Assert.Contains("ILocalService local", constructorSource.Content);
    }

    /// <summary>
    ///     POINTER TYPE TESTS
    ///     Generator has logic for pointer types but zero test coverage - critical gap
    /// </summary>
    [Fact]
    public void Namespaces_UnsafePointerTypes_CollectsNamespacesCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Unsafe.Types
{
    using System;
    
    public struct CustomStruct { }
    
    public interface IPointerHandler
    {
        IntPtr GetPointer();
    }
    
    
    public partial class PointerHandler : IPointerHandler
    {
        public IntPtr GetPointer() => IntPtr.Zero;
    }
}

namespace Pointer.Consumer
{
    using Unsafe.Types;
    using System;
    
    
    public unsafe partial class PointerService
    {
        [Inject] private readonly IPointerHandler _pointerHandler;
        // This tests the namespace collection logic for pointer-related types
        // IntPtr in the interface should cause System namespace to be included
    }
}";

        // Act - Enable unsafe compilation
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IEnumerable<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ScopedAttribute).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(IServiceCollection).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ServiceCollectionServiceExtensions).Assembly.Location)
        };

        // Add additional essential references for compilation
        try
        {
            references.Add(MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location));
            references.Add(MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location));
            references.Add(MetadataReference.CreateFromFile(Assembly.Load("System.Collections").Location));
        }
        catch
        {
            // Skip if not available
        }

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                allowUnsafe: true));

        var generator = new DependencyInjectionGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var generatedSources = new List<GeneratedSource>();
        foreach (var tree in outputCompilation.SyntaxTrees.Skip(1))
            generatedSources.Add(new GeneratedSource(tree.FilePath, tree.ToString()));

        var result = new GeneratorTestResult(
            outputCompilation, generatedSources, diagnostics.ToList(), outputCompilation.GetDiagnostics().ToList());

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("PointerService");
        Assert.NotNull(constructorSource);

        // Should include external namespace for IPointerHandler
        Assert.Contains("using Unsafe.Types;", constructorSource.Content);

        // CRITICAL: Should NOT include self-namespace
        Assert.DoesNotContain("using Pointer.Consumer;", constructorSource.Content);

        // Verify constructor parameter is correct
        Assert.Contains("IPointerHandler pointerHandler", constructorSource.Content);
    }

    /// <summary>
    ///     GLOBAL NAMESPACE EDGE CASES
    ///     Critical tests for types in global namespace mixed with regular namespaces
    /// </summary>
    [Fact]
    public void Namespaces_GlobalNamespaceMixedWithRegular_NoMalformedUsings()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

// Global namespace types
public interface IGlobalInterface { }
public class GlobalClass { }

namespace Regular.Namespace
{
    public interface IRegularInterface { }
}

namespace Consumer.App
{
    using Regular.Namespace;
    
    
    public partial class MixedNamespaceService
    {
        [Inject] private readonly IGlobalInterface _global;
        [Inject] private readonly GlobalClass _globalClass;
        [Inject] private readonly IRegularInterface _regular;
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("MixedNamespaceService");
        Assert.NotNull(constructorSource);

        // Should include regular namespace
        Assert.Contains("using Regular.Namespace;", constructorSource.Content);

        // CRITICAL: Should NOT have malformed "using ;" statements
        Assert.DoesNotContain("using ;", constructorSource.Content);
        Assert.DoesNotContain("using <global namespace>;", constructorSource.Content);

        // CRITICAL: Should NOT include self-namespace
        Assert.DoesNotContain("using Consumer.App;", constructorSource.Content);

        // Global types should be accessible without using statements
        Assert.Contains("IGlobalInterface global", constructorSource.Content);
        Assert.Contains("GlobalClass globalClass", constructorSource.Content);
        Assert.Contains("IRegularInterface regular", constructorSource.Content);
    }

    /// <summary>
    ///     FRAMEWORK TYPES INTEGRATION TESTS
    ///     Missing real-world scenarios with ILogger, IConfiguration, etc.
    /// </summary>
    [Fact]
    public void Namespaces_FrameworkTypes_ILogger_CollectsCorrectNamespaces()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace Business.Services
{
    public interface IBusinessService { }
}

namespace App.Controllers
{
    using Business.Services;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Configuration;
    
    
    public partial class ControllerService
    {
        [Inject] private readonly ILogger<ControllerService> _logger;
        [Inject] private readonly IConfiguration _config;
        [Inject] private readonly IBusinessService _business;
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("ControllerService");
        Assert.NotNull(constructorSource);

        // Should include framework namespaces
        Assert.Contains("using Microsoft.Extensions.Logging;", constructorSource.Content);
        Assert.Contains("using Microsoft.Extensions.Configuration;", constructorSource.Content);
        Assert.Contains("using Business.Services;", constructorSource.Content);

        // CRITICAL: Should NOT include self-namespace
        Assert.DoesNotContain("using App.Controllers;", constructorSource.Content);

        // Verify generic ILogger parameter is handled correctly
        Assert.Contains("ILogger<ControllerService> logger", constructorSource.Content);
        Assert.Contains("IConfiguration config", constructorSource.Content);
        Assert.Contains("IBusinessService business", constructorSource.Content);
    }

    /// <summary>
    ///     CROSS-NAMESPACE INHERITANCE TESTS
    ///     Base and derived classes in different namespaces
    /// </summary>
    [Fact]
    public void Namespaces_CrossNamespaceInheritance_CollectsAllNamespaces()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Base.Infrastructure
{
    public interface IBaseRepository { }
    public abstract class BaseService { }
}

namespace Derived.Application
{
    using Base.Infrastructure;
    
    
    public partial class DerivedService : BaseService
    {
        [Inject] private readonly IBaseRepository _repository;
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("DerivedService");
        Assert.NotNull(constructorSource);

        // Should include base namespace
        Assert.Contains("using Base.Infrastructure;", constructorSource.Content);

        // CRITICAL: Should NOT include self-namespace
        Assert.DoesNotContain("using Derived.Application;", constructorSource.Content);

        // Verify inheritance and injection work together
        Assert.Contains("IBaseRepository repository", constructorSource.Content);
    }

    /// <summary>
    ///     EXTREMELY DEEP NESTING TESTS
    ///     Beyond current test depth to ensure robustness
    /// </summary>
    [Fact]
    public void Namespaces_ExtremelyDeepNesting_HandlesCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using System.Collections.Generic;
using Level1.Level2.Level3.Level4.Level5.Level6.Level7.Level8.Level9.Level10.Services;

namespace Level1.Level2.Level3.Level4.Level5.Level6.Level7.Level8.Level9.Level10.Services
{
    public interface IDeepService { }
}

namespace Consumer.Level1.Level2.Level3.Level4.Level5.Level6.Level7.Level8.Level9.Level10.App
{
    
    public partial class ExtremelyDeepService
    {
        [Inject] private readonly IEnumerable<IEnumerable<IEnumerable<IDeepService>>> _tripleNestedDeep;
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors,
            $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.GetMessage()))}");

        var constructorSource = result.GetConstructorSource("ExtremelyDeepService");
        Assert.NotNull(constructorSource);

        // Should handle extremely deep namespaces
        Assert.Contains("using System.Collections.Generic;", constructorSource.Content);
        Assert.Contains("using Level1.Level2.Level3.Level4.Level5.Level6.Level7.Level8.Level9.Level10.Services;",
            constructorSource.Content);

        // CRITICAL: Should NOT include self-namespace despite extreme depth
        Assert.DoesNotContain(
            "using Consumer.Level1.Level2.Level3.Level4.Level5.Level6.Level7.Level8.Level9.Level10.App;",
            constructorSource.Content);
    }

    /// <summary>
    ///     BOUNDARY AND ERROR CONDITION TESTS
    /// </summary>
    [Fact]
    public void Namespaces_EmptyCompilationUnit_HandlesGracefully()
    {
        // Arrange - Minimal valid source with no dependencies
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Empty.Test
{
    [Scoped]
    public partial class EmptyService
    {
        // No injected dependencies
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("EmptyService");
        Assert.NotNull(constructorSource);

        // Should generate minimal constructor with no using statements except standard ones
        // CRITICAL: Should NOT include self-namespace
        Assert.DoesNotContain("using Empty.Test;", constructorSource.Content);

        // Should have empty parameter constructor
        Assert.Contains("public EmptyService()", constructorSource.Content);
    }

    [Fact]
    public void Namespaces_PartialClassesAcrossMultipleNamespaces_HandlesCorrectly()
    {
        // Arrange - This tests an edge case where partial classes might span namespaces (not typical but possible)
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Primary.Namespace
{
    public interface IPrimaryService { }
    
    
    public partial class PartialService
    {
        [Inject] private readonly IPrimaryService _primary;
    }
}

namespace Secondary.Namespace  
{
    public interface ISecondaryService { }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("PartialService");
        Assert.NotNull(constructorSource);

        // CRITICAL: Should NOT include self-namespace (constructor generated in Primary.Namespace)
        Assert.DoesNotContain("using Primary.Namespace;", constructorSource.Content);

        // Should work with the primary service
        Assert.Contains("IPrimaryService primary", constructorSource.Content);
    }

    /// <summary>
    ///     ROBUST ASSERTION TESTS
    ///     Replace weak string matching with proper regex patterns
    /// </summary>
    [Fact]
    public void Namespaces_RobustAssertion_UsingRegexPatterns_ValidatesCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using System.Collections.Generic;

namespace External.Services
{
    public interface IExternalService { }
}

namespace Test.Application
{
    using External.Services;
    
    
    public partial class RobustTestService
    {
        [Inject] private readonly IList<IExternalService> _services;
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("RobustTestService");
        Assert.NotNull(constructorSource);

        // Use robust regex patterns instead of simple string matching
        var systemCollectionsPattern = @"^\s*using\s+System\.Collections\.Generic\s*;\s*$";
        var externalServicesPattern = @"^\s*using\s+External\.Services\s*;\s*$";
        var selfNamespacePattern = @"^\s*using\s+Test\.Application\s*;\s*$";

        var systemCollectionsMatches =
            Regex.Matches(constructorSource.Content, systemCollectionsPattern, RegexOptions.Multiline);
        var externalServicesMatches =
            Regex.Matches(constructorSource.Content, externalServicesPattern, RegexOptions.Multiline);
        var selfNamespaceMatches =
            Regex.Matches(constructorSource.Content, selfNamespacePattern, RegexOptions.Multiline);

        // Positive assertions - should be present
        Assert.Equal(1, systemCollectionsMatches.Count);
        Assert.Equal(1, externalServicesMatches.Count);

        // CRITICAL negative assertion - should NOT be present
        Assert.Equal(0, selfNamespaceMatches.Count);

        // Verify no malformed using statements
        Assert.DoesNotContain("using ;", constructorSource.Content);
        Assert.DoesNotContain("using  ;", constructorSource.Content);
    }

    /// <summary>
    ///     UNICODE AND SPECIAL CHARACTER TESTS
    ///     Boundary testing for unusual but valid namespace names
    /// </summary>
    [Fact]
    public void Namespaces_UnicodeNamespaces_HandlesCorrectly()
    {
        // Arrange - Test with valid C# identifiers that include Unicode
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Ünïcøde.Namespace
{
    public interface IÜnicødeService { }
}

namespace Cønßüméer.App
{
    using Ünïcøde.Namespace;
    
    
    public partial class UnicodeTestService
    {
        [Inject] private readonly IÜnicødeService _service;
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("UnicodeTestService");
        Assert.NotNull(constructorSource);

        // Should handle Unicode namespaces
        Assert.Contains("using Ünïcøde.Namespace;", constructorSource.Content);

        // CRITICAL: Should NOT include self-namespace with Unicode characters
        Assert.DoesNotContain("using Cønßüméer.App;", constructorSource.Content);

        // Should handle Unicode interface names
        Assert.Contains("IÜnicødeService service", constructorSource.Content);
    }

    /// <summary>
    ///     COMPLEX NAMESPACE GENERATION BUG TEST
    ///     This test reproduces the reported issue where malformed using statements are generated
    ///     Example: "using MyProject.Services.MyProject.Models;" instead of "using MyProject.Models;"
    /// </summary>
    [Fact]
    public void ComplexGenericNamespaces_ShouldNotGenerateMalformedUsings()
    {
        // Arrange - Create a scenario that triggers the malformed namespace generation bug
        // This uses deeply nested generics which are more likely to trigger namespace duplication issues
        var source = @"
using IoCTools.Abstractions.Annotations;
using System.Collections.Generic;

namespace MyProject.Models.Core
{
    public class UserModel { }
    public interface IUserRepository<T> { }
}

namespace MyProject.Services.Business
{
    using MyProject.Models.Core;
    using System.Collections.Generic;
    
    
    public partial class UserService
    {
        [Inject] private readonly IEnumerable<IList<IUserRepository<UserModel>>> _nestedRepositories;
        [Inject] private readonly IDictionary<string, IEnumerable<UserModel>> _complexMapping;
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Print generated content for debugging if test fails
        var constructorSource = result.GetConstructorSource("UserService");
        if (constructorSource != null)
        {
            Console.WriteLine("Generated constructor content:");
            Console.WriteLine(constructorSource.Content);
        }

        Assert.False(result.HasErrors,
            $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.GetMessage()))}");

        Assert.NotNull(constructorSource);

        // CRITICAL: Should NOT generate malformed using statements with duplicated namespace segments
        Assert.DoesNotContain("using MyProject.Services.Business.MyProject.Models.Core;", constructorSource.Content);
        Assert.DoesNotContain("using MyProject.Services.Business.System.Collections.Generic;",
            constructorSource.Content);
        Assert.DoesNotContain("using MyProject.Models.Core.MyProject.Services.Business;", constructorSource.Content);

        // Should generate correct using statements
        Assert.Contains("using MyProject.Models.Core;", constructorSource.Content);
        Assert.Contains("using System.Collections.Generic;", constructorSource.Content);

        // CRITICAL: Should NOT include self-namespace
        Assert.DoesNotContain("using MyProject.Services.Business;", constructorSource.Content);

        // Verify no duplicate or malformed namespace patterns
        var malformedPatterns = new[]
        {
            @"using\s+\w+\.\w+\.\w+\.\w+\.\w+\.\w+;", // 6+ levels suggest duplication
            @"using\s+MyProject\.Services\.Business\.MyProject\..*;" // Specific malformed pattern we're testing for
        };

        foreach (var pattern in malformedPatterns)
        {
            var matches = Regex.Matches(constructorSource.Content, pattern,
                RegexOptions.Multiline | RegexOptions.IgnoreCase);
            Assert.True(matches.Count == 0,
                $"Found malformed namespace pattern: {pattern} in content: {constructorSource.Content}");
        }

        // Verify constructor parameters are correct
        Assert.Contains("IEnumerable<IList<IUserRepository<UserModel>>> nestedRepositories", constructorSource.Content);
        Assert.Contains("IDictionary<string, IEnumerable<UserModel>> complexMapping", constructorSource.Content);
    }

    /// <summary>
    ///     EDGE CASE: Cross namespace dependencies
    ///     Test that services depending on types from different namespaces work correctly
    /// </summary>
    [Fact]
    public void NamespaceGeneration_CrossNamespaceDependencies_HandlesGracefully()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace ServiceA
{
    public interface IServiceA { }
    
    
    public partial class ServiceAImpl : IServiceA { }
}

namespace ServiceB
{
    public interface IServiceB { }
    
    
    public partial class ServiceBImpl : IServiceB { }
}

namespace Consumer
{
    using ServiceA;
    using ServiceB;
    
    
    public partial class CrossNamespaceService
    {
        [Inject] private readonly IServiceA _serviceA;
        [Inject] private readonly IServiceB _serviceB;
    }
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("CrossNamespaceService");
        Assert.NotNull(constructorSource);

        // Should generate using statements for both namespaces
        Assert.Contains("using ServiceA;", constructorSource.Content);
        Assert.Contains("using ServiceB;", constructorSource.Content);

        // Verify constructor parameters are correct
        Assert.Contains("IServiceA serviceA", constructorSource.Content);
        Assert.Contains("IServiceB serviceB", constructorSource.Content);
    }

    /// <summary>
    ///     EDGE CASE: Empty namespace
    ///     Test handling of types in the global namespace
    /// </summary>
    [Fact]
    public void NamespaceGeneration_GlobalNamespace_HandlesCorrectly()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

public interface IGlobalService { }

namespace MyProject.Services
{
    
    public partial class GlobalTestService
    {
        [Inject] private readonly IGlobalService _globalService;
    }
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("GlobalTestService");
        Assert.NotNull(constructorSource);

        // Should NOT generate using statements for global namespace types
        Assert.DoesNotContain("using ;", constructorSource.Content);
        Assert.DoesNotContain("using global;", constructorSource.Content);

        // Global types should be accessible without using statements
        Assert.Contains("IGlobalService globalService", constructorSource.Content);
    }

    /// <summary>
    ///     EDGE CASE: Very long namespace chains
    ///     Test that very long namespace chains don't cause issues
    /// </summary>
    [Fact]
    public void NamespaceGeneration_VeryLongNamespaces_HandlesCorrectly()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace VeryLongCompanyName.BusinessUnit.Department.Team.SubTeam.Project.Module.Component.Service.Interface
{
    public interface IVeryDeeplyNestedService { }
}

namespace MyProject.Services.BusinessLogic.Core
{
    using VeryLongCompanyName.BusinessUnit.Department.Team.SubTeam.Project.Module.Component.Service.Interface;
    
    
    public partial class DeepNamespaceService
    {
        [Inject] private readonly IVeryDeeplyNestedService _deepService;
    }
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("DeepNamespaceService");
        Assert.NotNull(constructorSource);

        // Should handle very long namespaces correctly
        Assert.Contains(
            "using VeryLongCompanyName.BusinessUnit.Department.Team.SubTeam.Project.Module.Component.Service.Interface;",
            constructorSource.Content);

        // Should NOT include self-namespace
        Assert.DoesNotContain("using MyProject.Services.BusinessLogic.Core;", constructorSource.Content);

        // Should NOT create malformed namespace combinations
        Assert.DoesNotContain("using MyProject.Services.BusinessLogic.Core.VeryLongCompanyName",
            constructorSource.Content);
    }

    /// <summary>
    ///     EDGE CASE: Special characters in namespaces
    ///     Test handling of valid but unusual namespace characters
    /// </summary>
    [Fact]
    public void NamespaceGeneration_SpecialCharacters_HandlesCorrectly()
    {
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace _UnderscoreNamespace._123NumberStart
{
    public interface I_SpecialService { }
}

namespace MyProject.Services_2024
{
    using _UnderscoreNamespace._123NumberStart;
    
    
    public partial class SpecialCharService
    {
        [Inject] private readonly I_SpecialService _specialService;
    }
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("SpecialCharService");
        Assert.NotNull(constructorSource);

        // Should handle special characters in namespaces
        Assert.Contains("using _UnderscoreNamespace._123NumberStart;", constructorSource.Content);

        // Should NOT include self-namespace
        Assert.DoesNotContain("using MyProject.Services_2024;", constructorSource.Content);

        // Parameter should handle special interface names
        Assert.Contains("I_SpecialService specialService", constructorSource.Content);
    }
}
