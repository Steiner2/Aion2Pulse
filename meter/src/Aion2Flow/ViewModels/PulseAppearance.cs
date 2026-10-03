using CommunityToolkit.Mvvm.ComponentModel;
using Cloris.Aion2Flow.SceneRuntime.Model;

namespace Cloris.Aion2Flow.ViewModels;

public enum MeterDesign { RaidClassic, FocusGlass, CombatStudio }
public enum HistoryLayout { SplitView, CompactList, Cards }

public sealed partial class SettingsFlyoutViewModel
{
    [ObservableProperty] public partial MeterDesign MeterDesign { get; set; }
    [ObservableProperty] public partial HistoryLayout HistoryLayout { get; set; }
    [ObservableProperty] public partial int PlayerRowHeight { get; set; } = 32;
    [ObservableProperty] public partial int SurfaceIntensity { get; set; } = 85;
    [ObservableProperty] public partial bool UseClassColors { get; set; } = true;
    [ObservableProperty] public partial bool ShowBothMetrics { get; set; }
    [ObservableProperty] public partial int OverlayWidth { get; set; }
    [ObservableProperty] public partial CombatTrackingBehavior CombatTracking { get; set; }
    [ObservableProperty] public partial int CombatChainSeconds { get; set; } = 30;
    public bool EffectiveAutoSegment => AutoSegmentCombats && CombatTracking != CombatTrackingBehavior.Manual;
    public int EffectiveIdleSeconds => CombatTracking == CombatTrackingBehavior.CombatChain ? CombatChainSeconds : CombatIdleSeconds;
    public string TrackingShortcutLabel => CombatTracking switch { CombatTrackingBehavior.CombatChain => "C", CombatTrackingBehavior.Manual => "M", _ => "P" };
    public string TrackingDescription => CombatTracking switch { CombatTrackingBehavior.CombatChain => "Combat chain · merge nearby pulls. Click to change tracking.", CombatTrackingBehavior.Manual => "Manual · reset when you choose. Click to change tracking.", _ => "Separate pulls · one result per fight. Click to change tracking." };
    partial void OnCombatTrackingChanged(CombatTrackingBehavior value)
    {
        if (!_isApplyingPersistedSettings) AutoSegmentCombats = true;
        OnPropertyChanged(nameof(TrackingShortcutLabel)); OnPropertyChanged(nameof(TrackingDescription)); PersistSettings();
    }
    partial void OnCombatChainSecondsChanged(int value) => PersistSettings();
    partial void OnOverlayWidthChanged(int value) => PersistSettings();
    public bool IsHealingMode => !ShowBothMetrics && (CombatantSortMetric is CombatantSortMetric.HealingPerSecond or CombatantSortMetric.TotalHealing);
    public string MetricModeLabel => ShowBothMetrics ? "DPS+HPS" : RateLabel;
    public bool ShowMetricHeaders => !IsFocusGlass || ShowBothMetrics;
    public string AmountLabel => IsHealingMode ? "HEALING" : "DAMAGE";
    public string RateLabel => IsHealingMode ? "HPS" : "DPS";
    public double EffectiveRowHeight => PlayerRowHeight + (MeterDesign == MeterDesign.FocusGlass ? 14 : MeterDesign == MeterDesign.CombatStudio ? 5 : 0);
    public bool IsFocusGlass => MeterDesign == MeterDesign.FocusGlass;

    partial void OnMeterDesignChanged(MeterDesign value) { OnPropertyChanged(nameof(EffectiveRowHeight)); OnPropertyChanged(nameof(IsFocusGlass)); OnPropertyChanged(nameof(ShowMetricHeaders)); PersistSettings(); }
    partial void OnShowBothMetricsChanged(bool value)
    {
        OnPropertyChanged(nameof(IsHealingMode));
        OnPropertyChanged(nameof(AmountLabel));
        OnPropertyChanged(nameof(RateLabel));
        OnPropertyChanged(nameof(MetricModeLabel));
        OnPropertyChanged(nameof(ShowMetricHeaders));
        PersistSettings();
    }
    partial void OnHistoryLayoutChanged(HistoryLayout value) => PersistSettings();
    partial void OnPlayerRowHeightChanged(int value) { OnPropertyChanged(nameof(EffectiveRowHeight)); PersistSettings(); }
    partial void OnSurfaceIntensityChanged(int value) => PersistSettings();
    partial void OnUseClassColorsChanged(bool value) => PersistSettings();
}
