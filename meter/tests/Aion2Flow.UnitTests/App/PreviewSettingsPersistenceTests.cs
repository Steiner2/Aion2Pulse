using Cloris.Aion2Flow.Services.Settings;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.ViewModels;

namespace Cloris.Aion2Flow.Tests.App;

public sealed class PreviewSettingsPersistenceTests
{
    [Fact]
    public void DisplayAndTrackingSettingsSurviveUnrelatedUpdatesAndRestart()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pulse-settings-preview-{Guid.NewGuid():N}.json");
        var service = new SettingsService(path);
        service.Update(s => { s.OverlayWidth = 420; s.SurfaceIntensity = 70; s.PlayerRowHeight = 28; s.UiScalePercent = 120; s.ShowBothMetrics = true; s.MeterDesign = MeterDesign.FocusGlass; s.HistoryLayout = HistoryLayout.Cards; s.CombatTracking = CombatTrackingBehavior.CombatChain; s.CombatChainSeconds = 60; s.PauseDamageTime = false; s.DamagePauseSeconds = 7; });
        service.Update(s => s.ShowPlayerNames = false);
        var restored = new SettingsService(path).Current;
        Assert.Equal(420, restored.OverlayWidth); Assert.Equal(70, restored.SurfaceIntensity);
        Assert.Equal(28, restored.PlayerRowHeight); Assert.Equal(120, restored.UiScalePercent);
        Assert.True(restored.ShowBothMetrics); Assert.Equal(MeterDesign.FocusGlass, restored.MeterDesign);
        Assert.Equal(HistoryLayout.Cards, restored.HistoryLayout); Assert.Equal(CombatTrackingBehavior.CombatChain, restored.CombatTracking);
        Assert.Equal(60, restored.CombatChainSeconds); Assert.False(restored.PauseDamageTime); Assert.Equal(7, restored.DamagePauseSeconds);
    }
}
