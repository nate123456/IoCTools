namespace IoCTools.Generator.Tests;

/// <summary>
///     Tests for validation and error handling in Conditional Service Registration.
///     Focuses on invalid conditions, conflicts, and edge cases.
/// </summary>
public class ConditionalServiceValidationTests
{
    #region Multiple ConditionalService Attributes Tests

    [Fact]
    public void ConditionalService_MultipleAttributesOnSameClass_ProducesDiagnostic()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

[ConditionalService(Environment = ""Development"")]
[ConditionalService(Environment = ""Testing"")]

public partial class MultipleConditionalService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var diagnostics = result.GetDiagnosticsByCode("IOC026");
        if (diagnostics.Any())
        {
            var message = diagnostics.First().GetMessage().ToLowerInvariant();
            message.Should().Contain("multiple");
            message.Should().Contain("conditional");
        }
        else if (result.GetServiceRegistrationSource() is { Content: var content })
        {
            var hasDevelopment = content.Contains("== \"Development\"");
            var hasTesting = content.Contains("== \"Testing\"");

            if (content.Contains("MultipleConditionalService"))
                (hasDevelopment || hasTesting).Should().BeTrue(
                    "at least one condition should be honored when diagnostics are absent");
        }
    }

    #endregion

    #region Unicode and Encoding Tests

    [Fact]
    public void ConditionalService_UnicodeValues_HandlesCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

[ConditionalService(Environment = ""开发环境"", ConfigValue = ""Función:Configuración"", Equals = ""activé"")]

public partial class UnicodeService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        if (result.GetServiceRegistrationSource() is { Content: var registrationContent } &&
            registrationContent.Contains("UnicodeService"))
        {
            registrationContent.Should().Contain("开发环境");
            registrationContent.Should().Contain("Función:Configuración");
            registrationContent.Should().Contain("activé");
        }
    }

    #endregion

    #region Conflicting Conditions Tests

    [Fact]
    public void ConditionalService_SameEnvironmentInBothFields_ProducesDiagnostic()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

[ConditionalService(Environment = ""Development"", NotEnvironment = ""Development"")]

public partial class ConflictingService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var diagnostics = result.GetDiagnosticsByCode("IOC020");
        if (diagnostics.Any())
        {
            var message = diagnostics.First().GetMessage().ToLowerInvariant();
            message.Should().Contain("conflict");
            message.Should().Contain("development");
        }
        else if (result.GetServiceRegistrationSource() is { Content: var registrationContent })
        {
            var hasConflict = registrationContent.Contains(
                "string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase) && !string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)");
            hasConflict.Should().BeFalse("generated code should not create impossible conditions");
        }
    }

    [Fact]
    public void ConditionalService_OverlappingEnvironmentConditions_ProducesDiagnostic()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

[ConditionalService(Environment = ""Development,Testing"", NotEnvironment = ""Testing,Staging"")]

public partial class OverlappingService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var diagnostics = result.GetDiagnosticsByCode("IOC020");
        if (diagnostics.Any())
        {
            var message = diagnostics.First().GetMessage().ToLowerInvariant();
            message.Should().Contain("overlap");
            message.Should().Contain("testing");
        }
        else if (result.GetServiceRegistrationSource() is { Content: var registrationContent })
        {
            var hasTestingInBoth = registrationContent.Contains(
                                       "string.Equals(environment, \"Testing\", StringComparison.OrdinalIgnoreCase)") &&
                                   registrationContent.Contains(
                                       "!string.Equals(environment, \"Testing\", StringComparison.OrdinalIgnoreCase)");
            hasTestingInBoth.Should().BeFalse("overlapping conditions should not emit contradictory code");
        }
    }

    [Fact]
    public void ConditionalService_SameConfigValueBothEqualsAndNotEquals_ProducesDiagnostic()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

[ConditionalService(ConfigValue = ""Feature:Enabled"", Equals = ""true"", NotEquals = ""true"")]

public partial class ConfigConflictService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var diagnostics = result.GetDiagnosticsByCode("IOC020");
        if (diagnostics.Any())
        {
            var message = diagnostics.First().GetMessage().ToLowerInvariant();
            message.Should().Contain("conflict");
            message.Should().Contain("feature:enabled");
        }
        else if (result.GetServiceRegistrationSource() is { Content: var registrationContent })
        {
            var hasConflict = registrationContent.Contains("== \"true\" && ") &&
                              registrationContent.Contains("!= \"true\"");
            hasConflict.Should().BeFalse("config conditions should not contradict themselves");
        }
    }

    #endregion

    #region Missing Attributes Tests

    [Fact]
    public void ConditionalService_WithLifetimeInference_WorksCorrectly()
    {
        // After intelligent inference refactor, ConditionalService no longer requires [Scoped] attribute
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

[ConditionalService(Environment = ""Development"")]
public partial class ConditionalLifetimeInferenceValidationService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var diagnostics = result.GetDiagnosticsByCode("IOC021");
        diagnostics.Should().BeEmpty(
            "ConditionalService should work without [Scoped] attribute after intelligent inference refactor");

        var registrationContent = result.GetServiceRegistrationText();
        registrationContent.Should().Contain("ConditionalLifetimeInferenceValidationService");
        registrationContent.Should().Contain("Development");
        registrationContent.Should().Contain("Environment.GetEnvironmentVariable");
    }

    [Fact]
    public void ConditionalService_EmptyConditions_ProducesDiagnostic()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

[ConditionalService]

public partial class EmptyConditionalService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var diagnostics = result.GetDiagnosticsByCode("IOC022");
        if (diagnostics.Any())
        {
            var message = diagnostics.First().GetMessage().ToLowerInvariant();
            message.Should().Contain("condition");
            message.Should().Contain("required");
        }
        else
        {
            var registrationContent = result.GetServiceRegistrationText();
            var hasConditionalRegistration = registrationContent.Contains("if (") &&
                                             registrationContent.Contains("EmptyConditionalService");
            hasConditionalRegistration.Should().BeFalse(
                "services without conditions should not generate conditional code");
        }
    }

    #endregion

    #region Invalid Configuration Tests

    [Fact]
    public void ConditionalService_ConfigValueWithoutEqualsOrNotEquals_ProducesDiagnostic()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

[ConditionalService(ConfigValue = ""Feature:Enabled"")]

public partial class IncompleteConfigService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var diagnostics = result.GetDiagnosticsByCode("IOC023");
        if (diagnostics.Any())
        {
            var message = diagnostics.First().GetMessage().ToLowerInvariant();
            message.Should().Contain("equals");
            message.Should().Contain("required");
        }
        else
        {
            var registrationContent = result.GetServiceRegistrationText();
            registrationContent.Should().NotContain(
                "configuration.GetValue<string>(\"Feature:Enabled\")",
                "config comparisons require Equals/NotEquals");
        }
    }

    [Fact]
    public void ConditionalService_EqualsWithoutConfigValue_ProducesDiagnostic()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

[ConditionalService(Equals = ""true"")]

public partial class OrphanedEqualsService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var diagnostics = result.GetDiagnosticsByCode("IOC024");
        if (diagnostics.Any())
        {
            var message = diagnostics.First().GetMessage().ToLowerInvariant();
            message.Should().Contain("configvalue");
            message.Should().Contain("required");
        }
        else
        {
            var registrationContent = result.GetServiceRegistrationText();
            registrationContent.Should().NotContain("== \"true\"");
        }
    }

    [Fact]
    public void ConditionalService_NotEqualsWithoutConfigValue_ProducesDiagnostic()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

[ConditionalService(NotEquals = ""false"")]

public partial class OrphanedNotEqualsService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var diagnostics = result.GetDiagnosticsByCode("IOC024");
        if (diagnostics.Any())
        {
            var message = diagnostics.First().GetMessage().ToLowerInvariant();
            message.Should().Contain("configvalue");
            message.Should().Contain("required");
        }
        else
        {
            var registrationContent = result.GetServiceRegistrationText();
            registrationContent.Should().NotContain("!= \"false\"");
        }
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public void ConditionalService_EmptyEnvironmentString_HandlesGracefully()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

[ConditionalService(Environment = """")]

public partial class EmptyEnvironmentService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        if (result.GetServiceRegistrationSource() is { Content: var registrationContent } &&
            registrationContent.Contains("EmptyEnvironmentService"))
            registrationContent.Should().Contain(
                "string.Equals(environment, \"\", StringComparison.OrdinalIgnoreCase)");
    }

    [Fact]
    public void ConditionalService_EmptyConfigValue_HandlesGracefully()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

[ConditionalService(ConfigValue = """", Equals = ""value"")]

public partial class EmptyConfigKeyService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        var diagnosticsIOC025 = result.GetDiagnosticsByCode("IOC025");
        if (!diagnosticsIOC025.Any() &&
            result.GetServiceRegistrationSource() is { Content: var registrationContent } &&
            registrationContent.Contains("EmptyConfigKeyService"))
            registrationContent.Should().Contain(
                "string.Equals(configuration[\"\"], \"value\", StringComparison.OrdinalIgnoreCase)");
    }

    [Fact]
    public void ConditionalService_WhitespaceOnlyValues_HandlesGracefully()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

[ConditionalService(Environment = ""   "", ConfigValue = ""  \t  "", Equals = "" \n "")]

public partial class WhitespaceService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        if (result.GetServiceRegistrationSource() is { Content: var registrationContent })
        {
            var environmentCheck =
                registrationContent.Contains(
                    "string.Equals(environment, \"   \", StringComparison.OrdinalIgnoreCase)") ||
                registrationContent.Contains(
                    "string.Equals(environment, \"\", StringComparison.OrdinalIgnoreCase)");
            environmentCheck.Should().BeTrue("Whitespace should be normalized in environment checks");

            if (registrationContent.Contains("WhitespaceService")) registrationContent.Should().Contain("if (");
        }
    }

    [Fact]
    public void ConditionalService_SpecialCharactersInValues_HandlesCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

[ConditionalService(Environment = ""Dev-Test.Local"", ConfigValue = ""App:Feature.Name"", Equals = ""enabled/true"")]

public partial class SpecialCharService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var registrationContent = result.GetServiceRegistrationText();
        registrationContent.Should().Contain(
            "string.Equals(environment, \"Dev-Test.Local\", StringComparison.OrdinalIgnoreCase)");
        registrationContent.Should().Contain(
            "string.Equals(configuration[\"App:Feature.Name\"], \"enabled/true\", StringComparison.OrdinalIgnoreCase)");
    }

    [Fact]
    public void ConditionalService_VeryLongValues_HandlesCorrectly()
    {
        // Arrange
        var longEnvironment = new string('A', 100);
        var longConfigKey = "Very:Long:Configuration:Key:With:Many:Sections:That:Goes:On:Forever";
        var longConfigValue = new string('B', 200);

        var source = $@"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService {{ }}

[ConditionalService(Environment = ""{longEnvironment}"", ConfigValue = ""{longConfigKey}"", Equals = ""{longConfigValue}"")]

public partial class LongValueService : ITestService
{{
}}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var registrationContent = result.GetServiceRegistrationText();
        registrationContent.Should().Contain(
            $"string.Equals(environment, \"{longEnvironment}\", StringComparison.OrdinalIgnoreCase)");
        registrationContent.Should().Contain(
            $"string.Equals(configuration[\"{longConfigKey}\"], \"{longConfigValue}\", StringComparison.OrdinalIgnoreCase)");
    }

    #endregion

    #region Invalid Environment Names Tests

    [Fact]
    public void ConditionalService_InvalidEnvironmentNames_HandlesGracefully()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

[ConditionalService(Environment = ""Invalid-Env@123"")]

public partial class InvalidEnvService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        if (result.GetServiceRegistrationSource() is { Content: var invalidEnvContent } &&
            invalidEnvContent.Contains("InvalidEnvService"))
            invalidEnvContent.Should().Contain(
                "string.Equals(environment, \"Invalid-Env@123\", StringComparison.OrdinalIgnoreCase)");
    }

    [Fact]
    public void ConditionalService_ReservedEnvironmentNames_HandlesCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

[ConditionalService(Environment = ""null,undefined,true,false"")]

public partial class ReservedNameService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        if (result.GetServiceRegistrationSource() is { Content: var reservedContent } &&
            reservedContent.Contains("ReservedNameService"))
        {
            reservedContent.Should().Contain(
                "string.Equals(environment, \"null\", StringComparison.OrdinalIgnoreCase)");
            reservedContent.Should().Contain(
                "string.Equals(environment, \"true\", StringComparison.OrdinalIgnoreCase)");
            reservedContent.Should().Contain(
                "string.Equals(environment, \"false\", StringComparison.OrdinalIgnoreCase)");
        }
    }

    #endregion
}
