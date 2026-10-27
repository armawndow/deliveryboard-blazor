using DeliveryBoard.Core;

namespace DeliveryBoard.Tests;

public class ReleasePolicyTests
{
    private static Gate Gate(string id, GateState state, bool critical = false, int weight = 1) =>
        new(id, id, critical, weight, state, state == GateState.Passed ? "Test evidence" : "");

    [Fact] public void AHighScoreCannotOverrideAPendingCriticalGate()
    {
        var verdict = ReleasePolicy.Evaluate([Gate("a", GateState.Passed, weight: 99), Gate("b", GateState.Pending, true)]);
        Assert.Equal(99m, verdict.ReadinessPercent);
        Assert.Equal("Blocked", verdict.Decision);
    }
    [Fact] public void FailedNonCriticalGateStillPreventsRelease() =>
        Assert.Equal("Blocked", ReleasePolicy.Evaluate([Gate("a", GateState.Failed)]).Decision);
    [Fact] public void PendingOptionalGateHoldsRelease() =>
        Assert.Equal("Hold", ReleasePolicy.Evaluate([Gate("a", GateState.Pending)]).Decision);
    [Fact] public void AllPassedGatesPermitRelease() =>
        Assert.Equal("Ready", ReleasePolicy.Evaluate([Gate("a", GateState.Passed, true), Gate("b", GateState.Passed)]).Decision);
    [Fact] public void EmptyGateSetCannotBeReady() =>
        Assert.Throws<ArgumentException>(() => ReleasePolicy.Evaluate([]));
    [Fact] public void DuplicateGateIdsAreRejected() =>
        Assert.Throws<ArgumentException>(() => ReleasePolicy.Evaluate([Gate("a", GateState.Pending), Gate("a", GateState.Passed)]));
    [Theory] [InlineData(0)] [InlineData(-1)] [InlineData(101)]
    public void InvalidWeightsAreRejected(int weight) =>
        Assert.Throws<ArgumentException>(() => ReleasePolicy.Evaluate([Gate("a", GateState.Passed, weight: weight)]));
    [Fact] public void PassingRequiresEvidence() =>
        Assert.Throws<ArgumentException>(() => ReleasePolicy.Evaluate([new Gate("a", "A", true, 1, GateState.Passed, " ")]));
    [Fact] public void UnknownStatusCannotBeTreatedAsPending() =>
        Assert.Throws<ArgumentException>(() => ReleasePolicy.Evaluate([Gate("a", (GateState)99)]));
    [Fact] public void ReadinessUsesWeightsRatherThanGateCount()
    {
        var verdict = ReleasePolicy.Evaluate([Gate("a", GateState.Passed, weight: 3), Gate("b", GateState.Pending)]);
        Assert.Equal(75m, verdict.ReadinessPercent);
    }
}
