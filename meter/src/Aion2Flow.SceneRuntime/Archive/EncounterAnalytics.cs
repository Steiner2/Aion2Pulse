using Cloris.Aion2Flow.Protocol.Combat;
using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Projection;

namespace Cloris.Aion2Flow.SceneRuntime.Archive;

public readonly record struct MetricBucket(double Second, double Damage, double Healing);
public readonly record struct SkillAnalysis(int Skill, double Damage, double Healing, double ShieldGranted, double ShieldAbsorbed,
    int Events, int DamageEvents, int Critical, int Back, int Front, int Block, int Parry, int Evade, int Invincible, double PeriodicDamage);
public readonly record struct TargetAnalysis(int Target, string Category, double Damage, double Healing, double ShieldAbsorbed);

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
                damageEvents.Where(e => e.Delivery == CombatDeliveryKind.Periodic).Sum(e => (double)e.Amount));
        }).OrderByDescending(e => e.Damage + e.Healing + e.ShieldAbsorbed).ToArray();

    public static string TargetCategory(SceneArchivePayload payload, int id)
    {
        if (payload.Entities.Any(e => e.EntityId == id && e.Kind == NpcKind.Boss) || payload.Bosses.Any(e => e.InstanceId == id)) return "Boss";
        if (payload.Entities.Any(e => e.EntityId == id && e.Kind == NpcKind.Monster)) return "Other enemy";
        if (payload.IdentityScope.TryGetPcMetadata(id, out _)) return "Player";
        return "Unknown";
    }

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

    public static bool ComparableBoss(SceneArchivePayload a, SceneArchivePayload b)
        => a.Snapshot.MapId != 0 && a.Snapshot.MapId == b.Snapshot.MapId && a.BossNpcCodes.Count > 0 &&
            a.BossNpcCodes.AsSpan().ToArray().Order().SequenceEqual(b.BossNpcCodes.AsSpan().ToArray().Order());
}
