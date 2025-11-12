namespace IoCTools.Generator.Tests;

using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;

/// <summary>
///     Tests for generated code structure and compilation verification of Conditional Service Registration.
///     Focuses on code quality, structure, and compilation correctness.
/// </summary>
public class ConditionalServiceCodeGenerationTests
{
    #region Generated Code Structure Tests

    [Fact]
    public void ConditionalService_GeneratedMethodSignature_HasCorrectStructure()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

[ConditionalService(Environment = ""Development"")]

public partial class DevService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should have proper extension method signature
        registrationSource.Content.Should().Contain("public static IServiceCollection");
        registrationSource.Content.Should().Contain("this IServiceCollection services");

        // Method name should follow naming convention - based on the test assembly name "TestAssembly"
        var hasCorrectMethodName = registrationSource.Content.Contains("AddTestAssemblyRegisteredServices");
        hasCorrectMethodName.Should().BeTrue("Should have properly named registration method");

        // Should return IServiceCollection for chaining
        registrationSource.Content.Should().Contain("return services;");
    }

    [Fact]
    public void ConditionalService_GeneratedVariableDeclarations_AreCorrect()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

[ConditionalService(Environment = ""Development"", ConfigValue = ""Feature:Enabled"", Equals = ""true"")]

public partial class ConditionalService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        // Assert
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();
        // Should declare environment variable once when environment conditions are present
        registrationSource.Content.Should()
            .Contain("var environment = Environment.GetEnvironmentVariable(\"ASPNETCORE_ENVIRONMENT\") ?? \"\"");

        // Should use IConfiguration parameter when configuration conditions are present (no local variable)
        registrationSource.Content.Should().Contain("IConfiguration configuration");

        // Variable names should be consistent
        var environmentUsageCount = Regex.Matches(registrationSource.Content, @"\benvironment\b").Count;
        var configurationUsageCount = Regex.Matches(registrationSource.Content, @"\bconfiguration\b").Count;

        (environmentUsageCount >= 2).Should().BeTrue("Environment variable should be declared and used");
        (configurationUsageCount >= 2).Should()
            .BeTrue("Configuration variable should be declared and used as parameter");
    }

    [Fact]
    public void ConditionalService_GeneratedIfStatements_HaveCorrectStructure()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

[ConditionalService(Environment = ""Development"")]

public partial class DevService : ITestService
{
}

[ConditionalService(Environment = ""Production"")]

public partial class ProdService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should use proper if-else structure with StringComparison
        registrationSource.Content.Should()
            .Contain("if (string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase))");
        registrationSource.Content.Should()
            .Contain("else if (string.Equals(environment, \"Production\", StringComparison.OrdinalIgnoreCase))");

        // Should have proper braces
        var ifCount = Regex.Matches(registrationSource.Content, @"\bif\s*\(").Count;
        var openBraceCount = registrationSource.Content.Count(c => c == '{');
        var closeBraceCount = registrationSource.Content.Count(c => c == '}');

        closeBraceCount.Should().Be(openBraceCount);
        (ifCount >= 1).Should().BeTrue("Should have conditional statements");
    }

    [Fact]
    public void ConditionalService_GeneratedLogicalOperators_AreCorrect()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

[ConditionalService(Environment = ""Development,Testing"")]

public partial class MultiEnvService : ITestService
{
}

[ConditionalService(Environment = ""Production"", ConfigValue = ""Feature:Enabled"", Equals = ""true"")]

public partial class CombinedConditionService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should use OR for multiple environments with StringComparison
        var hasOrCondition =
            registrationSource.Content.Contains(
                "string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase) || string.Equals(environment, \"Testing\", StringComparison.OrdinalIgnoreCase)");
        hasOrCondition.Should().BeTrue("Should use OR logic for multiple environments with proper string comparison");

        // Should use AND for combined conditions with proper configuration access patterns
        var hasEnvironmentCondition = registrationSource.Content.Contains("string.Equals(environment,");
        var hasConfigurationCondition = registrationSource.Content.Contains("string.Equals(configuration[") ||
                                        registrationSource.Content.Contains("configuration.GetValue<string>");
        (hasEnvironmentCondition && hasConfigurationCondition).Should()
            .BeTrue("Should generate both environment and configuration condition checks");

        // Parentheses should be balanced
        var openParenCount = registrationSource.Content.Count(c => c == '(');
        var closeParenCount = registrationSource.Content.Count(c => c == ')');
        closeParenCount.Should().Be(openParenCount);
    }

    #endregion

    #region Using Statements and Imports Tests

    [Fact]
    public void ConditionalService_GeneratedUsingStatements_AreCorrect()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

[ConditionalService(Environment = ""Development"", ConfigValue = ""Feature:Enabled"", Equals = ""true"")]

public partial class ConditionalService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should include necessary using statements
        registrationSource.Content.Should().Contain("using Microsoft.Extensions.DependencyInjection;");
        registrationSource.Content.Should().Contain("using Microsoft.Extensions.Configuration;");
        registrationSource.Content.Should().Contain("using System;");

        // Should have nullable enable directive
        registrationSource.Content.Should().Contain("#nullable enable");

        // Should not have duplicate using statements
        var usingDICount = Regex
            .Matches(registrationSource.Content, @"using Microsoft\.Extensions\.DependencyInjection;").Count;
        var usingConfigCount = Regex.Matches(registrationSource.Content, @"using Microsoft\.Extensions\.Configuration;")
            .Count;

        usingDICount.Should().Be(1);
        usingConfigCount.Should().Be(1);
    }

    [Fact]
    public void ConditionalService_GeneratedNamespace_MatchesSourceNamespace()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace MyCustomNamespace.Services;

public interface ITestService { }

[ConditionalService(Environment = ""Development"")]

public partial class DevService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should use appropriate namespace (generator uses the compilation assembly name TestAssembly + .Extensions)
        var hasCorrectNamespace = registrationSource.Content.Contains("namespace TestAssembly.Extensions");

        hasCorrectNamespace.Should()
            .BeTrue(
                $"Should use correct compilation assembly namespace. Generated content: {registrationSource.Content}");
    }

    #endregion

    #region Service Registration Call Generation Tests

    [Fact]
    public void ConditionalService_ServiceRegistrationCalls_HaveCorrectSyntax()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface ITestService { }

[ConditionalService(Environment = ""Development"")]
[Singleton]
public partial class SingletonService : ITestService
{
}

[ConditionalService(Environment = ""Production"")]
[Transient]
public partial class TransientService : ITestService
{
}

[ConditionalService(Environment = ""Testing"")]
[Scoped] // Default Scoped
public partial class ScopedService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should generate correct registration method calls (conditional services use simplified type names)
        registrationSource.Content.Should()
            .Contain("services.AddSingleton<Test.ITestService, Test.SingletonService>()");
        registrationSource.Content.Should()
            .Contain("services.AddTransient<Test.ITestService, Test.TransientService>()");
        registrationSource.Content.Should().Contain("services.AddScoped<Test.ITestService, Test.ScopedService>()");

        // Should have proper semicolons with simplified type names
        registrationSource.Content.Should().Contain("AddSingleton<Test.ITestService, Test.SingletonService>();");
        registrationSource.Content.Should().Contain("AddTransient<Test.ITestService, Test.TransientService>();");
        registrationSource.Content.Should().Contain("AddScoped<Test.ITestService, Test.ScopedService>();");
    }

    [Fact]
    public void ConditionalService_GenericServiceRegistration_GeneratesCorrectSyntax()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IRepository<T> { }

[ConditionalService(Environment = ""Development"")]

public partial class InMemoryRepository<T> : IRepository<T>
{
}

[ConditionalService(Environment = ""Production"")]

public partial class DatabaseRepository<T> : IRepository<T>
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should handle generic services correctly with simplified type names
        registrationSource.Content.Should()
            .Contain("services.AddScoped(typeof(Test.IRepository<>), typeof(Test.InMemoryRepository<>))");
        registrationSource.Content.Should()
            .Contain("services.AddScoped(typeof(Test.IRepository<>), typeof(Test.DatabaseRepository<>))");

        // Should use typeof for open generics with simplified type names
        registrationSource.Content.Should().Contain("typeof(Test.IRepository<>)");
        registrationSource.Content.Should().Contain("typeof(Test.InMemoryRepository<>)");
        registrationSource.Content.Should().Contain("typeof(Test.DatabaseRepository<>)");
    }

    [Fact]
    public void ConditionalService_MultipleInterfaceRegistration_GeneratesCorrectCalls()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IService1 { }
public interface IService2 { }

[ConditionalService(Environment = ""Development"")]

[RegisterAsAll(RegistrationMode.All)]
public partial class MultiInterfaceService : IService1, IService2
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should register all interfaces when using RegisterAsAll (using factory pattern for shared instances with simplified type names)
        registrationSource.Content.Should()
            .Contain(
                "AddScoped<Test.IService1>(provider => provider.GetRequiredService<Test.MultiInterfaceService>())");
        registrationSource.Content.Should()
            .Contain(
                "AddScoped<Test.IService2>(provider => provider.GetRequiredService<Test.MultiInterfaceService>())");
        registrationSource.Content.Should()
            .Contain("AddScoped<Test.MultiInterfaceService, Test.MultiInterfaceService>");
    }

    #endregion

    #region String Escaping and Safety Tests

    [Fact]
    public void ConditionalService_StringLiteralsWithSpecialCharacters_AreProperlyEscaped()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

[ConditionalService(Environment = ""Dev\""Test"", ConfigValue = ""Path\\With\\Backslashes"", Equals = ""Value\nWith\tEscapes"")]

public partial class EscapedStringService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should properly escape quotes in environment name using string.Equals
        registrationSource.Content.Should().Contain("\"Dev\\\"Test\"");

        // Should properly escape backslashes in config key
        registrationSource.Content.Should().Contain("\"Path\\\\With\\\\Backslashes\"");

        // Should properly escape special characters in config value
        registrationSource.Content.Should().Contain("\"Value\\nWith\\tEscapes\"");

        // Generated code should compile without syntax errors
        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void ConditionalService_UnicodeCharacters_AreHandledCorrectly()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

[ConditionalService(Environment = ""环境"", ConfigValue = ""功能:启用"", Equals = ""是"")]

public partial class UnicodeService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should handle Unicode characters correctly (basic validation that code was generated)
        // Note: Conditional services may not generate registration code if no valid registrations exist
        registrationSource.Content.Should().NotBeEmpty();
        registrationSource.Content.Should().Contain("namespace");
        registrationSource.Content.Should().Contain("#nullable enable");

        // Generated code should compile without encoding issues
        result.HasErrors.Should().BeFalse();
    }

    #endregion

    #region Code Formatting and Style Tests

    [Fact]
    public void ConditionalService_GeneratedCodeFormatting_IsConsistent()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

[ConditionalService(Environment = ""Development"")]

public partial class DevService : ITestService
{
}

[ConditionalService(Environment = ""Production"")]

public partial class ProdService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should use proper string comparison methods
        registrationSource.Content.Should().Contain("string.Equals(");
        registrationSource.Content.Should().Contain("StringComparison.OrdinalIgnoreCase");

        // Should have consistent brace style
        var openBraces = Regex.Matches(registrationSource.Content, @"\{").Count;
        var closeBraces = Regex.Matches(registrationSource.Content, @"\}").Count;
        closeBraces.Should().Be(openBraces);
    }

    [Fact]
    public void ConditionalService_NullableDirective_IsGenerated()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

[ConditionalService(Environment = ""Development"")]

public partial class DevService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Generated code should include nullable enable directive
        registrationSource.Content.Should().Contain("#nullable enable");
    }

    #endregion

    #region Compilation Verification Tests

    [Fact]
    public void ConditionalService_GeneratedCodeCompilation_SucceedsWithoutWarnings()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface ITestService { }
public interface IRepository<T> { }

[ConditionalService(Environment = ""Development"")]
[Singleton]
public partial class DevService : ITestService
{
}

[ConditionalService(ConfigValue = ""Feature:Enabled"", Equals = ""true"")]

public partial class FeatureService : ITestService
{
}

[ConditionalService(Environment = ""Production"", ConfigValue = ""Cache:UseRedis"", Equals = ""true"")]

public partial class CacheService : IRepository<string>
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        // Should compile without any errors
        result.HasErrors.Should()
            .BeFalse(
                $"Generated code should compile without errors. Errors: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.GetMessage()))}");

        // Should not have compilation warnings in generated code
        var generatedWarnings = result.CompilationDiagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Warning &&
                        d.Location.SourceTree?.FilePath?.Contains("ServiceCollectionExtensions") == true)
            .ToList();

        generatedWarnings.Should().BeEmpty();

        // Verify registration source was generated
        var registrationSource = result.GetRequiredServiceRegistrationSource();
        registrationSource.Content.Should().NotBeEmpty();
    }

    [Fact]
    public void ConditionalService_ComplexScenarioCompilation_SucceedsWithCorrectOutput()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IPaymentService { }
public interface IEmailService { }
public interface ICacheService { }
public interface INotificationService { }

[ConditionalService(Environment = ""Development"")]

[RegisterAsAll(RegistrationMode.All)]
public partial class DevPaymentService : IPaymentService, INotificationService
{
    [Inject] private readonly IEmailService _emailService;
}

[ConditionalService(Environment = ""Production"")]
[Singleton]
public partial class ProdPaymentService : IPaymentService
{
}

[ConditionalService(ConfigValue = ""Cache:Provider"", Equals = ""Redis"")]
[Singleton]
public partial class RedisCacheService : ICacheService
{
}

[ConditionalService(ConfigValue = ""Cache:Provider"", NotEquals = ""Redis"")]
[Singleton]
public partial class MemoryCacheService : ICacheService
{
}

[Scoped] // Regular unconditional service
public partial class EmailService : IEmailService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Should generate comprehensive registration method
        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Assert
        // Should compile successfully
        result.HasErrors.Should()
            .BeFalse(
                $"Generated code should compile without errors. Errors: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.GetMessage()))}");

        // Should generate constructors for services with dependencies
        var devPaymentConstructor = result.GetRequiredConstructorSource("DevPaymentService");
        devPaymentConstructor.Content.Should().Contain("IEmailService emailService");

        // Should have generated comprehensive registration method
        // Verify basic conditional registration generation works
        registrationSource.Content.Should().Contain("string.Equals(");
        registrationSource.Content.Should().Contain("StringComparison.OrdinalIgnoreCase");
        registrationSource.Content.Should().Contain("environment");
        registrationSource.Content.Should().Contain("configuration");

        // Verify service registrations contain expected patterns
        registrationSource.Content.Should().Contain("AddScoped");
        registrationSource.Content.Should().Contain("AddSingleton");
        registrationSource.Content.Should().Contain("Test.IPaymentService");
        registrationSource.Content.Should().Contain("Test.ICacheService");
        registrationSource.Content.Should().Contain("Test.IEmailService");

        // Generated code should be well-structured
        registrationSource.Content.Should().Contain("return services;");
        // Configuration should be passed as method parameter when conditional services need it
        registrationSource.Content.Should().Contain("IConfiguration");
    }

    #endregion
}
