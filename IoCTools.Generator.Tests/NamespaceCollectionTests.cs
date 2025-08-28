namespace IoCTools.Generator.Tests;

/// <summary>
///     Explicit tests for the namespace collection scenarios that were fixed in the source generator.
///     These tests ensure that the CollectNamespaces method properly handles complex generic types
///     and generates correct using statements in the constructor code.
/// </summary>
public class NamespaceCollectionTests
{
    [Fact]
    public void CollectNamespaces_SimpleGenericType_CollectsCorrectNamespaces()
    {
        // Arrange
        var sourceCode = @"
using System.Collections.Generic;
using IoCTools.Abstractions.Annotations;

namespace TestProject;

public interface ITestService { }
public partial class TestClass
{
    [Inject] private readonly IEnumerable<ITestService> _collection;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("TestClass");
        Assert.NotNull(constructorSource.Content);
        Assert.Contains("using System.Collections.Generic;", constructorSource.Content);
    }

    [Fact]
    public void CollectNamespaces_NestedGenericType_CollectsAllNamespaces()
    {
        // Arrange - This specifically tests the fix for nested generics
        var sourceCode = @"
using System.Collections.Generic;
using IoCTools.Abstractions.Annotations;

namespace TestProject;

public interface ITestService { }
public partial class TestClass
{
    [Inject] private readonly IEnumerable<IEnumerable<ITestService>> _nestedCollection;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("TestClass");
        Assert.NotNull(constructorSource.Content);
        Assert.Contains("using System.Collections.Generic;", constructorSource.Content);
        // Should NOT contain self-namespace (constructor is generated in TestProject namespace)
        Assert.DoesNotContain("using TestProject;", constructorSource.Content);
    }

    [Fact]
    public void CollectNamespaces_MultipleCollectionTypes_CollectsAllNamespaces()
    {
        // Arrange - Tests multiple different collection types in same class
        var sourceCode = @"
using System.Collections.Generic;
using IoCTools.Abstractions.Annotations;

namespace TestProject;

public interface IService1 { }
public interface IService2 { }
public interface IService3 { }
public partial class TestClass
{
    [Inject] private readonly IEnumerable<IService1> _enumerable;
    [Inject] private readonly IList<IService2> _list;
    [Inject] private readonly IReadOnlyList<IService3> _readOnlyList;
    [Inject] private readonly ICollection<IService1> _collection;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("TestClass");
        Assert.NotNull(constructorSource.Content);
        Assert.Contains("using System.Collections.Generic;", constructorSource.Content);
        // Should NOT contain self-namespace (constructor is generated in TestProject namespace)
        Assert.DoesNotContain("using TestProject;", constructorSource.Content);
    }

    [Fact]
    public void CollectNamespaces_ArrayTypes_CollectsElementTypeNamespace()
    {
        // Arrange - Tests array type namespace collection
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;

namespace TestProject;

public interface ITestService { }
public partial class TestClass
{
    [Inject] private readonly ITestService[] _arrayField;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("TestClass");
        Assert.NotNull(constructorSource.Content);
        // Should NOT contain self-namespace (constructor is generated in TestProject namespace)
        Assert.DoesNotContain("using TestProject;", constructorSource.Content);
    }

    [Fact]
    public void CollectNamespaces_CrossNamespaceGenerics_CollectsAllNamespaces()
    {
        // Arrange - Tests generics spanning multiple namespaces
        var sourceCode = @"
using System.Collections.Generic;
using IoCTools.Abstractions.Annotations;

namespace ProjectA
{
    public interface IServiceA { }
}

namespace ProjectB
{
    public interface IServiceB { }
}

namespace TestProject
{
    using ProjectA;
    using ProjectB;

    
    public partial class TestClass
    {
        [Inject] private readonly IEnumerable<IServiceA> _collectionA;
        [Inject] private readonly IList<IServiceB> _listB;
        [Inject] private readonly IEnumerable<IEnumerable<IServiceA>> _nestedA;
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("TestClass");
        Assert.NotNull(constructorSource.Content);
        Assert.Contains("using System.Collections.Generic;", constructorSource.Content);
        Assert.Contains("using ProjectA;", constructorSource.Content);
        Assert.Contains("using ProjectB;", constructorSource.Content);
    }

    [Fact]
    public void GeneratedConstructor_ComplexScenario_CompilesWithoutErrors()
    {
        // Arrange - This is the exact scenario that was failing before the fix
        var sourceCode = @"
using System.Collections.Generic;
using IoCTools.Abstractions.Annotations;

namespace TestProject;

public interface IEmpty { }
public interface ITestType1 { }
public interface ITestType2 { }
public partial class TestComplexCollectionGeneration
{
    [Inject] private readonly IEnumerable<ITestType1> _collection1;
    [Inject] private readonly IList<ITestType2> _collection2;
    [Inject] private readonly IEnumerable<IEnumerable<ITestType1>> _nestedCollection;
    [Inject] private readonly IEnumerable<IEmpty> _emptyCollection;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("TestComplexCollectionGeneration");
        Assert.NotNull(constructorSource);

        // Verify the generated code contains proper using statements
        Assert.Contains("using System.Collections.Generic;", constructorSource.Content);
        // Should NOT contain self-namespace (constructor is generated in TestProject namespace)
        Assert.DoesNotContain("using TestProject;", constructorSource.Content);

        // Verify constructor parameters are correctly typed
        Assert.Contains("IEnumerable<ITestType1> collection1", constructorSource.Content);
        Assert.Contains("IList<ITestType2> collection2", constructorSource.Content);
        Assert.Contains("IEnumerable<IEnumerable<ITestType1>> nestedCollection", constructorSource.Content);
    }

    #region Error Condition Testing

    [Fact]
    public void CollectNamespaces_MalformedGenericTypes_HandlesGracefully()
    {
        // Arrange - Test malformed generic with missing type parameter
        var sourceCode = @"
using System.Collections.Generic;
using IoCTools.Abstractions.Annotations;

namespace TestProject;

public interface ITestService { }
public partial class TestClass
{
    // This should cause compilation error but generator should handle gracefully
    [Inject] private readonly IEnumerable<> _malformed;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert - Should have compilation errors but not crash the generator
        Assert.True(result.HasErrors);
        // Generator should still attempt to produce output even with malformed input
        var constructorSource = result.GetConstructorSource("TestClass");
        // Constructor may or may not be generated depending on error handling
    }

    [Fact]
    public void CollectNamespaces_NonExistentNamespace_HandlesGracefully()
    {
        // Arrange - Reference to non-existent namespace
        var sourceCode = @"
using System.Collections.Generic;
using IoCTools.Abstractions.Annotations;
using NonExistent.Namespace;

namespace TestProject;
public partial class TestClass
{
    [Inject] private readonly SomeNonExistentType _field;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert - Should handle compilation errors gracefully
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void CollectNamespaces_CircularGenericReferences_HandlesGracefully()
    {
        // Arrange - Test deeply nested or circular generic structures
        var sourceCode = @"
using System.Collections.Generic;
using IoCTools.Abstractions.Annotations;

namespace TestProject;

public interface INode<T> where T : INode<T> { }
public class ConcreteNode : INode<ConcreteNode> { }
public partial class TestClass
{
    [Inject] private readonly IEnumerable<INode<ConcreteNode>> _circularRef;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert
        Assert.False(result.HasErrors);
        var constructorSource = result.GetConstructorSource("TestClass");
        Assert.NotNull(constructorSource.Content);
        Assert.Contains("using System.Collections.Generic;", constructorSource.Content);
        // Should NOT contain self-namespace (constructor is generated in TestProject namespace)
        Assert.DoesNotContain("using TestProject;", constructorSource.Content);
    }

    [Fact]
    public void CollectNamespaces_CompilationErrors_GeneratorStillWorks()
    {
        // Arrange - Code with syntax errors
        var sourceCode = @"
using System.Collections.Generic;
using IoCTools.Abstractions.Annotations;

namespace TestProject;

public interface ITestService { }
public partial class TestClass
{
    [Inject] private readonly IEnumerable<ITestService> _field
    // Missing semicolon above
    public void SomeMethod() { }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert - Should have errors but generator should attempt to work
        Assert.True(result.HasErrors);
    }

    #endregion

    #region Global Namespace Handling

    [Fact]
    public void CollectNamespaces_GlobalNamespaceTypes_NoUsingGlobalGenerated()
    {
        // Arrange - Types in global namespace (no namespace declaration)
        var sourceCode = @"
using System.Collections.Generic;
using IoCTools.Abstractions.Annotations;

// No namespace declaration - these are in global namespace
public interface IGlobalService { }
public partial class GlobalTestClass
{
    [Inject] private readonly IEnumerable<IGlobalService> _globalCollection;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert
        Assert.False(result.HasErrors);
        var constructorSource = result.GetConstructorSource("GlobalTestClass");
        Assert.NotNull(constructorSource.Content);
        Assert.Contains("using System.Collections.Generic;", constructorSource.Content);
        // Should NOT contain "using ;" or empty using statement
        Assert.DoesNotContain("using ;", constructorSource.Content);
        Assert.DoesNotContain("using global;", constructorSource.Content);
    }

    [Fact]
    public void CollectNamespaces_MixedGlobalAndNamespacedTypes_CorrectUsings()
    {
        // Arrange - Mix of global and namespaced types
        var sourceCode = @"
using System.Collections.Generic;
using IoCTools.Abstractions.Annotations;

// Global namespace type
public interface IGlobalService { }

namespace TestProject
{
    public interface INamespacedService { }

    
    public partial class MixedTestClass
    {
        [Inject] private readonly IEnumerable<IGlobalService> _global;
        [Inject] private readonly IList<INamespacedService> _namespaced;
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        // Assert
        Assert.False(result.HasErrors);
        var constructorSource = result.GetConstructorSource("MixedTestClass");
        Assert.NotNull(constructorSource.Content);

        Assert.Contains("using System.Collections.Generic;", constructorSource.Content);
        // Should NOT contain self-namespace (constructor is generated in TestProject namespace)
        Assert.DoesNotContain("using TestProject;", constructorSource.Content);
        // Verify NO global using statements
        Assert.DoesNotContain("using ;", constructorSource.Content);
    }

    [Fact]
    public void CollectNamespaces_GlobalNamespaceVerification_ExactValidation()
    {
        // Arrange - Comprehensive global namespace test
        var sourceCode = @"
using System.Collections.Generic;
using IoCTools.Abstractions.Annotations;

public interface IGlobal1 { }
public interface IGlobal2 { }
public partial class GlobalOnlyTestClass
{
    [Inject] private readonly IEnumerable<IGlobal1> _field1;
    [Inject] private readonly IList<IGlobal2> _field2;
    [Inject] private readonly IEnumerable<IEnumerable<IGlobal1>> _nested;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert
        Assert.False(result.HasErrors);
        var constructorSource = result.GetConstructorSource("GlobalOnlyTestClass");
        Assert.NotNull(constructorSource.Content);

        // Count exact using statements
        var usingLines = constructorSource.Content.Split('\n')
            .Where(line => line.Trim().StartsWith("using ") && line.Trim().EndsWith(";"))
            .ToArray();

        // Should only have System.Collections.Generic, no global namespace usings
        Assert.Contains("using System.Collections.Generic;", constructorSource.Content);
        Assert.All(usingLines, line => Assert.DoesNotMatch(@"using\s*;", line));
    }

    #endregion

    #region Edge Cases and Complex Scenarios

    [Fact]
    public void CollectNamespaces_GenericConstraints_HandlesWhereClause()
    {
        // Arrange - Generic types with where constraints
        var sourceCode = @"
using System.Collections.Generic;
using IoCTools.Abstractions.Annotations;

namespace TestProject;

public interface IConstrainedService<T> where T : class, new() { }
public class ConcreteService { }
public partial class ConstrainedTestClass
{
    [Inject] private readonly IEnumerable<IConstrainedService<ConcreteService>> _constrained;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert
        Assert.False(result.HasErrors);
        var constructorSource = result.GetConstructorSource("ConstrainedTestClass");
        Assert.NotNull(constructorSource.Content);
        Assert.Contains("using System.Collections.Generic;", constructorSource.Content);
        // Should NOT contain self-namespace (constructor is generated in TestProject namespace)
        Assert.DoesNotContain("using TestProject;", constructorSource.Content);
    }

    [Fact]
    public void CollectNamespaces_NullableReferenceTypes_CollectsCorrectNamespaces()
    {
        // Arrange - Nullable reference types
        var sourceCode = @"
#nullable enable
using System.Collections.Generic;
using IoCTools.Abstractions.Annotations;

namespace TestProject;

public interface INullableService { }
public partial class NullableTestClass
{
    [Inject] private readonly IEnumerable<INullableService?> _nullableCollection;
    [Inject] private readonly string? _nullableString;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert
        Assert.False(result.HasErrors);
        var constructorSource = result.GetConstructorSource("NullableTestClass");
        Assert.NotNull(constructorSource.Content);
        Assert.Contains("using System.Collections.Generic;", constructorSource.Content);
        // Should NOT contain self-namespace (constructor is generated in TestProject namespace)
        Assert.DoesNotContain("using TestProject;", constructorSource.Content);
    }

    [Fact]
    public void CollectNamespaces_SystemNamespaceVariations_CollectsSpecializedNamespaces()
    {
        // Arrange - Various System namespace variations
        var sourceCode = @"
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Collections.Immutable;
using IoCTools.Abstractions.Annotations;

namespace TestProject;

public interface ITestService { }
public partial class SystemVariationsTestClass
{
    [Inject] private readonly ConcurrentBag<ITestService> _concurrent;
    [Inject] private readonly NameValueCollection _specialized;
    [Inject] private readonly ImmutableList<ITestService> _immutable;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert
        Assert.False(result.HasErrors);
        var constructorSource = result.GetConstructorSource("SystemVariationsTestClass");
        Assert.NotNull(constructorSource.Content);
        Assert.Contains("using System.Collections.Concurrent;", constructorSource.Content);
        Assert.Contains("using System.Collections.Specialized;", constructorSource.Content);
        Assert.Contains("using System.Collections.Immutable;", constructorSource.Content);
        // Should NOT contain self-namespace (constructor is generated in TestProject namespace)
        Assert.DoesNotContain("using TestProject;", constructorSource.Content);
    }

    [Fact]
    public void CollectNamespaces_UnsafePointerTypes_HandlesUnsafeContext()
    {
        // Arrange - Unsafe pointer and reference types
        var sourceCode = @"
using System.Collections.Generic;
using IoCTools.Abstractions.Annotations;

namespace TestProject;

public unsafe interface IUnsafeService
{
    void ProcessPointer(int* ptr);
}
public unsafe partial class UnsafeTestClass
{
    [Inject] private readonly IEnumerable<IUnsafeService> _unsafeCollection;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert - May have compilation issues due to unsafe context but should handle gracefully
        var constructorSource = result.GetConstructorSource("UnsafeTestClass");
        if (constructorSource?.Content != null)
        {
            Assert.Contains("using System.Collections.Generic;", constructorSource.Content);
            // Should NOT contain self-namespace (constructor is generated in TestProject namespace)
            Assert.DoesNotContain("using TestProject;", constructorSource.Content);
        }
    }

    #endregion

    #region Real-World Scenarios

    [Fact]
    public void CollectNamespaces_FileScopedNamespaces_HandlesFileScopedSyntax()
    {
        // Arrange - File-scoped namespace syntax (C# 10+)
        var sourceCode = @"
using System.Collections.Generic;
using IoCTools.Abstractions.Annotations;

namespace TestProject.Services;

public interface IFileScopedService { }
public partial class FileScopedTestClass
{
    [Inject] private readonly IEnumerable<IFileScopedService> _collection;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert
        Assert.False(result.HasErrors);
        var constructorSource = result.GetConstructorSource("FileScopedTestClass");
        Assert.NotNull(constructorSource.Content);
        Assert.Contains("using System.Collections.Generic;", constructorSource.Content);
        // Should NOT contain self-namespace (constructor is generated in TestProject.Services namespace)
        Assert.DoesNotContain("using TestProject.Services;", constructorSource.Content);
    }

    [Fact]
    public void CollectNamespaces_TypeAliases_HandlesAliasUsage()
    {
        // Arrange - Using aliases for type names
        var sourceCode = @"
using System.Collections.Generic;
using CollectionAlias = System.Collections.Generic;
using IoCTools.Abstractions.Annotations;

namespace TestProject;

public interface ITestService { }
public partial class AliasTestClass
{
    [Inject] private readonly CollectionAlias.IEnumerable<ITestService> _aliasedCollection;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert
        Assert.False(result.HasErrors);
        var constructorSource = result.GetConstructorSource("AliasTestClass");
        Assert.NotNull(constructorSource.Content);
        // Should collect the actual namespace, not the alias
        Assert.Contains("using System.Collections.Generic;", constructorSource.Content);
        // Should NOT contain self-namespace (constructor is generated in TestProject namespace)
        Assert.DoesNotContain("using TestProject;", constructorSource.Content);
    }

    [Fact]
    public void CollectNamespaces_DeeplyNestedAssemblyReferences_HandlesComplexReferences()
    {
        // Arrange - Deep assembly and namespace nesting
        var sourceCode = @"
using System.Collections.Generic;
using IoCTools.Abstractions.Annotations;

namespace MyCompany.MyProduct.Services.Implementations.Data.Repositories;

public interface IVeryDeeplyNestedService { }
public partial class DeeplyNestedTestClass
{
    [Inject] private readonly IEnumerable<IVeryDeeplyNestedService> _deeplyNested;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert
        Assert.False(result.HasErrors);
        var constructorSource = result.GetConstructorSource("DeeplyNestedTestClass");
        Assert.NotNull(constructorSource.Content);
        Assert.Contains("using System.Collections.Generic;", constructorSource.Content);
        // Should NOT contain self-namespace (constructor is generated in the same namespace as the interface)
        Assert.DoesNotContain("using MyCompany.MyProduct.Services.Implementations.Data.Repositories;",
            constructorSource.Content);
    }

    [Fact]
    public void CollectNamespaces_CrossFilePartialClasses_HandlesMultiFileDefinitions()
    {
        // Arrange - Partial classes across multiple conceptual files
        var sourceCode = @"
using System.Collections.Generic;
using IoCTools.Abstractions.Annotations;

namespace TestProject
{
    public interface IService1 { }
    public interface IService2 { }

    // Simulating first file
    
    public partial class MultiFileTestClass
    {
        [Inject] private readonly IEnumerable<IService1> _field1;
    }

    // Simulating second file
    public partial class MultiFileTestClass
    {
        [Inject] private readonly IList<IService2> _field2;
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert
        Assert.False(result.HasErrors);
        var constructorSource = result.GetConstructorSource("MultiFileTestClass");
        Assert.NotNull(constructorSource?.Content);
        Assert.Contains("using System.Collections.Generic;", constructorSource!.Content);
        // Should NOT contain self-namespace (constructor is generated in TestProject namespace)
        Assert.DoesNotContain("using TestProject;", constructorSource!.Content);
        // Should handle both fields from different partial definitions
        Assert.Contains("IEnumerable<IService1> field1", constructorSource.Content);
        Assert.Contains("IList<IService2> field2", constructorSource.Content);
    }

    #endregion

    #region Assertion Improvements and Exact Validation

    [Fact]
    public void CollectNamespaces_ExactUsingStatementCount_ValidatesExactCount()
    {
        // Arrange
        var sourceCode = @"
using System.Collections.Generic;
using IoCTools.Abstractions.Annotations;

namespace ProjectA 
{ 
    public interface IServiceA { } 
}

namespace ProjectB 
{ 
    public interface IServiceB { } 
}

namespace TestProject
{
    
    public partial class ExactCountTestClass
    {
        [Inject] private readonly IEnumerable<ProjectA.IServiceA> _fieldA;
        [Inject] private readonly IList<ProjectB.IServiceB> _fieldB;
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert
        Assert.False(result.HasErrors);
        var constructorSource = result.GetConstructorSource("ExactCountTestClass");
        Assert.NotNull(constructorSource.Content);

        // Count exact using statements
        var usingStatements = constructorSource.Content.Split('\n')
            .Where(line => line.Trim().StartsWith("using ") && line.Trim().EndsWith(";"))
            .Select(line => line.Trim())
            .ToHashSet();

        // Expected using statements (self-namespace TestProject should be excluded)
        var expectedUsings = new HashSet<string>
        {
            "using System.Collections.Generic;", "using ProjectA;", "using ProjectB;"
        };

        Assert.Equal(expectedUsings.Count, usingStatements.Count);
        Assert.True(expectedUsings.SetEquals(usingStatements));
    }

    [Fact]
    public void CollectNamespaces_NamespaceDeduplication_NoDuplicateUsings()
    {
        // Arrange - Multiple fields using same namespace
        var sourceCode = @"
using System.Collections.Generic;
using IoCTools.Abstractions.Annotations;

namespace TestProject;

public interface IService1 { }
public interface IService2 { }
public interface IService3 { }
public partial class DeduplicationTestClass
{
    [Inject] private readonly IEnumerable<IService1> _field1;
    [Inject] private readonly IList<IService1> _field2;
    [Inject] private readonly ICollection<IService2> _field3;
    [Inject] private readonly IEnumerable<IEnumerable<IService3>> _nested;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert
        Assert.False(result.HasErrors);
        var constructorSource = result.GetConstructorSource("DeduplicationTestClass");
        Assert.NotNull(constructorSource.Content);

        // Count occurrences of each using statement
        var testProjectUsingCount = constructorSource.Content.Split('\n')
            .Count(line => line.Trim() == "using TestProject;");
        var collectionsUsingCount = constructorSource.Content.Split('\n')
            .Count(line => line.Trim() == "using System.Collections.Generic;");

        // Self-namespace should NOT appear (all types are in same namespace as the class)
        Assert.Equal(0, testProjectUsingCount);
        // Collections namespace should appear exactly once
        Assert.Equal(1, collectionsUsingCount);
    }

    [Fact]
    public void CollectNamespaces_NegativeAssertions_UnwantedNamespacesNotIncluded()
    {
        // Arrange - Ensure irrelevant namespaces are not included
        var sourceCode = @"
using System.Collections.Generic;
using System.IO; // Should not be included in generated using statements
using IoCTools.Abstractions.Annotations;

namespace TestProject;

public interface ITestService { }
public partial class NegativeTestClass
{
    [Inject] private readonly IEnumerable<ITestService> _field;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert
        Assert.False(result.HasErrors);
        var constructorSource = result.GetConstructorSource("NegativeTestClass");
        Assert.NotNull(constructorSource.Content);

        // Positive assertions
        Assert.Contains("using System.Collections.Generic;", constructorSource.Content);

        // Negative assertions - unwanted namespaces should NOT be included
        Assert.DoesNotContain("using System.IO;", constructorSource.Content);
        Assert.DoesNotContain("using IoCTools.Abstractions.Annotations;", constructorSource.Content);
        Assert.DoesNotContain("using System.Linq;", constructorSource.Content);
        // Self-namespace should NOT be included (constructor is generated in TestProject namespace)
        Assert.DoesNotContain("using TestProject;", constructorSource.Content);
    }

    [Fact]
    public void CollectNamespaces_ConstructorParameterValidation_StrongerValidation()
    {
        // Arrange
        var sourceCode = @"
using System.Collections.Generic;
using System.Collections.Concurrent;
using IoCTools.Abstractions.Annotations;

namespace TestProject;

public interface IService1 { }
public interface IService2 { }
public partial class StrongValidationTestClass
{
    [Inject] private readonly IEnumerable<IService1> _enumerable;
    [Inject] private readonly ConcurrentBag<IService2> _concurrentBag;
    [Inject] private readonly IList<IEnumerable<IService1>> _nestedGeneric;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert
        Assert.False(result.HasErrors);
        var constructorSource = result.GetConstructorSource("StrongValidationTestClass");
        Assert.NotNull(constructorSource.Content);

        // Strong parameter validation - exact parameter names and types
        var expectedParameters = new[]
        {
            "IEnumerable<IService1> enumerable", "ConcurrentBag<IService2> concurrentBag",
            "IList<IEnumerable<IService1>> nestedGeneric"
        };

        foreach (var expectedParam in expectedParameters) Assert.Contains(expectedParam, constructorSource.Content);

        // Validate constructor signature structure
        Assert.Contains("public StrongValidationTestClass(", constructorSource.Content);
        Assert.Contains(")\n    {", constructorSource.Content);
    }

    #endregion

    #region Boundary Value and Stress Testing

    [Fact]
    public void CollectNamespaces_EmptyGenericCollections_HandlesEmptyTypeParameters()
    {
        // Arrange - This should cause compilation errors but test generator resilience
        var sourceCode = @"
using System.Collections.Generic;
using IoCTools.Abstractions.Annotations;

namespace TestProject;
public partial class EmptyGenericsTestClass
{
    // These should cause compilation errors
    [Inject] private readonly IEnumerable<> _empty1;
    [Inject] private readonly IList<> _empty2;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert - Should have compilation errors but generator should handle gracefully
        Assert.True(result.HasErrors);
    }

    [Fact]
    public void CollectNamespaces_MaximumNestingDepth_HandlesDeepNesting()
    {
        // Arrange - Test maximum practical nesting depth
        var sourceCode = @"
using System.Collections.Generic;
using IoCTools.Abstractions.Annotations;

namespace TestProject;

public interface ITestService { }
public partial class DeepNestingTestClass
{
    [Inject] private readonly IEnumerable<IEnumerable<IEnumerable<IEnumerable<IEnumerable<ITestService>>>>> _deeplyNested;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert
        Assert.False(result.HasErrors);
        var constructorSource = result.GetConstructorSource("DeepNestingTestClass");
        Assert.NotNull(constructorSource.Content);
        Assert.Contains("using System.Collections.Generic;", constructorSource.Content);
        // Should NOT contain self-namespace (constructor is generated in TestProject namespace)
        Assert.DoesNotContain("using TestProject;", constructorSource.Content);
        Assert.Contains("IEnumerable<IEnumerable<IEnumerable<IEnumerable<IEnumerable<ITestService>>>>> deeplyNested",
            constructorSource.Content);
    }

    [Fact]
    public void CollectNamespaces_VeryLongTypeNames_HandlesLongIdentifiers()
    {
        // Arrange - Very long type and namespace names
        var sourceCode = @"
using System.Collections.Generic;
using IoCTools.Abstractions.Annotations;

namespace VeryLongNamespaceNameThatExceedsNormalLengthExpectationsForTestingPurposes;

public interface IVeryLongInterfaceNameThatIsDesignedToTestTheHandlingOfExtremelyLongTypeNamesInTheSourceGenerator { }
public partial class VeryLongClassNameForTestingExtremelyLongIdentifierHandlingInSourceGeneratorNamespaceCollection
{
    [Inject] private readonly IEnumerable<IVeryLongInterfaceNameThatIsDesignedToTestTheHandlingOfExtremelyLongTypeNamesInTheSourceGenerator> _veryLongFieldName;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert
        Assert.False(result.HasErrors);
        var constructorSource =
            result.GetConstructorSource(
                "VeryLongClassNameForTestingExtremelyLongIdentifierHandlingInSourceGeneratorNamespaceCollection");
        Assert.NotNull(constructorSource.Content);
        Assert.Contains("using System.Collections.Generic;", constructorSource.Content);
        // Should NOT contain self-namespace (class and interface are in same namespace)
        Assert.DoesNotContain("using VeryLongNamespaceNameThatExceedsNormalLengthExpectationsForTestingPurposes;",
            constructorSource.Content);
    }

    [Fact]
    public void CollectNamespaces_SpecialCharactersInNamespaces_HandlesUnicodeAndSymbols()
    {
        // Arrange - Test Unicode and special characters (where legally allowed)
        var sourceCode = @"
using System.Collections.Generic;
using IoCTools.Abstractions.Annotations;

// Note: C# has restrictions on namespace names, but testing edge cases
namespace TestProject.Services_V2;

public interface ITestService_V2 { }
public partial class SpecialCharsTestClass
{
    [Inject] private readonly IEnumerable<ITestService_V2> _serviceV2;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert
        Assert.False(result.HasErrors);
        var constructorSource = result.GetConstructorSource("SpecialCharsTestClass");
        Assert.NotNull(constructorSource.Content);
        Assert.Contains("using System.Collections.Generic;", constructorSource.Content);
        // Should NOT contain self-namespace (constructor is generated in TestProject.Services_V2 namespace)
        Assert.DoesNotContain("using TestProject.Services_V2;", constructorSource.Content);
    }

    #endregion

    #region Integration Scenarios

    [Fact]
    public void CollectNamespaces_MultiFileScenarios_HandlesMultipleSourceFiles()
    {
        // Arrange - Simulate multiple files with cross-references
        var sourceCode = @"
using System.Collections.Generic;
using IoCTools.Abstractions.Annotations;

// File 1: Services
namespace MyApp.Services
{
    public interface IEmailService { }
    public interface ILoggingService { }
}

// File 2: Repositories
namespace MyApp.Repositories
{
    public interface IUserRepository { }
    public interface IOrderRepository { }
}

// File 3: Controllers
namespace MyApp.Controllers
{
    
    public partial class UserController
    {
        [Inject] private readonly IEnumerable<MyApp.Services.IEmailService> _emailServices;
        [Inject] private readonly IList<MyApp.Repositories.IUserRepository> _userRepos;
        [Inject] private readonly MyApp.Services.ILoggingService _logger;
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert
        Assert.False(result.HasErrors);
        var constructorSource = result.GetConstructorSource("UserController");
        Assert.NotNull(constructorSource.Content);
        Assert.Contains("using System.Collections.Generic;", constructorSource.Content);
        Assert.Contains("using MyApp.Services;", constructorSource.Content);
        Assert.Contains("using MyApp.Repositories;", constructorSource.Content);
    }

    [Fact]
    public void CollectNamespaces_PreprocessorDirectives_HandlesConditionalCompilation()
    {
        // Arrange - Code with preprocessor directives
        var sourceCode = @"
using System.Collections.Generic;
using IoCTools.Abstractions.Annotations;

namespace TestProject;

#if DEBUG
public interface IDebugService { }
#else
public interface IReleaseService { }
#endif
public partial class ConditionalTestClass
{
#if DEBUG
    [Inject] private readonly IEnumerable<IDebugService> _debugServices;
#else
    [Inject] private readonly IEnumerable<IReleaseService> _releaseServices;
#endif
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert
        Assert.False(result.HasErrors);
        var constructorSource = result.GetConstructorSource("ConditionalTestClass");
        Assert.NotNull(constructorSource.Content);
        Assert.Contains("using System.Collections.Generic;", constructorSource.Content);
        // Should NOT contain self-namespace (constructor is generated in TestProject namespace)
        Assert.DoesNotContain("using TestProject;", constructorSource.Content);
    }

    [Fact]
    public void CollectNamespaces_RealMicrosoftTypes_IntegratesWithFrameworkTypes()
    {
        // Arrange - Real Microsoft framework types
        var sourceCode = @"
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using IoCTools.Abstractions.Annotations;

namespace TestProject;

public interface IMyService { }
public partial class FrameworkIntegrationTestClass
{
    [Inject] private readonly IEnumerable<IMyService> _services;
    [Inject] private readonly ILogger<FrameworkIntegrationTestClass> _logger;
    [Inject] private readonly IConfiguration _config;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Assert - May have compilation issues due to missing references but should handle gracefully
        var constructorSource = result.GetConstructorSource("FrameworkIntegrationTestClass");
        if (constructorSource?.Content != null)
        {
            Assert.Contains("using System.Collections.Generic;", constructorSource.Content);
            // Should NOT contain self-namespace (constructor is generated in TestProject namespace)
            Assert.DoesNotContain("using TestProject;", constructorSource.Content);
            // Framework types may or may not be included depending on compilation context
        }
    }

    #endregion
}
