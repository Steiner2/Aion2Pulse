using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Cloris.Aion2Flow.Services.Overlay;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Views;

public partial class MainWindow
{
    private void ResizePulseWidth(object? sender, VectorEventArgs e)
    {
        if (_overlayInteractionController.Mode != OverlayInteractionMode.Interactive) return;
        DataContext.SettingsFlyout.OverlayWidth = (int)Math.Clamp(Math.Round(Width / _uiScale.Scale + e.Vector.X), DataContext.SettingsFlyout.ShowBothMetrics ? 420 : 300, 1000);
    }
    private PulseSettingsWindow? _settingsWindow;
    private PulseHistoryWindow? _historyWindow;
    private void OpenSettings(object? sender, RoutedEventArgs e)
    {
        if (_settingsWindow is not null) { _settingsWindow.Activate(); return; }
        _settingsWindow = new PulseSettingsWindow(DataContext.SettingsFlyout);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show(this);
    }
    private void OpenHistory(object? sender, RoutedEventArgs e)
    {
        if (_historyWindow is not null) { _historyWindow.Activate(); return; }
        _historyWindow = new PulseHistoryWindow(DataContext);
        _historyWindow.Closed += (_, _) => _historyWindow = null;
        _historyWindow.Show(this);
    }
    private void ToggleHealing(object? sender, RoutedEventArgs e)
    {
        var settings = DataContext.SettingsFlyout;
        if (settings.ShowBothMetrics) { settings.ShowBothMetrics = false; settings.CombatantSortMetric = CombatantSortMetric.DamagePerSecond; }
        else if (settings.IsHealingMode) { settings.ShowBothMetrics = true; settings.CombatantSortMetric = CombatantSortMetric.DamagePerSecond; }
        else settings.CombatantSortMetric = CombatantSortMetric.HealingPerSecond;
    }

    internal void ApplyPulseAppearance()
    {
        var settings = DataContext.SettingsFlyout;
        MainHudShell.Classes.Set("studio", settings.MeterDesign == MeterDesign.CombatStudio);
        MainHudShell.Classes.Set("focus", settings.MeterDesign == MeterDesign.FocusGlass);
        MainHudShell.Classes.Set("combined", settings.ShowBothMetrics);
        var minimumWidth = settings.ShowBothMetrics ? 420 : 300;
        var automaticWidth = minimumWidth;
        Width = Math.Max(minimumWidth, settings.OverlayWidth == 0 ? automaticWidth : settings.OverlayWidth) * _uiScale.Scale;
        var color = settings.MeterDesign == MeterDesign.FocusGlass ? "#1C3039" : "#10161E";
        var background = Color.Parse(color);
        background = Color.FromArgb((byte)Math.Round(settings.SurfaceIntensity * 2.55), background.R, background.G, background.B);
        foreach (var border in MainHudShell.Children.OfType<Border>())
            if (border.Classes.Contains("MainHudContentBackdrop")) border.Background = new SolidColorBrush(background);
        foreach (var grid in MainHudShell.GetVisualDescendants().OfType<Grid>().Where(g => g.Classes.Contains("PlayerMetricsGrid") || g.Classes.Contains("PulseMetricHeaders"))) ApplyPlayerGrid(grid);
        _uiScale.UpdateWindowBaseSize(this);
        ScheduleOverlayAutoHeightRefresh();
    }
    private void PulseGridLoaded(object? sender, RoutedEventArgs e) { if (sender is Grid grid) ApplyPlayerGrid(grid); }
    private void ApplyPlayerGrid(Grid grid)
    {
        var both = DataContext.SettingsFlyout.ShowBothMetrics;
        var focus = DataContext.SettingsFlyout.IsFocusGlass && !both;
        grid.RowDefinitions = new RowDefinitions(focus ? "*,*" : "*");
        grid.ColumnSpacing = 3;
        grid.ColumnDefinitions = new ColumnDefinitions(both ? "16,*,48,60,48,60,Auto" : focus ? "16,*,0,72,0,0,Auto" : "16,*,48,60,0,0,Auto");
        foreach (var child in grid.Children)
        {
            if (child.Classes.Contains("PulseAmount")) { Grid.SetColumn(child, focus ? 3 : 2); Grid.SetRow(child, focus ? 1 : 0); }
            if (child.Classes.Contains("PulsePlayer") || child is TextBlock) Grid.SetRowSpan(child, focus ? 2 : 1);
        }
    }
}
