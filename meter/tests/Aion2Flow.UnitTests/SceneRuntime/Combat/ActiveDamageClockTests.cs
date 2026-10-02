using Cloris.Aion2Flow.SceneRuntime.Combat;

namespace Cloris.Aion2Flow.Tests.SceneRuntime.Combat;

public sealed class ActiveDamageClockTests
{
    [Fact]
    public void IncludesShortIntervalsAndExcludesLongGapsWithoutAcceptingOlderEvents()
    {
        var clock = new ActiveDamageClock();
        clock.Observe(1_000);
        clock.Observe(3_000);
        Assert.Equal(2_000, clock.Duration);
        clock.Observe(2_000);
        Assert.Equal(3_000, clock.LastDamageAt);
        Assert.True(clock.IsPaused(6_000));
        clock.Observe(30_000);
        Assert.Equal(2_000, clock.Duration);
        clock.Observe(33_000);
        Assert.Equal(5_000, clock.Duration);
        clock.Reset();
        Assert.False(clock.HasDamage);
        clock.Observe(50_000);
        Assert.Equal(1_000, clock.Duration);
    }
}
