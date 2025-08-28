namespace IoCTools.Sample.Services;

using Abstractions.Annotations;

/// <summary>
///     Example of ManualService + RegisterAsAll combination for DbContext-like scenarios
/// </summary>
public interface IReadOnlyRepository
{
    Task<string> GetDataAsync();
}

public interface IWriteRepository
{
    Task SaveDataAsync(string data);
}

[RegisterAsAll]
public partial class TestDbContext : IReadOnlyRepository, IWriteRepository
{
    public Task<string> GetDataAsync() => Task.FromResult("data");
    public Task SaveDataAsync(string data) => Task.CompletedTask;
}
