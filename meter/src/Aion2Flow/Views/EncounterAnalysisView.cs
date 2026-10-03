using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;
using Cloris.Aion2Flow.Protocol.Combat;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Media;
using Cloris.Aion2Flow.Controls;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.SceneRuntime.Archive;
using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.SceneRuntime.Projection;

namespace Cloris.Aion2Flow.Views;

public sealed class EncounterAnalysisView : UserControl
{
    private readonly record struct AnalysisRow(int Skill, string Label, string Value, string Note);
    private readonly SceneArchivePayload _payload;
    private readonly int _player;
    private readonly SceneDisplayContext _display;
    private readonly IReadOnlyList<ArchivedEncounterRecord> _history;
    private readonly Slider _from;
    private readonly Slider _to;
    private readonly ComboBox _direction = new() { ItemsSource = new[] { "Outgoing", "Incoming" }, SelectedIndex = 0, Width = 125 };
    private readonly ComboBox _targetScope = new() { ItemsSource = new[] { "All counterparts", "Known bosses", "Other enemies", "Players", "Unknown identities" }, SelectedIndex = 0, Width = 160 };
    private readonly TextBlock _range = Text("");
    private readonly TabControl _tabs = new() { Name = "AnalysisTabs" };
    private bool _refreshing;

    public EncounterAnalysisView(SceneArchivePayload payload, int player, SceneDisplayContext display, IReadOnlyList<ArchivedEncounterRecord> history)
    {
        _payload = payload; _player = player; _display = display; _history = history;
        DisplayContextProvider.SetDisplayContext(this, display);
        var seconds = Math.Max(0.001, (payload.Snapshot.EncounterEndTime - payload.Snapshot.EncounterStartTime + 1) / 1000d);
        _from = new Slider { Minimum = 0, Maximum = seconds - 0.001, Value = 0, Width = 155 };
        _to = new Slider { Minimum = 0.001, Maximum = seconds, Value = seconds, Width = 155 };
        PulseHelp.Tip(_from, "Move the start of the analysis range. All breakdowns use this elapsed encounter offset; rate time includes pauses.");
        PulseHelp.Tip(_to, "Move the end of the analysis range. Review shows the final five seconds of this selection; other tabs use the full selection.");
        PulseHelp.Tip(_direction, "Outgoing follows effects from this player. Incoming follows effects targeting this player; target breakdowns then identify their sources.");
        PulseHelp.Tip(_targetScope, "Filter combat metrics by the outgoing target or incoming source: boss, other enemy, player or unresolved identity. Buff/cooldown support observations still refer to the selected player and time range.");
        var header = new StackPanel { Spacing = 5 };
        header.Children.Add(new CombatantDisplay { EntityId = player, IconSize = 18 });
        var controls = new WrapPanel();
        controls.Children.Add(Text("From ")); controls.Children.Add(_from); controls.Children.Add(Text(" To ")); controls.Children.Add(_to); controls.Children.Add(_direction); controls.Children.Add(_targetScope);
        header.Children.Add(controls); header.Children.Add(_range);
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), RowSpacing = 8 };
        grid.Children.Add(header); Grid.SetRow(_tabs, 1); grid.Children.Add(_tabs); Content = grid;
        _from.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) Refresh(); };
        _to.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) Refresh(); };
        _direction.SelectionChanged += (_, _) => Refresh(); _targetScope.SelectionChanged += (_, _) => Refresh(); Refresh();
    }

    private static TextBlock Text(string value) => new() { Text = value, TextWrapping = TextWrapping.Wrap, FontSize = 11, Foreground = Brush.Parse("#AAB7C6") };
    private static string Amount(double value) => value >= 1_000_000 ? $"{value / 1_000_000:0.00}m" : value >= 1000 ? $"{value / 1000:0.0}k" : $"{value:0}";
    private static Control Page(params Control[] children)
    {
        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(0, 8) };
        foreach (var child in children) panel.Children.Add(child);
        return new ScrollViewer { Content = panel };
    }
    private static Control Rows(IEnumerable<AnalysisRow> source)
    {
        var rows = source.ToArray();
        if (rows.Length == 0) return Text("No observations in this range. Missing data is not proof that an effect did not occur.");
        var list = new ListBox { ItemsSource = rows, Height = 210, ItemTemplate = new FuncDataTemplate<AnalysisRow>((row, _) =>
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,120"), RowDefinitions = new RowDefinitions("Auto,Auto"), ColumnSpacing = 8 };
            Control name = row.Skill > 0 ? new SkillDisplay { SkillCode = row.Skill, IconSize = 20 } : Text(row.Label);
            grid.Children.Add(name);
            var value = Text(row.Value); value.TextAlignment = TextAlignment.Right; Grid.SetColumn(value, 1); grid.Children.Add(value);
            var note = Text(row.Note); Grid.SetRow(note, 1); Grid.SetColumnSpan(note, 2); grid.Children.Add(note); return PulseHelp.Tip(grid, $"{row.Value} · {row.Note}");
        }) };
        ScrollViewer.SetHorizontalScrollBarVisibility(list, ScrollBarVisibility.Disabled);
        list.Styles.Add(new Style(x => x.OfType<ListBoxItem>()) { Setters = { new Setter(ContentControl.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch) } });
        return list;
    }
    private void Add(string name, Control page) => _tabs.Items.Add(PulseHelp.Tip(new TabItem { Header = name, Content = page }, PulseHelp.Analysis(name)));
    private string EffectName(ResourceEffectRef effect)
    {
        var name = _display.ResolveSkillName(effect);
        return string.IsNullOrWhiteSpace(name) ? $"Effect {effect.RawId}" : name;
    }
    private CombatMetricDetailEvent[] SelectedEvents(long from, long to, bool incoming)
        => EncounterAnalytics.FilterTargets(_payload, EncounterAnalytics.Select(_payload, _player, from, to, incoming), (AnalysisTargetScope)Math.Max(0, _targetScope.SelectedIndex), incoming);
    private long Absolute(double seconds) => _payload.Snapshot.EncounterStartTime + (long)Math.Round(seconds * 1000);
    private double Relative(long at) => (at - _payload.Snapshot.EncounterStartTime) / 1000d;
    private string Time(long at) => TimeSpan.FromMilliseconds(Math.Max(0, at - _payload.Snapshot.EncounterStartTime)).ToString(@"mm\:ss\.fff", CultureInfo.InvariantCulture);

    private void Refresh()
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            if (_to.Value <= _from.Value) _to.Value = Math.Min(_to.Maximum, _from.Value + 0.001);
            var from = Absolute(_from.Value); var to = Absolute(_to.Value); var incoming = _direction.SelectedIndex == 1;
            var events = SelectedEvents(from, to, incoming);
            var seconds = Math.Max(0.001, (to - from) / 1000d);
            var damage = events.Where(e => e.Metric == CombatMetricKind.Damage).Sum(e => (double)e.Amount);
            var healing = events.Where(e => e.Metric == CombatMetricKind.Healing).Sum(e => (double)e.Amount);
            _range.Text = $"{_from.Value:0.00}–{_to.Value:0.00}s · {events.Length:N0} observed effects · elapsed range time";
            var selected = Math.Max(0, _tabs.SelectedIndex); _tabs.Items.Clear();
            var chart = EncounterAnalytics.Buckets(events, from, to).Select(p => p with { Second = p.Second - _payload.Snapshot.EncounterStartTime / 1000d }).ToArray();
            Add("Trend", Page(Text($"Damage {Amount(damage)} · DPS {Amount(damage / seconds)} · Healing {Amount(healing)} · HPS {Amount(healing / seconds)}"),
                new AnalysisTrend(chart, "DPS", "HPS"), Text("Rates use elapsed selected time, including pauses. They can differ from the overlay's active-time rates. Adaptive buckets preserve totals; shields remain separate."),
                Rows(events.Where(e => e.Metric is CombatMetricKind.ShieldGranted or CombatMetricKind.ShieldAbsorbed).GroupBy(e => e.Metric).Select(g => new AnalysisRow(0, g.Key.ToString(), Amount(g.Sum(e => (double)e.Amount)), "Packet-reported shield amount")))));
            var skills = EncounterAnalytics.Skills(events);
            var mechanics = _payload.CreateDetailDelta(_player).MechanicEvents.Where(e => (incoming ? e.TargetId : e.SourceId) == _player && e.ObservedAtMilliseconds >= from && e.ObservedAtMilliseconds < to && EncounterAnalytics.CounterpartInScope(_payload, incoming ? e.SourceId : e.TargetId, (AnalysisTargetScope)Math.Max(0, _targetScope.SelectedIndex))).ToArray();
            var deliveries = EncounterAnalytics.Deliveries(events);
            var gaps = EncounterAnalytics.DamageGaps(events);
            Add("Skills", Page(Text("Counts refer to observed effects, not casts. Crit/back/front fractions use damage events; multi-hit packets are not expanded into speculative casts."),
                Text($"Mechanic observations: {mechanics.Sum(e => e.Mechanic.AttemptCount)} attempts · {mechanics.Sum(e => e.Mechanic.EvadeCount)} evades · {mechanics.Sum(e => e.Mechanic.InvincibleCount)} invincible outcomes"),
                Rows(skills.Select(s => new AnalysisRow(s.Skill, "", $"{Amount(s.Damage)} / {Amount(s.Healing)}", $"{s.Events} effects · crit {s.Critical}/{s.DamageEvents} · back {s.Back}/{s.DamageEvents} · front {s.Front}/{s.DamageEvents} · block {s.Block} · parry {s.Parry} · DoT {Amount(s.PeriodicDamage)} ({(s.Damage > 0 ? s.PeriodicDamage / s.Damage * 100 : 0):0.0}%) · HoT {Amount(s.PeriodicHealing)} ({(s.Healing > 0 ? s.PeriodicHealing / s.Healing * 100 : 0):0.0}%) · perfect {s.Perfect} · defensive perfect {s.DefensivePerfect} · shields {Amount(s.ShieldGranted)} granted / {Amount(s.ShieldAbsorbed)} absorbed"))),
                Text("Delivery breakdown · direct, periodic, drain, regeneration, reflect and pool remain separate"),
                Rows(deliveries.Select(d => new AnalysisRow(0, d.Delivery.ToString(), $"{Amount(d.Damage)} / {Amount(d.Healing)}", $"{d.Events} effects · {(damage > 0 ? d.Damage / damage * 100 : 0):0.0}% damage · {(healing > 0 ? d.Healing / healing * 100 : 0):0.0}% healing"))),
                Text($"{gaps.Length} gaps above 3 seconds between this player's observed damage events; these do not identify cutscenes or invulnerability."),
                new ObservedTimelineTracks(gaps.Select(g => new ObservedTrackMark("Damage gap", Relative(g.Start), Relative(g.End), "No positive damage from this player between these observations. Not an inferred phase boundary.")), Relative(from), Relative(to))));
            var targetEvents = EncounterAnalytics.Targets(_payload, events, incoming);
            var changes = EncounterAnalytics.TargetChanges(events, incoming);
            Add("Targets", Page(Text($"{changes.Length} successive damage destination/source changes. Multi-target packets can create changes without a deliberate target switch."),
                Rows(targetEvents.Select(t => new AnalysisRow(0, _display.ResolveEntityName(t.Target), Amount(t.Damage), $"{t.Category} · healing {Amount(t.Healing)} · absorbed {Amount(t.ShieldAbsorbed)} · {(damage > 0 ? t.Damage / damage * 100 : 0):0.0}% of selected damage"))),
                Rows(events.Where(e => e.Metric == CombatMetricKind.Damage).GroupBy(e => EncounterAnalytics.TargetCategory(_payload, incoming ? e.SourceId : e.TargetId)).Select(g => new AnalysisRow(0, g.Key, Amount(g.Sum(e => (double)e.Amount)), "Known boss / other enemy / player / unknown; unknown identities remain separate."))),
                Text("Observed target/source change timeline"),
                new ObservedTimelineTracks(changes.Select(c => new ObservedTrackMark(_display.ResolveEntityName(c.Next), Relative(c.At), Relative(c.At), $"{Time(c.At)} · {_display.ResolveEntityName(c.Previous)} → {_display.ResolveEntityName(c.Next)} · {_display.ResolveSkillName(c.Skill)}; successive damage effects, not proof of a chosen target change.")), Relative(from), Relative(to)),
                Rows(changes.Select(c => new AnalysisRow(c.Skill, "", Time(c.At), $"{_display.ResolveEntityName(c.Previous)} → {_display.ResolveEntityName(c.Next)} · {EncounterAnalytics.TargetCategory(_payload, c.Next)}")))));
            AddEffects(from, to, events);
            AddReview(from, to);
            AddComparison(from, to);
            Add("Coverage", Page(Text("Observed: damage/healing, targets, modifier flags, shields, resources, typed action/cooldown state, aura windows and HP snapshots when received."),
                Text("Not established: exact cast starts/cancels, effective overheal, interrupts/dispels, threat percentages, position/dodge errors and explicit cutscene/phase boundaries. These require verified Global packet semantics and are not fabricated."),
                Text($"Capture end: {_payload.Snapshot.Encounter.Reason} · persisted support: {_payload.SupportData.Available} · truncated support: {_payload.SupportData.Truncated}")));
            _tabs.SelectedIndex = Math.Min(selected, _tabs.Items.Count - 1);
        }
        finally { _refreshing = false; }
    }

    private void AddEffects(long from, long to, CombatMetricDetailEvent[] selectedEvents)
    {
        var support = _payload.SupportData;
        var local = _payload.IdentityScope.TryGetPcMetadata(_player, out var metadata) && metadata.IsLocalPlayer;
        var auraRows = support.Auras.Where(a => a.EntityId == _player && a.End > from && a.Start < to).GroupBy(a => a.Skill).Select(g =>
        {
            var seconds = EncounterAnalytics.ObservedAuraSeconds(g, from, to);
            return new AnalysisRow(0, EffectName(g.Key), $"{seconds:0.00}s", $"Observed {string.Join("/", g.Select(a => a.Disposition).Distinct())} coverage · {seconds / Math.Max(.001, (to - from) / 1000d) * 100:0.0}% of range · {g.Count()} windows · {string.Join(", ", g.Take(4).Select(a => $"{Time(a.Start)}–{Time(a.End)}"))}{(g.Count() > 4 ? " …" : "")} · {(g.Any(a => a.OpenEnd) ? "open end / bounded by archive" : "bounded observed intervals")}");
        });
        var states = support.Events.Where(e => e.At >= from && e.At < to && (e.EntityId == _player || e.EntityId == 0 && local)).Select(e =>
        {
            var description = e.Kind switch
            {
                SupportEventKind.Health => $"HP {e.Value:N0} / {(e.Maximum is { } max ? max.ToString("N0") : "unknown")}",
                SupportEventKind.Charges when CooldownChargeObservationDetail.TryDecode(e.Detail, out _, out var charges) => $"{charges} charges · next in {e.Value / 1000d:0.00}s",
                SupportEventKind.Cooldown => $"remaining {e.Value / 1000d:0.00}s",
                _ => $"observed phase {e.Value} · state {e.Detail}"
            };
            return new AnalysisRow(0, $"{Time(e.At)} · {e.Kind} · {EffectName(e.Skill)}", description, e.EntityId == 0 ? "Unassigned local-client state; availability is not a cast." : "Typed observation; exact action phase meaning requires Global validation.");
        });
        var resources = _payload.CreateDetailDelta(_player).ResourceEvents.Where(e => e.SourceId == _player && e.ObservedAtMilliseconds >= from && e.ObservedAtMilliseconds < to)
            .Select(e => new AnalysisRow(e.SkillCode, "", $"{e.Amount:N0}", $"{Time(e.ObservedAtMilliseconds)} · {e.Resource.Resource} · {e.Resource.Flow} · {e.Resource.Delivery}; observed change, not a reconstructed balance"));
        var hp = support.Events.Where(e => e.Kind == SupportEventKind.Health && e.EntityId == _player && e.At >= from && e.At < to).Select(e => new MetricBucket((e.At - _payload.Snapshot.EncounterStartTime) / 1000d, e.Value, e.Maximum ?? 0)).ToArray();
        var auraMarks = support.Auras.Where(a => a.EntityId == _player && a.End > from && a.Start < to).Select(a => new ObservedTrackMark(
            a.Disposition + " · " + EffectName(a.Skill), Relative(a.Start), Relative(a.End), $"Observed aura window · origin {_display.ResolveEntityName(a.OriginId)} · {(a.OpenEnd ? "open end, bounded by archive" : "bounded observed window")}", a.OpenEnd));
        var stateMarks = support.Events.Where(e => e.Kind is SupportEventKind.Cooldown or SupportEventKind.Charges or SupportEventKind.Action &&
                (e.EntityId == _player || e.EntityId == 0 && local) && e.At < to && (e.At >= from || e.Kind == SupportEventKind.Cooldown && e.Value > 0 && Relative(e.At) + e.Value / 1000d > Relative(from)))
            .Select(e => new ObservedTrackMark(e.Kind + " · " + EffectName(e.Skill), Relative(e.At),
                Relative(e.At) + (e.Kind == SupportEventKind.Cooldown ? Math.Max(0, e.Value) / 1000d : 0),
                e.Kind == SupportEventKind.Cooldown ? $"{Time(e.At)} · reported remaining {e.Value / 1000d:0.00}s. Bar projects this observation; it does not prove the skill stayed unavailable or was cast." : $"{Time(e.At)} · observed {e.Kind} · value {e.Value} · detail {e.Detail}; not a verified cast."));
        var effectChart = EncounterAnalytics.Buckets(selectedEvents, from, to).Select(p => p with { Second = p.Second - _payload.Snapshot.EncounterStartTime / 1000d }).ToArray();
        var resourceTotals = _payload.CreateDetailDelta(_player).ResourceEvents.Where(e => e.SourceId == _player && e.ObservedAtMilliseconds >= from && e.ObservedAtMilliseconds < to)
            .GroupBy(e => (e.Resource.Resource, e.Resource.Flow)).Select(g => new AnalysisRow(0, $"{g.Key.Resource} · {g.Key.Flow}", Amount(g.Sum(e => (double)e.Amount)), $"{g.Count()} observed changes; not a reconstructed balance"));
        Add("Effects", Page(Text(support.Available ? "Observed buffs, cooldowns, charges, actions, HP and resources. Local-client cooldowns do not imply party coverage." : "Legacy archive: support state was not persisted. Combat and resource events remain available."),
            Text("Buff windows and damage bursts · shared elapsed axis · up to 12 lanes"),
            new AnalysisTrend(effectChart, "DPS", "HPS", Relative(from), Relative(to), 144),
            new ObservedTimelineTracks(auraMarks, Relative(from), Relative(to)), Rows(auraRows),
            Text("Cooldown / charge / action observations · up to 12 lanes"), new ObservedTimelineTracks(stateMarks, Relative(from), Relative(to)), Rows(states),
            new AnalysisTrend(hp, "HP", "Max HP", Relative(from), Relative(to)), Text("Resource gain / spend"), Rows(resourceTotals), Rows(resources), Text(support.Truncated ? "Support recording reached its cap; coverage is incomplete." : "Aura coverage is observed coverage, not guaranteed full uptime. Missing observations stay unknown.")));
    }

    private void AddReview(long from, long to)
    {
        var incidentStart = Math.Max(from, to - 5000);
        var incoming = SelectedEvents(incidentStart, to, true);
        var rows = incoming.Select(e => new AnalysisRow(e.SkillCode, "", $"{e.Amount:N0} {e.Metric}", $"{Time(e.ObservedAtMilliseconds)} · from {_display.ResolveEntityName(e.SourceId)} · {e.Delivery} · {e.Observation.Modifiers}"));
        var zeros = _payload.SupportData.Events.Where(e => e.Kind == SupportEventKind.Health && e.EntityId == _player && e.Value <= 0 && e.At >= incidentStart && e.At < to).ToArray();
        var bossHp = _payload.SupportData.Events.Where(e => e.Kind == SupportEventKind.Health && EncounterAnalytics.TargetCategory(_payload, e.EntityId) == "Boss" && e.At >= from && e.At < to)
            .Select(e => new AnalysisRow(0, _display.ResolveEntityName(e.EntityId), $"{e.Value:N0}", $"{Time(e.At)} · max {(e.Maximum is { } max ? max.ToString("N0") : "unknown")} · observed HP, not a verified phase boundary"));
        var charts = _payload.SupportData.Events.Where(e => e.Kind == SupportEventKind.Health && EncounterAnalytics.TargetCategory(_payload, e.EntityId) == "Boss" && e.At >= from && e.At < to)
            .GroupBy(e => e.EntityId).Take(3).Select(g => (Control)Page(Text(_display.ResolveEntityName(g.Key)),
                new AnalysisTrend(g.Select(e => new MetricBucket(Relative(e.At), e.Value, e.Maximum ?? 0)).ToArray(), "HP", "Max HP", Relative(from), Relative(to)))).ToArray();
        var bossCharts = new StackPanel { Spacing = 4 };
        foreach (var chart in charts) bossCharts.Children.Add(chart);
        var allIncoming = SelectedEvents(from, to, true);
        var received = allIncoming.GroupBy(e => e.Metric).Select(g => new AnalysisRow(0, g.Key.ToString(), Amount(g.Sum(e => (double)e.Amount)), $"{g.Count()} incoming effects in the full selected range · reported amounts, not effective-overheal or inferred mitigation"));
        Add("Review", Page(Text("Incoming totals · full selected range"), Rows(received), Text($"Incoming events during the last {Math.Min(5, (to - from) / 1000d):0.00}s of the range. {zeros.Length} zero-HP observations; a killing blow is not inferred."),
            Rows(rows), Text("Boss HP progression · up to three observed bosses"), bossCharts, Rows(bossHp), Text("For a different incident, move the range end. Missing player HP/death evidence prevents a verified death explanation or effective-overheal calculation.")));
    }

    private void AddComparison(long from, long to)
    {
        var relativeStart = from - _payload.Snapshot.EncounterStartTime; var relativeEnd = to - _payload.Snapshot.EncounterStartTime;
        var rows = _history.Where(r => EncounterAnalytics.ComparableBoss(_payload, r.ScenePayload)).Select(r =>
        {
            var p = r.ScenePayload;
            var pcs = p.Snapshot.Combatants.AsSpan().ToArray().Where(c => c.Metrics.IsVisiblePlayerCombatant).ToArray();
            var end = Math.Min(p.Snapshot.EncounterEndTime + 1, p.Snapshot.EncounterStartTime + relativeEnd);
            var start = p.Snapshot.EncounterStartTime + relativeStart;
            var seconds = Math.Max(0, end - start) / 1000d;
            var amount = pcs.Sum(pc => EncounterAnalytics.FilterTargets(p, EncounterAnalytics.Select(p, pc.Id, start, end), (AnalysisTargetScope)Math.Max(0, _targetScope.SelectedIndex)).Where(e => e.Metric == CombatMetricKind.Damage).Sum(e => (double)e.Amount));
            var healing = pcs.Sum(pc => EncounterAnalytics.FilterTargets(p, EncounterAnalytics.Select(p, pc.Id, start, end), (AnalysisTargetScope)Math.Max(0, _targetScope.SelectedIndex)).Where(e => e.Metric == CombatMetricKind.Healing).Sum(e => (double)e.Amount));
            return new AnalysisRow(0, r.ArchivedAt.ToLocalTime().ToString("dd MMM HH:mm"), seconds > 0 ? $"{Amount(amount / seconds)} DPS / {Amount(healing / seconds)} HPS" : "No overlap",
                $"{seconds:0.00}s elapsed · {pcs.Length} players · damage {Amount(amount)} · healing {Amount(healing)} · same known boss IDs/map · {PulseHistoryWindow.EndReason(p.Snapshot.Encounter.Reason)}{(seconds < (relativeEnd - relativeStart) / 1000d ? " · shortened range" : "")}");
        });
        Add("Compare", Page(Text("Outgoing group damage and healing against the selected counterpart category. Same known boss and map only. Compare the same elapsed offsets, not inferred phases. Group size, shorter overlap and incomplete capture affect comparability."), Rows(rows)));
    }
}

internal sealed class AnalysisTrend(IReadOnlyList<MetricBucket> points, string first, string second, double? axisStart = null, double? axisEnd = null, double axisLeft = 48) : Control
{
    protected override Size MeasureOverride(Size availableSize) => new(double.IsInfinity(availableSize.Width) ? 500 : availableSize.Width, 125);
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (points.Count == 0) { PulseHelp.Tip(this, "No observations in this selected range."); return; }
        var start = axisStart ?? points.Min(p => p.Second); var end = axisEnd ?? points.Max(p => p.Second);
        var secondAt = start + Math.Clamp((e.GetPosition(this).X - axisLeft) / Math.Max(1, Bounds.Width - axisLeft - 10), 0, 1) * Math.Max(.001, end - start);
        var nearest = points.MinBy(p => Math.Abs(p.Second - secondAt));
        PulseHelp.Tip(this, $"Nearest observed point/bucket · {nearest.Second:0.000}s\n{first}: {nearest.Damage:N2} · {second}: {nearest.Healing:N2}\nElapsed time includes pauses. A line between HP observations does not verify intermediate health or a boss phase.");
    }
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var text = Brush.Parse("#AAB7C6"); var left = axisLeft; var right = Math.Max(left + 1, Bounds.Width - 10); var top = 26d; var bottom = 101d;
        var maximum = Math.Max(1, points.Count == 0 ? 1 : points.Max(p => Math.Max(p.Damage, p.Healing)));
        void Label(string label, double x, double y, IBrush? color = null)
        {
            var value = new FormattedText(label, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 11, color ?? text);
            context.DrawText(value, new Point(Math.Clamp(x, 0, Math.Max(0, Bounds.Width - value.Width)), y));
        }
        Label(first, 0, 0, Brush.Parse("#71DBC4")); Label(second, 90, 0, Brush.Parse("#A4A5F0")); Label($"{maximum:0}", 0, top); Label("0", 0, bottom - 10);
        context.DrawLine(new Pen(Brush.Parse("#405463"), 1), new Point(left, top), new Point(left, bottom));
        context.DrawLine(new Pen(Brush.Parse("#405463"), 1), new Point(left, bottom), new Point(right, bottom));
        if (points.Count == 0) { Label("No observed points", left + 8, 55); return; }
        var start = axisStart ?? points.Min(p => p.Second); var end = axisEnd ?? points.Max(p => p.Second); var span = Math.Max(.001, end - start);
        Label($"{start:0.0}s", left, 105); Label($"{end:0.0}s", right - 35, 105);
        Point Position(MetricBucket p, bool healing) => new(left + (p.Second - start) / span * (right - left), bottom - Math.Clamp((healing ? p.Healing : p.Damage) / maximum, 0, 1) * (bottom - top));
        foreach (var healing in new[] { false, true })
        {
            var brush = Brush.Parse(healing ? "#A4A5F0" : "#71DBC4"); var pen = new Pen(brush, 1.5);
            for (var i = 0; i < points.Count; i++)
            {
                var at = Position(points[i], healing);
                if (i > 0) context.DrawLine(pen, Position(points[i - 1], healing), at);
                if (points.Count < 100) context.DrawEllipse(brush, null, at, 2, 2);
            }
        }
    }
}
