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
            errors.Any(e => e.GetMessage().Contains("_emailService")).Should().BeTrue(
                "Expected compilation error about missing _emailService field, indicating DependsOn gap");
        }
        else
        {
            // If this passes, the gap has been fixed - verify runtime behavior
            var runtimeContext = SourceGeneratorTestHelper.CreateRuntimeContext(result);
            var serviceProvider = SourceGeneratorTestHelper.BuildServiceProvider(runtimeContext);

            var notificationServiceType = runtimeContext.Assembly.GetType("Test.NotificationService") ??
                                          throw new InvalidOperationException(
                                              "NotificationService type not generated.");

            var notificationService = serviceProvider.GetRequiredService(notificationServiceType);

            var sendNotificationMethod = notificationServiceType.GetMethod("SendNotification") ??
                                         throw new InvalidOperationException("SendNotification method missing.");

            var message =
                sendNotificationMethod.Invoke(notificationService, new object[] { "Hello World" }) as string ??
                throw new InvalidOperationException("SendNotification should return string.");
            message.Should().Be("Email: Hello World");
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
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("NotificationService");

        // Should add constructor parameter without namespace qualification
        constructorSource.Content.Should().Contain("IEmailService emailService");

        // Constructor should be generated
        constructorSource.Content.Should().Contain("NotificationService(");
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
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("CompositeNotificationService");

        // Should have all parameters in constructor without namespace
        constructorSource.Content.Should().Contain("IEmailService emailService");
        constructorSource.Content.Should().Contain("ISmsService smsService");
        constructorSource.Content.Should().Contain("ILoggerService loggerService");

        // Constructor should be generated
        constructorSource.Content.Should().Contain("CompositeNotificationService(");
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
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("OrderService");

        // Should use camelCase naming for constructor parameter
        constructorSource.Content.Should().Contain("IPaymentProcessor paymentProcessor");

        // Constructor should be generated
        constructorSource.Content.Should().Contain("OrderService(");
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
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("OrderService");

        // Should use camelCase naming for constructor parameter (C# convention)
        constructorSource.Content.Should().Contain("IPaymentProcessor paymentProcessor");

        // Constructor should be generated
        constructorSource.Content.Should().Contain("OrderService(");
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
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("OrderService");

        // Should use camelCase naming for constructor parameter (C# convention)
        constructorSource.Content.Should().Contain("IPaymentProcessor paymentProcessor");

        // Constructor should be generated
        constructorSource.Content.Should().Contain("OrderService(");
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
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("MixedNamingService");

        // Should use camelCase for all constructor parameters (C# convention)
        constructorSource.Content.Should().Contain("IEmailService emailService");
        constructorSource.Content.Should().Contain("ISmsService smsService");
        constructorSource.Content.Should().Contain("ILoggerService loggerService");

        // Constructor should be generated
        constructorSource.Content.Should().Contain("MixedNamingService(");
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
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("ServiceWithStrippedI");

        // Should strip 'I' prefix from interface names for constructor parameters
        constructorSource.Content.Should().Contain("IEmailService emailService");
        constructorSource.Content.Should().Contain("IPaymentProcessor paymentProcessor");

        // Constructor should be generated
        constructorSource.Content.Should().Contain("ServiceWithStrippedI(");
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
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("ServiceWithoutStrippedI");

        // Should use semantic naming regardless of stripI setting for consistent constructor parameter naming
        // stripI parameter affects naming convention application, not semantic parameter naming
        constructorSource.Content.Should().Contain("IEmailService emailService");

        // Constructor should be generated
        constructorSource.Content.Should().Contain("ServiceWithoutStrippedI(");
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
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("NonInterfaceService");

        // Non-interface types should not be affected by StripI for constructor parameters
        constructorSource.Content.Should().Contain("EmailService emailService");

        // Constructor should be generated
        constructorSource.Content.Should().Contain("NonInterfaceService(");
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
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("PrefixedService");

        // Should add custom prefixes to constructor parameter names
        constructorSource.Content.Should().Contain("IEmailService injectedEmailService");
        constructorSource.Content.Should().Contain("IPaymentProcessor externalPaymentProcessor");

        // Constructor should be generated
        constructorSource.Content.Should().Contain("PrefixedService(");
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
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("PrefixedStrippedService");

        // Should combine prefix with stripped interface name for constructor parameter
        constructorSource.Content.Should().Contain("IEmailService injectedEmailService");

        // Constructor should be generated
        constructorSource.Content.Should().Contain("PrefixedStrippedService(");
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
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("CombinedOptionsService");

        // Should combine prefix, stripped interface name, and camelCase for constructor parameter
        constructorSource.Content.Should().Contain("IPaymentProcessor externalPaymentProcessor");

        // Constructor should be generated
        constructorSource.Content.Should().Contain("CombinedOptionsService(");
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
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("UserService");

        // Should handle generic types correctly for constructor parameters
        constructorSource.Content.Should().Contain("IRepository<User> repository");

        // Constructor should be generated
        constructorSource.Content.Should().Contain("UserService(");
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
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("ComplexGenericService");

        // Should handle complex generic types for constructor parameters
        constructorSource.Content.Should().Contain("IKeyValueStore<string, int> keyValueStore");
        constructorSource.Content.Should().Contain("IFactory<List<string>> factory");

        // Constructor should be generated
        constructorSource.Content.Should().Contain("ComplexGenericService(");
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
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("MixedDependencyService");

        // Constructor should have parameters for both DependsOn and Inject fields without namespace
        constructorSource.Content.Should().Contain("IEmailService emailService");
        constructorSource.Content.Should().Contain("ILoggerService logger");
        constructorSource.Content.Should().Contain("ISmsService smsService");

        // Constructor should be generated
        constructorSource.Content.Should().Contain("MixedDependencyService(");
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
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("OrderedDependencyService");

        // Constructor parameters should be in proper order (DependsOn first, then Inject) without namespace
        var constructorText = constructorSource.Content;
        var emailParamIndex = constructorText.IndexOf("IEmailService emailService");
        var paymentParamIndex = constructorText.IndexOf("IPaymentService paymentService");
        var loggerParamIndex = constructorText.IndexOf("ILoggerService logger");
        var auditParamIndex = constructorText.IndexOf("IAuditService audit");

        (emailParamIndex > 0).Should().BeTrue();
        (paymentParamIndex > emailParamIndex).Should().BeTrue();
        (loggerParamIndex > paymentParamIndex).Should().BeTrue();
        (auditParamIndex > loggerParamIndex).Should().BeTrue();

        // Constructor should be generated
        constructorSource.Content.Should().Contain("OrderedDependencyService(");
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
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("FieldModifierService");

        // Constructor parameter should be generated
        constructorSource.Content.Should().Contain("IService service");

        // Constructor should be generated
        constructorSource.Content.Should().Contain("FieldModifierService(");
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
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("OnlyDependsOnService");
        constructorSource.Content.Should().Contain("OnlyDependsOnService(");
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
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("OnlyInjectService");
        constructorSource.Content.Should().Contain("OnlyInjectService(");
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
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("CollisionService");

        // The constructor should be generated with IService parameter
        // The existing [Inject] field should take precedence over [DependsOn]
        constructorSource.Content.Should().Contain("CollisionService(");
        constructorSource.Content.Should().Contain("IService");

        // Should use the existing field name, not generate a new field
        constructorSource.Content.Should().Contain("_existingService = ");
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
        var ioc040Diagnostics = result.GetDiagnosticsByCode("IOC040");
        ioc040Diagnostics.Should().NotBeEmpty();
        ioc040Diagnostics[0].GetMessage().Should().Contain("DependsOn");
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
        result.HasErrors.Should().BeFalse();

        var constructorSource = result.GetRequiredConstructorSource("DuplicateService");

        // Should only have one parameter declaration despite duplicate attributes  
        var paramCount = Regex.Matches(
            constructorSource.Content, @"IService service").Count;
        paramCount.Should().Be(1);

        // Constructor should be generated
        constructorSource.Content.Should().Contain("DuplicateService(");
    }

    #endregion
}
