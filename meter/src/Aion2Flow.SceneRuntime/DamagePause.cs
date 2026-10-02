using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Observation;

namespace Cloris.Aion2Flow.SceneRuntime;

public sealed partial class SceneLiveReadModel
{
    private readonly ActiveDamageClock _damageClock = new();
    private bool _pauseDamageTime;
    public bool IsDamageTimePaused { get { lock (_gate) return _pauseDamageTime && _damageClock.IsPaused(GetPullNow()); } }

    public void ConfigureDamagePause(bool enabled, int gapSeconds = 3)
    {
        lock (_gate)
        {
            var threshold = Math.Clamp(gapSeconds, 1, 10) * 1_000L;
            if (_pauseDamageTime != enabled || _damageClock.GapThresholdMilliseconds != threshold)
                ResetCore(_timeProvider.GetUtcNow());
            _pauseDamageTime = enabled;
            _damageClock.GapThresholdMilliseconds = threshold;
        }
    }

    private void ObserveDamageTime(in PacketObservationSource packet, int sourceId, int targetId, in CombatWireObservation observation)
    {
        if (!_pauseDamageTime || !IsPullParticipant(sourceId) || IsPullParticipant(targetId)) return;
        var resolution = CombatOccurrenceResolution.Primary;
        if (!CombatContributionResolver.TryResolve(sourceId, targetId, in observation, in resolution, out var contribution) ||
            contribution.Metric != CombatMetricKind.Damage || contribution.Amount <= 0) return;
        if (Owner.Entities.TryGet(targetId, out var target) && (target.IsPlayer || target.OwnerEntityId.HasValue)) return;
        var at = PacketTime(in packet);
        _damageClock.Observe(at);
        AnchorPullClock(at);
        Owner.SetRateDuration(_damageClock.Duration);
    }
}
