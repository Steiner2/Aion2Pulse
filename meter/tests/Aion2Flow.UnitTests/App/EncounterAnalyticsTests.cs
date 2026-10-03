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
    [Fact]
    public void DeliverySharesKeepHotDrainAndShieldsSeparate()
    {
        var events = new[] { Event(0, 100, CombatMetricKind.Damage), Event(1, 40, CombatMetricKind.Damage, delivery: CombatDeliveryKind.Periodic),
            Event(2, 80, CombatMetricKind.Healing, delivery: CombatDeliveryKind.Periodic), Event(3, 20, CombatMetricKind.Healing, delivery: CombatDeliveryKind.Drain),
            Event(4, 999, CombatMetricKind.ShieldGranted, delivery: CombatDeliveryKind.Periodic) };
        var deliveries = EncounterAnalytics.Deliveries(events);
        var periodic = Assert.Single(deliveries, d => d.Delivery == CombatDeliveryKind.Periodic);
        Assert.Equal(2, periodic.Events); Assert.Equal(40, periodic.Damage); Assert.Equal(80, periodic.Healing);
        Assert.Equal(140, deliveries.Sum(d => d.Damage)); Assert.Equal(100, deliveries.Sum(d => d.Healing));
        Assert.Equal(80, Assert.Single(EncounterAnalytics.Skills(events)).PeriodicHealing);
    }

    [Fact]
    public void TargetChangesUseTimeThenOrdinalAndIgnoreHealingOrZeroDamage()
    {
        CombatMetricDetailEvent Damage(long at, int target, long ordinal) => Event(at, 10, CombatMetricKind.Damage) with { Fact = Event(at, 10, CombatMetricKind.Damage).Fact with { TargetId = target, SourceId = 100, SourceObservationOrdinal = ordinal } };
        var events = new[] { Damage(1000, 2, 2), Damage(1000, 1, 1), Damage(2000, 2, 3), Damage(3000, 3, 4),
            Event(2500, 90, CombatMetricKind.Healing), Damage(3500, 4, 5) with { Contribution = new(CombatMetricKind.Damage, CombatDeliveryKind.Direct, 0, default) } };
        var changes = EncounterAnalytics.TargetChanges(events);
        Assert.Equal(2, changes.Length); Assert.Equal(new ObservedTargetChange(1000, 1, 2, 100, 2), changes[0]);
        Assert.Equal(3, changes[1].Next); Assert.Empty(EncounterAnalytics.TargetChanges(events, true));
    }

    [Fact]
    public void DamageGapsDoNotTurnHealingOrZeroHitsIntoDamageActivity()
    {
        var events = new[] { Event(0, 10, CombatMetricKind.Damage), Event(3000, 20, CombatMetricKind.Damage),
            Event(4000, 100, CombatMetricKind.Healing), Event(6000, 0, CombatMetricKind.Damage), Event(6001, 10, CombatMetricKind.Damage), Event(6001, 10, CombatMetricKind.Damage) };
        Assert.Equal(new ObservedDamageGap(3000, 6001), Assert.Single(EncounterAnalytics.DamageGaps(events)));
        Assert.Empty(EncounterAnalytics.DamageGaps(events, 4000));
    }

    [Fact]
    public void CounterpartFilterSeparatesKnownBossesAddsPlayersAndUnknowns()
    {
        var scene = new Cloris.Aion2Flow.SceneRuntime.SceneLiveReadModel(DateTimeOffset.UnixEpoch);
        var sink = Cloris.Aion2Flow.SceneRuntime.SceneSinkFactory.CreateForLive(scene)();
        var source = new PacketObservationSource(1000, 1, 0x0438, 30, 0, default);
        sink.AppendNickname(source, 100, "Preview", isLocalPlayer: true);
        sink.AppendNickname(source, 101, "Other preview");
        sink.AppendNpcKind(source, 200, Cloris.Aion2Flow.SceneRuntime.Model.NpcKind.Boss);
        sink.AppendNpcKind(source, 201, Cloris.Aion2Flow.SceneRuntime.Model.NpcKind.Monster);
        var wire = new CombatWireObservation { SkillCode = 11_000_010, Damage = 10, HitCount = 1, AttemptCount = 1 };
        foreach (var target in new[] { 200, 201, 101, 999 }) sink.AppendCombatWireObservation(source, 100, target, wire);
        sink.CompleteFlush(1);
        var payload = scene.CreateArchivePayload();
        var events = payload.CreateDetailDelta(100).MetricEvents;
        Assert.Equal(4, events.Count);
        Assert.Equal(200, Assert.Single(EncounterAnalytics.FilterTargets(payload, events, AnalysisTargetScope.Boss)).TargetId);
        Assert.Equal(201, Assert.Single(EncounterAnalytics.FilterTargets(payload, events, AnalysisTargetScope.OtherEnemy)).TargetId);
        Assert.Equal(101, Assert.Single(EncounterAnalytics.FilterTargets(payload, events, AnalysisTargetScope.Player)).TargetId);
        Assert.Equal(999, Assert.Single(EncounterAnalytics.FilterTargets(payload, events, AnalysisTargetScope.Unknown)).TargetId);
        Assert.Empty(EncounterAnalytics.FilterTargets(payload, events, AnalysisTargetScope.Boss, true));
        Assert.Equal(4, EncounterAnalytics.FilterTargets(payload, events, AnalysisTargetScope.Player, true).Length);
    }

}
