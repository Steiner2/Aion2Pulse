using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Identity;
using Cloris.Aion2Flow.SceneRuntime.Model;

namespace Cloris.Aion2Flow.SceneRuntime.Archive;

/// <summary>One map visit. Combat time is summed; travel and idle time are excluded.</summary>
public sealed class InstanceCombatTotals
{
    private readonly Dictionary<int, SceneCombatantMetrics> _metrics = [];
    private readonly Dictionary<int, PcMetadata> _players = [];
    private readonly Dictionary<(int Server, string Name), int> _identities = [];
    private int _nextSyntheticId = int.MaxValue;
    private readonly HashSet<Guid> _included = [];
    private Guid _id = Guid.NewGuid();
    private uint _mapId, _instanceId;
    private long _duration;
    public int FightCount => _included.Count;
    public SceneIdentityScope IdentityScope
    {
        get
        {
            var builder = new SceneIdentityScopeBuilder();
            foreach (var player in _players.Values) builder.AddPcMetadata(player);
            builder.AddMapCode(_instanceId, _mapId);
            return builder.ToScope();
        }
    }

    public void ObserveMap(uint mapId, uint instanceId)
    {
        if (mapId == 0 && instanceId == 0) return;
        if ((_mapId != 0 && mapId != 0 && _mapId != mapId) ||
            (_instanceId != 0 && instanceId != 0 && _instanceId != instanceId)) Reset();
        if (mapId != 0) _mapId = mapId;
        if (instanceId != 0) _instanceId = instanceId;
    }

    public void Reset()
    {
        _metrics.Clear(); _players.Clear(); _identities.Clear(); _included.Clear(); _duration = 0;
        _nextSyntheticId = int.MaxValue;
        _id = Guid.NewGuid(); _mapId = 0; _instanceId = 0;
    }

    public void Add(SceneArchivePayload payload)
    {
        var snapshot = payload.Snapshot;
        ObserveMap(snapshot.MapId, snapshot.MapInstanceId);
        if (!_included.Add(snapshot.EncounterId)) return;
        _duration += snapshot.EncounterTime;
        foreach (var entry in snapshot.Combatants)
        {
            var metadata = payload.IdentityScope.TryGetPcMetadata(entry.Id, out var player) ? player : (PcMetadata?)null;
            AddMetrics(_metrics, ResolvePlayerId(entry.Id, metadata), entry.Metrics);
        }
    }

    public SceneCombatSnapshot CreateSnapshot(SceneCombatSnapshot? live = null, RuntimeMetadataRegistry? registry = null)
    {
        // Resolve live identities before cloning the totals; late identity discovery can migrate old counters.
        var liveIds = new Dictionary<int, int>();
        if (live is not null)
            foreach (var entry in live.Combatants)
            {
                var metadata = registry?.TryGetPcMetadata(entry.Id, out var player) == true ? player : (PcMetadata?)null;
                liveIds[entry.Id] = ResolvePlayerId(entry.Id, metadata);
            }
        var duration = _duration;
        var metrics = new Dictionary<int, SceneCombatantMetrics>(_metrics);
        if (live is not null && !_included.Contains(live.EncounterId) && live.Combatants.Count > 0 &&
            (_mapId == 0 || live.MapId == 0 || live.MapId == _mapId) &&
            (_instanceId == 0 || live.MapInstanceId == 0 || live.MapInstanceId == _instanceId))
        {
            duration += live.EncounterTime;
            foreach (var entry in live.Combatants) AddMetrics(metrics, liveIds[entry.Id], entry.Metrics);
        }
        var seconds = Math.Max(1d, duration / 1000d);
        var entries = metrics.OrderBy(p => p.Key).Select(p => new CombatantSnapshotEntry(p.Key,
            p.Value with { DamagePerSecond = p.Value.DamageAmount / seconds, HealingPerSecond = p.Value.HealingAmount / seconds })).ToArray();
        return new SceneCombatSnapshot(_id, SceneKind.Standard, 0, 0, _mapId, _instanceId,
            0, duration, duration, entries, null,
            new EncounterSummarySnapshot(0, NpcRuntimePhaseHint.Unknown, false, false, "overall"), [], []);
    }

    private int ResolvePlayerId(int entityId, PcMetadata? metadata)
    {
        if (metadata is not { HasNickname: true } player) return entityId;
        var key = (player.OriginServerId ?? 0, player.Nickname);
        if (player.OriginServerId is > 0 && !_identities.ContainsKey(key) &&
            _identities.Remove((0, player.Nickname), out var provisionalId))
            _identities[key] = provisionalId;
        if (_identities.TryGetValue(key, out var stableId))
        {
            // Merge only an earlier unidentified entity; a known different player keeps their own row.
            if (stableId != entityId && !_players.ContainsKey(entityId) && _metrics.Remove(entityId, out var provisional))
                AddMetrics(_metrics, stableId, provisional);
        }
        else
        {
            stableId = entityId;
            if (_players.TryGetValue(entityId, out var previous) &&
                (previous.Nickname != player.Nickname || previous.OriginServerId != player.OriginServerId))
            {
                while (_players.ContainsKey(_nextSyntheticId) || _metrics.ContainsKey(_nextSyntheticId)) _nextSyntheticId--;
                stableId = _nextSyntheticId--;
            }
            _identities[key] = stableId;
        }
        var prior = _players.GetValueOrDefault(stableId);
        _players[stableId] = player with
        {
            EntityId = stableId,
            CharacterClass = player.CharacterClass ?? prior.CharacterClass,
            IsLocalPlayer = player.IsLocalPlayer || prior.IsLocalPlayer,
            GroupRelation = player.GroupRelation == PlayerGroupRelation.Unknown ? prior.GroupRelation : player.GroupRelation
        };
        return stableId;
    }

    private static void AddMetrics(Dictionary<int, SceneCombatantMetrics> metrics, int id, SceneCombatantMetrics value)
    {
        var old = metrics.GetValueOrDefault(id);
        metrics[id] = value with
        {
            CharacterClass = value.CharacterClass ?? old.CharacterClass,
            IsVisiblePlayerCombatant = value.IsVisiblePlayerCombatant || old.IsVisiblePlayerCombatant,
            DamageAmount = old.DamageAmount + value.DamageAmount,
            HealingAmount = old.HealingAmount + value.HealingAmount,
            PeriodicHealingAmount = old.PeriodicHealingAmount + value.PeriodicHealingAmount,
            DrainDamageAmount = old.DrainDamageAmount + value.DrainDamageAmount,
            DrainHealingAmount = old.DrainHealingAmount + value.DrainHealingAmount,
            RegenerationHealingAmount = old.RegenerationHealingAmount + value.RegenerationHealingAmount,
            ShieldAmount = old.ShieldAmount + value.ShieldAmount,
            ShieldTimes = old.ShieldTimes + value.ShieldTimes,
            ShieldAbsorbedAmount = old.ShieldAbsorbedAmount + value.ShieldAbsorbedAmount,
            ShieldAbsorbedTimes = old.ShieldAbsorbedTimes + value.ShieldAbsorbedTimes,
            DamageContribution = old.DamageContribution + value.DamageContribution
        };
    }
}
