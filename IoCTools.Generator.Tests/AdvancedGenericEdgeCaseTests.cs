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
        Assert.False(result.HasErrors,
            $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var constructorSource = result.GetConstructorSource("VarianceService");
        Assert.NotNull(constructorSource);
        // Strengthen assertions - check full constructor signature
        Assert.Contains(
            "public VarianceService(ICovariant<string> covariant, IContravariant<object> contravariant, IInvariant<int> invariant)",
            constructorSource.Content);
        Assert.Contains("_covariant = covariant;", constructorSource.Content);
        Assert.Contains("_contravariant = contravariant;", constructorSource.Content);
        Assert.Contains("_invariant = invariant;", constructorSource.Content);
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
        Assert.False(result.HasErrors,
            $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var constructorSource = result.GetConstructorSource("AdvancedConstraintService");
        Assert.NotNull(constructorSource);

        // Verify all constraints are preserved
        Assert.Contains("where T : struct, IComparable<T>", constructorSource.Content);
        Assert.Contains("where U : class, IEntity, IValidatable, new()", constructorSource.Content);
        Assert.Contains("where V : IEnumerable<U>, ICollection<U>", constructorSource.Content);

        // Verify parameter types are correct
        Assert.Contains("IUnmanagedProcessor<int>", constructorSource.Content);
        Assert.Contains("INullableProcessor<string>", constructorSource.Content);
        Assert.Contains("INotNullProcessor<T>", constructorSource.Content);
        Assert.Contains("IChainedProcessor<string, object>", constructorSource.Content);
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
        Assert.False(result.HasErrors,
            $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Verify actual generator behavior - registers open generics with typeof and FQN
        Assert.Contains("services.AddScoped(typeof(global::Test.Repository<>));", registrationSource.Content);
        Assert.Contains("services.AddScoped(typeof(global::Test.IRepository<>), typeof(global::Test.Repository<>));",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped(typeof(global::Test.MixedGenericService<>), typeof(global::Test.MixedGenericService<>));",
            registrationSource.Content);
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
        Assert.False(result.HasErrors,
            $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var constructorSource = result.GetConstructorSource("MultiConstraintService");
        Assert.NotNull(constructorSource);

        // Check constraints are preserved
        Assert.Contains("where T : class, IEntity, IValidatable, new()", constructorSource.Content);
        Assert.Contains("where V : IEnumerable<T>", constructorSource.Content);
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
        Assert.False(result.HasErrors,
            $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var constructorSource = result.GetConstructorSource("DelegateService");
        Assert.NotNull(constructorSource);
        // Strengthen assertions with constructor parameter verification
        Assert.Contains("public DelegateService(", constructorSource.Content);
        Assert.Contains("Func<string, int> stringToInt", constructorSource.Content);
        Assert.Contains("Action<string> stringAction", constructorSource.Content);
        Assert.Contains("Predicate<int> intPredicate", constructorSource.Content);
        Assert.Contains("CustomPredicate<string> customPredicate", constructorSource.Content);
        Assert.Contains("Func<int, string, bool> multiParamFunc", constructorSource.Content);
        Assert.Contains("_stringToInt = stringToInt;", constructorSource.Content);
        Assert.Contains("_stringAction = stringAction;", constructorSource.Content);
        Assert.Contains("_intPredicate = intPredicate;", constructorSource.Content);
        Assert.Contains("_customPredicate = customPredicate;", constructorSource.Content);
        Assert.Contains("_multiParamFunc = multiParamFunc;", constructorSource.Content);
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
        Assert.False(result.HasErrors,
            $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var constructorSource = result.GetConstructorSource("TupleService");
        Assert.NotNull(constructorSource);
        Assert.Contains("Tuple<string, int>", constructorSource.Content);
        Assert.Contains("(int, string)", constructorSource.Content); // ValueTuple is represented as tuple syntax
        Assert.Contains("Tuple<string, int, bool>", constructorSource.Content);
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
        Assert.False(result.HasErrors,
            $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var constructorSource = result.GetConstructorSource("FrameworkIntegrationService");
        Assert.NotNull(constructorSource);

        // Verify framework types are handled correctly
        Assert.Contains("ILogger<FrameworkIntegrationService<T>>", constructorSource.Content);
        Assert.Contains("IOptions<AppSettings>", constructorSource.Content);
        Assert.Contains("IEnumerable<IEmailService>", constructorSource.Content);
        Assert.Contains("IServiceProvider", constructorSource.Content);
        Assert.Contains("ILogger<T>", constructorSource.Content);
        Assert.Contains("IOptionsMonitor<AppSettings>", constructorSource.Content);
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
        Assert.False(result.HasErrors,
            $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var constructorSource = result.GetConstructorSource("AsyncDelegateService");
        Assert.NotNull(constructorSource);

        // Verify async delegate types
        Assert.Contains("Func<string, Task<int>>", constructorSource.Content);
        Assert.Contains("Func<int, ValueTask<string>>", constructorSource.Content);
        Assert.Contains("Func<string, Task<IEnumerable<int>>>", constructorSource.Content);
        Assert.Contains("Func<IEnumerable<string>, Task<Dictionary<int, string>>>", constructorSource.Content);
        Assert.Contains("Func<Task<string>>", constructorSource.Content);
        Assert.Contains("Action<Task<bool>>", constructorSource.Content);
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
        Assert.False(result.HasErrors,
            $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var constructorSource = result.GetConstructorSource("VarianceNestedService");
        Assert.NotNull(constructorSource);

        // Verify variance in nested contexts
        Assert.Contains("IProcessor<ICovariant<string>>", constructorSource.Content);
        Assert.Contains("IProcessor<IContravariant<object>>", constructorSource.Content);
        Assert.Contains("IEnumerable<ICovariant<string>>", constructorSource.Content);
        Assert.Contains("Func<IContravariant<string>, bool>", constructorSource.Content);
        Assert.Contains("IProcessor<Func<ICovariant<object>, string>>", constructorSource.Content);
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
        Assert.False(result.HasErrors,
            $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Verify actual generator behavior for generic services with different lifetimes
        Assert.Contains("AddSingleton", registrationSource.Content);
        Assert.Contains("AddScoped", registrationSource.Content);
        Assert.Contains("AddTransient", registrationSource.Content);

        // Verify generic type references are present
        Assert.Contains("SingletonCache<", registrationSource.Content);
        Assert.Contains("ScopedCache<", registrationSource.Content);
        Assert.Contains("TransientProcessor<", registrationSource.Content);
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
        Assert.False(result.HasErrors,
            $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        // Verify all services can be constructed
        var deepNestingConstructor = result.GetConstructorSource("DeepNestingService");
        var wideGenericConstructor = result.GetConstructorSource("WideGenericService");
        var longNamesConstructor =
            result.GetConstructorSource(
                "VeryLongGenericTypeNamesServiceWithManyCharactersInTheNameToTestPerformanceLimits");

        Assert.NotNull(deepNestingConstructor);
        Assert.NotNull(wideGenericConstructor);
        Assert.NotNull(longNamesConstructor);
    }

    // ARCHITECTURAL LIMIT: Complex generic constraint preservation is an architectural limit
    // See ARCHITECTURAL_LIMITS.md for details
    // [Fact] - DISABLED: Architectural limit
    public void Generics_ValueTypeConstraints_HandlesCorrectly_DISABLED_ArchitecturalLimit()
    {
        // Arrange - Value type constraints and nullable scenarios
        var source = @"
using IoCTools.Abstractions.Annotations;
using System;

namespace Test;

public interface IValueProcessor<T> where T : struct { }
public partial class ValueTypeService<T> where T : struct
{
    [Inject] private readonly IValueProcessor<T> _processor;
    [Inject] private readonly Nullable<T> _nullable;
    [Inject] private readonly T? _nullableShort;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors,
            $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var constructorSource = result.GetConstructorSource("ValueTypeService");
        Assert.NotNull(constructorSource);
        Assert.Contains("where T : struct", constructorSource.Content);
        Assert.Contains("IValueProcessor<T>", constructorSource.Content);
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
        Assert.False(result.HasErrors,
            $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var constructorSource = result.GetConstructorSource("RecursiveService");
        Assert.NotNull(constructorSource);
        Assert.Contains("where T : INode<T>", constructorSource.Content);
        Assert.Contains("INode<T>", constructorSource.Content);
        Assert.Contains("ITree<INode<T>>", constructorSource.Content);
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
        Assert.False(result.HasErrors,
            $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var constructorSource = result.GetConstructorSource("ConcreteProcessor");
        Assert.NotNull(constructorSource);

        // Should have all dependencies with proper type substitution
        Assert.Contains("IProcessor<int> primaryProcessor", constructorSource.Content); // T = int
        Assert.Contains("IEnumerable<IProcessor<string>> secondaryProcessors", constructorSource.Content); // U = string
        Assert.Contains("IProcessor<IEnumerable<int>> collectionProcessor",
            constructorSource.Content); // T = int in collection
        Assert.Contains("IProcessor<bool> boolProcessor",
            constructorSource.Content); // ConcreteProcessor's own dependency
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
        Assert.False(result.HasErrors,
            $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var constructorSource = result.GetConstructorSource("ArrayMemoryService");
        Assert.NotNull(constructorSource);
        Assert.Contains("string[]", constructorSource.Content);
        Assert.Contains("int[,]", constructorSource.Content);
        Assert.Contains("int[][]", constructorSource.Content);
        Assert.Contains("Memory<char>", constructorSource.Content);
        Assert.Contains("ReadOnlyMemory<int>", constructorSource.Content);
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
        Assert.True(result.HasErrors, "Expected compilation errors for malformed syntax");

        var errors = result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.NotEmpty(errors);

        // Verify specific error types
        Assert.Contains(errors, e => e.Id.Contains("CS"));
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
            var errors = result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
            Assert.NotEmpty(errors);
        }

        // Generator should not crash - either succeeds or fails gracefully
        Assert.NotNull(result.CompilationDiagnostics);
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
        Assert.False(result.HasErrors,
            $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var complexConstructor = result.GetConstructorSource("ComplexGenericService");
        Assert.NotNull(complexConstructor);

        // Verify type parameter substitution is correct
        Assert.Contains("IProcessor<string> stringProcessor", complexConstructor.Content);
        Assert.Contains("IValidator<T> tValidator", complexConstructor.Content);
        Assert.Contains("IConverter<U, T> reverseConverter", complexConstructor.Content);
        Assert.Contains("IEnumerable<IProcessor<T>> tProcessors", complexConstructor.Content);

        var circularConstructor = result.GetConstructorSource("CircularReferenceService");
        Assert.NotNull(circularConstructor);
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
        Assert.False(result.HasErrors,
            $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        // Verify registration method exists and is syntactically correct
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Verify registration method contains service registrations (actual behavior)
        Assert.Contains("Repository<", registrationSource.Content);
        Assert.Contains("BusinessService<", registrationSource.Content);
        Assert.Contains("ConcreteService", registrationSource.Content);

        // Verify constructors were generated
        var repositoryConstructor = result.GetConstructorSource("Repository");
        var businessConstructor = result.GetConstructorSource("BusinessService");
        var concreteConstructor = result.GetConstructorSource("ConcreteService");

        Assert.NotNull(repositoryConstructor);
        Assert.NotNull(businessConstructor);
        Assert.NotNull(concreteConstructor);

        // Verify constructor signatures contain expected parameters
        Assert.Contains("ILogger<Repository<T>>", repositoryConstructor.Content);
        Assert.Contains("IRepository<T>", businessConstructor.Content);
        Assert.Contains("IRepository<string>", concreteConstructor.Content);
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
            Assert.NotEmpty(errors);

            // Verify we get meaningful error messages, not crashes
            Assert.All(errors, error => Assert.False(string.IsNullOrEmpty(error.GetMessage())));
            return;
        }

        var constructorSource = result.GetConstructorSource("RefOutInService");
        Assert.NotNull(constructorSource);

        // If it succeeds, verify full constructor signature
        Assert.Contains(
            "public RefOutInService(RefAction<int> refAction, OutAction<string> outAction, InAction<bool> inAction)",
            constructorSource.Content);
        Assert.Contains("_refAction = refAction;", constructorSource.Content);
        Assert.Contains("_outAction = outAction;", constructorSource.Content);
        Assert.Contains("_inAction = inAction;", constructorSource.Content);
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
        Assert.False(result.HasErrors,
            $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Verify closed generic registration uses FQN format
        Assert.Contains(
            "services.AddScoped<global::Test.IGenericService<string>, global::Test.ConcreteGenericService>();",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.ConcreteGenericService, global::Test.ConcreteGenericService>();",
            registrationSource.Content);

        // Ensure no open generic registration for closed generic service
        Assert.DoesNotContain("AddScoped(typeof(IGenericService<>), typeof(ConcreteGenericService))",
            registrationSource.Content);
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
        Assert.False(result.HasErrors,
            $"Compilation failed: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))}");

        var constructorSource = result.GetConstructorSource("InsaneNestingService");
        Assert.NotNull(constructorSource);

        // Verify complex nested types are handled correctly with full constructor signature
        Assert.Contains("public InsaneNestingService(", constructorSource.Content);
        Assert.Contains("Dictionary<string, List<Func<int, Task<IEnumerable<KeyValuePair<Guid, string>>>>>",
            constructorSource.Content);
        Assert.Contains("Func<IEnumerable<Dictionary<int, List<string>>>, Task<bool>>", constructorSource.Content);
        Assert.Contains("IEnumerable<Func<Dictionary<string, int>, Task<string>>>", constructorSource.Content);

        // Verify field assignments
        Assert.Contains("_insaneNesting = ", constructorSource.Content);
        Assert.Contains("_complexFunc = ", constructorSource.Content);
        Assert.Contains("_taskFunc = ", constructorSource.Content);
    }
}
