namespace IoCTools.Generator.Tests;

/// <summary>
///     Tests for configuration-based conditional service registration
/// </summary>
public class ConditionalServiceConfigurationTests
{
    #region Configuration Value Tests

    [Fact]
    public void ConditionalService_ConfigurationEquals_StringComparison()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ICacheProvider { }

[ConditionalService(ConfigValue = ""Cache:Provider"", Equals = ""Redis"")]

public partial class RedisCacheProvider : ICacheProvider { }

[ConditionalService(ConfigValue = ""Cache:Provider"", Equals = ""Memory"")]

public partial class MemoryCacheProvider : ICacheProvider { }
";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should generate mutually exclusive configuration conditions
        Assert.Contains(
            "if (string.Equals(configuration[\"Cache:Provider\"], \"Redis\", StringComparison.OrdinalIgnoreCase))",
            registrationSource.Content);
        Assert.Contains(
            "else if (string.Equals(configuration[\"Cache:Provider\"], \"Memory\", StringComparison.OrdinalIgnoreCase))",
            registrationSource.Content);
    }

    [Fact]
    public void ConditionalService_ConfigurationNotEquals_StringComparison()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService { }

[ConditionalService(ConfigValue = ""Features:Disabled"", NotEquals = ""true"")]

public partial class EnabledService : IService { }
";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should generate NOT equals condition with proper null handling
        Assert.Contains("(configuration.GetValue<string>(\"Features:Disabled\") ?? \"\") != \"true\"",
            registrationSource.Content);
    }

    [Fact]
    public void ConditionalService_ConfigurationWithSpecialCharacters_ProperlyEscaped()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService { }

[ConditionalService(ConfigValue = ""App:Config\""WithQuotes"", Equals = ""Value\""WithQuotes"")]

public partial class QuotedService : IService { }
";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should properly escape special characters in config key and value
        Assert.Contains("App:Config\\\"WithQuotes", registrationSource.Content);
        Assert.Contains("Value\\\"WithQuotes", registrationSource.Content);
    }

    [Fact]
    public void ConditionalService_MultipleConfigurationNotEquals_AndCondition()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IService { }

[ConditionalService(ConfigValue = ""Features:Provider"", NotEquals = ""None,Disabled"")]

public partial class EnabledService : IService { }
";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should generate multiple NOT equals with AND logic and proper null handling
        var configAccess = "(configuration.GetValue<string>(\"Features:Provider\") ?? \"\")";
        Assert.Contains($"{configAccess} != \"None\" && {configAccess} != \"Disabled\"", registrationSource.Content);
    }

    #endregion

    #region Configuration + Environment Tests

    [Fact]
    public void ConditionalService_EnvironmentAndConfigurationCombined_AndLogic()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IAdvancedService { }

[ConditionalService(Environment = ""Production"", ConfigValue = ""Features:Advanced"", Equals = ""enabled"")]

public partial class ProductionAdvancedService : IAdvancedService { }
";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should generate combined environment and configuration check with AND logic
        Assert.Contains("string.Equals(environment, \"Production\", StringComparison.OrdinalIgnoreCase)",
            registrationSource.Content);
        Assert.Contains(
            "string.Equals(configuration[\"Features:Advanced\"], \"enabled\", StringComparison.OrdinalIgnoreCase)",
            registrationSource.Content);
        Assert.Contains("&&", registrationSource.Content);
    }

    [Fact]
    public void ConditionalService_MultipleEnvironmentsWithConfiguration_ComplexCondition()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

[ConditionalService(Environment = ""Development,Testing"", ConfigValue = ""Features:TestMode"", Equals = ""true"")]

public partial class TestModeService : ITestService { }
";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should generate OR condition for environments AND configuration condition
        Assert.Contains(
            "string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase) || string.Equals(environment, \"Testing\", StringComparison.OrdinalIgnoreCase)",
            registrationSource.Content);
        Assert.Contains(
            "string.Equals(configuration[\"Features:TestMode\"], \"true\", StringComparison.OrdinalIgnoreCase)",
            registrationSource.Content);
        Assert.Contains("&&", registrationSource.Content);
    }

    #endregion

    #region Configuration Value Type Handling

    [Fact]
    public void ConditionalService_ConfigurationBooleanValues_StringComparison()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IBooleanService { }

[ConditionalService(ConfigValue = ""Features:BooleanFeature"", Equals = ""true"")]

public partial class TrueService : IBooleanService { }

[ConditionalService(ConfigValue = ""Features:BooleanFeature"", Equals = ""false"")]

public partial class FalseService : IBooleanService { }
";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should treat boolean values as strings in comparison
        Assert.Contains(
            "string.Equals(configuration[\"Features:BooleanFeature\"], \"true\", StringComparison.OrdinalIgnoreCase)",
            registrationSource.Content);
        Assert.Contains(
            "string.Equals(configuration[\"Features:BooleanFeature\"], \"false\", StringComparison.OrdinalIgnoreCase)",
            registrationSource.Content);
    }

    [Fact]
    public void ConditionalService_ConfigurationNumericValues_StringComparison()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface INumericService { }

[ConditionalService(ConfigValue = ""App:Version"", Equals = ""2"")]

public partial class V2Service : INumericService { }
";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should treat numeric values as strings
        Assert.Contains("string.Equals(configuration[\"App:Version\"], \"2\", StringComparison.OrdinalIgnoreCase)",
            registrationSource.Content);
    }

    [Fact]
    public void ConditionalService_ConfigurationEmptyValue_HandlesProperly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IEmptyService { }

[ConditionalService(ConfigValue = ""App:EmptyConfig"", Equals = """")]

public partial class EmptyValueService : IEmptyService { }
";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should handle empty string comparison properly
        Assert.Contains("string.Equals(configuration[\"App:EmptyConfig\"], \"\", StringComparison.OrdinalIgnoreCase)",
            registrationSource.Content);
    }

    #endregion

    #region Complex Configuration Scenarios

    [Fact]
    public void ConditionalService_NestedConfigurationKeys_WorkCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface INestedService { }

[ConditionalService(ConfigValue = ""App:Features:Database:Provider"", Equals = ""SqlServer"")]

public partial class SqlServerService : INestedService { }
";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should handle nested configuration keys properly
        Assert.Contains(
            "string.Equals(configuration[\"App:Features:Database:Provider\"], \"SqlServer\", StringComparison.OrdinalIgnoreCase)",
            registrationSource.Content);
    }

    [Fact]
    public void ConditionalService_CaseSensitiveConfiguration_ExactMatch()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ICaseSensitiveService { }

[ConditionalService(ConfigValue = ""App:CaseTest"", Equals = ""UPPERCASE"")]

public partial class UppercaseService : ICaseSensitiveService { }

[ConditionalService(ConfigValue = ""App:CaseTest"", Equals = ""lowercase"")]

public partial class LowercaseService : ICaseSensitiveService { }
";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should be case-insensitive comparison (using StringComparison.OrdinalIgnoreCase)
        Assert.Contains(
            "string.Equals(configuration[\"App:CaseTest\"], \"UPPERCASE\", StringComparison.OrdinalIgnoreCase)",
            registrationSource.Content);
        Assert.Contains(
            "string.Equals(configuration[\"App:CaseTest\"], \"lowercase\", StringComparison.OrdinalIgnoreCase)",
            registrationSource.Content);
    }

    #endregion
}
