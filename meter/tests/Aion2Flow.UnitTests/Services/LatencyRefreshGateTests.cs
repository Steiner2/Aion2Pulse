using Cloris.Aion2Flow.Services;

namespace Cloris.Aion2Flow.Tests.Services;

public sealed class LatencyRefreshGateTests
{
    [Fact]
    public void RefreshesAtFiveSecondsAndImmediatelyAfterConnectionReset()
    {
        var time = new ManualTime();
        var gate = new LatencyRefreshGate(time);
        Assert.True(gate.TryRefresh());
        time.Milliseconds = 4_999;
        Assert.False(gate.TryRefresh());
        time.Milliseconds = 5_000;
        Assert.True(gate.TryRefresh());
        Assert.False(gate.TryRefresh());
        time.Milliseconds = 9_999;
        Assert.False(gate.TryRefresh());
        gate.Reset();
        Assert.True(gate.TryRefresh());
        time.Milliseconds = 1; // A changed/restarted timestamp source must not stall updates.
        Assert.True(gate.TryRefresh());
    }

    private sealed class ManualTime : TimeProvider
    {
        public long Milliseconds { get; set; }
        public override long TimestampFrequency => 1_000;
        public override long GetTimestamp() => Milliseconds;
    }
}
