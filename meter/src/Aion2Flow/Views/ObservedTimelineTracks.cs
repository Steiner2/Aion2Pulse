using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Cloris.Aion2Flow.Views;

internal readonly record struct ObservedTrackMark(string Lane, double Start, double End, string Description, bool OpenEnd = false);

// Shares the selected elapsed axis with the adjacent analysis graph. Bars describe observations, never inferred casts.
internal sealed class ObservedTimelineTracks : Control
{
    private readonly ObservedTrackMark[] _marks;
    private readonly string[] _lanes;
    private readonly double _start;
    private readonly double _end;
    private const double Left = 144;
    public ObservedTimelineTracks(IEnumerable<ObservedTrackMark> marks, double start, double end)
    {
        _marks = marks.OrderBy(m => m.Start).ToArray();
        _lanes = _marks.Select(m => m.Lane).Distinct().Take(12).ToArray();
        _start = start; _end = Math.Max(start + .001, end);
        PulseHelp.Tip(this, "Observed intervals and state markers on the selected elapsed timeline. Hover a bar or marker for its details. At most 12 lanes are drawn; the lists retain the observations.");
    }
    protected override Size MeasureOverride(Size availableSize) => new(double.IsInfinity(availableSize.Width) ? 500 : availableSize.Width, 36 + Math.Max(1, _lanes.Length) * 26);
    private double X(double seconds) => Left + Math.Clamp((seconds - _start) / (_end - _start), 0, 1) * Math.Max(1, Bounds.Width - Left - 10);
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var point = e.GetPosition(this);
        var lane = (int)((point.Y - 24) / 26);
        if (point.Y < 24 || lane < 0 || lane >= _lanes.Length) return;
        var mark = _marks.Where(m => m.Lane == _lanes[lane]).OrderBy(m => Math.Abs((X(m.Start) + X(m.End)) / 2 - point.X)).FirstOrDefault();
        PulseHelp.Tip(this, string.IsNullOrEmpty(mark.Lane) ? "No observations in this lane." : $"{mark.Lane}: {mark.Start:0.000}–{mark.End:0.000}s\n{mark.Description}");
    }
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var textBrush = Brush.Parse("#AAB7C6");
        void Label(string text, double x, double y) => context.DrawText(new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 10, textBrush), new Point(x, y));
        Label($"{_start:0.0}s", Left, 0); Label($"{_end:0.0}s", Math.Max(Left, Bounds.Width - 48), 0);
        if (_lanes.Length == 0) { Label("No observed intervals", 0, 25); return; }
        for (var i = 0; i < _lanes.Length; i++)
        {
            var y = 24 + i * 26;
            Label(_lanes[i].Length > 21 ? _lanes[i][..20] + "…" : _lanes[i], 0, y + 3);
            context.DrawLine(new Pen(Brush.Parse("#30414D"), 1), new Point(Left, y + 10), new Point(Math.Max(Left + 1, Bounds.Width - 10), y + 10));
            var brush = Brush.Parse(i % 2 == 0 ? "#71DBC4" : "#A4A5F0");
            var occupied = new HashSet<(int Start, int End)>();
            foreach (var mark in _marks.Where(m => m.Lane == _lanes[i] && m.End >= _start && m.Start <= _end))
            {
                var x = X(mark.Start); var end = X(mark.End);
                if (!occupied.Add(((int)x, (int)end))) continue;
                context.DrawRectangle(brush, null, new Rect(x, y + 5, Math.Max(2, end - x), 10));
                if (mark.OpenEnd) Label("?", Math.Max(x, end - 9), y + 1);
            }
        }
    }
}
