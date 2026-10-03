using Cloris.Aion2Flow.Protocol.Combat;
using Cloris.Aion2Flow.SceneRuntime.Journal;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.SceneRuntime.Playback;
using Cloris.Aion2Flow.SceneRuntime.Stores;

namespace Cloris.Aion2Flow.SceneRuntime.Archive;

public enum SupportEventKind { Health, Cooldown, Charges, Action }
public readonly record struct EncounterSupportEvent(long At, int EntityId, SupportEventKind Kind, ResourceEffectRef Skill, long Value, long? Maximum, long Detail);
public readonly record struct EncounterAuraWindow(int EntityId, int OriginId, ResourceEffectRef Skill, long Start, long End, bool OpenEnd, AuraDisposition Disposition = AuraDisposition.Unknown);
public sealed record EncounterSupportData
{
    public bool Available { get; init; }
    public bool Truncated { get; init; }
    public EncounterSupportEvent[] Events { get; init; } = [];
    public EncounterAuraWindow[] Auras { get; init; } = [];
}

public sealed partial class SceneArchivePayload
{
    private EncounterSupportData? _supportData;
    public EncounterSupportData SupportData => _supportData ??= CaptureSupport();

    private EncounterSupportData CaptureSupport()
    {
        if (TimelineSegment.IsEmpty) return new();
        const int limit = 100_000;
        var result = new List<EncounterSupportEvent>();
        var cursor = TimelineSegment.CreateCursor();
        var truncated = false;
        while (cursor.NextObservationOrdinal < TimelineSegment.CurrentEndObservationOrdinalExclusive)
        {
            var read = TimelineSegment.ReadEntries(cursor, 512, entries =>
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    if (e.ObservedAtMilliseconds > Snapshot.EncounterEndTime) continue;
                    EncounterSupportEvent? item = null;
                    if (e.Domain == ObservedEventDomain.EntityVital)
                        item = new(e.ObservedAtMilliseconds, e.EntityVital.EntityId, SupportEventKind.Health, default, e.EntityVital.CurrentHp, e.EntityVital.MaxHp, 0);
                    else if (e.Domain == ObservedEventDomain.State && e.State.StateCode is StateCodes.Cooldown4738 or StateCodes.CooldownStart0238 or StateCodes.CooldownCharge2238)
                        item = new(e.ObservedAtMilliseconds, e.State.EntityId, e.State.StateCode == StateCodes.CooldownCharge2238 ? SupportEventKind.Charges : SupportEventKind.Cooldown,
                            new ResourceEffectRef(unchecked((uint)e.State.Value0)), e.State.Value1, e.State.StateCode, e.State.DetailRaw);
                    else if (e.Domain == ObservedEventDomain.Action)
                        item = new(e.ObservedAtMilliseconds, e.Action.SourceEntityId, SupportEventKind.Action, e.Action.ActionResourceEffectRef, e.Action.Phase, null, e.Action.StateValue);
                    if (item is not null)
                    {
                        if (result.Count < limit) result.Add(item.Value); else truncated = true;
                    }
                }
            });
            if (read.Count == 0) break;
            cursor = read.Cursor;
        }
        // Existing aura lifecycle interpretation is reused; only interpreted windows enter disk files.
        var auras = new List<EncounterAuraWindow>();
        var entities = Entities.Select(e => e.EntityId).Distinct().OrderByDescending(id => IdentityScope.TryGetPcMetadata(id, out _)).ThenByDescending(id => EncounterAnalytics.TargetCategory(this, id) == "Boss").Take(32).ToArray();
        truncated |= Entities.Count > 32;
        foreach (var entity in entities)
        {
            var timeline = ScenePlaybackAuraTimelineReader.Read(TimelineSegment, entity, Math.Max(1, Snapshot.EncounterEndTime));
            foreach (var window in timeline.Coverages)
            {
                if (auras.Count >= limit) { truncated = true; break; }
                if (window.EndMilliseconds < Snapshot.EncounterStartTime) continue;
                auras.Add(new(window.EntityId, window.OriginEntityId, window.DisplayResourceEffectRef,
                    Math.Max(Snapshot.EncounterStartTime, window.StartMilliseconds), window.EndMilliseconds,
                    window.EndMilliseconds >= Snapshot.EncounterEndTime, window.Semantics.Disposition));
            }
        }
        return new() { Available = true, Truncated = truncated, Events = result.OrderBy(e => e.At).ToArray(), Auras = auras.ToArray() };
    }
}
