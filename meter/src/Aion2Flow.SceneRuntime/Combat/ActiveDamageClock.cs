namespace Cloris.Aion2Flow.SceneRuntime.Combat;

/// <summary>Accumulates short intervals between group damage events, excluding long gaps.</summary>
internal sealed class ActiveDamageClock
{
    public long GapThresholdMilliseconds { get; set; } = 3_000;
    public bool HasDamage { get; private set; }
    public long LastDamageAt { get; private set; }
    private long _activeMilliseconds;
    public long Duration => Math.Max(1_000, _activeMilliseconds);

    public void Observe(long at)
    {
        if (HasDamage && at <= LastDamageAt) return;
        if (HasDamage && at - LastDamageAt <= GapThresholdMilliseconds)
            _activeMilliseconds += at - LastDamageAt;
        LastDamageAt = at;
        HasDamage = true;
    }

    public bool IsPaused(long now) => HasDamage && now - LastDamageAt >= GapThresholdMilliseconds;
    public void Reset() { HasDamage = false; LastDamageAt = _activeMilliseconds = 0; }
}
