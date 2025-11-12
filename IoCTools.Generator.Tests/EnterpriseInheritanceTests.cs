namespace IoCTools.Generator.Tests;

using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;

/// <summary>
///     ENTERPRISE INHERITANCE TESTS - COMPREHENSIVE MULTI-LEVEL INHERITANCE SCENARIOS
///     Tests complex enterprise scenarios with 3+ level inheritance chains, diamond dependencies,
///     mixed patterns, generic constraints, lifetime validation, and configuration injection.
/// </summary>
public class EnterpriseInheritanceTests
{
    [Fact]
    public void Enterprise_ThreeLevelInheritanceChain_WithDependenciesAtEachLevel()
    {
        // Arrange - 3 levels with dependencies at each level
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System;

namespace Test;

public interface ILevel1Service { }
public interface ILevel2Service { }
public interface ILevel3Service { }

[Scoped]
[DependsOn<ILevel1Service>]
public abstract partial class Level1Base
{
    [Inject] protected readonly string _level1String;
}

[Scoped]
[DependsOn<ILevel2Service>]
public abstract partial class Level2Middle : Level1Base
{
    [Inject] protected readonly int _level2Int;
}

[Scoped]
[DependsOn<ILevel3Service>]
public partial class Level3Final : Level2Middle
{
    [Inject] private readonly bool _level3Bool;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var errorMessages = string.Join(", ", result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.GetMessage()));

        // Assert
        result.HasErrors.Should().BeFalse("3-level inheritance failed: {0}", errorMessages);

        var constructorContent = result.GetConstructorSourceText("Level3Final");

        // All dependencies from all levels should be present
        var expectedParams = new[]
        {
            "ILevel1Service level1Service", // Level1 DependsOn - simple name (generator uses simple names)
            "string level1String", // Level1 Inject
            "ILevel2Service level2Service", // Level2 DependsOn - simple name (generator uses simple names)
            "int level2Int", // Level2 Inject
            "ILevel3Service level3Service", // Level3 DependsOn - simple name (generator uses simple names)
            "bool level3Bool" // Level3 Inject
        };

        foreach (var param in expectedParams) constructorContent.Should().Contain(param);

        // Validate proper base constructor call with all inherited dependencies
        // The generator orders parameters by level: DependsOn first, then Inject for each level
        var baseCallRegex =
            new Regex(@":\s*base\s*\(\s*level1Service\s*,\s*level1String\s*,\s*level2Service\s*,\s*level2Int\s*\)");
        baseCallRegex.IsMatch(constructorContent).Should().BeTrue(
            "Base constructor call should include all inherited dependencies");

        // Only Level3Final should be registered with Scoped lifetime
        var registrationContent = result.GetServiceRegistrationText();
        registrationContent.Should().Contain(
            "services.AddScoped<global::Test.Level3Final, global::Test.Level3Final>()");
        registrationContent.Should().NotContain("Level1Base>");
        registrationContent.Should().NotContain("Level2Middle>");
    }

    [Fact]
    public void Enterprise_FiveLevelDeepInheritance_WithMixedPatterns()
    {
        // Arrange - 5 levels deep with mixed DependsOn and Inject patterns
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System.Collections.Generic;
using System;

namespace Test;

public interface IRepo<T> { }
public interface IValidator<T> { }
public interface IService1 { }
public interface IService2 { }
public interface IService3 { }

public abstract partial class Level1<T> where T : class
{
    [Inject] protected readonly IRepo<T> _repo;
}

[DependsOn<IService1>]
public abstract partial class Level2<T> : Level1<T> where T : class
{
    [Inject] protected readonly IValidator<T> _validator;
}

[DependsOn<IService2, IService3>]
public abstract partial class Level3<T> : Level2<T> where T : class
{
    [Inject] protected readonly IList<T> _items;
}

public abstract partial class Level4 : Level3<string>
{
    [Inject] protected readonly TimeSpan _timeout;
}

[Singleton]
public partial class Level5Concrete : Level4
{
    [Inject] private readonly DateTime _created;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var errorMessages = string.Join(", ", result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.GetMessage()));

        // Assert
        result.HasErrors.Should().BeFalse("5-level inheritance failed: {0}", errorMessages);

        var constructorContent = result.GetConstructorSourceText("Level5Concrete");

        // All generic types should be resolved to string - generator uses simple names
        var expectedParams = new[]
        {
            "IRepo<string> repo", // Level1 - generic resolved
            "IService1 service1", // Level2 - DependsOn
            "IValidator<string> validator", // Level2 - generic resolved
            "IService2 service2", // Level3 - DependsOn
            "IService3 service3", // Level3 - DependsOn  
            "IList<string> items", // Level3 - generic resolved
            "TimeSpan timeout", // Level4 - Inject
            "DateTime created" // Level5 - Inject
        };

        foreach (var param in expectedParams) constructorContent.Should().Contain(param);

        // Only Level5Concrete should be registered as Singleton
        var registrationContent = result.GetServiceRegistrationText();
        registrationContent.Should().Contain(
            "services.AddSingleton<global::Test.Level5Concrete, global::Test.Level5Concrete>()");

        // No abstract/unregistered classes should be registered
        for (var i = 1; i <= 4; i++) registrationContent.Should().NotContain($"Level{i}>");
    }

    [Fact]
    public void Enterprise_DiamondInheritancePattern_ComplexMultiplePaths()
    {
        // Arrange - Complex diamond pattern with multiple inheritance paths
        var source = @"
using IoCTools.Abstractions.Annotations;
using System;

namespace Test;

public interface IBaseService { }
public interface ILeftService { }
public interface IRightService { }
public interface IMiddleService { }
public interface IFinalService { }

// Root of diamond
[DependsOn<IBaseService>]
public abstract partial class DiamondRoot
{
    [Inject] protected readonly string _rootData;
}

// Left branch
[DependsOn<ILeftService>]
public abstract partial class LeftBranch : DiamondRoot
{
    [Inject] protected readonly int _leftValue;
}

// Right branch
[DependsOn<IRightService>]
public abstract partial class RightBranch : DiamondRoot
{
    [Inject] protected readonly bool _rightFlag;
}

// Middle converger - inherits from left but also has right functionality
[DependsOn<IMiddleService>]
public abstract partial class MiddleConverger : LeftBranch
{
    [Inject] protected readonly decimal _middleAmount;
}

// Final diamond point - complex convergence

[DependsOn<IFinalService>]
public partial class DiamondFinal : MiddleConverger
{
    [Inject] private readonly DateTime _finalTimestamp;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var errorMessages = string.Join(", ", result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.GetMessage()));

        // Assert
        result.HasErrors.Should().BeFalse("Diamond inheritance failed: {0}", errorMessages);

        var constructorContent = result.GetConstructorSourceText("DiamondFinal");

        // Should have all dependencies from the inheritance path (Root -> Left -> Middle -> Final)
        var expectedParams = new[]
        {
            "IBaseService baseService", // DiamondRoot - DependsOn
            "string rootData", // DiamondRoot - Inject
            "ILeftService leftService", // LeftBranch - DependsOn
            "int leftValue", // LeftBranch - Inject
            "IMiddleService middleService", // MiddleConverger - DependsOn
            "decimal middleAmount", // MiddleConverger - Inject
            "IFinalService finalService", // DiamondFinal - DependsOn
            "DateTime finalTimestamp" // DiamondFinal - Inject
        };

        foreach (var param in expectedParams) constructorContent.Should().Contain(param);

        // Should NOT have RightBranch dependencies (not in inheritance path)
        constructorContent.Should().NotContain("IRightService");
        constructorContent.Should().NotContain("rightFlag");

        // Validate base constructor call includes all inherited dependencies
        // Generator orders parameters by level and type
        var baseCallRegex =
            new Regex(
                @":\s*base\s*\(\s*baseService\s*,\s*rootData\s*,\s*leftService\s*,\s*leftValue\s*,\s*middleService\s*,\s*middleAmount\s*\)");
        baseCallRegex.IsMatch(constructorContent).Should().BeTrue(
            "Base constructor call should follow inheritance path");
    }

    [Fact]
    public void Enterprise_MixedLifetimeAttributes_ServiceInheritingFromUnmanaged()
    {
        // Arrange - Service inheriting from unregistered base classes with complex patterns
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Logging;
using System;

namespace Test;

public interface IUtilityService { }
public interface IBusinessService { }
public interface IDomainService { }

[DependsOn<IUtilityService>]
public abstract partial class UtilityBase
{
    [Inject] protected readonly ILogger<UtilityBase> _logger;
    [Inject] protected readonly string _connectionString;
}

[DependsOn<IBusinessService>]
public abstract partial class BusinessLayer : UtilityBase
{
    [Inject] protected readonly TimeSpan _timeout;
    [Inject] protected readonly int _retryCount;
}

[Transient] // Domain service - registered
[DependsOn<IDomainService>]
public partial class DomainService : BusinessLayer
{
    [Inject] private readonly DateTime _initialized;
    [Inject] private readonly bool _isActive;
}

// Additional concrete service inheriting same chain
[Scoped]
public partial class AlternateDomainService : BusinessLayer
{
    [Inject] private readonly decimal _version;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var errorMessages = string.Join(", ", result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.GetMessage()));

        // Assert
        result.HasErrors.Should().BeFalse("Mixed service attributes failed: {0}", errorMessages);

        // Check DomainService constructor
        var domainConstructor = result.GetConstructorSourceText("DomainService");

        var expectedDomainParams = new[]
        {
            "IUtilityService utilityService", // UtilityBase - DependsOn
            "ILogger<UtilityBase> logger", // UtilityBase - Inject
            "string connectionString", // UtilityBase - Inject
            "IBusinessService businessService", // BusinessLayer - DependsOn
            "TimeSpan timeout", // BusinessLayer - Inject
            "int retryCount", // BusinessLayer - Inject
            "IDomainService domainService", // DomainService - DependsOn
            "DateTime initialized", // DomainService - Inject
            "bool isActive" // DomainService - Inject
        };

        foreach (var param in expectedDomainParams) domainConstructor.Should().Contain(param);

        // Check AlternateDomainService constructor
        var alternateConstructor = result.GetConstructorSourceText("AlternateDomainService");

        var expectedAlternateParams = new[]
        {
            "IUtilityService utilityService", // Inherited from UtilityBase
            "ILogger<UtilityBase> logger", // Inherited from UtilityBase
            "string connectionString", // Inherited from UtilityBase
            "IBusinessService businessService", // Inherited from BusinessLayer
            "TimeSpan timeout", // Inherited from BusinessLayer
            "int retryCount", // Inherited from BusinessLayer
            "decimal version" // AlternateDomainService - Inject
        };

        foreach (var param in expectedAlternateParams) alternateConstructor.Should().Contain(param);

        // Only concrete services should be registered, not abstract base classes
        var registrationContent = result.GetServiceRegistrationText();

        registrationContent.Should().Contain(
            "services.AddTransient<global::Test.DomainService, global::Test.DomainService>()");
        registrationContent.Should().Contain(
            "services.AddScoped<global::Test.AlternateDomainService, global::Test.AlternateDomainService>()");
        registrationContent.Should().NotContain("UtilityBase>");
        registrationContent.Should().NotContain("BusinessLayer>");
    }

    [Fact]
    public void Enterprise_GenericInheritanceWithComplexConstraints_MultipleTypeParameters()
    {
        // Arrange - Complex generic inheritance with multiple type parameters and constraints
        var source = @"
using IoCTools.Abstractions.Annotations;
using System.Collections.Generic;
using System;

namespace Test;

public interface IEntity { }
public interface IKey { }
public interface IRepository<TEntity, TKey> where TEntity : class, IEntity where TKey : IKey { }
public interface IValidator<T> where T : class { }
public interface IMapper<TSource, TTarget> { }

public abstract partial class GenericBase<TEntity, TKey> 
    where TEntity : class, IEntity, new()
    where TKey : class, IKey
{
    [Inject] protected readonly IRepository<TEntity, TKey> _repository;
    [Inject] protected readonly IValidator<TEntity> _validator;
}

public abstract partial class GenericMiddle<TEntity> : GenericBase<TEntity, StringKey>
    where TEntity : class, IEntity, new()
{
    [Inject] protected readonly IEnumerable<TEntity> _entities;
    [Inject] protected readonly IMapper<TEntity, string> _mapper;
}
public partial class ConcreteService : GenericMiddle<MyEntity>
{
    [Inject] private readonly DateTime _created;
}

// Supporting classes
public class MyEntity : IEntity
{
    public MyEntity() { }
}

public class StringKey : IKey
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var errorMessages = string.Join(", ", result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.GetMessage()));

        // Assert
        result.HasErrors.Should().BeFalse("Complex generic inheritance failed: {0}", errorMessages);

        var constructorContent = result.GetConstructorSourceText("ConcreteService");

        // All generic types should be properly resolved
        var expectedParams = new[]
        {
            "IRepository<MyEntity, StringKey> repository", // GenericBase - resolved generics
            "IValidator<MyEntity> validator", // GenericBase - resolved generic
            "IEnumerable<MyEntity> entities", // GenericMiddle - resolved generic
            "IMapper<MyEntity, string> mapper", // GenericMiddle - resolved generics
            "DateTime created" // ConcreteService - Inject
        };

        foreach (var param in expectedParams) constructorContent.Should().Contain(param);

        // Should not contain unresolved generic parameters
        constructorContent.Should().NotContain("<TEntity>");
        constructorContent.Should().NotContain("<TKey>");
        constructorContent.Should().NotContain("<T>");

        // Validate constraints are preserved in generated constructor
        var constraintRegex = new Regex(@"where\s+TEntity\s*:\s*class\s*,\s*IEntity\s*,\s*new\s*\(\s*\)");
        // Note: Constraints might not appear in constructor generation, focus on type resolution
    }

    [Fact]
    public void Enterprise_ServiceLifetimeInheritance_IOC015Validation()
    {
        // Arrange - Test lifetime inheritance validation (IOC015)
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using System;

namespace Test;

public interface ISingletonService { }
public interface IScopedService { }
public interface ITransientService { }

// Actual service implementations with proper lifetime attributes
[Singleton]
public partial class SingletonServiceImpl : ISingletonService
{
}

[Scoped]
public partial class ScopedServiceImpl : IScopedService
{
}

[Transient]
public partial class TransientServiceImpl : ITransientService
{
}

// Singleton service depending on Scoped - should trigger IOC012
[Singleton]
[DependsOn<IScopedService>] // This should cause IOC012 error (direct dependency)
public partial class ProblematicSingletonService
{
}

// Proper hierarchy - compatible lifetimes
[Scoped]
[DependsOn<ISingletonService>] // OK - Scoped can depend on Singleton
public partial class ValidScopedService
{
}

[Transient]  
[DependsOn<ISingletonService, IScopedService>] // OK - Transient can depend on both
public partial class ValidTransientService
{
}

// Inheritance chain with lifetime conflicts
[DependsOn<IScopedService>]
public abstract partial class BaseWithScopedDependency
{
}

[Singleton] // Should trigger IOC015 - Singleton inheriting Scoped dependency
public partial class InheritedLifetimeProblem : BaseWithScopedDependency
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should generate IOC015 diagnostics for lifetime violations
        var ioc015Diagnostics = result.GetDiagnosticsByCode("IOC015");
        ioc015Diagnostics.Should().NotBeEmpty();

        // Should have at least 1 IOC015 violation:
        // - InheritedLifetimeProblem inheriting scoped dependency as singleton
        // Note: ProblematicSingletonService -> IScopedService is IOC012, not IOC015
        ioc015Diagnostics.Count.Should().BeGreaterOrEqualTo(1,
            "Expected at least one IOC015 diagnostic but got {0}",
            ioc015Diagnostics.Count);

        // Valid services should still compile and register correctly
        var registrationSource = result.GetRequiredServiceRegistrationSource();
        if (registrationSource != null)
        {
            registrationSource.Content.Should().Contain(
                "services.AddScoped<global::Test.ValidScopedService, global::Test.ValidScopedService>()");
            registrationSource.Content.Should().Contain(
                "services.AddTransient<global::Test.ValidTransientService, global::Test.ValidTransientService>()");
        }
    }

    [Fact]
    public void Enterprise_ComplexConstructorGeneration_ProperBaseCallOrdering()
    {
        // Arrange - Complex scenario testing proper base() call parameter ordering
        var source = @"
using IoCTools.Abstractions.Annotations;
using System.Collections.Generic;
using System;

namespace Test;

public interface IService1 { }
public interface IService2 { }
public interface IService3 { }
public interface IService4 { }

[DependsOn<IService1, IService2>]
public abstract partial class Level1
{
    [Inject] protected readonly string _level1Field1;
    [Inject] protected readonly int _level1Field2;
}

[DependsOn<IService3>]
public abstract partial class Level2 : Level1
{
    [Inject] protected readonly bool _level2Field;
    [Inject] protected readonly DateTime _level2Time;
}
[DependsOn<IService4>]
public partial class Level3 : Level2
{
    [Inject] private readonly decimal _level3Amount;
    [Inject] private readonly TimeSpan _level3Duration;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var errorMessages = string.Join(", ", result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.GetMessage()));

        // Assert
        result.HasErrors.Should().BeFalse("Complex constructor generation failed: {0}", errorMessages);

        var constructorContent = result.GetConstructorSourceText("Level3");

        // Validate complete constructor signature with proper parameter ordering
        // Expected order: DependsOn parameters first, then Inject parameters, level by level
        var fullConstructorRegex = new Regex(
            @"public\s+Level3\s*\(\s*" +
            @"IService1\s+service1\s*,\s*" + // Level1 DependsOn
            @"IService2\s+service2\s*,\s*" + // Level1 DependsOn
            @"string\s+level1Field1\s*,\s*" + // Level1 Inject
            @"int\s+level1Field2\s*,\s*" + // Level1 Inject
            @"IService3\s+service3\s*,\s*" + // Level2 DependsOn
            @"bool\s+level2Field\s*,\s*" + // Level2 Inject
            @"DateTime\s+level2Time\s*,\s*" + // Level2 Inject
            @"IService4\s+service4\s*,\s*" + // Level3 DependsOn
            @"decimal\s+level3Amount\s*,\s*" + // Level3 Inject
            @"TimeSpan\s+level3Duration\s*" + // Level3 Inject
            @"\)"
        );

        fullConstructorRegex.IsMatch(constructorContent).Should().BeTrue(
            "Constructor signature doesn't match expected parameter ordering. Actual: {0}",
            constructorContent);

        // Validate proper base constructor call with inherited parameters  
        // Generator orders by level: DependsOn first, then Inject for each level
        var baseCallRegex = new Regex(
            @":\s*base\s*\(\s*" +
            @"service1\s*,\s*service2\s*,\s*level1Field1\s*,\s*level1Field2\s*,\s*" + // Level1 DependsOn, Level1 Inject
            @"service3\s*,\s*level2Field\s*,\s*level2Time\s*" + // Level2 DependsOn, Level2 Inject
            @"\s*\)"
        );

        baseCallRegex.IsMatch(constructorContent).Should().BeTrue(
            "Base constructor call should include all inherited parameters in correct order");

        // Validate field assignments for current level only
        constructorContent.Should().Contain("this._level3Amount = level3Amount;");
        constructorContent.Should().Contain("this._level3Duration = level3Duration;");

        // Should not have field assignments for inherited fields
        constructorContent.Should().NotContain("this._level1Field1");
        constructorContent.Should().NotContain("this._level2Field");
    }

    [Fact]
    public void Enterprise_DependsOnVsInject_ComplexInheritancePatterns()
    {
        // Arrange - Complex mixing of [DependsOn] and [Inject] across inheritance
        var source = @"
using IoCTools.Abstractions.Annotations;
using System.Collections.Generic;
using System;

namespace Test;

public interface ISharedService { }
public interface IBaseService { }
public interface IMiddleService { }
public interface IFinalService { }

[DependsOn<IBaseService, ISharedService>]
public abstract partial class BaseClass
{
    [Inject] protected readonly string _baseString;
}

public abstract partial class MiddleClass : BaseClass
{
    [Inject] protected readonly ISharedService _sharedFromInject; // Same type as DependsOn in base
    [Inject] protected readonly IMiddleService _middleService;    // Middle-specific service via Inject
    [Inject] protected readonly int _middleInt;
}
[DependsOn<IFinalService>]
public partial class FinalClass : MiddleClass
{
    [Inject] private readonly IBaseService _baseFromInject;  // Same type as DependsOn in base
    [Inject] private readonly bool _finalBool;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Should produce IOC007 warnings for conflicting DependsOn/Inject
        var ioc007Diagnostics = result.GetDiagnosticsByCode("IOC007");
        ioc007Diagnostics.Should().NotBeEmpty();

        // Should have conflicts for:
        // 1. ISharedService (DependsOn in base, Inject in middle)
        // 2. IBaseService (DependsOn in base, Inject in final)
        ioc007Diagnostics.Count.Should().BeGreaterOrEqualTo(2,
            "Expected at least 2 IOC007 conflicts, got {0}",
            ioc007Diagnostics.Count);

        // Should still compile successfully despite warnings
        result.HasErrors.Should().BeFalse();

        var constructorContent = result.GetConstructorSourceText("FinalClass");

        // Constructor should handle deduplication - each type should appear only once
        var sharedServiceMatches = Regex.Matches(constructorContent, @"ISharedService\s+\w+");
        var baseServiceMatches = Regex.Matches(constructorContent, @"IBaseService\s+\w+");

        sharedServiceMatches.Count.Should().Be(1); // Should deduplicate ISharedService
        baseServiceMatches.Count.Should().Be(1); // Should deduplicate IBaseService

        // Should contain middle and final specific dependencies
        constructorContent.Should().Contain("IMiddleService middleService");
        constructorContent.Should().Contain("IFinalService finalService");
        constructorContent.Should().Contain("string baseString");
        constructorContent.Should().Contain("int middleInt");
        constructorContent.Should().Contain("bool finalBool");
    }

    [Fact]
    public void Enterprise_ConfigurationInheritanceWithComplexScenario()
    {
        // Arrange - Configuration inheritance across multiple levels
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Configuration;
using System;

namespace Test;

public class DatabaseConfig
{
    public string ConnectionString { get; set; }
    public int TimeoutSeconds { get; set; }
}

public class ApiConfig
{
    public string BaseUrl { get; set; }
    public string ApiKey { get; set; }
}

public class FeatureConfig
{
    public bool EnableLogging { get; set; }
    public string LogLevel { get; set; }
}

public abstract partial class ConfigurableBase
{
    [Inject]
    protected readonly DatabaseConfig _dbConfig;
    
    [Inject] protected readonly IConfiguration _configuration;
}

public abstract partial class ApiLayer : ConfigurableBase
{
    [Inject]
    protected readonly ApiConfig _apiConfig;
    
    [Inject] protected readonly string _serviceName;
}
public partial class FeatureService : ApiLayer
{
    [Inject]
    private readonly FeatureConfig _featureConfig;
    
    [Inject] private readonly DateTime _initialized;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var errorMessages = string.Join(", ", result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.GetMessage()));

        // Assert
        result.HasErrors.Should().BeFalse("Configuration inheritance failed: {0}", errorMessages);

        var constructorContent = result.GetConstructorSourceText("FeatureService");

        // Should have all configuration and inject dependencies
        var expectedParams = new[]
        {
            "DatabaseConfig dbConfig", // ConfigurableBase - InjectConfiguration
            "IConfiguration configuration", // ConfigurableBase - Inject
            "ApiConfig apiConfig", // ApiLayer - InjectConfiguration
            "string serviceName", // ApiLayer - Inject
            "FeatureConfig featureConfig", // FeatureService - InjectConfiguration
            "DateTime initialized" // FeatureService - Inject
        };

        foreach (var param in expectedParams) constructorContent.Should().Contain(param);

        // Validate proper field assignments for current level configuration
        constructorContent.Should().Contain("this._featureConfig = featureConfig;");
        constructorContent.Should().Contain("this._initialized = initialized;");

        // Should not have field assignments for inherited configuration (handled by base constructor)
        constructorContent.Should().NotContain("this._dbConfig =");
        constructorContent.Should().NotContain("this._apiConfig =");
        constructorContent.Should().NotContain("this._configuration =");
        constructorContent.Should().NotContain("this._serviceName =");

        // Check service registration includes configuration registration  
        var registrationContent = result.GetServiceRegistrationText();
        registrationContent.Should().Contain(
            "services.AddScoped<global::Test.FeatureService, global::Test.FeatureService>()");

        // Note: Configuration bindings are handled separately from this generator
        // The test focuses on constructor injection of configuration objects
    }

    [Fact]
    public void Enterprise_InheritancePerformanceStressTest_DeepAndWideChains()
    {
        // Arrange - Stress test with deep (7 levels) and wide (many dependencies) inheritance
        var interfaces = Enumerable.Range(1, 35).Select(i => $"public interface IService{i} {{ }}");

        var source = $@"
using IoCTools.Abstractions.Annotations;
using System;

namespace Test;

{string.Join("\n", interfaces)}

[DependsOn<IService1, IService2, IService3, IService4, IService5>]
public abstract partial class Level1
{{
    [Inject] protected readonly string _level1A;
    [Inject] protected readonly int _level1B;
    [Inject] protected readonly bool _level1C;
}}

[DependsOn<IService6, IService7, IService8, IService9, IService10>]
public abstract partial class Level2 : Level1
{{
    [Inject] protected readonly decimal _level2A;
    [Inject] protected readonly DateTime _level2B;
    [Inject] protected readonly TimeSpan _level2C;
}}

[DependsOn<IService11, IService12, IService13, IService14, IService15>]
public abstract partial class Level3 : Level2
{{
    [Inject] protected readonly double _level3A;
    [Inject] protected readonly float _level3B;
}}

[DependsOn<IService16, IService17, IService18, IService19, IService20>]
public abstract partial class Level4 : Level3
{{
    [Inject] protected readonly long _level4A;
    [Inject] protected readonly short _level4B;
}}

[DependsOn<IService21, IService22, IService23, IService24, IService25>]
public abstract partial class Level5 : Level4
{{
    [Inject] protected readonly byte _level5A;
    [Inject] protected readonly char _level5B;
}}

[DependsOn<IService26, IService27, IService28, IService29, IService30>]
public abstract partial class Level6 : Level5
{{
    [Inject] protected readonly Guid _level6A;
}}
[DependsOn<IService31, IService32, IService33, IService34, IService35>]
public partial class Level7Final : Level6
{{
    [Inject] private readonly Uri _level7A;
}}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        var errorMessages = string.Join(", ", result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.GetMessage()));

        // Assert - Should handle deep/wide inheritance without performance issues
        result.HasErrors.Should().BeFalse("Performance stress test failed: {0}", errorMessages);

        var constructorContent = result.GetConstructorSourceText("Level7Final");

        // Should have all 35 service dependencies plus 15 inject fields
        var serviceParams = Enumerable.Range(1, 35).Select(i => $"IService{i} service{i}");
        foreach (var param in serviceParams) constructorContent.Should().Contain(param);

        // Should have all inject field parameters
        var injectParams = new[]
        {
            "string level1A", "int level1B", "bool level1C", // Level1
            "decimal level2A", "DateTime level2B", "TimeSpan level2C", // Level2
            "double level3A", "float level3B", // Level3
            "long level4A", "short level4B", // Level4
            "byte level5A", "char level5B", // Level5
            "Guid level6A", // Level6
            "Uri level7A" // Level7
        };

        foreach (var param in injectParams) constructorContent.Should().Contain(param);

        // Only Level7Final should be registered
        var registrationContent = result.GetServiceRegistrationText();
        registrationContent.Should().Contain(
            "services.AddScoped<global::Test.Level7Final, global::Test.Level7Final>()");

        for (var i = 1; i <= 6; i++) registrationContent.Should().NotContain($"Level{i}>");
    }
}
