namespace IoCTools.Generator.Tests;

public class RegisterAsInstanceSharingBugTest
{
    [Fact]
    public void RegisterAs_InstanceSharingShared_ShouldGenerateOnlyFactoryPattern()
    {
        // This test reproduces the reported bug where InstanceSharing.Shared 
        // should NOT register the concrete class when it's manually registered elsewhere (like EF Core DbContext)
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test
{
    public interface ITransactionService
    {
        void BeginTransaction();
        void CommitTransaction();
    }

    // This represents a scenario like EF Core DbContext where the concrete class
    // is registered elsewhere (AddDbContext) and we just need interface forwarding
    [RegisterAs<ITransactionService>(InstanceSharing.Shared)]
    public partial class DeltaDbContext : ITransactionService
    {
        public void BeginTransaction() => throw new System.NotImplementedException();
        public void CommitTransaction() => throw new System.NotImplementedException();
    }
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Expected behavior for InstanceSharing.Shared:
        // ONLY interface factory registration - NO concrete registration
        // services.AddScoped<ITransactionService>(provider => provider.GetRequiredService<DeltaDbContext>());

        Assert.Contains(
            "services.AddScoped<global::Test.ITransactionService>(provider => provider.GetRequiredService<global::Test.DeltaDbContext>());",
            registrationSource.Content);

        // Should NOT register the concrete class at all - that's handled elsewhere (like EF Core AddDbContext)
        Assert.DoesNotContain("services.AddScoped<global::Test.DeltaDbContext>", registrationSource.Content);
        Assert.DoesNotContain("services.AddScoped<global::Test.DeltaDbContext,", registrationSource.Content);
    }

    [Fact]
    public void RegisterAs_InstanceSharingSeparate_ShouldGenerateDirectRegistrations()
    {
        // This test verifies InstanceSharing.Separate works correctly (should be separate instances)
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test
{
    public interface ITransactionService
    {
        void BeginTransaction();
        void CommitTransaction();
    }

    [RegisterAs<ITransactionService>(InstanceSharing.Separate)]
    public partial class BusinessService : ITransactionService
    {
        public void BeginTransaction() => throw new System.NotImplementedException();
        public void CommitTransaction() => throw new System.NotImplementedException();
    }
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // For InstanceSharing.Separate, direct registrations are correct
        Assert.Contains("services.AddScoped<global::Test.BusinessService, global::Test.BusinessService>();",
            registrationSource.Content);
        Assert.Contains("services.AddScoped<global::Test.ITransactionService, global::Test.BusinessService>();",
            registrationSource.Content);

        // Should NOT contain factory pattern for InstanceSharing.Separate
        Assert.DoesNotContain("provider.GetRequiredService<global::Test.BusinessService>", registrationSource.Content);
    }

    [Fact]
    public void RegisterAs_InstanceSharingShared_WithExplicitLifetime_ShouldGenerateConcreteAndFactoryPattern()
    {
        // CRITICAL TEST: Services with explicit lifetime attributes ([Scoped], [Singleton], [Transient])
        // + RegisterAs<T>(InstanceSharing.Shared) should generate BOTH concrete AND factory registrations
        var source = @"
using IoCTools.Abstractions.Annotations;
using IoCTools.Abstractions.Enumerations;

namespace Test
{
    public interface IUserService
    {
        void GetUser();
    }

    public interface INotificationService  
    {
        void SendNotification();
    }

    [Scoped]  // Explicit lifetime attribute
    [RegisterAs<IUserService, INotificationService>(InstanceSharing.Shared)]
    public partial class UserNotificationService : IUserService, INotificationService
    {
        [Inject] private readonly System.IServiceProvider _provider;
        
        public void GetUser() => throw new System.NotImplementedException();
        public void SendNotification() => throw new System.NotImplementedException();
    }
}";

        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);
        Assert.False(result.HasErrors,
            $"Compilation errors: {string.Join(", ", result.Diagnostics.Select(d => d.GetMessage()))}");

        var registrationSource = result.GetServiceRegistrationSource();
        Assert.NotNull(registrationSource);

        // Expected behavior for InstanceSharing.Shared + explicit lifetime:
        // 1. Concrete registration because service has [Scoped] attribute  
        // 2. Factory registrations for interfaces to share the same instance

        // Should register concrete class because of [Scoped] attribute
        Assert.Contains("services.AddScoped<global::Test.UserNotificationService>();", registrationSource.Content);

        // Should register factory patterns for interfaces
        Assert.Contains(
            "services.AddScoped<global::Test.IUserService>(provider => provider.GetRequiredService<global::Test.UserNotificationService>());",
            registrationSource.Content);
        Assert.Contains(
            "services.AddScoped<global::Test.INotificationService>(provider => provider.GetRequiredService<global::Test.UserNotificationService>());",
            registrationSource.Content);
    }
}
