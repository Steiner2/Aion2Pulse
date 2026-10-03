using Cloris.Aion2Flow.SceneRuntime.Archive;
using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.SceneRuntime.Projection;
using Cloris.Aion2Flow.Protocol.Combat;

namespace Cloris.Aion2Flow.Tests.App;

public sealed class EncounterAnalyticsTests
{
    private static CombatMetricDetailEvent Event(long at, long amount, CombatMetricKind metric, DamageModifiers flags = DamageModifiers.None, CombatDeliveryKind delivery = CombatDeliveryKind.Direct)
        => new(new CombatDetailFact { ObservedAtMilliseconds = at, Observation = new CombatWireObservation { SkillCode = 100, Modifiers = flags } }, new CombatContribution(metric, delivery, amount, default));

    [Fact]
    public void AdaptiveBucketsPreserveAmountsAndIncludeQuietIntervals()
    {
        var events = new[] { Event(0, 100, CombatMetricKind.Damage), Event(2100, 50, CombatMetricKind.Healing), Event(3500, 999, CombatMetricKind.Damage) };
        var buckets = EncounterAnalytics.Buckets(events, 0, 2500);
        Assert.Equal(3, buckets.Length); Assert.Equal(100, buckets[0].Damage);
        Assert.Equal(0, buckets[1].Damage); Assert.Equal(100, buckets[2].Healing);
        Assert.Equal(50, buckets[2].Healing * .5);
        Assert.InRange(EncounterAnalytics.Buckets(events, 0, 3_600_000).Length, 1, 300);
    }

    [Fact]
    public void HealingAndShieldsDoNotInflateCriticalDenominator()
    {
        var summary = Assert.Single(EncounterAnalytics.Skills(new[] { Event(0, 100, CombatMetricKind.Damage, DamageModifiers.Critical | DamageModifiers.Back), Event(1, 50, CombatMetricKind.Damage, delivery: CombatDeliveryKind.Periodic), Event(2, 200, CombatMetricKind.Healing), Event(3, 300, CombatMetricKind.ShieldAbsorbed) }));
        Assert.Equal(2, summary.DamageEvents); Assert.Equal(1, summary.Critical); Assert.Equal(1, summary.Back);
        Assert.Equal(200, summary.Healing); Assert.Equal(300, summary.ShieldAbsorbed); Assert.Equal(50, summary.PeriodicDamage);
    }

    [Fact]
    public void AuraCoverageUnionsOverlapsAndClipsToSelectedRange()
    {
        var windows = new[] { new EncounterAuraWindow(1, 2, default, 0, 5000, false), new EncounterAuraWindow(1, 3, default, 2000, 8000, true) };
        Assert.Equal(6, EncounterAnalytics.ObservedAuraSeconds(windows, 1000, 7000));
        Assert.Equal(0, EncounterAnalytics.ObservedAuraSeconds(windows, 9000, 10000));
    }
}
