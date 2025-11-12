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

[Scoped]
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Shared)]
public partial class SharedUserNotificationService : IUserService, INotificationService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Verify shared instance pattern: concrete class with single parameter registration, interfaces with factory lambdas
        registrationSource.Content.Should().Contain("AddScoped<global::Test.SharedUserNotificationService>");
        registrationSource.Content.Should()
            .Contain(
                "AddScoped<global::Test.IUserService>(provider => provider.GetRequiredService<global::Test.SharedUserNotificationService>())");
        registrationSource.Content.Should()
            .Contain(
                "AddScoped<global::Test.INotificationService>(provider => provider.GetRequiredService<global::Test.SharedUserNotificationService>())");
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

[Scoped]
[RegisterAsAll(RegistrationMode.All, InstanceSharing.Separate)]
public partial class SeparateUserNotificationService : IUserService, INotificationService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        result.HasErrors.Should().BeFalse();

        var registrationSource = result.GetRequiredServiceRegistrationSource();

        // Verify separate instance pattern: direct registrations (generator correctly uses single parameter form)
        registrationSource.Content.Should().Contain("AddScoped<global::Test.SeparateUserNotificationService>");
        registrationSource.Content.Should()
            .Contain("AddScoped<global::Test.IUserService, global::Test.SeparateUserNotificationService>");
        registrationSource.Content.Should()
            .Contain("AddScoped<global::Test.INotificationService, global::Test.SeparateUserNotificationService>");
    }
}
