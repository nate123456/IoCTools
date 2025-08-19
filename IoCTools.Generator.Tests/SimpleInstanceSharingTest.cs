namespace IoCTools.Generator.Tests;

/// <summary>
///     Simple test to validate instance sharing registration patterns work correctly
/// </summary>
public class SimpleInstanceSharingTest
{
    [Fact]
    public void SharedInstances_GeneratesFactoryLambdaRegistrations()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IUserService { }
public interface INotificationService { }

[Service(Lifetime.Scoped)]
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class SharedUserNotificationService : IUserService, INotificationService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Verify shared instance pattern: concrete class with direct registration, interfaces with factory lambdas
        Assert.Contains("AddScoped<global::Test.SharedUserNotificationService, global::Test.SharedUserNotificationService>",
            registrationSource.Content);
        Assert.Contains(
            "AddScoped<global::Test.IUserService>(provider => provider.GetRequiredService<global::Test.SharedUserNotificationService>())",
            registrationSource.Content);
        Assert.Contains(
            "AddScoped<global::Test.INotificationService>(provider => provider.GetRequiredService<global::Test.SharedUserNotificationService>())",
            registrationSource.Content);
    }

    [Fact]
    public void SeparateInstances_GeneratesDirectRegistrations()
    {
        // Arrange
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test;

public interface IUserService { }
public interface INotificationService { }

[Service(Lifetime.Scoped)]
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Separate)]
public partial class SeparateUserNotificationService : IUserService, INotificationService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors);

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Verify separate instance pattern: direct registrations (generator correctly uses direct pattern)
        Assert.Contains("AddScoped<global::Test.SeparateUserNotificationService, global::Test.SeparateUserNotificationService>",
            registrationSource.Content);
        Assert.Contains("AddScoped<global::Test.IUserService, global::Test.SeparateUserNotificationService>", registrationSource.Content);
        Assert.Contains("AddScoped<global::Test.INotificationService, global::Test.SeparateUserNotificationService>", registrationSource.Content);
    }
}