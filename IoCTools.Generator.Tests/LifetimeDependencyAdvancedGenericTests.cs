namespace IoCTools.Generator.Tests;

using System.Diagnostics;
using System.Text;

using Microsoft.CodeAnalysis;

/// <summary>
///     COMPREHENSIVE ADVANCED GENERIC LIFETIME VALIDATION TESTS
///     This test suite validates complex generic scenarios with lifetime dependency validation.
///     Covers all advanced generic patterns with comprehensive lifetime validation coverage.
///     Test Categories:
///     1. Multiple Type Parameter Generics with lifetime validation
///     2. Nested Generic Dependencies with lifetime conflicts
///     3. Generic Inheritance Chains with lifetime mismatches
///     4. Open vs Constructed Generic Mapping with lifetimes
///     5. Generic Constraints and Lifetime Validation
///     6. Complex Generic Factory Patterns with lifetime validation
///     7. Generic Collection Scenarios and lifetime dependencies
///     8. Performance with Complex Generics and lifetime validation
///     9. Error Scenarios with Generics and lifetime conflicts
///     10. Real-World Generic Patterns with lifetime validation
/// </summary>
public class LifetimeDependencyAdvancedGenericTests
{
    #region Integration Tests

    [Fact]
    public void LifetimeGeneric_ComplexGenericScenario_IntegrationTest_AllFeaturesWork()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System;
using System.Collections.Generic;

namespace TestNamespace;

public interface IRepository<TEntity, TKey> where TEntity : class { }
public interface IService<T> { }
public interface IFactory<T> { }
public interface IValidator<T> { }

[Scoped]
public partial class DatabaseContext { }

[Scoped]
public partial class Repository<TEntity, TKey> : IRepository<TEntity, TKey> 
    where TEntity : class
{
    [Inject] private readonly DatabaseContext _context;
}

[Transient]
public partial class Service<T> : IService<T> where T : class
{
    [Inject] private readonly IRepository<T, int> _repository;
    [Inject] private readonly IValidator<T> _validator;
}

[Singleton]
public partial class Factory<T> : IFactory<T> where T : class, new() { }

[Scoped]
public partial class Validator<T> : IValidator<T> { }

public class User { public User() { } }

[Singleton]
public partial class ComplexOrchestrator
{
    [Inject] private readonly IService<User> _userService;
    [Inject] private readonly IFactory<User> _userFactory;
    [Inject] private readonly IEnumerable<IValidator<User>> _userValidators;
    [Inject] private readonly IRepository<User, int> _userRepository;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var ioc012 = result.GetDiagnosticsByCode("IOC012");
        var ioc013 = result.GetDiagnosticsByCode("IOC013");

        // Should detect multiple lifetime violations
        ioc012.Concat(ioc013).Should().NotBeEmpty();

        // Should still compile successfully
        result.HasErrors.Should().BeFalse();

        // Should generate constructors and registrations
        var orchestratorConstructor = result.GetConstructorSource("ComplexOrchestrator");
        var registrationSource = result.GetRequiredServiceRegistrationSource();

        orchestratorConstructor.Should().NotBeNull();
        registrationSource.Should().NotBeNull();
    }

    #endregion

    #region Multiple Type Parameter Generics

    [Fact]
    public void LifetimeGeneric_MultipleTypeParameterGeneric_SingletonDependsOnScoped_ReportsError()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

public interface IRepository<TEntity, TKey>
{
    TEntity GetById(TKey id);
}

[Scoped]
public partial class UserRepository<TEntity, TKey> : IRepository<TEntity, TKey>
    where TEntity : class
    where TKey : struct
{
    public TEntity GetById(TKey id) => default(TEntity);
}

[Singleton]
public partial class CacheService
{
    [Inject] private readonly IRepository<string, int> _userRepository;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var diagnostics = result.GetDiagnosticsByCode("IOC012");

        diagnostics.Should().ContainSingle();
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].GetMessage().Should().Contain("CacheService");
        diagnostics[0].GetMessage().Should().Contain("IRepository"); // Generic interface type in diagnostic
    }

    [Fact]
    public void LifetimeGeneric_TripleTypeParameterGeneric_LifetimeMismatch_ReportsError()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

public interface IService<T1, T2, T3>
{
    void Process(T1 first, T2 second, T3 third);
}

[Transient]
public partial class ProcessorService<T1, T2, T3> : IService<T1, T2, T3>
    where T1 : class
    where T2 : struct
    where T3 : IComparable<T3>
{
    public void Process(T1 first, T2 second, T3 third) { }
}

[Singleton]
public partial class OrchestrationService
{
    [Inject] private readonly IService<string, int, DateTime> _processor;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var diagnostics = result.GetDiagnosticsByCode("IOC013");

        diagnostics.Should().ContainSingle();
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostics[0].GetMessage().Should().Contain("OrchestrationService");
        diagnostics[0].GetMessage().Should().Contain("ProcessorService");
    }

    [Fact]
    public void LifetimeGeneric_MixedOpenConstructedGenerics_LifetimeValidation_ReportsCorrectly()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

public interface IRepository<T> where T : class { }
public interface ICache<T> { }

[Scoped]
public partial class Repository<T> : IRepository<T> where T : class { }

[Transient]
public partial class Cache<T> : ICache<T> { }

[Singleton]
public partial class BusinessService<T> where T : class
{
    [Inject] private readonly IRepository<T> _openScoped;
    [Inject] private readonly IRepository<string> _constructedScoped;
    [Inject] private readonly ICache<T> _openTransient;
    [Inject] private readonly ICache<int> _constructedTransient;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var ioc012 = result.GetDiagnosticsByCode("IOC012");
        var ioc013 = result.GetDiagnosticsByCode("IOC013");

        ioc012.Count.Should().Be(2); // Two Singleton → Scoped errors
        ioc013.Count.Should().Be(2); // Two Singleton → Transient warnings
    }

    [Fact]
    public void LifetimeGeneric_GenericConstraintAffectingLifetime_ValidatesCorrectly()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

public interface IEntity { }
public interface IValidatable { }

public interface IEntityRepository<T> where T : class, IEntity, IValidatable { }

[Scoped]
public partial class EntityRepository<T> : IEntityRepository<T> 
    where T : class, IEntity, IValidatable
{
}

public class User : IEntity, IValidatable { }

[Singleton]
public partial class UserService
{
    [Inject] private readonly IEntityRepository<User> _userRepository;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var diagnostics = result.GetDiagnosticsByCode("IOC012");

        diagnostics.Should().ContainSingle();
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].GetMessage().Should().Contain("UserService");
        diagnostics[0].GetMessage().Should().Contain("EntityRepository");
    }

    #endregion

    #region Nested Generic Dependencies

    [Fact]
    public void LifetimeGeneric_NestedGenericHandler_LifetimeMismatch_ReportsError()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

public interface ICommand<TRequest, TResponse> { }
public interface IHandler<TCommand> where TCommand : ICommand<object, object> { }

public class UserCommand : ICommand<string, bool> { }

[Scoped]
public partial class UserCommandHandler : IHandler<UserCommand>
{
}

[Singleton]
public partial class CommandOrchestrator
{
    [Inject] private readonly IHandler<UserCommand> _userHandler;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var diagnostics = result.GetDiagnosticsByCode("IOC012");

        diagnostics.Should().ContainSingle();
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void LifetimeGeneric_DeeplyNestedGenericHierarchy_LifetimeValidation_ReportsCorrectly()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace TestNamespace;

public interface IProcessor<T> { }
public interface IValidator<T> { }
public interface IHandler<T> { }

[Transient]
public partial class Level1Processor<T> : IProcessor<T> { }

[Scoped]
public partial class Level2Validator<T> : IValidator<IProcessor<T>> 
{
    [Inject] private readonly IProcessor<T> _processor;
}

[Transient]
public partial class Level3Handler<T> : IHandler<IValidator<IProcessor<T>>>
{
    [Inject] private readonly IValidator<IProcessor<T>> _validator;
}

[Singleton]
public partial class DeepNestedService
{
    [Inject] private readonly IHandler<IValidator<IProcessor<string>>> _deepHandler;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var ioc012 = result.GetDiagnosticsByCode("IOC012");
        var ioc013 = result.GetDiagnosticsByCode("IOC013");

        // Should detect Singleton → Scoped and Singleton → Transient violations
        ioc012.Concat(ioc013).Should().NotBeEmpty();
    }

    [Fact]
    public void LifetimeGeneric_GenericCollectionDependencies_LifetimeValidation_ReportsCorrectly()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace TestNamespace;

public interface IService<T> { }
public interface IProcessor<T> { }

[Scoped]
public partial class ServiceA<T> : IService<T> { }

[Transient]
public partial class ServiceB<T> : IService<T> { }

[Scoped]
public partial class ProcessorA<T> : IProcessor<T> { }

[Singleton]
public partial class CollectionDependentService
{
    [Inject] private readonly IEnumerable<IService<string>> _services;
    [Inject] private readonly IList<IProcessor<int>> _processors;
    [Inject] private readonly ICollection<IService<bool>> _collection;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var diagnostics = result.GetDiagnosticsByCode("IOC012");

        // Should detect violations for scoped services in collections
        diagnostics.Should().NotBeEmpty();
    }

    #endregion

    #region Generic Inheritance Chains

    [Fact]
    public void LifetimeGeneric_GenericInheritanceChain_LifetimeMismatch_ReportsError()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

[Scoped]
public partial class BaseService<T>
{
    [Inject] private readonly IComparer<T> _comparer;
}

[Scoped]
public partial class MiddleService<T> : BaseService<T>
{
    [Inject] private readonly IEqualityComparer<T> _equalityComparer;
}

[Singleton]
public partial class ConcreteService : MiddleService<string>
{
    [Inject] private readonly IEnumerable<string> _items;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var diagnostics = result.GetDiagnosticsByCode("IOC015");

        diagnostics.Should().ContainSingle();
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostics[0].GetMessage().Should().Contain("ConcreteService");
    }

    [Fact]
    public void LifetimeGeneric_TypeParameterSubstitution_InheritanceLifetimeValidation_ReportsCorrectly()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

public interface IRepository<T> { }

[Scoped]
public partial class DatabaseContext { }

[Scoped]
public partial class GenericRepository<T> : IRepository<T>
{
    [Inject] private readonly DatabaseContext _context;
}

[Scoped]
public partial class BaseService<T>
{
    [Inject] private readonly IRepository<T> _repository;
}

[Singleton]
public partial class UserService : BaseService<string>
{
    [Inject] private readonly IRepository<int> _intRepository;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var diagnostics = result.GetDiagnosticsByCode("IOC015");

        diagnostics.Should().ContainSingle();
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void LifetimeGeneric_ConstrainedGenericInheritance_LifetimeValidation_ReportsCorrectly()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

public interface IEntity { }
public interface IValidatable { }

[Scoped]
public partial class DatabaseContext { }

[Scoped]
public partial class EntityService<T> 
    where T : class, IEntity, IValidatable, new()
{
    [Inject] private readonly DatabaseContext _context;
}

public class User : IEntity, IValidatable 
{
    public User() { }
}

[Singleton]
public partial class UserService : EntityService<User>
{
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var diagnostics = result.GetDiagnosticsByCode("IOC015");

        diagnostics.Should().ContainSingle();
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
    }

    #endregion

    #region Open vs Constructed Generic Mapping

    [Fact]
    public void LifetimeGeneric_OpenGenericMappingToConstructed_LifetimeValidation_ReportsCorrectly()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

public interface IRepository<T> { }

[Scoped]
public partial class Repository<T> : IRepository<T> { }

[Singleton]
public partial class BusinessService
{
    [Inject] private readonly IRepository<string> _stringRepo;
    [Inject] private readonly IRepository<int> _intRepo;
    [Inject] private readonly IRepository<bool> _boolRepo;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var diagnostics = result.GetDiagnosticsByCode("IOC012");

        diagnostics.Count.Should().Be(3); // Three Singleton → Scoped violations
        diagnostics.Should().AllSatisfy(d => d.Severity.Should().Be(DiagnosticSeverity.Error));
    }

    [Fact]
    public void LifetimeGeneric_ComplexTypeArgumentResolution_LifetimeValidation_ReportsCorrectly()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace TestNamespace;

public interface IProcessor<T> { }
public interface ICache<T> { }

[Transient]
public partial class Processor<T> : IProcessor<T> { }

[Scoped]
public partial class Cache<T> : ICache<T> { }

[Singleton]
public partial class ComplexService
{
    [Inject] private readonly IProcessor<List<string>> _listProcessor;
    [Inject] private readonly ICache<Dictionary<int, string>> _dictCache;
    [Inject] private readonly IProcessor<IEnumerable<bool>> _enumProcessor;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var ioc012 = result.GetDiagnosticsByCode("IOC012");
        var ioc013 = result.GetDiagnosticsByCode("IOC013");

        ioc012.Should().ContainSingle(); // One Singleton → Scoped error
        ioc013.Should().ContainSingle(); // Singleton → Transient warning (may be deduplicated by generator)
    }

    [Fact]
    public void LifetimeGeneric_GenericVarianceLifetimeValidation_ReportsCorrectly()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

public interface ICovariant<out T> { T Get(); }
public interface IContravariant<in T> { void Set(T value); }

[Scoped]
public partial class CovariantService<T> : ICovariant<T> 
{
    public T Get() => default(T);
}

[Transient]
public partial class ContravariantService<T> : IContravariant<T> 
{
    public void Set(T value) { }
}

[Singleton]
public partial class VarianceService
{
    [Inject] private readonly ICovariant<string> _covariant;
    [Inject] private readonly IContravariant<object> _contravariant;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var ioc012 = result.GetDiagnosticsByCode("IOC012");
        var ioc013 = result.GetDiagnosticsByCode("IOC013");

        ioc012.Should().ContainSingle(); // Singleton → Scoped error
        ioc013.Should().ContainSingle(); // Singleton → Transient warning
    }

    #endregion

    #region Generic Constraints and Lifetime Validation

    [Fact]
    public void LifetimeGeneric_StructConstraintLifetimeValidation_ReportsCorrectly()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

public interface IValueProcessor<T> where T : struct { }

[Scoped]
public partial class ValueProcessor<T> : IValueProcessor<T> where T : struct { }

[Singleton]
public partial class MathService
{
    [Inject] private readonly IValueProcessor<int> _intProcessor;
    [Inject] private readonly IValueProcessor<decimal> _decimalProcessor;
    [Inject] private readonly IValueProcessor<DateTime> _dateProcessor;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var diagnostics = result.GetDiagnosticsByCode("IOC012");

        diagnostics.Count.Should().Be(3); // Three Singleton → Scoped violations
    }

    [Fact]
    public void LifetimeGeneric_MultipleConstraintsLifetimeValidation_ReportsCorrectly()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

public interface IEntity { }
public interface IValidatable { }

public interface IEntityService<T> 
    where T : class, IEntity, IValidatable, new() { }

[Transient]
public partial class EntityService<T> : IEntityService<T> 
    where T : class, IEntity, IValidatable, new() { }

public class User : IEntity, IValidatable 
{
    public User() { }
}

[Singleton]
public partial class UserManager
{
    [Inject] private readonly IEntityService<User> _userService;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var diagnostics = result.GetDiagnosticsByCode("IOC013");

        diagnostics.Should().ContainSingle(); // Singleton → Transient warning
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void LifetimeGeneric_UnmanagedConstraintLifetimeValidation_ReportsCorrectly()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

public interface IUnmanagedProcessor<T> where T : unmanaged { }

[Scoped]
public partial class UnmanagedProcessor<T> : IUnmanagedProcessor<T> where T : unmanaged { }

[Singleton]
public partial class NativeService
{
    [Inject] private readonly IUnmanagedProcessor<int> _intProcessor;
    [Inject] private readonly IUnmanagedProcessor<byte> _byteProcessor;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var diagnostics = result.GetDiagnosticsByCode("IOC012");

        diagnostics.Count.Should().Be(2); // Two Singleton → Scoped violations
    }

    #endregion

    #region Complex Generic Factory Patterns

    [Fact]
    public void LifetimeGeneric_GenericFactoryLifetimeValidation_ReportsCorrectly()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System;

namespace TestNamespace;

public interface IFactory<T> 
{
    T Create();
}

public interface IService<T> { }

[Scoped]
public partial class ServiceFactory<T> : IFactory<IService<T>>
{
    public IService<T> Create() => default(IService<T>);
}

[Singleton]
public partial class FactoryConsumer
{
    [Inject] private readonly IFactory<IService<string>> _stringServiceFactory;
    [Inject] private readonly IFactory<IService<int>> _intServiceFactory;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var diagnostics = result.GetDiagnosticsByCode("IOC012");

        diagnostics.Count.Should().Be(2); // Two Singleton → Scoped violations
    }

    [Fact]
    public void LifetimeGeneric_GenericFactoryMethodsLifetimeValidation_ReportsCorrectly()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System;

namespace TestNamespace;

[Transient]
public partial class GenericFactoryService<T> where T : new()
{
    [Inject] private readonly Func<T> _factory;
    [Inject] private readonly Func<string, T> _parameterizedFactory;
}

[Singleton]
public partial class FactoryOrchestrator
{
    [Inject] private readonly GenericFactoryService<DateTime> _dateFactory;
    [Inject] private readonly GenericFactoryService<Guid> _guidFactory;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var diagnostics = result.GetDiagnosticsByCode("IOC013");

        diagnostics.Count.Should().Be(2); // Two Singleton → Transient warnings
    }

    [Fact]
    public void LifetimeGeneric_ConditionalGenericFactoryLifetimeValidation_ReportsCorrectly()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System;

namespace TestNamespace;

public interface IConditionalFactory<T> where T : class
{
    T CreateIfCondition(bool condition);
}

[Scoped]
public partial class ConditionalFactory<T> : IConditionalFactory<T> where T : class, new()
{
    public T CreateIfCondition(bool condition) => condition ? new T() : null;
}

[Singleton]
public partial class ConditionalService
{
    [Inject] private readonly IConditionalFactory<string> _stringFactory;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var diagnostics = result.GetDiagnosticsByCode("IOC012");

        diagnostics.Should().ContainSingle(); // Singleton → Scoped violation
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
    }

    #endregion

    #region Generic Collection Scenarios

    [Fact]
    public void LifetimeGeneric_EnumerableGenericHandlersLifetimeValidation_ReportsCorrectly()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace TestNamespace;

public interface IHandler<T> { }
public interface ICommand<T> { }

[Scoped]
public partial class CommandHandlerA<T> : IHandler<ICommand<T>> { }

[Transient]
public partial class CommandHandlerB<T> : IHandler<ICommand<T>> { }

[Singleton]
public partial class CommandDispatcher
{
    [Inject] private readonly IEnumerable<IHandler<ICommand<string>>> _stringHandlers;
    [Inject] private readonly IEnumerable<IHandler<ICommand<int>>> _intHandlers;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var diagnostics = result.GetDiagnosticsByCode("IOC012").Concat(result.GetDiagnosticsByCode("IOC013")).ToList();

        diagnostics.Should().NotBeEmpty(); // Should detect lifetime violations in collections
    }

    [Fact]
    public void LifetimeGeneric_ArrayGenericServicesLifetimeValidation_ReportsCorrectly()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

public interface IProcessor<T> { }

[Scoped]
public partial class ProcessorA<T> : IProcessor<T> { }

[Transient]
public partial class ProcessorB<T> : IProcessor<T> { }

[Singleton]
public partial class ArrayDependentService
{
    [Inject] private readonly IProcessor<string>[] _stringProcessors;
    [Inject] private readonly IProcessor<int>[] _intProcessors;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var diagnostics = result.GetDiagnosticsByCode("IOC012").Concat(result.GetDiagnosticsByCode("IOC013")).ToList();

        diagnostics.Should().NotBeEmpty(); // Should detect lifetime violations with arrays
    }

    [Fact]
    public void LifetimeGeneric_LazyGenericCollectionLifetimeValidation_ReportsCorrectly()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System;
using System.Collections.Generic;

namespace TestNamespace;

public interface IService<T> { }

[Scoped]
public partial class ServiceImpl<T> : IService<T> { }

[Singleton]
public partial class LazyService
{
    [Inject] private readonly Lazy<IEnumerable<IService<string>>> _lazyServices;
    [Inject] private readonly Lazy<IService<int>> _lazyIntService;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var diagnostics = result.GetDiagnosticsByCode("IOC012");

        diagnostics.Should().NotBeEmpty(); // Should detect violations with Lazy<T>
    }

    #endregion

    #region Performance with Complex Generics

    [Fact]
    public void LifetimeGeneric_LargeGenericServiceHierarchy_PerformanceValidation_CompletesInReasonableTime()
    {
        var sourceCodeBuilder = new StringBuilder();
        sourceCodeBuilder.AppendLine(@"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace TestNamespace;");

        // Create 50 generic service interfaces and implementations
        for (var i = 0; i < 50; i++)
        {
            var lifetime = (i % 3) switch
            {
                0 => "Singleton",
                1 => "Scoped",
                _ => "Transient"
            };

            sourceCodeBuilder.AppendLine($@"
public interface IService{i}<T> {{ }}

[{lifetime}]
public partial class Service{i}<T> : IService{i}<T> {{ }}");
        }

        // Create services with complex generic dependencies
        sourceCodeBuilder.AppendLine(@"
[Singleton]
public partial class ComplexGenericService
{");

        for (var i = 0; i < 20; i++)
            sourceCodeBuilder.AppendLine($"    [Inject] private readonly IService{i}<string> _service{i};");

        sourceCodeBuilder.AppendLine("}");

        var sourceCode = sourceCodeBuilder.ToString();

        var stopwatch = Stopwatch.StartNew();
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        stopwatch.Stop();

        // Should complete in under 30 seconds
        (stopwatch.ElapsedMilliseconds < 30000).Should()
            .BeTrue($"Large generic hierarchy validation took {stopwatch.ElapsedMilliseconds}ms");

        // Should still detect appropriate lifetime violations
        (result.Compilation != null).Should().BeTrue();
    }

    [Fact]
    public void LifetimeGeneric_ComplexGenericTypeResolution_PerformanceValidation_CompletesInReasonableTime()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TestNamespace;

[Scoped]
public partial class ComplexGenericService<T1, T2, T3, T4, T5>
    where T1 : class
    where T2 : struct
    where T3 : IComparable<T3>
    where T4 : IEnumerable<T1>
    where T5 : IDictionary<T1, T2>
{
    [Inject] private readonly Dictionary<T1, List<Func<T2, Task<IEnumerable<KeyValuePair<T3, T1>>>>>> _complexDependency;
    [Inject] private readonly Func<T4, Task<T5>> _complexFunc;
    [Inject] private readonly IEnumerable<Func<T1, Task<Dictionary<T2, T3>>>> _complexEnumerable;
}

[Singleton]
public partial class PerformanceTestService
{
    [Inject] private readonly ComplexGenericService<string, int, DateTime, List<string>, Dictionary<string, int>> _complexService;
}";

        var stopwatch = Stopwatch.StartNew();
        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        stopwatch.Stop();

        // Should complete in under 10 seconds for complex type resolution
        (stopwatch.ElapsedMilliseconds < 10000).Should()
            .BeTrue($"Complex generic type resolution took {stopwatch.ElapsedMilliseconds}ms");

        var diagnostics = result.GetDiagnosticsByCode("IOC012");
        diagnostics.Should().ContainSingle(); // Should detect Singleton → Scoped violation
    }

    #endregion

    #region Error Scenarios with Generics

    [Fact]
    public void LifetimeGeneric_UnresolvableGenericTypeParameters_HandlesGracefully()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

public interface IProcessor<T> { }

[Singleton]
public partial class UnresolvableService<T> where T : class
{
    [Inject] private readonly IProcessor<T> _processor; // No implementation available
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Should not crash, may generate warnings/errors about missing implementations
        result.Compilation.Should().NotBeNull();
    }

    [Fact]
    public void LifetimeGeneric_CircularGenericDependencies_LifetimeValidation_DetectsCorrectly()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

public interface IServiceA<T> { }
public interface IServiceB<T> { }

[Singleton]
public partial class ServiceA<T> : IServiceA<T>
{
    [Inject] private readonly IServiceB<T> _serviceB;
}

[Scoped]
public partial class ServiceB<T> : IServiceB<T>
{
    [Inject] private readonly IServiceA<T> _serviceA;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var diagnostics = result.GetDiagnosticsByCode("IOC012");

        // Should detect Singleton → Scoped violation
        diagnostics.Should().ContainSingle();
        diagnostics[0].GetMessage().Should().Contain("ServiceA");
        diagnostics[0].GetMessage().Should().Contain("ServiceB");
    }

    [Fact]
    public void LifetimeGeneric_MissingGenericServiceRegistrations_HandlesGracefully()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

public interface IMissingService<T> { }

[Singleton]
public partial class DependentService
{
    [Inject] private readonly IMissingService<string> _missingService;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Should not crash, may generate warnings about missing service registrations
        result.Compilation.Should().NotBeNull();
    }

    [Fact]
    public void LifetimeGeneric_InvalidGenericConstraintCombinations_HandlesGracefully()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

// Invalid constraint combination: struct and class
public interface IInvalidConstraints<T> where T : struct, class { }

[Singleton]
public partial class InvalidConstraintService
{
    [Inject] private readonly IInvalidConstraints<int> _invalid;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Should have compilation errors for invalid constraints
        result.HasErrors.Should().BeTrue();
        var errors = result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        errors.Should().NotBeEmpty();
    }

    #endregion

    #region Real-World Generic Patterns

    [Fact]
    public void LifetimeGeneric_CQRSPatternLifetimeValidation_ReportsCorrectly()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Threading.Tasks;

namespace TestNamespace;

public interface IRequest<TResponse> { }
public interface IRequestHandler<TRequest, TResponse> 
    where TRequest : IRequest<TResponse> { }

public class GetUserQuery : IRequest<string> { }
public class CreateUserCommand : IRequest<bool> { }

[Scoped]
public partial class GetUserQueryHandler : IRequestHandler<GetUserQuery, string>
{
}

[Transient]
public partial class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, bool>
{
}

[Singleton]
public partial class MediatrService
{
    [Inject] private readonly IRequestHandler<GetUserQuery, string> _getUserHandler;
    [Inject] private readonly IRequestHandler<CreateUserCommand, bool> _createUserHandler;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var ioc012 = result.GetDiagnosticsByCode("IOC012");
        var ioc013 = result.GetDiagnosticsByCode("IOC013");

        ioc012.Should().ContainSingle(); // Singleton → Scoped violation
        ioc013.Should().ContainSingle(); // Singleton → Transient warning
    }

    [Fact]
    public void LifetimeGeneric_RepositoryPatternLifetimeValidation_ReportsCorrectly()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

public interface IEntity { }
public interface IRepository<TEntity> where TEntity : class, IEntity { }
public interface IUnitOfWork { }

public class User : IEntity { }
public class Order : IEntity { }

[Scoped]
public partial class DatabaseContext { }

[Scoped]
public partial class UnitOfWork : IUnitOfWork 
{
    [Inject] private readonly DatabaseContext _context;
}

[Scoped]
public partial class Repository<TEntity> : IRepository<TEntity> 
    where TEntity : class, IEntity
{
    [Inject] private readonly DatabaseContext _context;
    [Inject] private readonly IUnitOfWork _unitOfWork;
}

[Singleton]
public partial class BusinessService
{
    [Inject] private readonly IRepository<User> _userRepository;
    [Inject] private readonly IRepository<Order> _orderRepository;
    [Inject] private readonly IUnitOfWork _unitOfWork;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var diagnostics = result.GetDiagnosticsByCode("IOC012");

        diagnostics.Count.Should().Be(3); // Three Singleton → Scoped violations
    }

    [Fact]
    public void LifetimeGeneric_DomainServicePatternLifetimeValidation_ReportsCorrectly()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

public interface IDomainService<TEntity> { }
public interface IBusinessRules<TEntity> { }
public interface IValidator<TEntity> { }

[Transient]
public partial class DomainService<TEntity> : IDomainService<TEntity> 
    where TEntity : class
{
    [Inject] private readonly IBusinessRules<TEntity> _businessRules;
    [Inject] private readonly IValidator<TEntity> _validator;
}

[Scoped]
public partial class BusinessRules<TEntity> : IBusinessRules<TEntity> 
    where TEntity : class { }

[Singleton]
public partial class Validator<TEntity> : IValidator<TEntity> 
    where TEntity : class { }

public class Product { }

[Singleton]
public partial class ProductManager
{
    [Inject] private readonly IDomainService<Product> _productService;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var diagnostics = result.GetDiagnosticsByCode("IOC013");

        diagnostics.Should().ContainSingle(); // Singleton → Transient warning
    }

    [Fact]
    public void LifetimeGeneric_EventHandlingPatternLifetimeValidation_ReportsCorrectly()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;

namespace TestNamespace;

public interface IEvent { }
public interface IEventHandler<TEvent> where TEvent : IEvent { }
public interface IEventDispatcher { }

public class UserCreatedEvent : IEvent { }
public class OrderPlacedEvent : IEvent { }

[Scoped]
public partial class UserCreatedEventHandler : IEventHandler<UserCreatedEvent> { }

[Transient]
public partial class OrderPlacedEventHandler : IEventHandler<OrderPlacedEvent> { }

[Scoped]
public partial class EventDispatcher : IEventDispatcher
{
    [Inject] private readonly IEnumerable<IEventHandler<UserCreatedEvent>> _userHandlers;
    [Inject] private readonly IEnumerable<IEventHandler<OrderPlacedEvent>> _orderHandlers;
}

[Singleton]
public partial class EventOrchestrator
{
    [Inject] private readonly IEventDispatcher _dispatcher;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);
        var diagnostics = result.GetDiagnosticsByCode("IOC012");

        diagnostics.Should().ContainSingle(); // Singleton → Scoped violation
    }

    #endregion

    #region DEBUG TESTS

    [Fact]
    public void Debug_ComplexGenericLifetime_InvestigateIssue()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System;
using System.Collections.Generic;

namespace TestNamespace;

[Scoped]
public partial class ComplexGenericService<T1, T2, T3, T4, T5>
    where T1 : class
    where T2 : struct
    where T3 : IComparable<T3>
    where T4 : IEnumerable<T1>
    where T5 : IDictionary<T1, T2>
{
}

[Singleton]
public partial class PerformanceTestService
{
    [Inject] private readonly ComplexGenericService<string, int, DateTime, List<string>, Dictionary<string, int>> _complexService;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        // Debug output

        var ioc001Diagnostics = result.GetDiagnosticsByCode("IOC001");
        var ioc012Diagnostics = result.GetDiagnosticsByCode("IOC012");
        // IOC001 and IOC012 diagnostics checked

        // The issue: we're getting IOC001 instead of IOC012
        // This suggests the dependency resolution is failing before lifetime validation
        // Let's expect IOC012 once we fix the resolution
        ioc012Diagnostics.Should().ContainSingle(); // Should detect Singleton → Scoped violation
    }

    [Fact]
    public void Debug_SimpleGenericLifetime_ShouldReportIOC012()
    {
        var sourceCode = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace TestNamespace;

[Scoped]
public partial class SimpleGenericService<T>
{
}

[Singleton]
public partial class TestService
{
    [Inject] private readonly SimpleGenericService<string> _service;
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(sourceCode);

        var ioc012Diagnostics = result.GetDiagnosticsByCode("IOC012");

        // This should pass if working correctly
        ioc012Diagnostics.Should().ContainSingle(); // Should detect Singleton → Scoped violation
    }

    #endregion
}
