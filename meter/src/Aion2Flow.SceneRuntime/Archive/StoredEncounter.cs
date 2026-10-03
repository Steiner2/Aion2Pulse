using System.Text.Json.Serialization;
using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Identity;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Projection;

namespace Cloris.Aion2Flow.SceneRuntime.Archive;

// Explicit versioned disk format; runtime journals and packet buffers never enter the file.
internal sealed class StoredEncounter
{
    public int Version { get; init; } = 2;
    public EncounterSupportData Support { get; init; } = new();
    public Guid Id { get; init; }
    public DateTimeOffset ArchivedAt { get; init; }
    public string Trigger { get; init; } = "";
    public bool IsAutomatic { get; init; }
    public Guid EncounterId { get; init; }
    public SceneKind Kind { get; init; }
    public DateTimeOffset SceneStarted { get; init; }
    public uint MapId { get; init; }
    public uint MapInstanceId { get; init; }
    public long Start { get; init; }
    public long End { get; init; }
    public long Duration { get; init; }
    public EncounterSummarySnapshot Encounter { get; init; }
    public NpcRuntimeObservationSnapshot? Target { get; init; }
    public CombatantSnapshotEntry[] Metrics { get; init; } = [];
    public SceneBossFocusSnapshot[] BossFocuses { get; init; } = [];
    public int[] BossNpcCodes { get; init; } = [];
    public PcMetadataEntry[] Players { get; init; } = [];
    public NpcCodeEntry[] Npcs { get; init; } = [];
    public MapCodeEntry[] Maps { get; init; } = [];
    public SceneArchiveEntityIdentity[] Entities { get; init; } = [];
    public SceneArchiveEntityVital[] Vitals { get; init; } = [];
    public SceneArchiveBossFocus[] Bosses { get; init; } = [];
    public DirectedPairSnapshot[] Pairs { get; init; } = [];
    public CombatantSummary[] Combatants { get; init; } = [];
    public Dictionary<int, CombatDetailDelta> Details { get; init; } = [];
}

[JsonSourceGenerationOptions(IgnoreReadOnlyProperties = true)]
[JsonSerializable(typeof(StoredEncounter))]
internal partial class EncounterJsonContext : JsonSerializerContext;
