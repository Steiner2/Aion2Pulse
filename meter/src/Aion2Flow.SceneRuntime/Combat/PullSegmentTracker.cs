namespace Cloris.Aion2Flow.SceneRuntime.Combat;

public enum PullSegmentState { Waiting, Recording, Frozen }

/// <summary>One pull may contain many enemies. All times share the capture timeline.</summary>
public sealed class PullSegmentTracker
{
    private readonly Dictionary<int, EnemyState> _enemies = [];
    public PullSegmentState State { get; private set; }
    public long StartedAt { get; private set; }
    public long LastActivityAt { get; private set; }
    public long EndedAt { get; private set; }
    public string CompletionReason { get; private set; } = "waiting";
    public long IdleTimeoutMilliseconds { get; set; } = 5_000;
    public bool HasBoss { get; private set; }
    public bool HasTrainingDummy { get; private set; }

    public bool CanStart(bool periodic, int enemyId, long now)
    {
        if (State == PullSegmentState.Recording)
            return now >= StartedAt && (!periodic || _enemies.ContainsKey(enemyId));
        // A trailing DoT must never create a new pull, even against another old target.
        if (periodic)
            return false;
        return now >= EndedAt && enemyId > 0;
    }

    public void ObserveDamage(int enemyId, long at, bool boss, bool trainingDummy)
    {
        if (State != PullSegmentState.Recording)
        {
            _enemies.Clear();
            StartedAt = at;
            LastActivityAt = at;
            EndedAt = 0;
            CompletionReason = "recording";
            HasBoss = false;
            HasTrainingDummy = false;
            State = PullSegmentState.Recording;
        }
        LastActivityAt = Math.Max(LastActivityAt, at);
        HasBoss |= boss;
        HasTrainingDummy |= trainingDummy;
        var active = _enemies.TryGetValue(enemyId, out var previous) && previous.Active == true ? true : (bool?)null;
        _enemies[enemyId] = new EnemyState(at, active, previous.ObservedAt);
    }

    public void ObserveEnemyState(int enemyId, bool? active, bool dead, long at)
    {
        if (State != PullSegmentState.Recording || !_enemies.TryGetValue(enemyId, out var state))
            return;
        // Only explicit state received after the last hit may close an enemy.
        if (at < state.LastDamageAt)
            return;
        var nextActive = dead ? false : active;
        if (!nextActive.HasValue || (state.Active == false && nextActive == false))
            return;
        _enemies[enemyId] = state with { Active = nextActive, ObservedAt = at };
    }

    public bool Advance(long now, bool captureHealthy = true)
    {
        if (State != PullSegmentState.Recording)
            return false;
        if (!captureHealthy)
            return Freeze(LastActivityAt, "capture-interrupted");

        var allInactive = _enemies.Count > 0;
        var latestStateAt = LastActivityAt;
        foreach (var enemy in _enemies.Values)
        {
            allInactive &= enemy.Active == false;
            latestStateAt = Math.Max(latestStateAt, enemy.ObservedAt);
        }
        // Allow late packets and staggered death/state messages to settle first.
        if (allInactive && now - latestStateAt >= 1_000)
            return Freeze(latestStateAt, "enemies-inactive");
        // Without an explicit end, boss intermissions are not split by an idle timer.
        if (HasBoss)
            return false;
        var timeout = HasTrainingDummy ? 10_000 : Math.Clamp(IdleTimeoutMilliseconds, 2_000, 30_000);
        // Active flags can remain set after despawn. Actual damage controls trash
        // inactivity; known bosses already have separate intermission protection.
        return now - LastActivityAt >= timeout && Freeze(LastActivityAt, "idle-heuristic");
    }

    public bool Freeze(long at, string reason)
    {
        if (State != PullSegmentState.Recording)
            return false;
        EndedAt = Math.Max(LastActivityAt, at);
        CompletionReason = reason;
        State = PullSegmentState.Frozen;
        return true;
    }

    public void Reset()
    {
        _enemies.Clear();
        State = PullSegmentState.Waiting;
        StartedAt = LastActivityAt = EndedAt = 0;
        HasBoss = HasTrainingDummy = false;
        CompletionReason = "waiting";
    }

    private readonly record struct EnemyState(long LastDamageAt, bool? Active, long ObservedAt);
}
