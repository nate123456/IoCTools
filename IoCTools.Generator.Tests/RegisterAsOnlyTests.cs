namespace IoCTools.Generator.Tests;

/// <summary>
///     Tests for services that use ONLY [RegisterAs] attributes without any other IoC concepts.
///     These services should still be registered but get no custom constructor generation.
///     This covers the scenario where RegisterAs is the sole registration mechanism.
/// </summary>
public class RegisterAsOnlyTests
{
    [Fact]
    public void RegisterAsOnly_SingleInterface_ShouldRegisterWithoutCustomConstructor()
    {
        // Arrange - Service with ONLY RegisterAs, no [Inject], no [DependsOn], no explicit lifetime
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IUserService 
{ 
    string GetUserName(); 
}

[RegisterAs<IUserService>]
public partial class UserService : IUserService
{
    public string GetUserName() => ""TestUser"";
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should()
            .BeFalse($"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var registrationContent = result.GetServiceRegistrationText();

        // Should register both the concrete class and interface
        registrationContent.Should().Contain("services.AddScoped<global::Test.UserService, global::Test.UserService>");
        registrationContent.Should().Contain("services.AddScoped<global::Test.IUserService, global::Test.UserService>");

        // Should not generate any constructor (no Inject fields)
        var constructorSource = result.GetConstructorSource("UserService");
        constructorSource.Should().BeNull(); // No constructor generation expected
    }

    [Fact]
    public void RegisterAsOnly_MultipleInterfaces_ShouldRegisterAllWithoutCustomConstructor()
    {
        // Arrange - Service with ONLY RegisterAs for multiple interfaces
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IUserService { }
public interface INotificationService { }
public interface IValidationService { }

[RegisterAs<IUserService, INotificationService>]
public partial class MultiService : IUserService, INotificationService, IValidationService
{
    // No IoC fields, no dependencies, just business logic
    public void DoWork() { }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should()
            .BeFalse($"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var registrationContent = result.GetServiceRegistrationText();

        // Should register concrete class and specified interfaces only
        registrationContent.Should()
            .Contain("services.AddScoped<global::Test.MultiService, global::Test.MultiService>");
        registrationContent.Should()
            .Contain("services.AddScoped<global::Test.IUserService, global::Test.MultiService>");
        registrationContent.Should()
            .Contain("services.AddScoped<global::Test.INotificationService, global::Test.MultiService>");

        // Should NOT register IValidationService (not specified in RegisterAs)
        registrationContent.Should().NotContain("IValidationService");

        // No constructor should be generated
        var constructorSource = result.GetConstructorSource("MultiService");
        constructorSource.Should().BeNull();
    }

    [Fact]
    public void RegisterAsOnly_WithInstanceSharingShared_ShouldCreateFactoryPattern()
    {
        // Arrange - RegisterAs-only service with InstanceSharing.Shared
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IUserService { }
public interface INotificationService { }

[RegisterAs<IUserService, INotificationService>(InstanceSharing.Shared)]
public partial class SharedService : IUserService, INotificationService
{
    // Pure business logic, no IoC dependencies
    public void ProcessData() { }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should()
            .BeFalse($"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var registrationContent = result.GetServiceRegistrationText();

        // RegisterAs-only service with InstanceSharing.Shared should NOT generate concrete registration 
        // (EF Core scenario - concrete class registered elsewhere)
        registrationContent.Should().NotContain("services.AddScoped<global::Test.SharedService");

        // Should generate ONLY factory patterns for interfaces
        registrationContent.Should()
            .Contain(
                "services.AddScoped<global::Test.IUserService>(provider => provider.GetRequiredService<global::Test.SharedService>())");
        registrationContent.Should()
            .Contain(
                "services.AddScoped<global::Test.INotificationService>(provider => provider.GetRequiredService<global::Test.SharedService>())");

        // No constructor should be generated
        var constructorSource = result.GetConstructorSource("SharedService");
        constructorSource.Should().BeNull();
    }

    [Fact]
    public void RegisterAsOnly_WithGenericInterface_ShouldRegisterCorrectly()
    {
        // Arrange - RegisterAs-only service with generic interface
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IRepository<T> where T : class 
{ 
    T GetById(int id); 
}

public class User 
{ 
    public int Id { get; set; } 
    public string Name { get; set; } = string.Empty; 
}

[RegisterAs<IRepository<User>>]
public partial class UserRepository : IRepository<User>
{
    public User GetById(int id) => new User { Id = id, Name = $""User{id}"" };
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should()
            .BeFalse($"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var registrationContent = result.GetServiceRegistrationText();

        // Should register generic interface correctly
        registrationContent.Should()
            .Contain("services.AddScoped<global::Test.UserRepository, global::Test.UserRepository>");
        registrationContent.Should()
            .Contain("services.AddScoped<global::Test.IRepository<global::Test.User>, global::Test.UserRepository>");

        // No constructor should be generated
        var constructorSource = result.GetConstructorSource("UserRepository");
        constructorSource.Should().BeNull();
    }

    [Fact]
    public void RegisterAsOnly_AbstractClass_ShouldNotRegister()
    {
        // Arrange - Abstract class with RegisterAs should not be registered
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IBaseService { }

[RegisterAs<IBaseService>]
public abstract partial class AbstractBaseService : IBaseService
{
    public abstract void DoWork();
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should()
            .BeFalse($"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var registrationContent = result.GetOptionalServiceRegistrationText();

        // Abstract classes should not be registered even with RegisterAs
        if (registrationContent is not null) registrationContent.Should().NotContain("AbstractBaseService");
    }

    [Fact]
    public void RegisterAsOnly_StaticClass_ShouldNotRegister()
    {
        // Arrange - Static class with RegisterAs should not be registered (and doesn't implement interface)
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IUtilityService { }

[RegisterAs<IUtilityService>]
public static partial class UtilityService
{
    public static void DoWork() { }
}";

        // Act  
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Static classes should be filtered out during service discovery
        result.HasErrors.Should()
            .BeFalse($"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var registrationContent = result.GetOptionalServiceRegistrationText();

        // Static classes should not be registered even with RegisterAs
        if (registrationContent is not null) registrationContent.Should().NotContain("UtilityService");
    }

    [Fact]
    public void RegisterAsOnly_CompareWithExplicitScoped_ShouldHaveSameRegistration()
    {
        // Arrange - Compare RegisterAs-only vs RegisterAs + explicit [Scoped]
        var registerAsOnlySource = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

[RegisterAs<ITestService>]
public partial class TestService1 : ITestService
{
    public void DoWork() { }
}";

        var explicitScopedSource = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ITestService { }

[Scoped]
[RegisterAs<ITestService>]
public partial class TestService2 : ITestService  
{
    public void DoWork() { }
}";

        // Act
        var registerAsOnlyResult = SourceGeneratorTestHelper.CompileWithGenerator(registerAsOnlySource);
        var explicitScopedResult = SourceGeneratorTestHelper.CompileWithGenerator(explicitScopedSource);

        // Assert both compile
        registerAsOnlyResult.HasErrors.Should().BeFalse();
        explicitScopedResult.HasErrors.Should().BeFalse();

        var registerAsOnlyRegistration = registerAsOnlyResult.GetServiceRegistrationText();
        var explicitScopedRegistration = explicitScopedResult.GetServiceRegistrationText();

        // Both should generate equivalent Scoped registrations
        registerAsOnlyRegistration.Should().Contain("AddScoped<global::Test.TestService1, global::Test.TestService1>");
        registerAsOnlyRegistration.Should().Contain("AddScoped<global::Test.ITestService, global::Test.TestService1>");

        explicitScopedRegistration.Should().Contain("AddScoped<global::Test.TestService2, global::Test.TestService2>");
        explicitScopedRegistration.Should().Contain("AddScoped<global::Test.ITestService, global::Test.TestService2>");
    }

    [Fact]
    public void RegisterAsOnly_NonPartialClass_ShouldStillRegister()
    {
        // Arrange - Non-partial class with RegisterAs (no constructor generation needed)
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface ISimpleService { }

[RegisterAs<ISimpleService>]
public class SimpleService : ISimpleService
{
    // Not partial, no IoC dependencies, should still register
    public void DoWork() { }
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should()
            .BeFalse($"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Should register even though not partial (no constructor generation needed)
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.SimpleService, global::Test.SimpleService>");
        registrationSource.Content.Should()
            .Contain("services.AddScoped<global::Test.ISimpleService, global::Test.SimpleService>");

        // No constructor should be generated (not partial)
        var constructorSource = result.GetConstructorSource("SimpleService");
        constructorSource.Should().BeNull();
    }
}
