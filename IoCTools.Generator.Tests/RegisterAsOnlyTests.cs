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
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should register both the concrete class and interface
        Assert.Contains("services.AddScoped<global::Test.UserService, global::Test.UserService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IUserService, global::Test.UserService>",
            registrationSource.Content);

        // Should not generate any constructor (no Inject fields)
        var constructorSource = result.GetConstructorSource("UserService");
        Assert.Null(constructorSource); // No constructor generation expected
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
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should register concrete class and specified interfaces only
        Assert.Contains("services.AddScoped<global::Test.MultiService, global::Test.MultiService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IUserService, global::Test.MultiService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.INotificationService, global::Test.MultiService>",
            registrationSource.Content);

        // Should NOT register IValidationService (not specified in RegisterAs)
        Assert.DoesNotContain("IValidationService", registrationSource.Content);

        // No constructor should be generated
        var constructorSource = result.GetConstructorSource("MultiService");
        Assert.Null(constructorSource);
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
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // RegisterAs-only service with InstanceSharing.Shared should NOT generate concrete registration 
        // (EF Core scenario - concrete class registered elsewhere)
        Assert.DoesNotContain("services.AddScoped<global::Test.SharedService", registrationSource.Content);

        // Should generate ONLY factory patterns for interfaces
        Assert.Contains(
            "services.AddScoped<global::Test.IUserService>(provider => provider.GetRequiredService<global::Test.SharedService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.INotificationService>(provider => provider.GetRequiredService<global::Test.SharedService>())",
            registrationSource.Content);

        // No constructor should be generated
        var constructorSource = result.GetConstructorSource("SharedService");
        Assert.Null(constructorSource);
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
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should register generic interface correctly
        Assert.Contains("services.AddScoped<global::Test.UserRepository, global::Test.UserRepository>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IRepository<global::Test.User>, global::Test.UserRepository>",
            registrationSource.Content);

        // No constructor should be generated
        var constructorSource = result.GetConstructorSource("UserRepository");
        Assert.Null(constructorSource);
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
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var registrationSource = result.GetServiceRegistrationSource();

        // Abstract classes should not be registered even with RegisterAs
        if (registrationSource != null) Assert.DoesNotContain("AbstractBaseService", registrationSource.Content);
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
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var registrationSource = result.GetServiceRegistrationSource();

        // Static classes should not be registered even with RegisterAs
        if (registrationSource != null) Assert.DoesNotContain("UtilityService", registrationSource.Content);
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
        Assert.False(registerAsOnlyResult.HasErrors);
        Assert.False(explicitScopedResult.HasErrors);

        var registerAsOnlyRegistration = registerAsOnlyResult.GetServiceRegistrationSource();
        var explicitScopedRegistration = explicitScopedResult.GetServiceRegistrationSource();

        Assert.NotNull(registerAsOnlyRegistration);
        Assert.NotNull(explicitScopedRegistration);

        // Both should generate equivalent Scoped registrations
        Assert.Contains("AddScoped<global::Test.TestService1, global::Test.TestService1>",
            registerAsOnlyRegistration.Content);
        Assert.Contains("AddScoped<global::Test.ITestService, global::Test.TestService1>",
            registerAsOnlyRegistration.Content);

        Assert.Contains("AddScoped<global::Test.TestService2, global::Test.TestService2>",
            explicitScopedRegistration.Content);
        Assert.Contains("AddScoped<global::Test.ITestService, global::Test.TestService2>",
            explicitScopedRegistration.Content);
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
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should register even though not partial (no constructor generation needed)
        Assert.Contains("services.AddScoped<global::Test.SimpleService, global::Test.SimpleService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.ISimpleService, global::Test.SimpleService>",
            registrationSource.Content);

        // No constructor should be generated (not partial)
        var constructorSource = result.GetConstructorSource("SimpleService");
        Assert.Null(constructorSource);
    }
}
