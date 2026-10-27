using DeliveryBoard.Core;
using DeliveryBoard.Web;
namespace DeliveryBoard.Tests;
public sealed class RepositoryTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "deliveryboard-tests-" + Guid.NewGuid().ToString("N"));
    private string PathName => Path.Combine(_folder, "board.json");
    [Fact] public async Task AcceptedEditSurvivesANewRepositoryInstance()
    {
        using var repository = new FileBoardRepository(PathName);
        Assert.True((await repository.UpdateAsync("tenant", GateState.Passed, "isolation test", 0)).Applied);
        using var reopened = new FileBoardRepository(PathName);
        Assert.Equal(1, (await reopened.ReadAsync()).Revision);
        Assert.Equal("isolation test", (await reopened.ReadAsync()).Gates.Single(g => g.Id == "tenant").Evidence);
    }
    [Fact] public async Task ConcurrentStaleEditsNeverBothWin()
    {
        using var repository = new FileBoardRepository(PathName);
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => repository.UpdateAsync("tenant", GateState.Passed, $"test {i}", 0)));
        Assert.Single(results, r => r.Applied); Assert.Equal(1, (await repository.ReadAsync()).Revision);
    }
    [Fact] public async Task RejectedEditLeavesPersistedBytesUnchanged()
    {
        using var repository = new FileBoardRepository(PathName);
        await repository.UpdateAsync("tenant", GateState.Passed, "evidence", 0);
        var bytes = await File.ReadAllBytesAsync(PathName);
        await Assert.ThrowsAsync<ArgumentException>(() => repository.UpdateAsync("tenant", GateState.Passed, " ", 1));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(PathName));
    }
    [Fact] public async Task UnknownGateDoesNotCreateAFile()
    {
        using var repository = new FileBoardRepository(PathName);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => repository.UpdateAsync("missing", GateState.Pending, "", 0)); Assert.False(File.Exists(PathName));
    }
    [Fact] public async Task CorruptSavedDataIsNotSilentlyReset()
    {
        Directory.CreateDirectory(_folder); await File.WriteAllTextAsync(PathName, "not json");
        using var repository = new FileBoardRepository(PathName);
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => repository.ReadAsync()); Assert.Equal("not json", await File.ReadAllTextAsync(PathName));
    }
    public void Dispose() { if (Directory.Exists(_folder)) Directory.Delete(_folder, true); }
}
