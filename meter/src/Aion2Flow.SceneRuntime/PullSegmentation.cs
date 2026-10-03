using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Identity;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Observation;

namespace Cloris.Aion2Flow.SceneRuntime;

public sealed partial class SceneLiveReadModel
{
    private readonly PullSegmentTracker _pull = new();
    private bool _autoSegmentEnabled;
    private long _pullClockAnchorCapture;
    private long _pullClockAnchorTimestamp;
    private readonly Dictionary<int, (bool? Active, bool Dead, long At)> _explicitPullStates = [];

    public PullSegmentState PullState { get { lock (_gate) return _pull.State; } }
    public string PullCompletionReason { get { lock (_gate) return _pull.CompletionReason; } }

    public void ConfigureAutoSegmentation(bool enabled, int idleSeconds = 5, bool mergeAdjacentPulls = false)
    {
        lock (_gate)
        {
            if (_autoSegmentEnabled != enabled || _pull.MergeAdjacentPulls != mergeAdjacentPulls)
                ResetCore(_timeProvider.GetUtcNow());
            _autoSegmentEnabled = enabled;
            _pull.MergeAdjacentPulls = mergeAdjacentPulls;
            _pull.IdleTimeoutMilliseconds = Math.Clamp(idleSeconds, 2, mergeAdjacentPulls ? 180 : 30) * 1_000L;
        }
    }

    public void AdvanceCombatSegments(bool captureHealthy = true)
    {
        lock (_gate)
            AdvancePullCore(GetPullNow(), captureHealthy);
    }

    void ILiveSceneCollectionPolicy.OnNpcCombatState(in PacketObservationSource packet, int entityId, bool? active, bool dead)
    {
        if (!_autoSegmentEnabled || _kind != SceneKind.Standard)
            return;
        var at = PacketTime(in packet);
        _explicitPullStates[entityId] = (active, dead, at);
        AnchorPullClock(at);
        _pull.ObserveEnemyState(entityId, active, dead, at);
    }

    private bool ShouldAppendPullCombat(in PacketObservationSource packet, int sourceId, int targetId, in CombatWireObservation observation, IRuntimeObservationSink sink)
    {
        var at = PacketTime(in packet);
        // Resolve only previous journal entries before admitting this packet. The reset
        // boundary is therefore before the first hit of the next pull, even within a flush.
        Owner.Refresh();
        AdvancePullCore(at);
        var sourceIsGroup = IsPullParticipant(sourceId);
        var targetIsGroup = IsPullParticipant(targetId);
        if (!sourceIsGroup && !targetIsGroup)
            return false;

        var enemyId = sourceIsGroup ? targetId : sourceId;
        var enemyIsPlayer = Owner.MetadataRegistry.TryGetPcMetadata(enemyId, out _) ||
                            Owner.Entities.TryGet(enemyId, out var enemyEntity) &&
                            (enemyEntity.IsPlayer || enemyEntity.OwnerEntityId.HasValue);
        var resolution = CombatOccurrenceResolution.Primary;
        var isDamage = CombatContributionResolver.TryResolve(sourceId, targetId, in observation, in resolution, out var contribution) &&
                       contribution.Metric == CombatMetricKind.Damage && contribution.Amount > 0;
        if (!isDamage || enemyIsPlayer || enemyId <= 0)
            return _pull.State == PullSegmentState.Recording && (!isDamage || !enemyIsPlayer);

        sink.TryGetNpcRuntimeState(enemyId, out var npc);
        if (npc.Kind is NpcKind.Friendly or NpcKind.Summon)
            return false;
        var periodic = contribution.Delivery == CombatDeliveryKind.Periodic;
        if (!_pull.CanStart(periodic, enemyId, at))
            return false;

        if (_pull.State != PullSegmentState.Recording)
        {
            ResetCore(DateTimeOffset.FromUnixTimeMilliseconds(at), SceneKind.Standard);
            if (npc.Kind is NpcKind.Boss or NpcKind.TrainingDummy)
                SeedBossSceneRuntimeState(in packet, enemyId, sink);
        }
        _pull.ObserveDamage(enemyId, at, npc.Kind == NpcKind.Boss, npc.Kind == NpcKind.TrainingDummy);
        // A positive active signal can protect a boss/trash pause. An old inactive
        // flag cannot close a pull newly started by real damage.
        if (_explicitPullStates.TryGetValue(enemyId, out var explicitState) &&
            explicitState.Active == true && !explicitState.Dead &&
            at >= explicitState.At && at - explicitState.At <= 5_000)
            _pull.ObserveEnemyState(enemyId, true, false, at);
        AnchorPullClock(at);
        UpdatePullWindow(at);
        return true;
    }

    private bool IsPullParticipant(int entityId)
    {
        if (Owner.Entities.TryGet(entityId, out var entity) && entity.OwnerEntityId is int ownerId)
            entityId = ownerId;
        var metadata = Owner.MetadataRegistry;
        if (entityId <= 0)
            return false;
        if (entityId == metadata.LocalPlayerEntityId)
            return true;
        if (!metadata.TryGetPcMetadata(entityId, out var pc))
            return false;
        return _combatantStatisticsScope switch
        {
            CombatantStatisticsScope.Self => false,
            CombatantStatisticsScope.Party => pc.GroupRelation == PlayerGroupRelation.PartyMember,
            _ => pc.GroupRelation is PlayerGroupRelation.PartyMember or PlayerGroupRelation.ForceMember
        };
    }

    private void AdvancePullCore(long now, bool captureHealthy = true)
    {
        if (!_autoSegmentEnabled || _kind != SceneKind.Standard || _pull.State != PullSegmentState.Recording)
            return;
        var completed = _pull.Advance(now, captureHealthy);
        UpdatePullWindow(now);
        if (!completed)
            return;
        _frozenEndObservationOrdinalExclusive = Clock.NextObservationOrdinal;
        _frozenArchive = Owner.CreateArchivePayload();
        FinalizePlaybackStateCore(_frozenArchive);
        if (_frozenArchive.Snapshot.Combatants.Count > 0)
            _pendingArchives.Enqueue(_frozenArchive);
    }

    private void UpdatePullWindow(long now)
    {
        var origin = SessionStarted.ToUnixTimeMilliseconds();
        var start = Math.Max(0, _pull.StartedAt - origin);
        var end = Math.Max(start, (_pull.State == PullSegmentState.Frozen ? _pull.EndedAt : now) - origin);
        // One-hit pulls use a one-second floor, rather than an undefined/infinite DPS.
        Owner.SetPullWindow(start, Math.Max(start + 1_000, end), _pull.State == PullSegmentState.Recording, _pull.CompletionReason);
    }

    private long PacketTime(in PacketObservationSource packet) =>
        packet.CaptureTimestampMilliseconds > 0 ? packet.CaptureTimestampMilliseconds : GetPullNow();

    private void AnchorPullClock(long at)
    {
        if (at < _pullClockAnchorCapture)
            return;
        _pullClockAnchorCapture = at;
        _pullClockAnchorTimestamp = _timeProvider.GetTimestamp();
    }

    private long GetPullNow() => _pullClockAnchorCapture > 0
        ? _pullClockAnchorCapture + (long)_timeProvider.GetElapsedTime(_pullClockAnchorTimestamp).TotalMilliseconds
        : _timeProvider.GetUtcNow().ToUnixTimeMilliseconds();
}
