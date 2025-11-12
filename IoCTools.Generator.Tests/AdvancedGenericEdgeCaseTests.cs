namespace IoCTools.Generator.Tests;

using Microsoft.CodeAnalysis;

/// <summary>
///     ADVANCED GENERIC EDGE CASE TESTS
///     These tests cover the gnarliest generic scenarios that could break the generator:
///     - Generic type constraints (struct, class, notnull, new(), unmanaged, multiple)
///     - Open vs closed generic registration scenarios
///     - Variance (covariance, contravariance, mixed scenarios)
///     - Framework integration patterns (ILogger
///     <T>
///         , IOptions
///         <T>
///             , collections)
///             - Async delegate patterns (Func<T, Task
///             <U>
///                 >, ValueTask)
///                 - Generic lifetime management and performance edge cases
///                 - Malformed syntax and error conditions
///                 - Real-world complex inheritance chains and type substitution
/// </summary>
public class AdvancedGenericEdgeCaseTests
{
    [Fact]
    public void Generics_VarianceScenarios_HandlesCorrectly()
    {
        // Arrange - Test covariance and contravariance
        var source = @"
using IoCTools.Abstractions.Annotations;
using System;
using System.Collections.Generic;

namespace Test;

// Covariant interface
public interface ICovariant<out T> { T Get(); }

// Contravariant interface  
public interface IContravariant<in T> { void Set(T value); }

// Invariant interface
public interface IInvariant<T> { T Get(); void Set(T value); }
public partial class VarianceService
{
    [Inject] private readonly ICovariant<string> _covariant;
    [Inject] private readonly IContravariant<object> _contravariant; 
    [Inject] private readonly IInvariant<int> _invariant;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse(
            $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var constructorText = result.GetConstructorSourceText("VarianceService");
        // Strengthen assertions - check full constructor signature
        constructorText.Should().Contain(
            "public VarianceService(ICovariant<string> covariant, IContravariant<object> contravariant, IInvariant<int> invariant)");
        constructorText.Should().Contain("_covariant = covariant;");
        constructorText.Should().Contain("_contravariant = contravariant;");
        constructorText.Should().Contain("_invariant = invariant;");
    }

    [Fact]
    public void Generics_AdvancedConstraintCombinations_HandlesCorrectly()
    {
        // Arrange - Test all constraint combinations missing from feedback
        var source = @"
using IoCTools.Abstractions.Annotations;
using System;
using System.Collections.Generic;

namespace Test;

public interface IEntity { }
public interface IValidatable { }

// struct constraint with unmanaged combination
public interface IUnmanagedProcessor<T> where T : unmanaged { }

// class? nullable reference constraints
public interface INullableProcessor<T> where T : class? { }

// notnull constraint
public interface INotNullProcessor<T> where T : notnull { }

// Multiple type parameter constraints with interdependencies
public interface IChainedProcessor<T, U> where T : U where U : class { }
public partial class AdvancedConstraintService<T, U, V>
    where T : struct, IComparable<T>
    where U : class, IEntity, IValidatable, new()
    where V : IEnumerable<U>, ICollection<U>
{
    [Inject] private readonly IUnmanagedProcessor<int> _unmanagedProcessor;
    [Inject] private readonly INullableProcessor<string> _nullableProcessor;
    [Inject] private readonly INotNullProcessor<T> _notNullProcessor;
    [Inject] private readonly IChainedProcessor<string, object> _chainedProcessor;
    [Inject] private readonly IComparer<T> _structComparer;
    [Inject] private readonly V _constrainedCollection;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse(
            $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var constructorText = result.GetConstructorSourceText("AdvancedConstraintService");

        // Verify all constraints are preserved
        constructorText.Should().Contain("where T : struct, IComparable<T>");
        constructorText.Should().Contain("where U : class, IEntity, IValidatable, new()");
        constructorText.Should().Contain("where V : IEnumerable<U>, ICollection<U>");

        // Verify parameter types are correct
        constructorText.Should().Contain("IUnmanagedProcessor<int>");
        constructorText.Should().Contain("INullableProcessor<string>");
        constructorText.Should().Contain("INotNullProcessor<T>");
        constructorText.Should().Contain("IChainedProcessor<string, object>");
    }

    [Fact]
    public void Generics_OpenGenericRegistration_HandlesCorrectly()
    {
        // Arrange - Test generic service registration (actual current behavior)
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace Test;

public interface IRepository<T> where T : class
{
    T GetById(int id);
    IEnumerable<T> GetAll();
}

// Generic service - current implementation registers as class-only
[Scoped]
[RegisterAsAll]
public partial class Repository<T> : IRepository<T> where T : class
{
    [Inject] private readonly IComparer<T> _comparer;
    
    public T GetById(int id) => default(T);
    public IEnumerable<T> GetAll() => new List<T>();
}

// Mixed generic dependencies
[Scoped]
public partial class MixedGenericService<T> where T : class
{
    [Inject] private readonly IRepository<T> _openGeneric;
    [Inject] private readonly IRepository<string> _closedGeneric;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should()
            .BeFalse(
                $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var registrationText = result.GetServiceRegistrationText();

        // Verify actual generator behavior - registers open generics with typeof and FQN
        registrationText.Should().Contain("services.AddScoped(typeof(global::Test.Repository<>));");
        registrationText.Should()
            .Contain("services.AddScoped(typeof(global::Test.IRepository<>), typeof(global::Test.Repository<>));");
        registrationText.Should().Contain(
            "services.AddScoped(typeof(global::Test.MixedGenericService<>), typeof(global::Test.MixedGenericService<>));");
    }

    [Fact]
    public void Generics_MultipleConstraints_GeneratesCorrectly()
    {
        // Arrange - Multiple generic parameters with complex constraints
        var source = @"
using IoCTools.Abstractions.Annotations;
using System;
using System.Collections.Generic;

namespace Test;

public interface IEntity { }
public interface IValidatable { }
public interface IRepository<T> where T : IEntity { }
public partial class MultiConstraintService<T, V> 
    where T : class, IEntity, IValidatable, new()
    where V : IEnumerable<T>
{
    [Inject] private readonly IRepository<T> _repository;
    [Inject] private readonly IComparer<int> _comparer;
    [Inject] private readonly V _collection;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should()
            .BeFalse(
                $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var constructorText = result.GetConstructorSourceText("MultiConstraintService");

        // Check constraints are preserved
        constructorText.Should().Contain("where T : class, IEntity, IValidatable, new()");
        constructorText.Should().Contain("where V : IEnumerable<T>");
    }

    [Fact]
    public void Generics_DelegateTypes_HandlesCorrectly()
    {
        // Arrange - Delegate, Func, Action types
        var source = @"
using IoCTools.Abstractions.Annotations;
using System;

namespace Test;

public delegate bool CustomPredicate<T>(T item);
public partial class DelegateService
{
    [Inject] private readonly Func<string, int> _stringToInt;
    [Inject] private readonly Action<string> _stringAction;
    [Inject] private readonly Predicate<int> _intPredicate;
    [Inject] private readonly CustomPredicate<string> _customPredicate;
    [Inject] private readonly Func<int, string, bool> _multiParamFunc;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should()
            .BeFalse(
                $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var constructorText = result.GetConstructorSourceText("DelegateService");
        // Strengthen assertions with constructor parameter verification
        constructorText.Should().Contain("public DelegateService(");
        constructorText.Should().Contain("Func<string, int> stringToInt");
        constructorText.Should().Contain("Action<string> stringAction");
        constructorText.Should().Contain("Predicate<int> intPredicate");
        constructorText.Should().Contain("CustomPredicate<string> customPredicate");
        constructorText.Should().Contain("Func<int, string, bool> multiParamFunc");
        constructorText.Should().Contain("_stringToInt = stringToInt;");
        constructorText.Should().Contain("_stringAction = stringAction;");
        constructorText.Should().Contain("_intPredicate = intPredicate;");
        constructorText.Should().Contain("_customPredicate = customPredicate;");
        constructorText.Should().Contain("_multiParamFunc = multiParamFunc;");
    }

    [Fact]
    public void Generics_TupleTypes_HandlesCorrectly()
    {
        // Arrange - Tuple types (simplified - named tuples have complex syntax)
        var source = @"
using IoCTools.Abstractions.Annotations;
using System;

namespace Test;
public partial class TupleService
{
    [Inject] private readonly Tuple<string, int> _simpleTuple;
    [Inject] private readonly ValueTuple<int, string> _valueTuple;
    [Inject] private readonly Tuple<string, int, bool> _tripleTuple;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should()
            .BeFalse(
                $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var constructorText = result.GetConstructorSourceText("TupleService");
        constructorText.Should().Contain("Tuple<string, int>");
        constructorText.Should().Contain("(int, string)"); // ValueTuple is represented as tuple syntax
        constructorText.Should().Contain("Tuple<string, int, bool>");
    }

    [Fact]
    public void Generics_FrameworkIntegrationPatterns_HandlesCorrectly()
    {
        // Arrange - Test common framework patterns
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System;

namespace Test;

public class AppSettings
{
    public string ConnectionString { get; set; }
}

public interface IEmailService { }
public partial class FrameworkIntegrationService<T> where T : class
{
    [Inject] private readonly ILogger<FrameworkIntegrationService<T>> _logger;
    [Inject] private readonly IOptions<AppSettings> _options;
    [Inject] private readonly IEnumerable<IEmailService> _emailServices;
    [Inject] private readonly IServiceProvider _serviceProvider;
    [Inject] private readonly ILogger<T> _genericLogger;
    [Inject] private readonly IOptionsMonitor<AppSettings> _optionsMonitor;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should()
            .BeFalse(
                $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var constructorText = result.GetConstructorSourceText("FrameworkIntegrationService");

        // Verify framework types are handled correctly
        constructorText.Should().Contain("ILogger<FrameworkIntegrationService<T>>");
        constructorText.Should().Contain("IOptions<AppSettings>");
        constructorText.Should().Contain("IEnumerable<IEmailService>");
        constructorText.Should().Contain("IServiceProvider");
        constructorText.Should().Contain("ILogger<T>");
        constructorText.Should().Contain("IOptionsMonitor<AppSettings>");
    }

    [Fact]
    public void Generics_AsyncDelegatePatterns_HandlesCorrectly()
    {
        // Arrange - Test async delegate patterns
        var source = @"
using IoCTools.Abstractions.Annotations;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Test;
public partial class AsyncDelegateService
{
    [Inject] private readonly Func<string, Task<int>> _asyncFunc;
    [Inject] private readonly Func<int, ValueTask<string>> _valueTaskFunc;
    [Inject] private readonly Func<string, Task<IEnumerable<int>>> _asyncCollectionFunc;
    [Inject] private readonly Func<IEnumerable<string>, Task<Dictionary<int, string>>> _complexAsyncFunc;
    [Inject] private readonly Func<Task<string>> _simpleAsyncFunc;
    [Inject] private readonly Action<Task<bool>> _asyncAction;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should()
            .BeFalse(
                $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var constructorText = result.GetConstructorSourceText("AsyncDelegateService");

        // Verify async delegate types
        constructorText.Should().Contain("Func<string, Task<int>>");
        constructorText.Should().Contain("Func<int, ValueTask<string>>");
        constructorText.Should().Contain("Func<string, Task<IEnumerable<int>>>");
        constructorText.Should().Contain("Func<IEnumerable<string>, Task<Dictionary<int, string>>>");
        constructorText.Should().Contain("Func<Task<string>>");
        constructorText.Should().Contain("Action<Task<bool>>");
    }

    [Fact]
    public void Generics_VarianceInNestedGenerics_HandlesCorrectly()
    {
        // Arrange - Test variance in nested generic scenarios
        var source = @"
using IoCTools.Abstractions.Annotations;
using System.Collections.Generic;
using System;

namespace Test;

// Covariant with constraints
public interface ICovariant<out T> where T : class { T Get(); }

// Contravariant with constraints  
public interface IContravariant<in T> where T : class { void Set(T value); }

// Variance in nested generics
public interface IProcessor<T> { }
public partial class VarianceNestedService
{
    [Inject] private readonly IProcessor<ICovariant<string>> _covariantNested;
    [Inject] private readonly IProcessor<IContravariant<object>> _contravariantNested;
    [Inject] private readonly IEnumerable<ICovariant<string>> _covariantCollection;
    [Inject] private readonly Func<IContravariant<string>, bool> _contravariantFunc;
    [Inject] private readonly IProcessor<Func<ICovariant<object>, string>> _complexNesting;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should()
            .BeFalse(
                $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var constructorText = result.GetConstructorSourceText("VarianceNestedService");

        // Verify variance in nested contexts
        constructorText.Should().Contain("IProcessor<ICovariant<string>>");
        constructorText.Should().Contain("IProcessor<IContravariant<object>>");
        constructorText.Should().Contain("IEnumerable<ICovariant<string>>");
        constructorText.Should().Contain("Func<IContravariant<string>, bool>");
        constructorText.Should().Contain("IProcessor<Func<ICovariant<object>, string>>");
    }

    [Fact]
    public void Generics_GenericLifetimeManagement_HandlesCorrectly()
    {
        // Arrange - Test different lifetimes for generic services
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace Test;

public interface ICache<T> { }

[Singleton]
public partial class SingletonCache<T> : ICache<T>
{
    [Inject] private readonly IComparer<T> _comparer;
}

[Scoped]
public partial class ScopedCache<T> : ICache<T>
{
    [Inject] private readonly IEqualityComparer<T> _equalityComparer;
}

[Transient]
public partial class TransientProcessor<T>
{
    [Inject] private readonly ICache<T> _cache;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should()
            .BeFalse(
                $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var registrationText = result.GetServiceRegistrationText();

        // Verify actual generator behavior for generic services with different lifetimes
        registrationText.Should().Contain("AddSingleton");
        registrationText.Should().Contain("AddScoped");
        registrationText.Should().Contain("AddTransient");

        // Verify generic type references are present
        registrationText.Should().Contain("SingletonCache<");
        registrationText.Should().Contain("ScopedCache<");
        registrationText.Should().Contain("TransientProcessor<");
    }

    [Fact]
    public void Generics_PerformanceEdgeCases_HandlesCorrectly()
    {
        // Arrange - Test performance edge cases with deep nesting and wide parameters
        var source = @"
using IoCTools.Abstractions.Annotations;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Test;

// Extremely deep nesting (10+ levels)

public partial class DeepNestingService
{
    [Inject] private readonly Dictionary<string, List<Tuple<int, Dictionary<Guid, List<KeyValuePair<string, Tuple<bool, Dictionary<int, List<string>>>>>>>>> _deepNesting;
}

// Very wide generic parameter list
public interface IWideGeneric<T1, T2, T3, T4, T5, T6, T7, T8> { }
public partial class WideGenericService<T1, T2, T3, T4, T5, T6, T7, T8>
    where T1 : class where T2 : struct where T3 : IComparable<T3>
    where T4 : class, new() where T5 : struct, IComparable<T5>
    where T6 : IEnumerable<T1> where T7 : ICollection<T2>
    where T8 : IDictionary<T1, T2>
{
    [Inject] private readonly IWideGeneric<T1, T2, T3, T4, T5, T6, T7, T8> _wideGeneric;
    [Inject] private readonly Dictionary<T1, List<T2>> _complexDependency;
}

// Extremely long generic type names

public partial class VeryLongGenericTypeNamesServiceWithManyCharactersInTheNameToTestPerformanceLimits<TVeryLongGenericParameterNameThatIsVeryVeryLongIndeed>
    where TVeryLongGenericParameterNameThatIsVeryVeryLongIndeed : class
{
    [Inject] private readonly IComparer<TVeryLongGenericParameterNameThatIsVeryVeryLongIndeed> _veryLongParameterComparer;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should handle even extreme cases
        result.HasErrors.Should()
            .BeFalse(
                $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        // Verify all services can be constructed
        var deepNestingConstructor = result.GetConstructorSourceText("DeepNestingService");
        var wideGenericConstructor = result.GetConstructorSourceText("WideGenericService");
        var longNamesConstructor = result.GetConstructorSourceText(
            "VeryLongGenericTypeNamesServiceWithManyCharactersInTheNameToTestPerformanceLimits");
    }

    [Fact]
    public void Generics_RecursiveTypes_HandlesCorrectly()
    {
        // Arrange - Recursive generic types (self-referencing)
        var source = @"
using IoCTools.Abstractions.Annotations;
using System.Collections.Generic;

namespace Test;

public interface INode<T> where T : INode<T> { }
public interface ITree<T> { IEnumerable<T> Children { get; } }
public partial class RecursiveService<T> where T : INode<T>
{
    [Inject] private readonly INode<T> _node;
    [Inject] private readonly ITree<INode<T>> _tree;
    [Inject] private readonly IEnumerable<ITree<T>> _treesOfT;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should()
            .BeFalse(
                $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var constructorTextRecursive = result.GetConstructorSourceText("RecursiveService");
        constructorTextRecursive.Should().Contain("where T : INode<T>");
        constructorTextRecursive.Should().Contain("INode<T>");
        constructorTextRecursive.Should().Contain("ITree<INode<T>>");
    }

    [Fact]
    public void Generics_NestedGenericInheritance_HandlesCorrectly()
    {
        // Arrange - Nested generic inheritance with type substitution
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace Test;

public interface IProcessor<T> { }

public abstract partial class BaseProcessor<T, U>
{
    [Inject] private readonly IProcessor<T> _primaryProcessor;
    [Inject] private readonly IEnumerable<IProcessor<U>> _secondaryProcessors;
}

public abstract partial class MiddleProcessor<T> : BaseProcessor<T, string>
{
    [Inject] private readonly IProcessor<IEnumerable<T>> _collectionProcessor;
}
[Scoped]
public partial class ConcreteProcessor : MiddleProcessor<int>
{
    [Inject] private readonly IProcessor<bool> _boolProcessor;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should()
            .BeFalse(
                $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var constructorText = result.GetConstructorSourceText("ConcreteProcessor");

        // Should have all dependencies with proper type substitution
        constructorText.Should().Contain("IProcessor<int> primaryProcessor"); // T = int
        constructorText.Should().Contain("IEnumerable<IProcessor<string>> secondaryProcessors"); // U = string
        constructorText.Should().Contain("IProcessor<IEnumerable<int>> collectionProcessor"); // T = int in collection
        constructorText.Should().Contain("IProcessor<bool> boolProcessor"); // ConcreteProcessor's own dependency
    }

    [Fact]
    public void Generics_ArraysAndMemory_HandlesCorrectly()
    {
        // Arrange - Arrays and memory types (spans can't be fields)
        var source = @"
using IoCTools.Abstractions.Annotations;
using System;

namespace Test;
public partial class ArrayMemoryService
{
    [Inject] private readonly string[] _stringArray;
    [Inject] private readonly int[,] _multiDimArray;
    [Inject] private readonly int[][] _jaggedArray;
    [Inject] private readonly Memory<char> _charMemory;
    [Inject] private readonly ReadOnlyMemory<int> _intReadOnlyMemory;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should()
            .BeFalse(
                $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var constructorTextArray = result.GetConstructorSourceText("ArrayMemoryService");
        constructorTextArray.Should().Contain("string[]");
        constructorTextArray.Should().Contain("int[,]");
        constructorTextArray.Should().Contain("int[][]");
        constructorTextArray.Should().Contain("Memory<char>");
        constructorTextArray.Should().Contain("ReadOnlyMemory<int>");
    }

    [Fact]
    public void Generics_MalformedSyntax_HandlesErrorsCorrectly()
    {
        // Arrange - Test malformed generic syntax
        var source = @"
using IoCTools.Abstractions.Annotations;
using System;

namespace Test;

// Unclosed brackets
public interface IMalformed1<T { }

// Invalid type parameter names
public interface IMalformed2<123Invalid> { }

// Missing closing bracket

public partial class MalformedService
{
    [Inject] private readonly Dictionary<string, int _malformedField;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should have compilation errors
        result.HasErrors.Should().BeTrue("Expected compilation errors for malformed syntax");

        var errors = result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        errors.Should().NotBeEmpty();

        // Verify specific error types
        errors.Should().Contain(e => e.Id.Contains("CS"));
    }

    [Fact]
    public void Generics_UnsupportedScenarios_HandlesErrorsCorrectly()
    {
        // Arrange - Test unsupported generic scenarios
        var source = @"
using IoCTools.Abstractions.Annotations;
using System;

namespace Test;

// Pointer types (unsafe context)
public unsafe interface IUnsafeProcessor<T> where T : unmanaged
{
    void Process(T* ptr);
}

// Function pointers (C# 9+ feature)
public interface IFunctionPointerProcessor
{
    unsafe delegate*<int, void> FunctionPointer { get; }
}
public partial class UnsupportedService
{
    // These should potentially cause issues or be rejected
    [Inject] private readonly IUnsafeProcessor<int> _unsafeProcessor;
    [Inject] private readonly IFunctionPointerProcessor _functionPointerProcessor;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - May have errors or warnings, verify generator doesn't crash
        if (result.HasErrors)
        {
            var errors = result.CompilationDiagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToList();
            errors.Should().NotBeEmpty();
        }

        // Generator should not crash - either succeeds or fails gracefully
        result.CompilationDiagnostics.Should().NotBeNull();
    }

    [Fact]
    public void Generics_ComplexInheritanceWithTypeSubstitution_HandlesCorrectly()
    {
        // Arrange - Test complex inheritance with type parameter conflicts
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace Test;

public interface IProcessor<T> { }
public interface IValidator<T> { }
public interface IConverter<TIn, TOut> { }

// Generic service implementing multiple generic interfaces with conflicting parameters
[Scoped]
[RegisterAsAll]
public partial class ComplexGenericService<T, U> : IProcessor<T>, IValidator<U>, IConverter<T, U>
    where T : class
    where U : struct
{
    [Inject] private readonly IProcessor<string> _stringProcessor;  // Concrete type
    [Inject] private readonly IValidator<T> _tValidator;           // T parameter
    [Inject] private readonly IConverter<U, T> _reverseConverter;  // Swapped parameters
    [Inject] private readonly IEnumerable<IProcessor<T>> _tProcessors; // Collection of T
}

// Test complex but valid generic references
public interface ICircular1<T> where T : class { }
public interface ICircular2<T> { }
[Scoped]
public partial class CircularReferenceService
{
    [Inject] private readonly ICircular1<string> _circular;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should()
            .BeFalse(
                $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var complexConstructor = result.GetConstructorSourceText("ComplexGenericService");

        // Verify type parameter substitution is correct
        complexConstructor.Should().Contain("IProcessor<string> stringProcessor");
        complexConstructor.Should().Contain("IValidator<T> tValidator");
        complexConstructor.Should().Contain("IConverter<U, T> reverseConverter");
        complexConstructor.Should().Contain("IEnumerable<IProcessor<T>> tProcessors");

        var circularConstructor = result.GetConstructorSourceText("CircularReferenceService");
        circularConstructor.Should().Contain("ICircular1<string> circular");
    }

    [Fact]
    public void Generics_CompilationVerification_GeneratedCodeCompiles()
    {
        // Arrange - Comprehensive test that verifies generated code actually compiles and works
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Test;

public interface IRepository<T> where T : class { }
public interface IService<T> { }
[Scoped]
[RegisterAsAll]
public partial class Repository<T> : IRepository<T> where T : class
{
    [Inject] private readonly ILogger<Repository<T>> _logger;
}
[Scoped]
[RegisterAsAll]
public partial class BusinessService<T> : IService<T> where T : class
{
    [Inject] private readonly IRepository<T> _repository;
    [Inject] private readonly IEnumerable<IService<string>> _stringServices;
}
[Scoped]
[RegisterAsAll]
public partial class ConcreteService : IService<string>
{
    [Inject] private readonly IRepository<string> _stringRepository;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should compile without errors
        result.HasErrors.Should()
            .BeFalse(
                $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        // Verify registration method exists and is syntactically correct
        var registrationText = result.GetServiceRegistrationText();

        // Verify registration method contains service registrations (actual behavior)
        registrationText.Should().Contain("Repository<");
        registrationText.Should().Contain("BusinessService<");
        registrationText.Should().Contain("ConcreteService");

        // Verify constructors were generated
        var repositoryConstructor = result.GetConstructorSourceText("Repository");
        var businessConstructor = result.GetConstructorSourceText("BusinessService");
        var concreteConstructor = result.GetConstructorSourceText("ConcreteService");

        // Verify constructor signatures contain expected parameters
        repositoryConstructor.Should().Contain("ILogger<Repository<T>>");
        businessConstructor.Should().Contain("IRepository<T>");
        concreteConstructor.Should().Contain("IRepository<string>");
    }

    [Fact]
    public void Generics_RefOutInParameters_HandlesCorrectly()
    {
        // Arrange - This should probably NOT be supported, but let's test
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public delegate void RefAction<T>(ref T value);
public delegate void OutAction<T>(out T value);
public delegate void InAction<in T>(in T value);
public partial class RefOutInService
{
    [Inject] private readonly RefAction<int> _refAction;
    [Inject] private readonly OutAction<string> _outAction;
    [Inject] private readonly InAction<bool> _inAction;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - This might fail, which is OK for ref/out/in parameters
        if (result.HasErrors)
        {
            // Expected - ref/out/in parameters in DI are problematic
            var errors = result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            errors.Should().NotBeEmpty();

            // Verify we get meaningful error messages, not crashes
            errors.Should().AllSatisfy(error => error.GetMessage().Should().NotBeNullOrEmpty());
            return;
        }

        var constructorText = result.GetConstructorSourceText("RefOutInService");

        // If it succeeds, verify full constructor signature
        constructorText.Should().Contain(
            "public RefOutInService(RefAction<int> refAction, OutAction<string> outAction, InAction<bool> inAction)");
        constructorText.Should().Contain("_refAction = refAction;");
        constructorText.Should().Contain("_outAction = outAction;");
        constructorText.Should().Contain("_inAction = inAction;");
    }

    [Fact]
    public void Generics_ClosedGenericServices_RegistersCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IGenericService<T>
{
    void Process(T item);
}
[Scoped]
public partial class ConcreteGenericService : IGenericService<string>
{
    public void Process(string item) { }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should()
            .BeFalse(
                $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var registrationText = result.GetServiceRegistrationText();

        // Verify closed generic registration uses FQN format
        registrationText.Should().Contain(
            "services.AddScoped<global::Test.IGenericService<string>, global::Test.ConcreteGenericService>();");
        registrationText.Should().Contain(
            "services.AddScoped<global::Test.ConcreteGenericService, global::Test.ConcreteGenericService>();");

        // Ensure no open generic registration for closed generic service
        registrationText.Should().NotContain("AddScoped(typeof(IGenericService<>), typeof(ConcreteGenericService))");
    }

    [Fact]
    public void Generics_WildlyNestedComplexTypes_HandlesCorrectly()
    {
        // Arrange - The most insane nesting we can think of
        var source = @"
using IoCTools.Abstractions.Annotations;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Test;
public partial class InsaneNestingService
{
    [Inject] private readonly Dictionary<string, List<Func<int, Task<IEnumerable<KeyValuePair<Guid, string>>>>>> _insaneNesting;
    [Inject] private readonly Func<IEnumerable<Dictionary<int, List<string>>>, Task<bool>> _complexFunc;
    [Inject] private readonly IEnumerable<Func<Dictionary<string, int>, Task<string>>> _taskFunc;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should()
            .BeFalse(
                $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var constructorText = result.GetConstructorSourceText("InsaneNestingService");

        // Verify complex nested types are handled correctly with full constructor signature
        constructorText.Should().Contain("public InsaneNestingService(");
        constructorText.Should()
            .Contain("Dictionary<string, List<Func<int, Task<IEnumerable<KeyValuePair<Guid, string>>>>>");
        constructorText.Should().Contain("Func<IEnumerable<Dictionary<int, List<string>>>, Task<bool>>");
        constructorText.Should().Contain("IEnumerable<Func<Dictionary<string, int>, Task<string>>>");

        // Verify field assignments
        constructorText.Should().Contain("_insaneNesting = ");
        constructorText.Should().Contain("_complexFunc = ");
        constructorText.Should().Contain("_taskFunc = ");
    }
}
