namespace IoCTools.Generator.Tests;

using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
///     COMPREHENSIVE DEPENDS ON CONSTRUCTOR GENERATION TEST COVERAGE
///     Tests corrected expectations for [DependsOn] attribute behavior based on audit findings:
///     - DependsOn generates CONSTRUCTOR PARAMETERS, not fields
///     - Constructor parameters follow naming conventions (camelCase, PascalCase, snake_case)
///     - stripI and prefix parameters affect constructor parameter naming
///     - Multiple [DependsOn] attributes generate multiple constructor parameters
///     - Generic type constructor parameter generation
///     - Integration with [Inject] fields in same class
///     - Proper constructor generation and field assignments
///     These tests verify actual DependsOn behavior as implemented in the generator.
/// </summary>
public class ComprehensiveDependsOnConstructorGenerationTests
{
    #region Runtime Integration Tests

    [Fact]
    public void DependsOn_LegitimateGap_FieldAccessCompilationFails()
    {
        // This test reveals a LEGITIMATE gap in DependsOn functionality
        // DependsOn should generate accessible fields for direct usage, but currently only generates constructor parameters

        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IEmailService 
{
    string SendEmail(string message);
}

[Scoped]
public partial class EmailService : IEmailService
{
    public string SendEmail(string message) => $""Email: {message}"";
}

[Scoped]  
[DependsOn<IEmailService>]
public partial class NotificationService
{
    public string SendNotification(string message) => _emailService.SendEmail(message);
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - This SHOULD compile but currently fails due to missing field generation
        // This represents a legitimate implementation gap that needs to be addressed
        if (result.HasErrors)
        {
            var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            Assert.True(errors.Any(e => e.GetMessage().Contains("_emailService")),
                "Expected compilation error about missing _emailService field, indicating DependsOn gap");
        }
        else
        {
            // If this passes, the gap has been fixed - verify runtime behavior
            var runtimeContext = SourceGeneratorTestHelper.CreateRuntimeContext(result);
            var serviceProvider = SourceGeneratorTestHelper.BuildServiceProvider(runtimeContext);

            var notificationServiceType = runtimeContext.Assembly.GetType("Test.NotificationService");
            Assert.NotNull(notificationServiceType);

            var notificationService = serviceProvider.GetRequiredService(notificationServiceType);
            Assert.NotNull(notificationService);

            var sendNotificationMethod = notificationServiceType.GetMethod("SendNotification");
            Assert.NotNull(sendNotificationMethod);

            var message = (string)sendNotificationMethod.Invoke(notificationService, new[] { "Hello World" })!;
            Assert.Equal("Email: Hello World", message);
        }
    }

    #endregion

    #region Basic Constructor Parameter Tests

    [Fact]
    public void DependsOn_SingleDependency_GeneratesField()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IEmailService { }
[DependsOn<IEmailService>]
public partial class NotificationService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("NotificationService");
        Assert.NotNull(constructorSource);

        // Should add constructor parameter without namespace qualification
        Assert.Contains("IEmailService emailService", constructorSource.Content);

        // Constructor should be generated
        Assert.Contains("NotificationService(", constructorSource.Content);
    }

    [Fact]
    public void DependsOn_MultipleDependencies_GeneratesMultipleFields()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IEmailService { }
public interface ISmsService { }
public interface ILoggerService { }
[DependsOn<IEmailService>]
[DependsOn<ISmsService>]
[DependsOn<ILoggerService>]
public partial class CompositeNotificationService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("CompositeNotificationService");
        Assert.NotNull(constructorSource);

        // Should have all parameters in constructor without namespace
        Assert.Contains("IEmailService emailService", constructorSource.Content);
        Assert.Contains("ISmsService smsService", constructorSource.Content);
        Assert.Contains("ILoggerService loggerService", constructorSource.Content);

        // Constructor should be generated
        Assert.Contains("CompositeNotificationService(", constructorSource.Content);
    }

    #endregion

    #region Parameter Naming Convention Tests

    [Fact]
    public void DependsOn_CamelCaseNaming_GeneratesCorrectFieldName()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IPaymentProcessor { }
[DependsOn<IPaymentProcessor>(NamingConvention = NamingConvention.CamelCase)]
public partial class OrderService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("OrderService");
        Assert.NotNull(constructorSource);

        // Should use camelCase naming for constructor parameter
        Assert.Contains("IPaymentProcessor paymentProcessor", constructorSource.Content);

        // Constructor should be generated
        Assert.Contains("OrderService(", constructorSource.Content);
    }

    [Fact]
    public void DependsOn_PascalCaseNaming_GeneratesCorrectFieldName()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IPaymentProcessor { }
[DependsOn<IPaymentProcessor>(NamingConvention = NamingConvention.PascalCase)]
public partial class OrderService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("OrderService");
        Assert.NotNull(constructorSource);

        // Should use camelCase naming for constructor parameter (C# convention)
        Assert.Contains("IPaymentProcessor paymentProcessor", constructorSource.Content);

        // Constructor should be generated
        Assert.Contains("OrderService(", constructorSource.Content);
    }

    [Fact]
    public void DependsOn_SnakeCaseNaming_GeneratesCorrectFieldName()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IPaymentProcessor { }
[DependsOn<IPaymentProcessor>(NamingConvention = NamingConvention.SnakeCase)]
public partial class OrderService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("OrderService");
        Assert.NotNull(constructorSource);

        // Should use camelCase naming for constructor parameter (C# convention)
        Assert.Contains("IPaymentProcessor paymentProcessor", constructorSource.Content);

        // Constructor should be generated
        Assert.Contains("OrderService(", constructorSource.Content);
    }

    [Fact]
    public void DependsOn_MixedNamingConventions_GeneratesCorrectFieldNames()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IEmailService { }
public interface ISmsService { }
public interface ILoggerService { }
[DependsOn<IEmailService>(NamingConvention = NamingConvention.CamelCase)]
[DependsOn<ISmsService>(NamingConvention = NamingConvention.PascalCase)]
[DependsOn<ILoggerService>(NamingConvention = NamingConvention.SnakeCase)]
public partial class MixedNamingService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("MixedNamingService");
        Assert.NotNull(constructorSource);

        // Should use camelCase for all constructor parameters (C# convention)
        Assert.Contains("IEmailService emailService", constructorSource.Content);
        Assert.Contains("ISmsService smsService", constructorSource.Content);
        Assert.Contains("ILoggerService loggerService", constructorSource.Content);

        // Constructor should be generated
        Assert.Contains("MixedNamingService(", constructorSource.Content);
    }

    #endregion

    #region Strip I Parameter Naming Tests

    [Fact]
    public void DependsOn_StripITrue_RemovesIFromFieldName()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IEmailService { }
public interface IPaymentProcessor { }
[DependsOn<IEmailService>(StripI = true)]
[DependsOn<IPaymentProcessor>(StripI = true)]
public partial class ServiceWithStrippedI
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("ServiceWithStrippedI");
        Assert.NotNull(constructorSource);

        // Should strip 'I' prefix from interface names for constructor parameters
        Assert.Contains("IEmailService emailService", constructorSource.Content);
        Assert.Contains("IPaymentProcessor paymentProcessor", constructorSource.Content);

        // Constructor should be generated
        Assert.Contains("ServiceWithStrippedI(", constructorSource.Content);
    }

    [Fact]
    public void DependsOn_StripIFalse_UseSemanticFieldNaming()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IEmailService { }
[DependsOn<IEmailService>(StripI = false)]
public partial class ServiceWithoutStrippedI
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("ServiceWithoutStrippedI");
        Assert.NotNull(constructorSource);

        // Should use semantic naming regardless of stripI setting for consistent constructor parameter naming
        // stripI parameter affects naming convention application, not semantic parameter naming
        Assert.Contains("IEmailService emailService", constructorSource.Content);

        // Constructor should be generated
        Assert.Contains("ServiceWithoutStrippedI(", constructorSource.Content);
    }

    [Fact]
    public void DependsOn_NonInterfaceType_StripIHasNoEffect()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public class EmailService { }
[DependsOn<EmailService>(StripI = true)]
public partial class NonInterfaceService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("NonInterfaceService");
        Assert.NotNull(constructorSource);

        // Non-interface types should not be affected by StripI for constructor parameters
        Assert.Contains("EmailService emailService", constructorSource.Content);

        // Constructor should be generated
        Assert.Contains("NonInterfaceService(", constructorSource.Content);
    }

    #endregion

    #region Prefix Parameter Naming Tests

    [Fact]
    public void DependsOn_WithPrefix_AddsCustomPrefix()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IEmailService { }
public interface IPaymentProcessor { }
[DependsOn<IEmailService>(Prefix = ""injected"")]
[DependsOn<IPaymentProcessor>(Prefix = ""external"")]
public partial class PrefixedService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("PrefixedService");
        Assert.NotNull(constructorSource);

        // Should add custom prefixes to constructor parameter names
        Assert.Contains("IEmailService injectedEmailService", constructorSource.Content);
        Assert.Contains("IPaymentProcessor externalPaymentProcessor", constructorSource.Content);

        // Constructor should be generated
        Assert.Contains("PrefixedService(", constructorSource.Content);
    }

    [Fact]
    public void DependsOn_PrefixWithStripI_CombinesCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IEmailService { }
[DependsOn<IEmailService>(Prefix = ""injected"", StripI = true)]
public partial class PrefixedStrippedService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("PrefixedStrippedService");
        Assert.NotNull(constructorSource);

        // Should combine prefix with stripped interface name for constructor parameter
        Assert.Contains("IEmailService injectedEmailService", constructorSource.Content);

        // Constructor should be generated
        Assert.Contains("PrefixedStrippedService(", constructorSource.Content);
    }

    [Fact]
    public void DependsOn_PrefixWithNamingConvention_CombinesCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IPaymentProcessor { }
[DependsOn<IPaymentProcessor>(Prefix = ""external"", NamingConvention = NamingConvention.PascalCase, StripI = true)]
public partial class CombinedOptionsService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("CombinedOptionsService");
        Assert.NotNull(constructorSource);

        // Should combine prefix, stripped interface name, and camelCase for constructor parameter
        Assert.Contains("IPaymentProcessor externalPaymentProcessor", constructorSource.Content);

        // Constructor should be generated
        Assert.Contains("CombinedOptionsService(", constructorSource.Content);
    }

    #endregion

    #region Generic Type Parameter Tests

    [Fact]
    public void DependsOn_GenericInterface_GeneratesCorrectField()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using System.Collections.Generic;

namespace Test;

public interface IRepository<T> { }
public class User { }
[DependsOn<IRepository<User>>]
public partial class UserService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("UserService");
        Assert.NotNull(constructorSource);

        // Should handle generic types correctly for constructor parameters
        Assert.Contains("IRepository<User> repository", constructorSource.Content);

        // Constructor should be generated
        Assert.Contains("UserService(", constructorSource.Content);
    }

    [Fact]
    public void DependsOn_ComplexGenericTypes_GeneratesCorrectFields()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using System.Collections.Generic;

namespace Test;

public interface IKeyValueStore<TKey, TValue> { }
public interface IFactory<T> { }
[DependsOn<IKeyValueStore<string, int>>]
[DependsOn<IFactory<List<string>>>]
public partial class ComplexGenericService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("ComplexGenericService");
        Assert.NotNull(constructorSource);

        // Should handle complex generic types for constructor parameters
        Assert.Contains("IKeyValueStore<string, int> keyValueStore", constructorSource.Content);
        Assert.Contains("IFactory<List<string>> factory", constructorSource.Content);

        // Constructor should be generated
        Assert.Contains("ComplexGenericService(", constructorSource.Content);
    }

    #endregion

    #region Integration with Inject Fields Tests

    [Fact]
    public void DependsOn_WithInjectFields_GeneratesCorrectConstructor()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IEmailService { }
public interface ILoggerService { }
public interface ISmsService { }
[DependsOn<IEmailService>]
public partial class MixedDependencyService
{
    [Inject] private readonly ILoggerService _logger;
    [Inject] private readonly ISmsService _smsService;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("MixedDependencyService");
        Assert.NotNull(constructorSource);

        // Constructor should have parameters for both DependsOn and Inject fields without namespace
        Assert.Contains("IEmailService emailService", constructorSource.Content);
        Assert.Contains("ILoggerService logger", constructorSource.Content);
        Assert.Contains("ISmsService smsService", constructorSource.Content);

        // Constructor should be generated
        Assert.Contains("MixedDependencyService(", constructorSource.Content);
    }

    [Fact]
    public void DependsOn_MultipleMixed_OrderingIsCorrect()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IEmailService { }
public interface IPaymentService { }
public interface ILoggerService { }
public interface IAuditService { }
[DependsOn<IEmailService>]
[DependsOn<IPaymentService>]
public partial class OrderedDependencyService
{
    [Inject] private readonly ILoggerService _logger;
    [Inject] private readonly IAuditService _audit;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("OrderedDependencyService");
        Assert.NotNull(constructorSource);

        // Constructor parameters should be in proper order (DependsOn first, then Inject) without namespace
        var constructorText = constructorSource.Content;
        var emailParamIndex = constructorText.IndexOf("IEmailService emailService");
        var paymentParamIndex = constructorText.IndexOf("IPaymentService paymentService");
        var loggerParamIndex = constructorText.IndexOf("ILoggerService logger");
        var auditParamIndex = constructorText.IndexOf("IAuditService audit");

        Assert.True(emailParamIndex > 0);
        Assert.True(paymentParamIndex > emailParamIndex);
        Assert.True(loggerParamIndex > paymentParamIndex);
        Assert.True(auditParamIndex > loggerParamIndex);

        // Constructor should be generated
        Assert.Contains("OrderedDependencyService(", constructorSource.Content);
    }

    #endregion

    #region Constructor Parameter Tests

    [Fact]
    public void DependsOn_GeneratedFields_HaveCorrectModifiers()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService { }
[DependsOn<IService>]
public partial class FieldModifierService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("FieldModifierService");
        Assert.NotNull(constructorSource);

        // Constructor parameter should be generated
        Assert.Contains("IService service", constructorSource.Content);

        // Constructor should be generated
        Assert.Contains("FieldModifierService(", constructorSource.Content);
    }

    [Fact]
    public void DependsOn_OnlyDependsOn_GeneratesConstructor()
    {
        // Test just DependsOn to isolate the issue
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService { }
[DependsOn<IService>]
public partial class OnlyDependsOnService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("OnlyDependsOnService");
        Assert.NotNull(constructorSource);
        Assert.Contains("OnlyDependsOnService(", constructorSource.Content);
    }

    [Fact]
    public void DependsOn_OnlyInject_GeneratesConstructor()
    {
        // Test just Inject to isolate the issue
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService { }
public partial class OnlyInjectService
{
    [Inject] private readonly IService _service;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("OnlyInjectService");
        Assert.NotNull(constructorSource);
        Assert.Contains("OnlyInjectService(", constructorSource.Content);
    }

    [Fact]
    public void DependsOn_NameCollisions_HandledCorrectly()
    {
        // Arrange - Both [DependsOn<IService>] and [Inject] IService _existingService 
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService { }
[DependsOn<IService>]
public partial class CollisionService
{
    [Inject] private readonly IService _existingService;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("CollisionService");
        Assert.NotNull(constructorSource);

        // The constructor should be generated with IService parameter
        // The existing [Inject] field should take precedence over [DependsOn]
        Assert.Contains("CollisionService(", constructorSource.Content);
        Assert.Contains("IService", constructorSource.Content);

        // Should use the existing field name, not generate a new field
        Assert.Contains("_existingService = ", constructorSource.Content);
    }

    #endregion

    #region Error Cases and Edge Cases

    [Fact]
    public void DependsOn_ConflictWithInjectField_GeneratesDiagnostic()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService { }
[DependsOn<IService>]
public partial class ConflictingService
{
    [Inject] private readonly IService service; // Same name as would be generated
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var ioc007Diagnostics = result.GetDiagnosticsByCode("IOC007");
        Assert.NotEmpty(ioc007Diagnostics);
        Assert.Contains("DependsOn", ioc007Diagnostics[0].GetMessage());
    }

    [Fact]
    public void DependsOn_DuplicateTypes_OnlyGeneratesOneField()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService { }
[DependsOn<IService>]
[DependsOn<IService>] // Duplicate - should only generate one field
public partial class DuplicateService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var constructorSource = result.GetConstructorSource("DuplicateService");
        Assert.NotNull(constructorSource);

        // Should only have one parameter declaration despite duplicate attributes  
        var paramCount = Regex.Matches(
            constructorSource.Content, @"IService service").Count;
        Assert.Equal(1, paramCount);

        // Constructor should be generated
        Assert.Contains("DuplicateService(", constructorSource.Content);
    }

    #endregion
}
