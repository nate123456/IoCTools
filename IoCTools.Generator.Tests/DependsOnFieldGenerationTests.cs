namespace IoCTools.Generator.Tests;

/// <summary>
///     Comprehensive tests for DependsOn field generation functionality
///     Tests the missing feature where [DependsOn] attributes should generate private readonly fields
/// </summary>
public class DependsOnFieldGenerationTests
{
    [Fact]
    public void DependsOn_SingleDependency_GeneratesField()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace Test;

public interface ITestService { }
[DependsOn<ITestService>]
public partial class TestClass
{
    public void UseService()
    {
        // Should be able to use generated _testService field
        _testService.ToString();
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - should compile without errors
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        // Check that field was generated
        var constructorSource = result.GetConstructorSource("TestClass");
        Assert.NotNull(constructorSource);
        Assert.Contains("private readonly ITestService _testService;", constructorSource.Content);

        // Check constructor parameter
        Assert.Contains("public TestClass(ITestService testService)", constructorSource.Content);

        // Check field assignment
        Assert.Contains("_testService = testService;", constructorSource.Content);
    }

    [Fact]
    public void DependsOn_MultipleDependencies_GeneratesAllFields()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService1 { }
public interface IService2 { }
public interface IService3 { }
[DependsOn<IService1, IService2, IService3>]
public partial class TestClass
{
    public void UseServices()
    {
        _service1.ToString();
        _service2.ToString();
        _service3.ToString();
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var constructorSource = result.GetConstructorSource("TestClass");
        Assert.NotNull(constructorSource);

        // Check all fields generated
        Assert.Contains("private readonly IService1 _service1;", constructorSource.Content);
        Assert.Contains("private readonly IService2 _service2;", constructorSource.Content);
        Assert.Contains("private readonly IService3 _service3;", constructorSource.Content);

        // Check constructor parameters
        Assert.Contains("IService1 service1", constructorSource.Content);
        Assert.Contains("IService2 service2", constructorSource.Content);
        Assert.Contains("IService3 service3", constructorSource.Content);

        // Check field assignments
        Assert.Contains("_service1 = service1;", constructorSource.Content);
        Assert.Contains("_service2 = service2;", constructorSource.Content);
        Assert.Contains("_service3 = service3;", constructorSource.Content);
    }

    [Fact]
    public void DependsOn_CamelCaseNaming_GeneratesCorrectFieldNames()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IUserManagementService { }
public interface IAuditService { }
[DependsOn<IUserManagementService, IAuditService>(NamingConvention.CamelCase)]
public partial class TestClass
{
    public void UseServices()
    {
        _userManagementService.ToString();
        _auditService.ToString();
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var constructorSource = result.GetConstructorSource("TestClass");
        Assert.NotNull(constructorSource);

        // Check camelCase field names (default: stripI=true, prefix="_")
        Assert.Contains("private readonly IUserManagementService _userManagementService;", constructorSource.Content);
        Assert.Contains("private readonly IAuditService _auditService;", constructorSource.Content);
    }

    [Fact]
    public void DependsOn_PascalCaseNaming_GeneratesCorrectFieldNames()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IUserManagementService { }
public interface IAuditService { }
[DependsOn<IUserManagementService, IAuditService>(NamingConvention.PascalCase)]
public partial class TestClass
{
    public void UseServices()
    {
        _UserManagementService.ToString();
        _AuditService.ToString();
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var constructorSource = result.GetConstructorSource("TestClass");
        Assert.NotNull(constructorSource);

        // Check PascalCase field names
        Assert.Contains("private readonly IUserManagementService _UserManagementService;", constructorSource.Content);
        Assert.Contains("private readonly IAuditService _AuditService;", constructorSource.Content);
    }

    [Fact]
    public void DependsOn_SnakeCaseNaming_GeneratesCorrectFieldNames()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IUserManagementService { }
public interface IAuditService { }
[DependsOn<IUserManagementService, IAuditService>(NamingConvention.SnakeCase)]
public partial class TestClass
{
    public void UseServices()
    {
        _user_management_service.ToString();
        _audit_service.ToString();
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var constructorSource = result.GetConstructorSource("TestClass");
        Assert.NotNull(constructorSource);

        // Check snake_case field names
        Assert.Contains("private readonly IUserManagementService _user_management_service;", constructorSource.Content);
        Assert.Contains("private readonly IAuditService _audit_service;", constructorSource.Content);
    }

    [Fact]
    public void DependsOn_CustomPrefixAndStripI_GeneratesCorrectFieldNames()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IPaymentService { }
public interface IEmailService { }
[DependsOn<IPaymentService, IEmailService>(prefix: ""svc_"", stripI: true)]
public partial class TestClass
{
    public void UseServices()
    {
        svc_paymentService.ToString();
        svc_emailService.ToString();
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var constructorSource = result.GetConstructorSource("TestClass");
        Assert.NotNull(constructorSource);

        // Check custom prefix with I stripped
        Assert.Contains("private readonly IPaymentService svc_paymentService;", constructorSource.Content);
        Assert.Contains("private readonly IEmailService svc_emailService;", constructorSource.Content);
    }

    [Fact]
    public void DependsOn_NoStripI_GeneratesSemanticFieldNames()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IPaymentService { }
public interface IEmailService { }
[DependsOn<IPaymentService, IEmailService>(prefix: """", stripI: false)]
public partial class TestClass
{
    public void UseServices()
    {
        paymentService.ToString();
        emailService.ToString();
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var constructorSource = result.GetConstructorSource("TestClass");
        Assert.NotNull(constructorSource);

        // Check semantic naming (camelCase even with stripI=false)
        Assert.Contains("private readonly IPaymentService paymentService;", constructorSource.Content);
        Assert.Contains("private readonly IEmailService emailService;", constructorSource.Content);
    }

    [Fact]
    public void DependsOn_MultipleDifferentConfigurations_GeneratesCorrectFields()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IPaymentService { }
public interface IEmailService { }
[DependsOn<IPaymentService>(prefix: ""payment_"", stripI: true, namingConvention: NamingConvention.SnakeCase)]
[DependsOn<IEmailService>(prefix: ""notification_"", stripI: true, namingConvention: NamingConvention.CamelCase)]
public partial class TestClass
{
    public void UseServices()
    {
        payment_payment_service.ToString();
        notification_emailService.ToString();
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var constructorSource = result.GetConstructorSource("TestClass");
        Assert.NotNull(constructorSource);

        // Check mixed configurations
        Assert.Contains("private readonly IPaymentService payment_payment_service;", constructorSource.Content);
        Assert.Contains("private readonly IEmailService notification_emailService;", constructorSource.Content);
    }

    [Fact]
    public void DependsOn_WithManualInjectFields_CombinesCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace Test;

public interface IPaymentService { }
public interface IInventoryService { }
[DependsOn<IPaymentService, IInventoryService>]
public partial class TestClass
{
    [Inject] private readonly ILogger<TestClass> _logger;
    [Inject] private readonly IConfiguration _configuration;
    
    public void UseServices()
    {
        _logger.LogInformation(""Test"");
        var setting = _configuration[""test""];
        _paymentService.ToString();
        _inventoryService.ToString();
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var constructorSource = result.GetConstructorSource("TestClass");
        Assert.NotNull(constructorSource);

        // Check ONLY generated fields from DependsOn (manual [Inject] fields are in source, not generated)
        Assert.Contains("private readonly IPaymentService _paymentService;", constructorSource.Content);
        Assert.Contains("private readonly IInventoryService _inventoryService;", constructorSource.Content);

        // Check constructor has all parameters
        Assert.Contains("ILogger<TestClass> logger", constructorSource.Content);
        Assert.Contains("IConfiguration configuration", constructorSource.Content);
        Assert.Contains("IPaymentService paymentService", constructorSource.Content);
        Assert.Contains("IInventoryService inventoryService", constructorSource.Content);

        // Check all field assignments (with proper this. prefix)
        Assert.Contains("this._logger = logger;", constructorSource.Content);
        Assert.Contains("this._configuration = configuration;", constructorSource.Content);
        Assert.Contains("this._paymentService = paymentService;", constructorSource.Content);
        Assert.Contains("this._inventoryService = inventoryService;", constructorSource.Content);
    }

    [Fact]
    public void DependsOn_GenericTypes_GeneratesCorrectFields()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace Test;

public interface IGenericRepository<T> { }
public class User { }
[DependsOn<ILogger<TestClass>>]
public partial class TestClass
{
    [Inject] private readonly IGenericRepository<User> _userRepository;
    
    public void UseServices()
    {
        _logger.LogInformation(""Test"");
        _userRepository.ToString();
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var constructorSource = result.GetConstructorSource("TestClass");
        Assert.NotNull(constructorSource);

        // Check ONLY generated field from DependsOn (manual [Inject] field is in source, not generated)
        Assert.Contains("private readonly ILogger<TestClass> _logger;", constructorSource.Content);
    }

    [Fact]
    public void DependsOn_InheritanceScenario_GeneratesFieldsCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using Microsoft.Extensions.Logging;

namespace Test;

public interface IAuditService { }
public interface ISecurityService { }
public interface IUserManagementService { }
[DependsOn<IAuditService, ISecurityService>]
public abstract partial class BaseSecureService
{
    [Inject] protected readonly ILogger<BaseSecureService> _logger;
    
    protected void UseBaseServices()
    {
        _auditService.ToString();
        _securityService.ToString();
    }
}
[DependsOn<IUserManagementService>]
public partial class EnhancedSecureService : BaseSecureService
{
    public void UseAllServices()
    {
        _logger.LogInformation(""Test"");
        _auditService.ToString();
        _securityService.ToString();
        _userManagementService.ToString();
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        // Check base class fields - should be protected for abstract class inheritance
        var baseConstructorSource = result.GetConstructorSource("BaseSecureService");
        Assert.NotNull(baseConstructorSource);
        Assert.Contains("protected readonly IAuditService _auditService;", baseConstructorSource.Content);
        Assert.Contains("protected readonly ISecurityService _securityService;", baseConstructorSource.Content);

        // Check derived class fields
        var derivedConstructorSource = result.GetConstructorSource("EnhancedSecureService");
        Assert.NotNull(derivedConstructorSource);
        Assert.Contains("private readonly IUserManagementService _userManagementService;",
            derivedConstructorSource.Content);

        // Check proper base constructor call
        Assert.Contains("base(", derivedConstructorSource.Content);
    }

    [Fact]
    public void DependsOn_MultipleAttributesOnSameClass_GeneratesAllFields()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService1 { }
public interface IService2 { }
public interface IService3 { }
public interface IService4 { }
[DependsOn<IService1, IService2>]
[DependsOn<IService3, IService4>]
public partial class TestClass
{
    public void UseServices()
    {
        _service1.ToString();
        _service2.ToString();
        _service3.ToString();
        _service4.ToString();
    }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var constructorSource = result.GetConstructorSource("TestClass");
        Assert.NotNull(constructorSource);

        // Check all fields from multiple DependsOn attributes
        Assert.Contains("private readonly IService1 _service1;", constructorSource.Content);
        Assert.Contains("private readonly IService2 _service2;", constructorSource.Content);
        Assert.Contains("private readonly IService3 _service3;", constructorSource.Content);
        Assert.Contains("private readonly IService4 _service4;", constructorSource.Content);
    }
}
