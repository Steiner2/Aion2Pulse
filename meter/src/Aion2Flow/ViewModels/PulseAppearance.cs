using CommunityToolkit.Mvvm.ComponentModel;

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
