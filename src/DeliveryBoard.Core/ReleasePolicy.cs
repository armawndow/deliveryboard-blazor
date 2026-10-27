namespace DeliveryBoard.Core;
public enum GateState { Pending, Passed, Failed }
public sealed record Gate(string Id, string Label, bool Critical, int Weight, GateState State, string Evidence);
public sealed record Verdict(string Decision, decimal ReadinessPercent, IReadOnlyList<string> Reasons);
public sealed record ReleaseBoard(string Name, long Revision, IReadOnlyList<Gate> Gates);
public sealed record UpdateResult(bool Applied, ReleaseBoard Board);
public interface IBoardRepository
{
    Task<ReleaseBoard> ReadAsync(CancellationToken cancellationToken = default);
    Task<UpdateResult> UpdateAsync(string gateId, GateState state, string evidence, long expectedRevision, CancellationToken cancellationToken = default);
}
public static class ReleasePolicy
{
    public static Verdict Evaluate(IEnumerable<Gate> source)
    {
        var gates = source.ToArray();
        if (gates.Length == 0) throw new ArgumentException("At least one release gate is required.");
        if (gates.Any(g => string.IsNullOrWhiteSpace(g.Id) || string.IsNullOrWhiteSpace(g.Label))) throw new ArgumentException("Every gate requires an identifier and label.");
        if (gates.Select(g => g.Id).Distinct(StringComparer.Ordinal).Count() != gates.Length) throw new ArgumentException("Gate identifiers must be unique.");
        if (gates.Any(g => g.Weight is < 1 or > 100 || !Enum.IsDefined(g.State))) throw new ArgumentException("Gate weights must be 1-100 and statuses must be recognized.");
        if (gates.Any(g => g.Evidence is null || g.Evidence.Length > 1000 || (g.State == GateState.Passed && string.IsNullOrWhiteSpace(g.Evidence)))) throw new ArgumentException("Passing gates require evidence; evidence is limited to 1,000 characters.");
        var reasons = gates.Where(g => g.State == GateState.Failed || (g.Critical && g.State != GateState.Passed)).Select(g => $"{g.Label}: {g.State.ToString().ToLowerInvariant()}").ToArray();
        var score = decimal.Round(100m * gates.Where(g => g.State == GateState.Passed).Sum(g => (long)g.Weight) / gates.Sum(g => (long)g.Weight), 1);
        return new(reasons.Length > 0 ? "Blocked" : gates.Any(g => g.State == GateState.Pending) ? "Hold" : "Ready", score, reasons);
    }
}
