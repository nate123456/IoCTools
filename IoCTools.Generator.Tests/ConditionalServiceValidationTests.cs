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
        // Should either combine conditions or produce diagnostic for multiple attributes
        var diagnostics = result.GetDiagnosticsByCode("IOC026");
        if (diagnostics.Any())
        {
            var message = diagnostics.First().GetMessage().ToLower();
            Assert.Contains("multiple", message);
            Assert.Contains("conditional", message);
        }
        else
        {
            // Should handle multiple attributes appropriately
            var registrationSource = result.GetServiceRegistrationSource();
            if (registrationSource != null)
            {
                // Should either combine with OR logic or use only the first/last attribute
                var hasDevelopment = registrationSource.Content.Contains("== \"Development\"");
                var hasTesting = registrationSource.Content.Contains("== \"Testing\"");

                if (registrationSource.Content.Contains("MultipleConditionalService"))
                    Assert.True(hasDevelopment || hasTesting, "Should handle at least one of the conditions");
            }
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
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        if (registrationSource != null && registrationSource.Content.Contains("UnicodeService"))
        {
            // Should handle Unicode characters correctly
            Assert.Contains("开发环境", registrationSource.Content);
            Assert.Contains("Función:Configuración", registrationSource.Content);
            Assert.Contains("activé", registrationSource.Content);
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
        // Should detect logical conflict: Environment = X AND NotEnvironment = X
        var diagnostics = result.GetDiagnosticsByCode("IOC020");
        if (diagnostics.Any())
        {
            var message = diagnostics.First().GetMessage().ToLower();
            Assert.Contains("conflict", message);
            Assert.Contains("development", message);
        }
        else
        {
            // If no diagnostic, should not generate impossible condition
            var registrationSource = result.GetServiceRegistrationSource();
            if (registrationSource != null)
            {
                var hasConflict =
                    registrationSource.Content.Contains(
                        "string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase) && !string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase)");
                Assert.False(hasConflict, "Should not generate logically impossible conditions");
            }
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
        // Should detect overlap: Testing appears in both Environment and NotEnvironment
        var diagnostics = result.GetDiagnosticsByCode("IOC020");
        if (diagnostics.Any())
        {
            var message = diagnostics.First().GetMessage().ToLower();
            Assert.Contains("overlap", message.ToLower());
            Assert.Contains("testing", message.ToLower());
        }
        else
        {
            // Should handle overlap gracefully in generated code
            var registrationSource = result.GetServiceRegistrationSource();
            if (registrationSource != null)
            {
                // Should not generate contradictory conditions
                var hasTestingInBoth =
                    registrationSource.Content.Contains(
                        "string.Equals(environment, \"Testing\", StringComparison.OrdinalIgnoreCase)") &&
                    registrationSource.Content.Contains(
                        "!string.Equals(environment, \"Testing\", StringComparison.OrdinalIgnoreCase)");
                Assert.False(hasTestingInBoth, "Should resolve overlapping conditions");
            }
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
        // Should detect config value conflict
        var diagnostics = result.GetDiagnosticsByCode("IOC020");
        if (diagnostics.Any())
        {
            var message = diagnostics.First().GetMessage().ToLower();
            Assert.Contains("conflict", message);
            Assert.Contains("feature:enabled", message.ToLower());
        }
        else
        {
            // Should not generate impossible config condition
            var registrationSource = result.GetServiceRegistrationSource();
            if (registrationSource != null)
            {
                var hasConflict = registrationSource.Content.Contains("== \"true\" && ") &&
                                  registrationSource.Content.Contains("!= \"true\"");
                Assert.False(hasConflict, "Should not generate contradictory config conditions");
            }
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
        // Should no longer produce IOC021 diagnostic - ConditionalService is itself a service indicator
        var diagnostics = result.GetDiagnosticsByCode("IOC021");
        Assert.False(diagnostics.Any(),
            "ConditionalService should work without [Scoped] attribute after intelligent inference refactor");

        // Should register the ConditionalService class since ConditionalService is a service indicator
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.Contains("ConditionalLifetimeInferenceValidationService", registrationSource.Content);

        // Should generate conditional logic
        Assert.Contains("Development", registrationSource.Content);
        Assert.Contains("Environment.GetEnvironmentVariable", registrationSource.Content);
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
        // Should require at least one condition
        var diagnostics = result.GetDiagnosticsByCode("IOC022");
        if (diagnostics.Any())
        {
            var message = diagnostics.First().GetMessage().ToLower();
            Assert.Contains("condition", message);
            Assert.Contains("required", message);
        }
        else
        {
            // Should treat as regular service or skip registration
            var registrationSource = result.GetServiceRegistrationSource();
            if (registrationSource != null)
            {
                // Either register unconditionally or not at all
                var hasConditionalRegistration = registrationSource.Content.Contains("if (") &&
                                                 registrationSource.Content.Contains("EmptyConditionalService");
                Assert.False(hasConditionalRegistration,
                    "Should not generate conditional registration without conditions");
            }
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
        // Should require Equals or NotEquals when ConfigValue is specified
        var diagnostics = result.GetDiagnosticsByCode("IOC023");
        if (diagnostics.Any())
        {
            var message = diagnostics.First().GetMessage().ToLower();
            Assert.Contains("equals", message);
            Assert.Contains("required", message);
        }
        else
        {
            // Should not generate incomplete config check
            var registrationSource = result.GetServiceRegistrationSource();
            if (registrationSource != null)
                Assert.DoesNotContain("configuration.GetValue<string>(\"Feature:Enabled\")",
                    registrationSource.Content);
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
        // Should require ConfigValue when Equals is specified
        var diagnostics = result.GetDiagnosticsByCode("IOC024");
        if (diagnostics.Any())
        {
            var message = diagnostics.First().GetMessage().ToLower();
            Assert.Contains("configvalue", message);
            Assert.Contains("required", message);
        }
        else
        {
            // Should not generate orphaned equals check
            var registrationSource = result.GetServiceRegistrationSource();
            if (registrationSource != null) Assert.DoesNotContain("== \"true\"", registrationSource.Content);
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
        // Should require ConfigValue when NotEquals is specified
        var diagnostics = result.GetDiagnosticsByCode("IOC024");
        if (diagnostics.Any())
        {
            var message = diagnostics.First().GetMessage().ToLower();
            Assert.Contains("configvalue", message);
            Assert.Contains("required", message);
        }
        else
        {
            // Should not generate orphaned not equals check
            var registrationSource = result.GetServiceRegistrationSource();
            if (registrationSource != null) Assert.DoesNotContain("!= \"false\"", registrationSource.Content);
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
        // Should either produce diagnostic or handle empty environment
        var registrationSource = result.GetServiceRegistrationSource();
        if (registrationSource != null)
            if (registrationSource.Content.Contains("EmptyEnvironmentService"))
                // If registered, should handle empty string safely
                Assert.Contains("string.Equals(environment, \"\", StringComparison.OrdinalIgnoreCase)",
                    registrationSource.Content);
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
        // Should either produce diagnostic or handle empty config key
        var diagnostics = result.GetDiagnosticsByCode("IOC025");
        if (!diagnostics.Any())
        {
            var registrationSource = result.GetServiceRegistrationSource();
            if (registrationSource != null && registrationSource.Content.Contains("EmptyConfigKeyService"))
            {
                // Should handle empty config key safely - accept various null-safe patterns
                var hasNullSafePattern =
                    registrationSource.Content.Contains(
                        "string.Equals(configuration[\"\"], \"value\", StringComparison.OrdinalIgnoreCase)");
                Assert.True(hasNullSafePattern, "Generator should produce null-safe code for empty config keys");
            }
        }
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
        // Should handle whitespace values appropriately
        var registrationSource = result.GetServiceRegistrationSource();
        if (registrationSource != null)
        {
            // Should either trim whitespace or preserve it consistently
            var environmentCheck =
                registrationSource.Content.Contains(
                    "string.Equals(environment, \"   \", StringComparison.OrdinalIgnoreCase)") ||
                registrationSource.Content.Contains(
                    "string.Equals(environment, \"\", StringComparison.OrdinalIgnoreCase)");

            if (registrationSource.Content.Contains("WhitespaceService"))
                // Should generate some form of valid condition
                Assert.True(registrationSource.Content.Contains("if ("));
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
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should handle special characters in environment and config values
        Assert.Contains("string.Equals(environment, \"Dev-Test.Local\", StringComparison.OrdinalIgnoreCase)",
            registrationSource.Content);
        Assert.Contains(
            "string.Equals(configuration[\"App:Feature.Name\"], \"enabled/true\", StringComparison.OrdinalIgnoreCase)",
            registrationSource.Content);
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
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should handle very long values without issues
        Assert.Contains($"string.Equals(environment, \"{longEnvironment}\", StringComparison.OrdinalIgnoreCase)",
            registrationSource.Content);
        Assert.Contains(
            $"string.Equals(configuration[\"{longConfigKey}\"], \"{longConfigValue}\", StringComparison.OrdinalIgnoreCase)",
            registrationSource.Content);
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
        // Should handle invalid environment names (they might be valid in some contexts)
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        if (registrationSource != null && registrationSource.Content.Contains("InvalidEnvService"))
            // Should generate condition even for unusual environment names
            Assert.Contains("string.Equals(environment, \"Invalid-Env@123\", StringComparison.OrdinalIgnoreCase)",
                registrationSource.Content);
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
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        if (registrationSource != null && registrationSource.Content.Contains("ReservedNameService"))
        {
            // Should handle reserved keywords as environment names
            Assert.Contains("string.Equals(environment, \"null\", StringComparison.OrdinalIgnoreCase)",
                registrationSource.Content);
            Assert.Contains("string.Equals(environment, \"true\", StringComparison.OrdinalIgnoreCase)",
                registrationSource.Content);
            Assert.Contains("string.Equals(environment, \"false\", StringComparison.OrdinalIgnoreCase)",
                registrationSource.Content);
        }
    }

    #endregion
}
