namespace Cloris.Aion2Flow.SceneRuntime.Observation;

internal interface ILiveSceneCollectionPolicy
{
    void StartMapContext(in PacketObservationSource packet, uint mapId);

    bool ShouldAppendCombat(in PacketObservationSource packet, int sourceId, int targetId, in CombatWireObservation observation, IRuntimeObservationSink sink);

    void OnNpcCombatState(in PacketObservationSource packet, int entityId, bool? active, bool dead);

    bool ShouldAppendExtendedObservation();

    bool ShouldAppendEntityVitalObservation();

    void OnBossMetadataChanged();
}
