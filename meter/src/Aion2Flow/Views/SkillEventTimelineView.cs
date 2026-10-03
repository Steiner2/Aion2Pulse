using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Media;
using Cloris.Aion2Flow.Controls;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.SceneRuntime.Archive;
using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Projection;

namespace Cloris.Aion2Flow.Views;

// Uses wall-clock offsets, independent of the pause-excluding DPS denominator.
public sealed class SkillEventTimelineView : UserControl
{
    internal static CombatMetricDetailEvent[] Events(SceneArchivePayload payload, int player, bool incoming)
        => payload.CreateDetailDelta(player).MetricEvents
            .Where(e => (incoming ? e.TargetId : e.SourceId) == player)
            .OrderBy(e => e.ObservedAtMilliseconds).ThenBy(e => e.SourceObservationOrdinal).ToArray();

    public SkillEventTimelineView(SceneArchivePayload payload, int player, SceneDisplayContext display)
    {
        DisplayContextProvider.SetDisplayContext(this, display);
        var list = new ListBox { MinHeight = 80, Margin = new Thickness(0, 8) };
        var chart = new EventStrip(payload.Snapshot.EncounterStartTime, payload.Snapshot.EncounterEndTime);
        chart.DescribeEvent = e => $"{display.ResolveSkillName(e.SkillCode)} · {(e.ObservedAtMilliseconds - payload.Snapshot.EncounterStartTime) / 1000d:0.000}s · {e.Amount:N0} {e.Metric} · {e.Delivery} · {e.Observation.Modifiers} · from {display.ResolveEntityName(e.SourceId)} to {display.ResolveEntityName(e.TargetId)}";
        var direction = new ComboBox { ItemsSource = new[] { "Outgoing events", "Incoming events" }, SelectedIndex = 0, Width = 175 };
        PulseHelp.Tip(direction, "Outgoing shows this player's damage, healing and shields. Incoming shows events whose recorded target is this player.");
        PulseHelp.Tip(chart, "Damage markers sit above the line; healing and shields below. Hover for the nearest effect, or click a marker to select its full list entry.");
        PulseHelp.Tip(list, "Select a recorded effect to see its skill, target/source, amount, delivery and modifier flags. Multiple effects can belong to one cast.");
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock { Text = "Skill event timeline", FontSize = 16 });
        panel.Children.Add(new CombatantDisplay { EntityId = player, IconSize = 20 });
        panel.Children.Add(new TextBlock { Text = "Observed hits, heals and shields. Times show effects, not verified cast starts.", TextWrapping = TextWrapping.Wrap, FontSize = 11, Foreground = Brush.Parse("#AAB7C6") });
        panel.Children.Add(direction); panel.Children.Add(chart);
        panel.Children.Add(new TextBlock { Text = "Damage above · Healing / shields below · Select a marker or event", TextWrapping = TextWrapping.Wrap, FontSize = 11 });
        list.ItemTemplate = new FuncDataTemplate<CombatMetricDetailEvent>((e, _) =>
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("76,*,110"), ColumnSpacing = 8 };
            var elapsed = Math.Max(0, e.ObservedAtMilliseconds - payload.Snapshot.EncounterStartTime);
            grid.Children.Add(new TextBlock { Text = TimeSpan.FromMilliseconds(elapsed).ToString(@"mm\:ss\.fff", CultureInfo.InvariantCulture), FontSize = 11, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
            var skill = new SkillDisplay { SkillCode = e.SkillCode, IconSize = 22, IconSpacing = 6 }; Grid.SetColumn(skill, 1); grid.Children.Add(skill);
            var amount = new TextBlock { Text = $"{e.Amount:N0} {e.Metric}", FontSize = 11, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right }; Grid.SetColumn(amount, 2); grid.Children.Add(amount);
            return PulseHelp.Tip(grid, $"{display.ResolveSkillName(e.SkillCode)} · {e.Amount:N0} {e.Metric} · {e.Delivery} · {e.Observation.Modifiers} · from {display.ResolveEntityName(e.SourceId)} to {display.ResolveEntityName(e.TargetId)}");
        });
        var selected = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
        void Refresh()
        {
            chart.Events = Events(payload, player, direction.SelectedIndex == 1);
            chart.InvalidateVisual(); list.ItemsSource = chart.Events;
            selected.Text = chart.Events.Length == 0 ? "No recorded events for this direction." : $"{chart.Events.Length:N0} events · elapsed encounter time includes pauses";
        }
        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedItem is not CombatMetricDetailEvent e) return;
            var counterpart = direction.SelectedIndex == 1 ? e.SourceId : e.TargetId;
            selected.Text = $"{display.ResolveSkillName(e.SkillCode)} · {TimeSpan.FromMilliseconds(Math.Max(0, e.ObservedAtMilliseconds - payload.Snapshot.EncounterStartTime)):mm\\:ss\\.fff} · {e.Amount:N0} {e.Metric} · {e.Delivery} · {e.Observation.Modifiers} · {display.ResolveEntityName(counterpart)}";
        };
        chart.SelectEvent = e => { list.SelectedItem = e; list.ScrollIntoView(e); };
        direction.SelectionChanged += (_, _) => Refresh();
        var body = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), MinHeight = 0 };
        body.Children.Add(panel); Grid.SetRow(list, 1); body.Children.Add(list);
        Grid.SetRow(selected, 2); body.Children.Add(selected);
        Content = body; Refresh();
    }

    private sealed class EventStrip(long start, long end) : Control
    {
        public CombatMetricDetailEvent[] Events { get; set; } = [];
        public Func<CombatMetricDetailEvent, string>? DescribeEvent { get; set; }
        public Action<CombatMetricDetailEvent>? SelectEvent { get; set; }
        private readonly IBrush _damage = Brush.Parse("#71DBC4");
        private readonly IBrush _healing = Brush.Parse("#A4A5F0");
        public EventStrip() : this(0, 1) { }
        protected override Size MeasureOverride(Size availableSize) => new(double.IsInfinity(availableSize.Width) ? 500 : availableSize.Width, 82);
        private double Position(long time) => 12 + Math.Clamp((time - start) / (double)Math.Max(1, end - start), 0, 1) * Math.Max(1, Bounds.Width - 24);
        public override void Render(DrawingContext context)
        {
            base.Render(context);
            context.DrawRectangle(Brush.Parse("#18212C"), null, new Rect(Bounds.Size));
            for (var i = 0; i <= 4; i++)
            {
                var x = 12 + i * Math.Max(1, Bounds.Width - 24) / 4;
                context.DrawLine(new Pen(Brush.Parse("#405463"), 1), new Point(x, 22), new Point(x, 70));
                var label = new FormattedText($"{i * Math.Max(1, end - start) / 4000d:0.0}s", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 11, Brush.Parse("#AAB7C6"));
                context.DrawText(label, new Point(Math.Clamp(x - label.Width / 2, 0, Math.Max(0, Bounds.Width - label.Width)), 2));
            }
            // Bound drawing work to occupied pixels; the virtualized list retains every event.
            var occupied = new HashSet<(int X, bool Damage)>();
            foreach (var e in Events)
            {
                var x = Position(e.ObservedAtMilliseconds); var damage = e.Metric == CombatMetricKind.Damage;
                if (occupied.Add(((int)x, damage))) context.DrawEllipse(damage ? _damage : _healing, null, new Point(x, damage ? 36 : 60), 3, 3);
            }
        }
        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (Events.Length == 0) return;
            var point = e.GetPosition(this);
            var candidates = Events.Where(item => (item.Metric == CombatMetricKind.Damage) == (point.Y < 48)).ToArray();
            if (candidates.Length == 0) return;
            var nearest = candidates.MinBy(item => Math.Abs(Position(item.ObservedAtMilliseconds) - point.X));
            PulseHelp.Tip(this, DescribeEvent?.Invoke(nearest) ?? $"{nearest.Amount:N0} {nearest.Metric}");
        }
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (Events.Length == 0) return;
            var point = e.GetPosition(this);
            var candidates = Events.Where(item => (item.Metric == CombatMetricKind.Damage) == (point.Y < 48)).ToArray();
            if (candidates.Length == 0) return;
            SelectEvent?.Invoke(candidates.MinBy(item => Math.Abs(Position(item.ObservedAtMilliseconds) - point.X)));
        }
    }
}
