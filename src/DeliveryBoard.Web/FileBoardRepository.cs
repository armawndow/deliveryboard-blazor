using System.Text.Json;
using DeliveryBoard.Core;
namespace DeliveryBoard.Web;
// A single process owns this file. Multi-instance hosting needs a shared database/CAS adapter.
public sealed class FileBoardRepository(string path) : IBoardRepository, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public async Task<ReleaseBoard> ReadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try { return await LoadAsync(cancellationToken); } finally { _gate.Release(); }
    }
    public async Task<UpdateResult> UpdateAsync(string gateId, GateState state, string evidence, long expectedRevision, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var current = await LoadAsync(cancellationToken);
            if (current.Revision != expectedRevision) return new(false, current);
            if (!current.Gates.Any(g => g.Id == gateId)) throw new KeyNotFoundException("Gate not found.");
            var gates = current.Gates.Select(g => g.Id == gateId ? g with { State = state, Evidence = evidence.Trim() } : g).ToArray();
            ReleasePolicy.Evaluate(gates);
            var updated = current with { Revision = checked(current.Revision + 1), Gates = gates };
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(updated, Json), cancellationToken); File.Move(temporary, path, overwrite: true); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return new(true, updated);
        }
        finally { _gate.Release(); }
    }
    private async Task<ReleaseBoard> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return Seed();
        await using var stream = File.OpenRead(path);
        var board = await JsonSerializer.DeserializeAsync<ReleaseBoard>(stream, Json, cancellationToken) ?? throw new InvalidDataException("Saved board is empty.");
        if (board.Revision < 0 || string.IsNullOrWhiteSpace(board.Name)) throw new InvalidDataException("Saved board metadata is invalid.");
        ReleasePolicy.Evaluate(board.Gates); return board;
    }
    private static ReleaseBoard Seed() => new("TenantFlow / Demo release", 0,
    [
        new("tests", "Regression test evidence", true, 30, GateState.Passed, "Illustrative fixture: test report reviewed"),
        new("tenant", "Tenant isolation review", true, 25, GateState.Pending, ""),
        new("migration", "Migration rehearsal", true, 20, GateState.Pending, ""),
        new("rollback", "Rollback runbook", false, 15, GateState.Passed, "Illustrative fixture: rollback steps documented"),
        new("telemetry", "Telemetry and alert review", false, 10, GateState.Pending, "")
    ]);
    public void Dispose() => _gate.Dispose();
}
