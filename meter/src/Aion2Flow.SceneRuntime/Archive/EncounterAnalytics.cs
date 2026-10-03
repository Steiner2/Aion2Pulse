using Cloris.Aion2Flow.Protocol.Combat;
using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Projection;

namespace Cloris.Aion2Flow.SceneRuntime.Archive;

public readonly record struct MetricBucket(double Second, double Damage, double Healing);
public readonly record struct SkillAnalysis(int Skill, double Damage, double Healing, double ShieldGranted, double ShieldAbsorbed,
    int Events, int DamageEvents, int Critical, int Back, int Front, int Block, int Parry, int Evade, int Invincible, double PeriodicDamage,
    double PeriodicHealing, int Perfect, int DefensivePerfect);
public readonly record struct TargetAnalysis(int Target, string Category, double Damage, double Healing, double ShieldAbsorbed);
public readonly record struct DeliveryAnalysis(CombatDeliveryKind Delivery, int Events, double Damage, double Healing);
public readonly record struct ObservedTargetChange(long At, int Previous, int Next, int Skill, long Ordinal);
public readonly record struct ObservedDamageGap(long Start, long End);
public enum AnalysisTargetScope { All, Boss, OtherEnemy, Player, Unknown }

public static class EncounterAnalytics
{
    public static CombatMetricDetailEvent[] Select(SceneArchivePayload payload, int player, long from, long to, bool incoming = false)
        => payload.CreateDetailDelta(player).MetricEvents.Where(e => (incoming ? e.TargetId : e.SourceId) == player && e.ObservedAtMilliseconds >= from && e.ObservedAtMilliseconds < to)
            .OrderBy(e => e.ObservedAtMilliseconds).ThenBy(e => e.SourceObservationOrdinal).ToArray();

    public static MetricBucket[] Buckets(IReadOnlyList<CombatMetricDetailEvent> events, long from, long to, int maxBuckets = 300)
    {
        if (to <= from) return [];
        var step = Math.Max(1000, (long)Math.Ceiling((to - from) / (double)Math.Clamp(maxBuckets, 1, 3000) / 1000) * 1000);
        var count = (int)Math.Ceiling((to - from) / (double)step);
        var damage = new double[count]; var healing = new double[count];
        foreach (var e in events)
        {
            if (e.ObservedAtMilliseconds < from || e.ObservedAtMilliseconds >= to) continue;
            var index = (int)((e.ObservedAtMilliseconds - from) / step);
            if (e.Metric == CombatMetricKind.Damage) damage[index] += e.Amount;
            if (e.Metric == CombatMetricKind.Healing) healing[index] += e.Amount;
        }
        return Enumerable.Range(0, count).Select(i =>
        {
            var seconds = Math.Min(step, to - (from + i * step)) / 1000d;
            return new MetricBucket((from + i * step) / 1000d, damage[i] / seconds, healing[i] / seconds);
        }).ToArray();
    }

    public static SkillAnalysis[] Skills(IReadOnlyList<CombatMetricDetailEvent> events)
        => events.GroupBy(e => e.SkillCode).Select(group =>
        {
            var damageEvents = group.Where(e => e.Metric == CombatMetricKind.Damage).ToArray();
            int Flag(DamageModifiers flag) => damageEvents.Count(e => e.Observation.Modifiers.HasFlag(flag));
            double Sum(CombatMetricKind kind) => group.Where(e => e.Metric == kind).Sum(e => (double)e.Amount);
            return new SkillAnalysis(group.Key, Sum(CombatMetricKind.Damage), Sum(CombatMetricKind.Healing), Sum(CombatMetricKind.ShieldGranted), Sum(CombatMetricKind.ShieldAbsorbed),
                group.Count(), damageEvents.Length, Flag(DamageModifiers.Critical), Flag(DamageModifiers.Back), Flag(DamageModifiers.Front), Flag(DamageModifiers.Block), Flag(DamageModifiers.Parry), Flag(DamageModifiers.Evade), Flag(DamageModifiers.Invincible),
                damageEvents.Where(e => e.Delivery == CombatDeliveryKind.Periodic).Sum(e => (double)e.Amount),
                group.Where(e => e.Metric == CombatMetricKind.Healing && e.Delivery == CombatDeliveryKind.Periodic).Sum(e => (double)e.Amount),
                Flag(DamageModifiers.Perfect), Flag(DamageModifiers.DefensivePerfect));
        }).OrderByDescending(e => e.Damage + e.Healing + e.ShieldAbsorbed).ToArray();

    public static string TargetCategory(SceneArchivePayload payload, int id)
    {
        if (payload.Entities.Any(e => e.EntityId == id && e.Kind == NpcKind.Boss) || payload.Bosses.Any(e => e.InstanceId == id)) return "Boss";
        if (payload.Entities.Any(e => e.EntityId == id && e.Kind == NpcKind.Monster)) return "Other enemy";
        if (payload.IdentityScope.TryGetPcMetadata(id, out _)) return "Player";
        return "Unknown";
    }

    public static CombatMetricDetailEvent[] FilterTargets(SceneArchivePayload payload, IReadOnlyList<CombatMetricDetailEvent> events, AnalysisTargetScope scope, bool incoming = false)
    {
        return events.Where(e => CounterpartInScope(payload, incoming ? e.SourceId : e.TargetId, scope)).ToArray();
    }

    public static bool CounterpartInScope(SceneArchivePayload payload, int entityId, AnalysisTargetScope scope)
        => scope == AnalysisTargetScope.All || TargetCategory(payload, entityId) == (scope switch
        { AnalysisTargetScope.Boss => "Boss", AnalysisTargetScope.OtherEnemy => "Other enemy", AnalysisTargetScope.Player => "Player", _ => "Unknown" });

    public static TargetAnalysis[] Targets(SceneArchivePayload payload, IReadOnlyList<CombatMetricDetailEvent> events, bool incoming = false)
        => events.GroupBy(e => incoming ? e.SourceId : e.TargetId).Select(group => new TargetAnalysis(group.Key, TargetCategory(payload, group.Key),
            group.Where(e => e.Metric == CombatMetricKind.Damage).Sum(e => (double)e.Amount),
            group.Where(e => e.Metric == CombatMetricKind.Healing).Sum(e => (double)e.Amount),
            group.Where(e => e.Metric == CombatMetricKind.ShieldAbsorbed).Sum(e => (double)e.Amount)))
            .OrderByDescending(e => e.Damage).ToArray();

    public static double ObservedAuraSeconds(IEnumerable<EncounterAuraWindow> windows, long from, long to)
    {
        var sorted = windows.Select(w => (Start: Math.Max(from, w.Start), End: Math.Min(to, w.End))).Where(w => w.End > w.Start).OrderBy(w => w.Start).ToArray();
        long total = 0, end = from;
        foreach (var window in sorted)
        {
            total += Math.Max(0, window.End - Math.Max(end, window.Start)); end = Math.Max(end, window.End);
        }
        return total / 1000d;
    }

    public static DeliveryAnalysis[] Deliveries(IReadOnlyList<CombatMetricDetailEvent> events)
        => events.Where(e => e.Metric is CombatMetricKind.Damage or CombatMetricKind.Healing).GroupBy(e => e.Delivery)
            .Select(g => new DeliveryAnalysis(g.Key, g.Count(), g.Where(e => e.Metric == CombatMetricKind.Damage).Sum(e => (double)e.Amount),
                g.Where(e => e.Metric == CombatMetricKind.Healing).Sum(e => (double)e.Amount)))
            .OrderBy(g => g.Delivery).ToArray();

    public static ObservedTargetChange[] TargetChanges(IReadOnlyList<CombatMetricDetailEvent> events, bool incoming = false)
    {
        var damage = events.Where(e => e.Metric == CombatMetricKind.Damage && e.Amount > 0)
            .OrderBy(e => e.ObservedAtMilliseconds).ThenBy(e => e.SourceObservationOrdinal).ToArray();
        var changes = new List<ObservedTargetChange>();
        for (var i = 1; i < damage.Length; i++)
        {
            var previous = incoming ? damage[i - 1].SourceId : damage[i - 1].TargetId;
            var next = incoming ? damage[i].SourceId : damage[i].TargetId;
            if (previous != next) changes.Add(new(damage[i].ObservedAtMilliseconds, previous, next, damage[i].SkillCode, damage[i].SourceObservationOrdinal));
        }
        return changes.ToArray();
    }

    public static ObservedDamageGap[] DamageGaps(IReadOnlyList<CombatMetricDetailEvent> events, long thresholdMilliseconds = 3000)
    {
        var times = events.Where(e => e.Metric == CombatMetricKind.Damage && e.Amount > 0).Select(e => e.ObservedAtMilliseconds).Distinct().Order().ToArray();
        return times.Zip(times.Skip(1)).Where(p => p.Second - p.First > Math.Max(0, thresholdMilliseconds))
            .Select(p => new ObservedDamageGap(p.First, p.Second)).ToArray();
    }

    public static bool ComparableBoss(SceneArchivePayload a, SceneArchivePayload b)
        => a.Snapshot.MapId != 0 && a.Snapshot.MapId == b.Snapshot.MapId && a.BossNpcCodes.Count > 0 &&
            a.BossNpcCodes.AsSpan().ToArray().Order().SequenceEqual(b.BossNpcCodes.AsSpan().ToArray().Order());
}
