using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;

namespace IoCTools.Generator.Tests;

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
[Service]
public partial class DevService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should have proper extension method signature
        Assert.Contains("public static IServiceCollection", registrationSource.Content);
        Assert.Contains("this IServiceCollection services", registrationSource.Content);

        // Method name should follow naming convention - based on the test assembly name "TestAssembly"
        var hasCorrectMethodName = registrationSource.Content.Contains("AddTestAssemblyRegisteredServices");
        Assert.True(hasCorrectMethodName, "Should have properly named registration method");

        // Should return IServiceCollection for chaining
        Assert.Contains("return services;", registrationSource.Content);
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
[Service]
public partial class ConditionalService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);


        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);


        // Should declare environment variable once when environment conditions are present
        Assert.Contains("var environment = Environment.GetEnvironmentVariable(\"ASPNETCORE_ENVIRONMENT\") ?? \"\"",
            registrationSource.Content);

        // Should use IConfiguration parameter when configuration conditions are present (no local variable)
        Assert.Contains("IConfiguration configuration", registrationSource.Content);

        // Variable names should be consistent
        var environmentUsageCount = Regex.Matches(registrationSource.Content, @"\benvironment\b").Count;
        var configurationUsageCount = Regex.Matches(registrationSource.Content, @"\bconfiguration\b").Count;

        Assert.True(environmentUsageCount >= 2, "Environment variable should be declared and used");
        Assert.True(configurationUsageCount >= 2, "Configuration variable should be declared and used as parameter");
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
[Service]
public partial class DevService : ITestService
{
}

[ConditionalService(Environment = ""Production"")]
[Service]
public partial class ProdService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should use proper if-else structure with StringComparison
        Assert.Contains("if (string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase))", registrationSource.Content);
        Assert.Contains("else if (string.Equals(environment, \"Production\", StringComparison.OrdinalIgnoreCase))", registrationSource.Content);

        // Should have proper braces
        var ifCount = Regex.Matches(registrationSource.Content, @"\bif\s*\(").Count;
        var openBraceCount = registrationSource.Content.Count(c => c == '{');
        var closeBraceCount = registrationSource.Content.Count(c => c == '}');

        Assert.Equal(openBraceCount, closeBraceCount);
        Assert.True(ifCount >= 1, "Should have conditional statements");
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
[Service]
public partial class MultiEnvService : ITestService
{
}

[ConditionalService(Environment = ""Production"", ConfigValue = ""Feature:Enabled"", Equals = ""true"")]
[Service]
public partial class CombinedConditionService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should use OR for multiple environments with StringComparison
        var hasOrCondition =
            registrationSource.Content.Contains("string.Equals(environment, \"Development\", StringComparison.OrdinalIgnoreCase) || string.Equals(environment, \"Testing\", StringComparison.OrdinalIgnoreCase)");
        Assert.True(hasOrCondition, "Should use OR logic for multiple environments with proper string comparison");

        // Should use AND for combined conditions with proper configuration access patterns
        var hasEnvironmentCondition = registrationSource.Content.Contains("string.Equals(environment,");
        var hasConfigurationCondition = registrationSource.Content.Contains("string.Equals(configuration[") ||
                                       registrationSource.Content.Contains("configuration.GetValue<string>");
        Assert.True(hasEnvironmentCondition && hasConfigurationCondition, "Should generate both environment and configuration condition checks");

        // Parentheses should be balanced
        var openParenCount = registrationSource.Content.Count(c => c == '(');
        var closeParenCount = registrationSource.Content.Count(c => c == ')');
        Assert.Equal(openParenCount, closeParenCount);
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
[Service]
public partial class ConditionalService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should include necessary using statements
        Assert.Contains("using Microsoft.Extensions.DependencyInjection;", registrationSource.Content);
        Assert.Contains("using Microsoft.Extensions.Configuration;", registrationSource.Content);
        Assert.Contains("using System;", registrationSource.Content);

        // Should have nullable enable directive
        Assert.Contains("#nullable enable", registrationSource.Content);

        // Should not have duplicate using statements
        var usingDICount = Regex
            .Matches(registrationSource.Content, @"using Microsoft\.Extensions\.DependencyInjection;").Count;
        var usingConfigCount = Regex.Matches(registrationSource.Content, @"using Microsoft\.Extensions\.Configuration;")
            .Count;

        Assert.Equal(1, usingDICount);
        Assert.Equal(1, usingConfigCount);
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
[Service]
public partial class DevService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should use appropriate namespace (generator uses the compilation assembly name TestAssembly)
        var hasCorrectNamespace = registrationSource.Content.Contains("namespace TestAssembly") ||
                                  registrationSource.Content.Contains("namespace TestAssembly;");

        Assert.True(hasCorrectNamespace,
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
[Service(Lifetime.Singleton)]
public partial class SingletonService : ITestService
{
}

[ConditionalService(Environment = ""Production"")]
[Service(Lifetime.Transient)]
public partial class TransientService : ITestService
{
}

[ConditionalService(Environment = ""Testing"")]
[Service] // Default Scoped
public partial class ScopedService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should generate correct registration method calls (conditional services use simplified type names)
        Assert.Contains("services.AddSingleton<Test.ITestService, Test.SingletonService>()", registrationSource.Content);
        Assert.Contains("services.AddTransient<Test.ITestService, Test.TransientService>()", registrationSource.Content);
        Assert.Contains("services.AddScoped<Test.ITestService, Test.ScopedService>()", registrationSource.Content);

        // Should have proper semicolons with simplified type names
        Assert.Contains("AddSingleton<Test.ITestService, Test.SingletonService>();", registrationSource.Content);
        Assert.Contains("AddTransient<Test.ITestService, Test.TransientService>();", registrationSource.Content);
        Assert.Contains("AddScoped<Test.ITestService, Test.ScopedService>();", registrationSource.Content);
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
[Service]
public partial class InMemoryRepository<T> : IRepository<T>
{
}

[ConditionalService(Environment = ""Production"")]
[Service]
public partial class DatabaseRepository<T> : IRepository<T>
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should handle generic services correctly with simplified type names
        Assert.Contains("services.AddScoped(typeof(Test.IRepository<>), typeof(Test.InMemoryRepository<>))",
            registrationSource.Content);
        Assert.Contains("services.AddScoped(typeof(Test.IRepository<>), typeof(Test.DatabaseRepository<>))",
            registrationSource.Content);

        // Should use typeof for open generics with simplified type names
        Assert.Contains("typeof(Test.IRepository<>)", registrationSource.Content);
        Assert.Contains("typeof(Test.InMemoryRepository<>)", registrationSource.Content);
        Assert.Contains("typeof(Test.DatabaseRepository<>)", registrationSource.Content);
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
[Service]
[RegisterAsAll(RegistrationMode.All)]
public partial class MultiInterfaceService : IService1, IService2
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should register all interfaces when using RegisterAsAll (using factory pattern for shared instances with simplified type names)
        Assert.Contains("AddScoped<Test.IService1>(provider => provider.GetRequiredService<Test.MultiInterfaceService>())",
            registrationSource.Content);
        Assert.Contains("AddScoped<Test.IService2>(provider => provider.GetRequiredService<Test.MultiInterfaceService>())",
            registrationSource.Content);
        Assert.Contains("AddScoped<Test.MultiInterfaceService, Test.MultiInterfaceService>", registrationSource.Content);
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
[Service]
public partial class EscapedStringService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should properly escape quotes in environment name using string.Equals
        Assert.Contains("\"Dev\\\"Test\"", registrationSource.Content);

        // Should properly escape backslashes in config key
        Assert.Contains("\"Path\\\\With\\\\Backslashes\"", registrationSource.Content);

        // Should properly escape special characters in config value
        Assert.Contains("\"Value\\nWith\\tEscapes\"", registrationSource.Content);

        // Generated code should compile without syntax errors
        Assert.False(result.HasErrors);
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
[Service]
public partial class UnicodeService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should handle Unicode characters correctly (basic validation that code was generated)
        // Note: Conditional services may not generate registration code if no valid registrations exist
        Assert.NotEmpty(registrationSource.Content);
        Assert.Contains("namespace", registrationSource.Content);
        Assert.Contains("#nullable enable", registrationSource.Content);

        // Generated code should compile without encoding issues
        Assert.False(result.HasErrors);
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
[Service]
public partial class DevService : ITestService
{
}

[ConditionalService(Environment = ""Production"")]
[Service]
public partial class ProdService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should use proper string comparison methods
        Assert.Contains("string.Equals(", registrationSource.Content);
        Assert.Contains("StringComparison.OrdinalIgnoreCase", registrationSource.Content);

        // Should have consistent brace style
        var openBraces = Regex.Matches(registrationSource.Content, @"\{").Count;
        var closeBraces = Regex.Matches(registrationSource.Content, @"\}").Count;
        Assert.Equal(openBraces, closeBraces);
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
[Service]
public partial class DevService : ITestService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Generated code should include nullable enable directive
        Assert.Contains("#nullable enable", registrationSource.Content);
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
[Service(Lifetime.Singleton)]
public partial class DevService : ITestService
{
}

[ConditionalService(ConfigValue = ""Feature:Enabled"", Equals = ""true"")]
[Service]
public partial class FeatureService : ITestService
{
}

[ConditionalService(Environment = ""Production"", ConfigValue = ""Cache:UseRedis"", Equals = ""true"")]
[Service]
public partial class CacheService : IRepository<string>
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        // Should compile without any errors
        Assert.False(result.HasErrors,
            $"Generated code should compile without errors. Errors: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.GetMessage()))}");

        // Should not have compilation warnings in generated code
        var generatedWarnings = result.CompilationDiagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Warning &&
                        d.Location.SourceTree?.FilePath?.Contains("ServiceCollectionExtensions") == true)
            .ToList();

        Assert.Empty(generatedWarnings);

        // Verify registration source was generated
        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);
        Assert.NotEmpty(registrationSource.Content);
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
[Service]
[RegisterAsAll(RegistrationMode.All)]
public partial class DevPaymentService : IPaymentService, INotificationService
{
    [Inject] private readonly IEmailService _emailService;
}

[ConditionalService(Environment = ""Production"")]
[Service(Lifetime.Singleton)]
public partial class ProdPaymentService : IPaymentService
{
}

[ConditionalService(ConfigValue = ""Cache:Provider"", Equals = ""Redis"")]
[Service(Lifetime.Singleton)]
public partial class RedisCacheService : ICacheService
{
}

[ConditionalService(ConfigValue = ""Cache:Provider"", NotEquals = ""Redis"")]
[Service(Lifetime.Singleton)]
public partial class MemoryCacheService : ICacheService
{
}

[Service] // Regular unconditional service
public partial class EmailService : IEmailService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Should generate comprehensive registration method
        var registrationSource = result.GetServiceRegistrationSource();

        // Assert
        // Should compile successfully
        Assert.False(result.HasErrors,
            $"Generated code should compile without errors. Errors: {string.Join(", ", result.CompilationDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.GetMessage()))}");

        // Should generate constructors for services with dependencies
        var devPaymentConstructor = result.GetConstructorSource("DevPaymentService");
        Assert.NotNull(devPaymentConstructor);
        Assert.Contains("IEmailService emailService", devPaymentConstructor.Content);

        // Should have generated comprehensive registration method
        Assert.NotNull(registrationSource);

        // Verify basic conditional registration generation works
        Assert.Contains("string.Equals(", registrationSource.Content);
        Assert.Contains("StringComparison.OrdinalIgnoreCase", registrationSource.Content);
        Assert.Contains("environment", registrationSource.Content);
        Assert.Contains("configuration", registrationSource.Content);

        // Verify service registrations contain expected patterns
        Assert.Contains("AddScoped", registrationSource.Content);
        Assert.Contains("AddSingleton", registrationSource.Content);
        Assert.Contains("Test.IPaymentService", registrationSource.Content);
        Assert.Contains("Test.ICacheService", registrationSource.Content);
        Assert.Contains("Test.IEmailService", registrationSource.Content);

        // Generated code should be well-structured
        Assert.Contains("return services;", registrationSource.Content);
        // Configuration should be passed as method parameter when conditional services need it
        Assert.Contains("IConfiguration", registrationSource.Content);
    }

    #endregion
}