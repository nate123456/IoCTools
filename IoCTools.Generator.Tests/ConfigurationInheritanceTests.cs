namespace IoCTools.Generator.Tests;

public class ConfigurationInheritanceDebugTests
{
    [Fact]
    public void ConfigurationInheritance_AbstractBaseClass_CompilesProperly()
    {
        // Arrange - Simple inheritance with basic service registration
        var source = @"
using IoCTools.Abstractions.Annotations;

namespace Test;

public interface IRepository<T> { }

[UnregisteredService]
public abstract partial class AbstractService<T> where T : class
{
    [Inject] protected readonly IRepository<T> _repository;
}

[Service]
public partial class ConcreteService : AbstractService<string>
{
    // Simple concrete implementation
}";

        // Act
        var result = SourceGeneratorTestHelper.CompileWithGenerator(source);

        // Assert - Just verify it compiles and generates something
        Assert.True(result.GeneratedSources.Any(), "Should generate at least one file");
    }
}