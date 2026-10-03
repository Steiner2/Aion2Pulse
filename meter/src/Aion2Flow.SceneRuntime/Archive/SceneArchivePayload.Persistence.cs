using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Identity;
using Cloris.Aion2Flow.SceneRuntime.Journal;
using Cloris.Aion2Flow.SceneRuntime.Projection;

namespace Cloris.Aion2Flow.SceneRuntime.Archive;

public sealed partial class SceneArchivePayload
{
    private Dictionary<int, CombatDetailDelta>? _storedDetails;

    internal StoredEncounter ToStored(ArchivedEncounterRecord record) => new()
    {
        Id = record.Id, ArchivedAt = record.ArchivedAt, Trigger = record.Trigger,
        Support = SupportData,
        IsAutomatic = record.IsAutomatic, EncounterId = Snapshot.EncounterId,
        Kind = Kind, SceneStarted = SceneStarted, MapId = Snapshot.MapId,
        MapInstanceId = Snapshot.MapInstanceId, Start = Snapshot.EncounterStartTime,
        End = Snapshot.EncounterEndTime, Duration = Snapshot.EncounterTime,
        Encounter = Snapshot.Encounter, Target = Snapshot.TargetObservation,
        Metrics = Snapshot.Combatants.AsSpan().ToArray(),
        BossFocuses = Snapshot.BossFocuses.AsSpan().ToArray(), BossNpcCodes = BossNpcCodes.AsSpan().ToArray(),
        Players = IdentityScope.PcMetadataSpan.ToArray(), Npcs = IdentityScope.NpcCodeSpan.ToArray(),
        Maps = IdentityScope.MapCodeSpan.ToArray(), Entities = Entities.AsSpan().ToArray(),
        Vitals = EntityVitals.AsSpan().ToArray(), Bosses = Bosses.AsSpan().ToArray(),
        Pairs = Pairs.AsSpan().ToArray(), Combatants = Combatants.AsSpan().ToArray(),
        Details = Combatants.ToDictionary(c => c.CombatantId, c => CreateDetailDelta(c.CombatantId))
    };

    internal static SceneArchivePayload FromStored(StoredEncounter data)
    {
        var support = data.Version == 1 ? new EncounterSupportData() : data.Support;
        if (data.Version is not 1 and not 2 || support is null || support.Events is null || support.Auras is null || data.Id == Guid.Empty || data.EncounterId == Guid.Empty || data.Duration <= 0 || data.End < data.Start ||
            data.Metrics is null || data.Metrics.Length == 0 || data.Players is null || data.Npcs is null || data.Maps is null ||
            data.BossFocuses is null || data.BossNpcCodes is null || data.Pairs is null || data.Combatants is null ||
            data.Entities is null || data.Vitals is null || data.Bosses is null || data.Details is null ||
            data.Details.Values.Any(detail => detail is null))
            throw new InvalidDataException("Unsupported or invalid encounter file.");
        Array.Sort(data.Metrics, (a, b) => a.Id.CompareTo(b.Id));
        Array.Sort(data.Players, (a, b) => a.EntityId.CompareTo(b.EntityId));
        Array.Sort(data.Npcs, (a, b) => a.InstanceId.CompareTo(b.InstanceId));
        Array.Sort(data.Maps, (a, b) => a.InstanceId.CompareTo(b.InstanceId));
        var snapshot = new SceneCombatSnapshot(data.EncounterId, data.Kind, 0, 0,
            data.MapId, data.MapInstanceId, data.Start, data.End, data.Duration,
            data.Metrics, data.Target, data.Encounter, data.BossFocuses, data.BossNpcCodes);
        var payload = new SceneArchivePayload(snapshot, data.Kind, data.SceneStarted,
            SceneJournalSegment.Empty, default, [], [],
            new SceneIdentityScope(data.Players, data.Npcs, data.Maps),
            data.Pairs, data.Combatants, data.Entities, data.Vitals, data.Bosses, data.BossNpcCodes,
            ArchivePayloadIndex.Create([], [], [], [], [], [], data.Pairs, data.Combatants));
        payload._storedDetails = data.Details;
        payload._supportData = support;
        return payload;
    }
}
