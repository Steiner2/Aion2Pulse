using Cloris.Aion2Flow.Protocol.Combat;
using Cloris.Aion2Flow.SceneRuntime;
using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Observation;

namespace Cloris.Aion2Flow.Tests.SceneRuntime;

public sealed class PullSegmentationTests
{
    [Fact]
    public void TwoPacksHaveSeparateTotalsAndPreservePlayerIdentity()
    {
        var f = new Fixture();
        f.Damage(100, 200, 100, 1_000);
        f.Damage(100, 201, 200, 2_000);
        var firstId = f.Scene.SessionId;
        f.At(7_100);
        var first = f.Scene.CreateFrame().Snapshot;
        Assert.Equal(PullSegmentState.Frozen, f.Scene.PullState);
        Assert.Equal(300, first.Combatants[100].DamageAmount);
        Assert.Equal(1_000, first.EncounterTime);
        Assert.True(f.Scene.TryDequeuePendingArchive(out var archive));
        Assert.Equal(firstId, archive.Snapshot.EncounterId);

        f.Damage(100, 202, 400, 8_000);
        var second = f.Scene.CreateFrame().Snapshot;
        Assert.NotEqual(firstId, second.EncounterId);
        Assert.Equal(400, second.Combatants[100].DamageAmount);
        Assert.Equal(300, archive.Snapshot.Combatants[100].DamageAmount);
        Assert.True(f.Scene.MetadataRegistry.TryGetPcMetadata(100, out var player));
        Assert.Equal("Tester", player.Nickname);
        Assert.Equal(100, f.Scene.MetadataRegistry.LocalPlayerEntityId);
    }

    [Fact]
    public void FrozenDurationAndDpsDoNotDecayDuringThirtySecondsOfDowntime()
    {
        var f = new Fixture();
        f.Damage(100, 200, 500, 1_000);
        f.Damage(100, 200, 500, 3_000);
        f.At(8_100);
        var frozen = f.Scene.CreateFrame().Snapshot;
        f.At(38_100);
        var later = f.Scene.CreateFrame().Snapshot;
        Assert.Equal(2_000, later.EncounterTime);
        Assert.Equal(500d, later.Combatants[100].DamagePerSecond);
        Assert.Equal(frozen.EncounterTime, later.EncounterTime);
        Assert.False(later.Encounter.IsActive);
        Assert.Equal("idle-heuristic", later.Encounter.Reason);
        Assert.True(f.Scene.TryDequeuePendingArchive(out _));
        Assert.False(f.Scene.TryDequeuePendingArchive(out _));
    }

    [Fact]
    public void RunningDurationAdvancesWithoutAdditionalPackets()
    {
        var f = new Fixture();
        f.Damage(100, 200, 900, 1_000);
        f.At(4_000);
        var frame = f.Scene.CreateFrame().Snapshot;
        Assert.Equal(3_000, frame.EncounterTime);
        Assert.Equal(300d, frame.Combatants[100].DamagePerSecond);
        Assert.Equal(PullSegmentState.Recording, f.Scene.PullState);
    }

    [Fact]
    public void ConsecutivePacksAreSplitEvenWithoutAnIntermediateUiFrame()
    {
        var f = new Fixture();
        f.Damage(100, 200, 100, 1_000);
        f.Damage(100, 201, 250, 8_000);
        var frame = f.Scene.CreateFrame().Snapshot;
        Assert.Equal(250, frame.Combatants[100].DamageAmount);
        Assert.True(f.Scene.TryDequeuePendingArchive(out var archive));
        Assert.Equal(100, archive.Snapshot.Combatants[100].DamageAmount);
    }

    [Fact]
    public void KillingOneOfSeveralEnemiesDoesNotFinishThePull()
    {
        var f = new Fixture();
        f.Damage(100, 200, 100, 1_000);
        f.Damage(100, 201, 200, 1_200);
        f.Sink.AppendNpcHp(f.Source(1_500), 200, 0);
        f.At(3_000);
        Assert.Equal(PullSegmentState.Recording, f.Scene.CreateFrame().Snapshot.Encounter.IsActive ? f.Scene.PullState : PullSegmentState.Frozen);
        f.Sink.AppendNpcHp(f.Source(3_100), 201, 0);
        f.At(4_200);
        Assert.False(f.Scene.CreateFrame().Snapshot.Encounter.IsActive);
        Assert.Equal("enemies-inactive", f.Scene.PullCompletionReason);
    }

    [Fact]
    public void ExplicitCombatEndFreezesAfterASettlementPeriod()
    {
        var f = new Fixture();
        f.Damage(100, 200, 100, 1_000);
        f.Sink.SetNpcBattle(f.Source(2_000), 200, false);
        f.At(2_900);
        Assert.True(f.Scene.CreateFrame().Snapshot.Encounter.IsActive);
        f.At(3_100);
        var frame = f.Scene.CreateFrame().Snapshot;
        Assert.Equal(1_000, frame.EncounterTime);
        Assert.False(frame.Encounter.IsActive);
        Assert.Equal("enemies-inactive", frame.Encounter.Reason);
    }

    [Fact]
    public void BossIntermissionIsNotSplitByIdleHeuristic()
    {
        var f = new Fixture();
        f.Sink.AppendNpcKind(f.Source(100), 200, NpcKind.Boss);
        f.Damage(100, 200, 100, 1_000);
        var id = f.Scene.SessionId;
        f.At(120_000);
        Assert.True(f.Scene.CreateFrame().Snapshot.Encounter.IsActive);
        f.Damage(100, 200, 200, 121_000);
        Assert.Equal(id, f.Scene.SessionId);
        Assert.Equal(300, f.Scene.CreateFrame().Snapshot.Combatants[100].DamageAmount);
        f.Sink.AppendNpcHp(f.Source(122_000), 200, 0);
        f.At(123_100);
        Assert.False(f.Scene.CreateFrame().Snapshot.Encounter.IsActive);
    }

    [Fact]
    public void PeriodicTailCannotStartAPhantomPullOrModifyAnArchive()
    {
        var f = new Fixture();
        f.Damage(100, 200, 500, 1_000);
        f.At(6_100);
        f.Scene.CreateFrame();
        Assert.True(f.Scene.TryDequeuePendingArchive(out var archive));
        f.Damage(100, 200, 100, 7_000, periodic: true);
        Assert.Equal(PullSegmentState.Frozen, f.Scene.PullState);
        Assert.Equal(500, f.Scene.CreateFrame().Snapshot.Combatants[100].DamageAmount);
        Assert.Equal(500, archive.Snapshot.Combatants[100].DamageAmount);
    }

    [Fact]
    public void PeriodicDamageDuringThePullIsIncluded()
    {
        var f = new Fixture();
        f.Damage(100, 200, 500, 1_000);
        f.Damage(100, 200, 100, 2_000, periodic: true);
        Assert.Equal(600, f.Scene.CreateFrame().Snapshot.Combatants[100].DamageAmount);
    }

    [Fact]
    public void OldTargetsPeriodicTailCannotContaminateTheNextPull()
    {
        var f = new Fixture();
        f.Damage(100, 200, 500, 1_000);
        f.Damage(100, 201, 200, 8_000);
        f.Damage(100, 200, 100, 8_100, periodic: true);
        Assert.Equal(200, f.Scene.CreateFrame().Snapshot.Combatants[100].DamageAmount);
    }

    [Fact]
    public void OutsidersCannotStartOrExtendOurPull()
    {
        var f = new Fixture();
        f.Sink.AppendNickname(f.Source(100), 101, "Outsider", characterClass: CharacterClass.Ranger);
        f.Damage(101, 201, 1_000, 1_000);
        Assert.Equal(PullSegmentState.Waiting, f.Scene.PullState);
        f.Damage(100, 200, 100, 2_000);
        f.Damage(101, 201, 1_000, 6_000);
        f.At(7_100);
        var frame = f.Scene.CreateFrame().Snapshot;
        Assert.Equal(PullSegmentState.Frozen, f.Scene.PullState);
        Assert.False(frame.Combatants.ContainsKey(101));
    }

    [Fact]
    public void SummonDamageIsAttributedToTheOwnerAndDoesNotMakeANewSegment()
    {
        var f = new Fixture();
        f.Sink.AppendSummon(f.Source(100), 100, 300);
        f.Damage(300, 200, 200, 1_000);
        f.Damage(100, 201, 300, 2_000);
        Assert.Equal(500, f.Scene.CreateFrame().Snapshot.Combatants[100].DamageAmount);
    }

    [Fact]
    public void HealingAndManaRecoveryCannotStartOrProlongAPull()
    {
        var f = new Fixture();
        f.Recovery(100, 100, 500, 1_000);
        Assert.Equal(PullSegmentState.Waiting, f.Scene.PullState);
        f.Damage(100, 200, 100, 2_000);
        f.Recovery(100, 100, 500, 6_000);
        f.At(7_100);
        Assert.False(f.Scene.CreateFrame().Snapshot.Encounter.IsActive);
        Assert.Equal("idle-heuristic", f.Scene.PullCompletionReason);
    }

    [Fact]
    public void CaptureLossMarksIncompleteRatherThanSuccessfulCompletion()
    {
        var f = new Fixture();
        f.Damage(100, 200, 100, 1_000);
        f.Scene.AdvanceCombatSegments(captureHealthy: false);
        Assert.Equal("capture-interrupted", f.Scene.CreateFrame().Snapshot.Encounter.Reason);
        f.Damage(100, 201, 250, 3_000);
        Assert.Equal(250, f.Scene.CreateFrame().Snapshot.Combatants[100].DamageAmount);
    }

    [Fact]
    public void IncomingDamageCanStartTheLocalPlayersPull()
    {
        var f = new Fixture();
        f.Damage(200, 100, 100, 1_000);
        Assert.Equal(PullSegmentState.Recording, f.Scene.PullState);
        f.Damage(100, 200, 500, 2_000);
        Assert.Equal(500, f.Scene.CreateFrame().Snapshot.Combatants[100].DamageAmount);
    }

    [Fact]
    public void StaleExplicitActiveFlagDoesNotDelayTrashFreeze()
    {
        var f = new Fixture();
        f.Sink.SetNpcBattle(f.Source(100), 200, true);
        f.Damage(100, 200, 100, 1_000);
        f.At(6_100);
        Assert.Equal("idle-heuristic", f.Scene.CreateFrame().Snapshot.Encounter.Reason);
    }

    [Fact]
    public void RepeatedInactivePacketsDoNotRestartSettlement()
    {
        var f = new Fixture();
        f.Damage(100, 200, 500, 1_000);
        f.Damage(100, 200, 500, 2_000);
        f.Sink.SetNpcBattle(f.Source(2_100), 200, false);
        f.Sink.SetNpcBattle(f.Source(2_600), 200, false);
        f.Sink.SetNpcBattle(f.Source(3_000), 200, false);
        f.At(3_150);
        var snapshot = f.Scene.CreateFrame().Snapshot;
        Assert.Equal(PullSegmentState.Frozen, f.Scene.PullState);
        Assert.Equal(1_100, snapshot.EncounterTime);
        Assert.Equal("enemies-inactive", snapshot.Encounter.Reason);
    }

    [Fact]
    public void BossDamageClockExcludesPauseButKeepsOneEncounterAndRawTimeline()
    {
        var f = new Fixture();
        f.Scene.ConfigureDamagePause(true);
        f.Sink.AppendNpcKind(f.Source(100), 200, NpcKind.Boss);
        f.Damage(100, 200, 500, 1_000);
        f.Damage(100, 200, 500, 3_000);
        var id = f.Scene.SessionId;
        var before = f.Scene.CreateFrame().Snapshot;
        f.At(50_000);
        var paused = f.Scene.CreateFrame().Snapshot;
        Assert.True(paused.Encounter.IsActive);
        Assert.True(f.Scene.IsDamageTimePaused);
        Assert.Equal(2_000, paused.EncounterTime);
        Assert.Equal(before.Combatants[100].DamagePerSecond, paused.Combatants[100].DamagePerSecond);
        f.Damage(200, 100, 900, 51_000); // Pattern damage received is not DPS activity.
        f.Recovery(100, 100, 100, 52_000);
        f.Damage(100, 200, 0, 53_000); // Zero-damage / immune attempts do not restart the clock.
        Assert.Equal(2_000, f.Scene.CreateFrame().Snapshot.EncounterTime);
        f.Damage(100, 200, 500, 60_000);
        f.Damage(100, 200, 500, 62_000);
        var resumed = f.Scene.CreateFrame().Snapshot;
        Assert.Equal(id, resumed.EncounterId);
        Assert.Equal(4_000, resumed.EncounterTime);
        Assert.Equal(2_000, resumed.Combatants[100].DamageAmount);
        Assert.Equal(500d, resumed.Combatants[100].DamagePerSecond);
        Assert.True(resumed.EncounterEndTime - resumed.EncounterStartTime >= 61_000);
        Assert.False(f.Scene.IsDamageTimePaused);
        f.Sink.AppendNpcHp(f.Source(63_000), 200, 0);
        f.At(64_100);
        Assert.False(f.Scene.CreateFrame().Snapshot.Encounter.IsActive);
        Assert.True(f.Scene.TryDequeuePendingArchive(out var archive));
        Assert.Equal(4_000, archive.Snapshot.EncounterTime);
        f.At(90_000);
        Assert.Equal(4_000, f.Scene.CreateFrame().Snapshot.EncounterTime);
    }

    [Fact]
    public void ActiveDamageClockFreezesWhileTrashEndSettlesAndResetsOnNextPack()
    {
        var f = new Fixture();
        f.Scene.ConfigureDamagePause(true);
        f.Damage(100, 200, 500, 1_000);
        f.Damage(100, 200, 500, 3_000);
        f.At(5_000);
        Assert.Equal(2_000, f.Scene.CreateFrame().Snapshot.EncounterTime);
        f.At(8_100);
        Assert.Equal(PullSegmentState.Frozen, f.Scene.CreateFrame().Snapshot.Encounter.IsActive ? PullSegmentState.Recording : f.Scene.PullState);
        f.Damage(100, 201, 250, 10_000);
        Assert.Equal(1_000, f.Scene.CreateFrame().Snapshot.EncounterTime);
        Assert.Equal(250, f.Scene.CreateFrame().Snapshot.Combatants[100].DamageAmount);
    }

    private sealed class Fixture
    {
        private static readonly DateTimeOffset Started = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
        private readonly ManualTimeProvider _time = new(Started);
        public SceneLiveReadModel Scene { get; }
        public IRuntimeObservationSink Sink { get; }
        public Fixture()
        {
            Scene = new SceneLiveReadModel(Started, _time);
            Scene.ConfigureAutoSegmentation(true);
            Sink = SceneSinkFactory.CreateForLive(Scene)();
            Sink.AppendNickname(Source(0), 100, "Tester", characterClass: CharacterClass.Gladiator, isLocalPlayer: true);
            foreach (var id in new[] { 200, 201, 202 })
                Sink.AppendNpcKind(Source(0), id, NpcKind.Monster);
        }
        public void At(long offset) => _time.Milliseconds = offset;
        public PacketObservationSource Source(long offset)
        {
            At(offset);
            return new(Started.ToUnixTimeMilliseconds() + offset, offset + 1, 0x0438, 30, offset, default);
        }
        public void Damage(int from, int to, int damage, long at, bool periodic = false)
        {
            var source = Source(at);
            var wire = new CombatWireObservation
            {
                SkillCode = 11_000_010, Damage = damage, HitCount = 1, AttemptCount = 1,
                PeriodicRelation = periodic ? PeriodicEffectRelation.Target : PeriodicEffectRelation.None,
                PeriodicMode = periodic ? 2 : 0
            };
            Sink.AppendCombatWireObservation(in source, from, to, in wire);
            Sink.CompleteFlush(source.FlushId);
        }
        public void Recovery(int from, int to, int amount, long at)
        {
            var source = Source(at);
            var wire = new CombatWireObservation { SkillCode = 14_000_010, Damage = amount, ResourceKind = CombatResourceKind.Health };
            Sink.AppendCombatWireObservation(in source, from, to, in wire);
            Sink.CompleteFlush(source.FlushId);
        }
    }

    private sealed class ManualTimeProvider(DateTimeOffset started) : TimeProvider
    {
        public long Milliseconds { get; set; }
        public override long TimestampFrequency => 1_000;
        public override long GetTimestamp() => Milliseconds;
        public override DateTimeOffset GetUtcNow() => started.AddMilliseconds(Milliseconds);
    }
}
