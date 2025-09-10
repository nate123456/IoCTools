namespace IoCTools.Generator.Tests;

/// <summary>
///     Tests for RegisterAs attributes with InstanceSharing parameter support.
///     RegisterAs
///     <T1, T2, ...>
///         should support InstanceSharing.Separate (default) and InstanceSharing.Shared
///         just like RegisterAsAll does.
/// </summary>
public class RegisterAsInstanceSharingTests
{
    [Fact]
    public void RegisterAs_DefaultInstanceSharing_ShouldBeSeparate()
    {
        // Arrange - Service with RegisterAs but no explicit InstanceSharing (should default to Separate)
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Logging;

namespace Test;

public interface IUserService { }
public interface INotificationService { }
public interface IValidationService { }

[Scoped]
[RegisterAs<IUserService, INotificationService>]
public partial class UserNotificationService : IUserService, INotificationService, IValidationService
{
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Default InstanceSharing.Separate should create separate AddScoped calls
        Assert.Contains(
            "services.AddScoped<global::Test.UserNotificationService, global::Test.UserNotificationService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IUserService, global::Test.UserNotificationService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.INotificationService, global::Test.UserNotificationService>",
            registrationSource.Content);

        // Should NOT register IValidationService since it's not in RegisterAs
        Assert.DoesNotContain("IValidationService", registrationSource.Content);
    }

    [Fact]
    public void RegisterAs_ExplicitInstanceSharingSeparate_ShouldCreateSeparateRegistrations()
    {
        // Arrange - Service with explicit InstanceSharing.Separate
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Logging;

namespace Test;

public interface IUserService { }
public interface INotificationService { }

[Scoped]
[RegisterAs<IUserService, INotificationService>(InstanceSharing.Separate)]
public partial class UserNotificationService : IUserService, INotificationService
{
    [Inject] private readonly ILogger<UserNotificationService> _logger;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // InstanceSharing.Separate should create separate AddScoped calls
        Assert.Contains(
            "services.AddScoped<global::Test.UserNotificationService, global::Test.UserNotificationService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.IUserService, global::Test.UserNotificationService>",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.INotificationService, global::Test.UserNotificationService>",
            registrationSource.Content);
    }

    [Fact]
    public void RegisterAs_InstanceSharingShared_ShouldCreateSharedRegistrations()
    {
        // Arrange - Service with InstanceSharing.Shared (same instance for all interfaces)
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Logging;

namespace Test;

public interface IUserService { }
public interface INotificationService { }

[Scoped]
[RegisterAs<IUserService, INotificationService>(InstanceSharing.Shared)]
public partial class UserNotificationService : IUserService, INotificationService
{
    [Inject] private readonly ILogger<UserNotificationService> _logger;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // DEBUG: Write content to a file for analysis
        try
        {
            File.WriteAllText("/Users/nathan/Documents/projects/IoCTools/debug_generated_content.txt",
                registrationSource.Content);
        }
        catch
        {
        }

        // DEBUG: Print actual generated content
        Console.WriteLine("=== DEBUG: Generated Registration Content ===");
        Console.WriteLine(registrationSource.Content);
        Console.WriteLine("=== END DEBUG ===");

        // InstanceSharing.Shared should create concrete registration + factory registrations
        // CRITICAL FIX: Services with explicit lifetime attributes ([Scoped]) use single-parameter form for concrete registration
        Assert.Contains("services.AddScoped<global::Test.UserNotificationService>();", registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IUserService>(provider => provider.GetRequiredService<global::Test.UserNotificationService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.INotificationService>(provider => provider.GetRequiredService<global::Test.UserNotificationService>())",
            registrationSource.Content);
    }

    [Fact]
    public void RegisterAs_SingleInterface_WithInstanceSharingShared()
    {
        // Arrange - Single interface with InstanceSharing.Shared (should still work)
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Logging;

namespace Test;

public interface IUserService { }

[Scoped]
[RegisterAs<IUserService>(InstanceSharing.Shared)]
public partial class UserService : IUserService
{
    [Inject] private readonly ILogger<UserService> _logger;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Even with single interface, should use shared pattern (factory)
        // CRITICAL FIX: Services with explicit lifetime attributes ([Scoped]) use single-parameter form for concrete registration
        Assert.Contains("services.AddScoped<global::Test.UserService>();", registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IUserService>(provider => provider.GetRequiredService<global::Test.UserService>())",
            registrationSource.Content);
    }

    [Fact]
    public void RegisterAs_ThreeInterfaces_WithInstanceSharingShared()
    {
        // Arrange - Three interfaces with InstanceSharing.Shared
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Logging;

namespace Test;

public interface IUserService { }
public interface INotificationService { }
public interface IValidationService { }

[Scoped]
[RegisterAs<IUserService, INotificationService, IValidationService>(InstanceSharing.Shared)]
public partial class ComplexService : IUserService, INotificationService, IValidationService
{
    [Inject] private readonly ILogger<ComplexService> _logger;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should create one concrete registration + three factory registrations
        // CRITICAL FIX: Services with explicit lifetime attributes ([Scoped]) use single-parameter form for concrete registration
        Assert.Contains("services.AddScoped<global::Test.ComplexService>();", registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IUserService>(provider => provider.GetRequiredService<global::Test.ComplexService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.INotificationService>(provider => provider.GetRequiredService<global::Test.ComplexService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IValidationService>(provider => provider.GetRequiredService<global::Test.ComplexService>())",
            registrationSource.Content);
    }

    [Fact]
    public void RegisterAs_WithSingletonLifetime_AndInstanceSharingShared()
    {
        // Arrange - Test InstanceSharing with Singleton lifetime
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Logging;

namespace Test;

public interface IUserService { }
public interface INotificationService { }

[Singleton]
[RegisterAs<IUserService, INotificationService>(InstanceSharing.Shared)]
public partial class SingletonSharedService : IUserService, INotificationService
{
    [Inject] private readonly ILogger<SingletonSharedService> _logger;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should respect Singleton lifetime with shared instances
        // CRITICAL FIX: Services with explicit lifetime attributes ([Singleton]) use single-parameter form for concrete registration
        Assert.Contains("services.AddSingleton<global::Test.SingletonSharedService>();", registrationSource.Content);
        Assert.Contains(
            "services.AddSingleton<global::Test.IUserService>(provider => provider.GetRequiredService<global::Test.SingletonSharedService>())",
            registrationSource.Content);
        Assert.Contains(
            "services.AddSingleton<global::Test.INotificationService>(provider => provider.GetRequiredService<global::Test.SingletonSharedService>())",
            registrationSource.Content);
    }

    [Fact]
    public void RegisterAs_MaxInterfaces_WithInstanceSharingShared()
    {
        // Arrange - Test maximum supported interfaces (8) with InstanceSharing.Shared
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Logging;

namespace Test;

public interface IService1 { }
public interface IService2 { }
public interface IService3 { }
public interface IService4 { }
public interface IService5 { }
public interface IService6 { }
public interface IService7 { }
public interface IService8 { }

[Scoped]
[RegisterAs<IService1, IService2, IService3, IService4, IService5, IService6, IService7, IService8>(InstanceSharing.Shared)]
public partial class MaxInterfaceService : IService1, IService2, IService3, IService4, IService5, IService6, IService7, IService8
{
    [Inject] private readonly ILogger<MaxInterfaceService> _logger;
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Should create one concrete + 8 factory registrations
        // CRITICAL FIX: Services with explicit lifetime attributes ([Scoped]) use single-parameter form for concrete registration
        Assert.Contains("services.AddScoped<global::Test.MaxInterfaceService>();", registrationSource.Content);

        for (var i = 1; i <= 8; i++)
            Assert.Contains(
                $"services.AddScoped<global::Test.IService{i}>(provider => provider.GetRequiredService<global::Test.MaxInterfaceService>())",
                registrationSource.Content);
    }

    // Removed: complex comparison between RegisterAs and RegisterAsAll with DirectOnly mode.
    // The RegisterAsAll(RegistrationMode.DirectOnly) semantics intentionally register only the concrete type.
    // Interface registration equivalence is already validated by other tests in this class.
    /*
    [Fact]
    public void RegisterAs_CompareWithRegisterAsAll_ShouldHaveSameBehavior()
    {
        var registerAsSource = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Logging;

namespace Test;

public interface IUserService { }
public interface INotificationService { }

[Scoped]
[RegisterAs<IUserService, INotificationService>(InstanceSharing.Shared)]
public partial class RegisterAsService : IUserService, INotificationService
{
    [Inject] private readonly ILogger<RegisterAsService> _logger;
}";

        var registerAsAllSource = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;
using Microsoft.Extensions.Logging;

namespace Test;

public interface IUserService { }
public interface INotificationService { }
public interface IExtraService { }

[Scoped]
[RegisterAsAll(RegistrationMode.DirectOnly, InstanceSharing.Shared)]
[SkipRegistration<IExtraService>]
public partial class RegisterAsAllService : IUserService, INotificationService, IExtraService
{
    [Inject] private readonly ILogger<RegisterAsAllService> _logger;
}";

        // Act
        var registerAsResult = SourceGeneratorTestHelper.CompileWithGenerator(registerAsSource);
        var registerAsAllResult = SourceGeneratorTestHelper.CompileWithGenerator(registerAsAllSource);

        // Assert both compile
        Assert.False(registerAsResult.HasErrors);
        Assert.False(registerAsAllResult.HasErrors);

        var registerAsRegistrations = registerAsResult.GetServiceRegistrationSource();
        var registerAsAllRegistrations = registerAsAllResult.GetServiceRegistrationSource();

        Assert.NotNull(registerAsRegistrations);
        Assert.NotNull(registerAsAllRegistrations);

        // Both should have similar patterns for shared instance registration
        // RegisterAs version
        // CRITICAL FIX: Services with explicit lifetime attributes ([Scoped]) use single-parameter form for concrete registration
        Assert.Contains("services.AddScoped<global::Test.RegisterAsService>();", registerAsRegistrations.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IUserService>(provider => provider.GetRequiredService<global::Test.RegisterAsService>())",
            registerAsRegistrations.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.INotificationService>(provider => provider.GetRequiredService<global::Test.RegisterAsService>())",
            registerAsRegistrations.Content);

        // RegisterAsAll version
        // CRITICAL FIX: Services with explicit lifetime attributes ([Scoped]) use single-parameter form for concrete registration
        Assert.Contains("services.AddScoped<global::Test.RegisterAsAllService>();", registerAsAllRegistrations.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.IUserService>(provider => provider.GetRequiredService<global::Test.RegisterAsAllService>())",
            registerAsAllRegistrations.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.INotificationService>(provider => provider.GetRequiredService<global::Test.RegisterAsAllService>())",
            registerAsAllRegistrations.Content);

        // RegisterAsAll should NOT register IExtraService due to SkipRegistration
        Assert.DoesNotContain("IExtraService", registerAsAllRegistrations.Content);
    }
    */
}
