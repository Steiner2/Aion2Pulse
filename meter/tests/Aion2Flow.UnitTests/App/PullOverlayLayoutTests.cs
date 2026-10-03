using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Cloris.Aion2Flow.Assets.Icons;
using Cloris.Aion2Flow.Capture;
using Cloris.Aion2Flow.SceneRuntime;
using Cloris.Aion2Flow.SceneRuntime.Archive;
using Cloris.Aion2Flow.SceneRuntime.Identity;
using Cloris.Aion2Flow.SceneRuntime.Model;
using Cloris.Aion2Flow.SceneRuntime.Observation;
using Cloris.Aion2Flow.Services;
using Cloris.Aion2Flow.Services.Hotkeys;
using Cloris.Aion2Flow.Services.Overlay;
using Cloris.Aion2Flow.Services.Settings;
using Cloris.Aion2Flow.ViewModels;
using Cloris.Aion2Flow.Views;
using Microsoft.Extensions.DependencyInjection;
using Cloris.Aion2Flow.Protocol.Combat;

namespace Cloris.Aion2Flow.Tests.App;

[Collection(AvaloniaTestCollection.Name)]
public sealed class PullOverlayLayoutTests
{
    [Fact]
    public void OverlayRendersOfflineWithoutStartingPacketCapture()
    {
        AvaloniaTestHost.Run(() =>
        {
            var (localization, frameBatch) = ViewTestServices.Get();
            Application.Current!.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
            Application.Current!.Resources.MergedDictionaries.Add(new IconGeometries());
            var provider = new ServiceCollection()
                .AddSingleton(localization)
                .AddSingleton(frameBatch)
                .AddSingleton(new SettingsService(Path.Combine(Path.GetTempPath(), $"pull-overlay-{Guid.NewGuid():N}.json")))
                .AddSingleton<LanguageService>()
                .AddSingleton<GameResourceService>()
                .AddSingleton<PlayerNameDisplayService>()
                .AddSingleton<UiScaleService>()
                .AddSingleton<AppUpdateService>()
                .AddSingleton<EncounterArchiveService>()
                .AddSingleton<AvaloniaFrameClockService>()
                .AddSingleton<CombatantDetailsFlyoutViewModel>()
                .AddSingleton<ProcessPortDiscoveryService>()
                .AddSingleton<ProcessForegroundWatcher>()
                .AddSingleton<WinDivertCaptureService>()
                .AddSingleton<GlobalHotkeyService>()
                .AddSingleton<OverlayInteractionController>()
                .AddSingleton<SkillMonitorWindowController>()
                .AddSingleton<SettingsFlyoutViewModel>()
                .AddSingleton<MainViewModel>()
                .BuildServiceProvider();
            var window = new MainWindow(provider, startCapture: false);
            var vm = window.DataContext;
            Assert.False(vm.IsCapturing);
            // Explicit test fixture only. No synthetic damage enters the shipping app.
            var scene = provider.GetRequiredService<WinDivertCaptureService>().Scene;
            var resources = provider.GetRequiredService<GameResourceService>();
            var previewSkills = resources.Skills.Where(skill => resources.IsPlayerProfessionSkill(skill.SkillId) && resources.ResolveSkillIconAssetName(skill.SkillId) is not null).Select(skill => skill.SkillId).Take(12).ToArray();
            Assert.NotEmpty(previewSkills);
            var sink = SceneSinkFactory.CreateForLive(scene)();
            var started = DateTimeOffset.Now.AddSeconds(-20).ToUnixTimeMilliseconds();
            PacketObservationSource Source(int offset) => new(started + offset, offset + 1, 0x0438, 30, offset, default);
            sink.SetCurrentMap(Source(0), 910035);
            var classes = new[] { CharacterClass.Gladiator, CharacterClass.Sorcerer, CharacterClass.Ranger, CharacterClass.Cleric };
            var names = new[] { "Preview · You", "Preview · Mage", "Preview · Ranger", "Preview · Healer" };
            for (var i = 0; i < classes.Length; i++)
            {
                sink.AppendNickname(Source(0), 100 + i, names[i], characterClass: classes[i], isLocalPlayer: i == 0);
                sink.AppendPlayerGroupMember(Source(0), 100 + i, PlayerGroupMembership.Party((byte)i));
            }
            sink.AppendNpcCode(Source(0), 200, 2_100_002);
            sink.AppendNpcKind(Source(0), 200, NpcKind.Boss);
            for (var offset = 0; offset <= 12_000; offset += 1_000)
            {
                for (var i = 0; i < classes.Length; i++)
                {
                    var wire = new CombatWireObservation { SkillCode = previewSkills[(offset / 1000 + i) % previewSkills.Length], Damage = 10_000 - i * 1_800, HitCount = 1, AttemptCount = 1 };
                    sink.AppendCombatWireObservation(Source(offset), 100 + i, 200, in wire);
                }
                if (offset == 2_000)
                {
                    sink.RegisterObservation2A38(Source(offset), 103, 1, 19, 9999, 0, 5000, 0, 0, 0, 103, 1, ResourceEffectRef.FromRaw(12_780_001), 0, 0, 0);
                    sink.RegisterCompactControl0238(Source(offset), 103, 1, (uint)previewSkills[0], 0, 0, 103, cooldownMilliseconds: 7000);
                }
                sink.AppendNpcHp(Source(offset), 200, 1_000_000 - offset * 50, 1_000_000);
                sink.RegisterCooldown4738(Source(offset), previewSkills[0], Math.Max(0, 10_000 - offset));
                sink.CompleteFlush(offset + 1);
            }
            vm.RefreshCombatStatsForTesting();
            frameBatch.FlushFrame();
            var content = Assert.IsAssignableFrom<Control>(window.Content);
            content.Width = window.Width;
            content.Measure(new Size(440, 1_000));
            content.Arrange(new Rect(new Point(), content.DesiredSize));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(4, vm.Combatants.Count);
            var totalDamage = vm.Combatants.Sum(row => row.Damage);
            Assert.Equal(totalDamage, vm.TotalFilteredDamage);
            vm.ShowOverallCommand.Execute(null);
            frameBatch.FlushFrame();
            Assert.True(vm.IsViewingOverall);
            Assert.Equal(totalDamage, vm.TotalFilteredDamage);
            Assert.Equal(4, vm.Combatants.Count);
            vm.ReturnToLiveCommand.Execute(null);
            frameBatch.FlushFrame();
            Assert.False(vm.IsViewingOverall);
            Assert.Equal(300, content.Bounds.Width);
            Assert.InRange(content.Bounds.Height, 160, 400);
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)content.Bounds.Width * 2, (int)Math.Ceiling(content.Bounds.Height * 2)), new Vector(192, 192));
            bitmap.Render(content);
            var previewPath = Environment.GetEnvironmentVariable("AION2_PREVIEW_PATH");
            if (!string.IsNullOrWhiteSpace(previewPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(previewPath)!);
                bitmap.Save(previewPath, PngBitmapEncoderOptions.Default);
            }
            var heal = new CombatWireObservation { SkillCode = 14_000_010, Damage = 60_000, ResourceKind = CombatResourceKind.Health, HitCount = 1, AttemptCount = 1 };
            sink.AppendCombatWireObservation(Source(12_000), 103, 100, in heal);
            sink.CompleteFlush(13_001);
            vm.SettingsFlyout.CombatantSortMetric = CombatantSortMetric.HealingPerSecond;
            vm.RefreshCombatStatsForTesting(); frameBatch.FlushFrame();
            Assert.True(vm.SettingsFlyout.IsHealingMode);
            Assert.Equal(60_000, vm.TotalFilteredHealing);
            Assert.Equal(vm.TotalFilteredHealingPerSecond, vm.DisplayTotalRate);
            Assert.Equal(103, vm.Combatants[0].Id);
            Assert.Equal(vm.Combatants[0].Healing, vm.Combatants[0].DisplayAmount);
            Assert.Equal("— ms", vm.RoundTripTimeDisplay);
            var settingsWindow = new PulseSettingsWindow(vm.SettingsFlyout, new SkillMonitorSettingsViewModel(resources, provider.GetRequiredService<SettingsService>(), localization));
            settingsWindow.Show(); Dispatcher.UIThread.RunJobs();
            var settingsContent = Assert.IsAssignableFrom<Control>(settingsWindow.Content);
            settingsContent.Measure(new Size(760, 600)); settingsContent.Arrange(new Rect(0, 0, 760, 600)); Dispatcher.UIThread.RunJobs();
            var designPicker = settingsContent.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Tag, MeterDesign.FocusGlass));
            designPicker.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(MeterDesign.FocusGlass, vm.SettingsFlyout.MeterDesign);
            var sliders = settingsContent.GetLogicalDescendants().OfType<Slider>().ToArray();
            Assert.Equal(3, sliders.Length);
            Assert.All(sliders, slider => Assert.NotNull(ToolTip.GetTip(slider)));
            sliders[0].Value = 28; Dispatcher.UIThread.RunJobs();
            Assert.Equal(28, vm.SettingsFlyout.PlayerRowHeight);
            sliders[1].Value = 70; Dispatcher.UIThread.RunJobs();
            Assert.Equal(70, vm.SettingsFlyout.SurfaceIntensity);
            sliders[2].Value = 110; Dispatcher.UIThread.RunJobs();
            Assert.Equal(110, vm.SettingsFlyout.UiScalePercent);
            sliders[0].Value = 32; sliders[1].Value = 85; sliders[2].Value = 100; Dispatcher.UIThread.RunJobs();
            var historyPicker = settingsContent.GetLogicalDescendants().OfType<ComboBox>().Single();
            Assert.Equal(HistoryLayout.SplitView, historyPicker.SelectedItem);
            historyPicker.SelectedItem = HistoryLayout.Cards; Dispatcher.UIThread.RunJobs();
            Assert.Equal(HistoryLayout.Cards, vm.SettingsFlyout.HistoryLayout);
            foreach (var design in Enum.GetValues<MeterDesign>())
            {
                vm.SettingsFlyout.MeterDesign = design;
                content.Width = window.Width;
                content.Measure(new Size(window.Width, 1000)); content.Arrange(new Rect(new Point(), content.DesiredSize));
                window.ApplyPulseAppearance(); Dispatcher.UIThread.RunJobs();
                content.Measure(new Size(window.Width, 1000)); content.Arrange(new Rect(new Point(), content.DesiredSize));
                Assert.InRange(content.Bounds.Height, 160, 500);
                SavePreview(content, $"pulse-{design}.png");
                vm.SettingsFlyout.ShowBothMetrics = true;
                vm.RefreshCombatStatsForTesting(); frameBatch.FlushFrame(); Dispatcher.UIThread.RunJobs();
                content.Width = window.Width;
                content.Measure(new Size(window.Width, 1000)); content.Arrange(new Rect(new Point(), content.DesiredSize));
                window.ApplyPulseAppearance(); Dispatcher.UIThread.RunJobs();
                content.Measure(new Size(window.Width, 1000)); content.Arrange(new Rect(new Point(), content.DesiredSize));
                Assert.False(vm.SettingsFlyout.IsHealingMode);
                Assert.Equal("DPS+HPS", Assert.IsType<TextBlock>(Assert.IsType<Button>(window.FindControl<Button>("MetricModeButton")).Content).Text);
                Assert.Equal(vm.TotalFilteredDamage, vm.DisplayTotalAmount);
                Assert.Equal(vm.Combatants.Sum(r => r.DamagePerSecond), vm.DisplayTotalRate, 6);
                var healingCells = content.GetVisualDescendants().OfType<Cloris.Aion2Flow.Controls.NumericBlock>().Where(c => c.Classes.Contains("PulseHps")).ToArray();
                Assert.Equal(4, healingCells.Length);
                Assert.Contains(healingCells, c => c.Value > 0);
                foreach (var cell in healingCells)
                {
                    Assert.True(cell.IsVisible);
                    Assert.InRange(cell.Bounds.Width, 60, 100);
                    var position = cell.TranslatePoint(new Point(), content);
                    Assert.NotNull(position);
                    Assert.True(position.Value.X + cell.Bounds.Width <= content.Bounds.Width + 1);
                }
                SavePreview(content, $"pulse-combined-{design}.png");
                vm.SettingsFlyout.ShowBothMetrics = false;
            }
            SavePreview(settingsContent, "pulse-settings.png");
            foreach (var input in settingsContent.GetLogicalDescendants().OfType<ComboBox>())
            {
                Assert.InRange(input.Bounds.Width, 100, 240);
                var position = input.TranslatePoint(new Point(), settingsContent);
                Assert.NotNull(position);
                Assert.True(position.Value.X + input.Bounds.Width <= 761, $"Settings input exceeds window: {position}, {input.Bounds}");
            }
            vm.ArchiveCurrentEncounterCommand.Execute(null);
            vm.SelectedEncounterHistory = vm.EncounterHistory.First();
            Assert.Equal(60_000, vm.TotalFilteredHealing);
            var totals = PulseHistoryWindow.Totals(vm.SelectedEncounterHistory.Record.ScenePayload.Snapshot);
            Assert.Equal(60_000, totals.Healing);
            Assert.Equal(vm.TotalFilteredHealingPerSecond, totals.Hps, 6);
            var historyWindow = new PulseHistoryWindow(vm);
            historyWindow.Show(); Dispatcher.UIThread.RunJobs();
            var historyContent = Assert.IsAssignableFrom<Control>(historyWindow.Content);
            foreach (var layout in Enum.GetValues<HistoryLayout>())
            {
                vm.SettingsFlyout.HistoryLayout = layout;
                historyContent.Measure(new Size(1050, 660)); historyContent.Arrange(new Rect(0, 0, 1050, 660)); Dispatcher.UIThread.RunJobs();
                SavePreview(historyContent, $"pulse-history-{layout}.png");
            }
            vm.SettingsFlyout.HistoryLayout = HistoryLayout.SplitView;
            historyContent.Measure(new Size(1050, 660)); historyContent.Arrange(new Rect(0, 0, 1050, 660)); Dispatcher.UIThread.RunJobs();
            var divider = Assert.Single(historyContent.GetVisualDescendants().OfType<GridSplitter>());
            Assert.True(divider.IsVisible);
            Assert.InRange(divider.TranslatePoint(default, historyContent)!.Value.X, 210, 320);
            var playerButton = historyContent.GetVisualDescendants().OfType<Button>().First(button => button.Content is Grid grid && grid.Children.OfType<Cloris.Aion2Flow.Controls.PcDisplay>().Any());
            playerButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs();
            var detailTabs = historyContent.GetVisualDescendants().OfType<TabControl>().Single(t => t.Name == "EncounterDetailTabs");
            detailTabs.SelectedIndex = 1;
            historyContent.Measure(new Size(1050, 660)); historyContent.Arrange(new Rect(0, 0, 1050, 660)); Dispatcher.UIThread.RunJobs();
            Assert.Single(historyContent.GetVisualDescendants().OfType<SkillEventTimelineView>());
            Assert.NotEmpty(historyContent.GetVisualDescendants().OfType<ListBox>().First().Items);
            SavePreview(historyContent, "pulse-history-Timeline.png");
            detailTabs.SelectedIndex = 2; Dispatcher.UIThread.RunJobs();
            historyContent.Measure(new Size(1050, 660)); historyContent.Arrange(new Rect(0, 0, 1050, 660)); Dispatcher.UIThread.RunJobs();
            var analysisTabs = historyContent.GetVisualDescendants().OfType<TabControl>().Single(t => t.Name == "AnalysisTabs");
            foreach (var index in Enumerable.Range(0, analysisTabs.Items.Count))
            {
                Assert.NotNull(ToolTip.GetTip(Assert.IsType<TabItem>(analysisTabs.Items[index])));
                analysisTabs.SelectedIndex = index;
                historyContent.Measure(new Size(1050, 660)); historyContent.Arrange(new Rect(0, 0, 1050, 660)); Dispatcher.UIThread.RunJobs();
                SavePreview(historyContent, $"pulse-analysis-{index}.png");
            }
            var combatCategory = settingsContent.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Combat"));
            combatCategory.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs();
            var trackingPicker = settingsContent.GetLogicalDescendants().OfType<ComboBox>().Single(c => c.ItemsSource?.GetType() == typeof(CombatTrackingBehavior[]));
            trackingPicker.SelectedItem = CombatTrackingBehavior.Manual; Dispatcher.UIThread.RunJobs();
            Assert.False(vm.SettingsFlyout.EffectiveAutoSegment);
            trackingPicker.SelectedItem = CombatTrackingBehavior.CombatChain; Dispatcher.UIThread.RunJobs();
            Assert.True(vm.SettingsFlyout.EffectiveAutoSegment);
            Assert.Equal(30, vm.SettingsFlyout.EffectiveIdleSeconds);
            settingsContent.Measure(new Size(760, 600)); settingsContent.Arrange(new Rect(0, 0, 760, 600)); Dispatcher.UIThread.RunJobs();
            SavePreview(settingsContent, "pulse-settings-Combat.png");
            var skillCategory = settingsContent.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Skills"));
            skillCategory.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs();
            Assert.Single(settingsContent.GetVisualDescendants().OfType<SkillMonitorSettingsView>());
            settingsContent.Measure(new Size(760, 600)); settingsContent.Arrange(new Rect(0, 0, 760, 600)); Dispatcher.UIThread.RunJobs();
            SavePreview(settingsContent, "pulse-settings-Skills.png");
            var skillSettings = Assert.IsType<SkillMonitorSettingsViewModel>(settingsContent.GetVisualDescendants().OfType<SkillMonitorSettingsView>().Single().DataContext);
            skillSettings.ClearAllBuffsCommand.Execute(null);
            vm.SettingsFlyout.SurfaceIntensity = 84; Dispatcher.UIThread.RunJobs();
            Assert.False(provider.GetRequiredService<SettingsService>().Current.SkillMonitorBuffSelectAll);
            Assert.Empty(provider.GetRequiredService<SettingsService>().Current.SkillMonitorBuffSkillIds);
            vm.SettingsFlyout.SurfaceIntensity = 85; Dispatcher.UIThread.RunJobs();
            combatCategory.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs();
            trackingPicker.SelectedItem = CombatTrackingBehavior.SeparatePulls; Dispatcher.UIThread.RunJobs();
            Assert.Equal(5, vm.SettingsFlyout.EffectiveIdleSeconds);
            historyWindow.Width = 800; Dispatcher.UIThread.RunJobs();
            Assert.False(divider.IsVisible);
            vm.SettingsFlyout.ShowBothMetrics = false;
            vm.SettingsFlyout.OverlayWidth = 700;
            Assert.Equal(700, window.Width);
            Assert.Equal(700, provider.GetRequiredService<SettingsService>().Current.OverlayWidth);
            vm.SettingsFlyout.OverlayWidth = 0;
            Assert.Equal(300, window.Width);
            vm.SettingsFlyout.ShowBothMetrics = true;
            Assert.Equal(420, window.Width);
            historyWindow.Close(); settingsWindow.Close();
            provider.DisposeAsync().AsTask().GetAwaiter().GetResult();
        });
    }
    private static void SavePreview(Control content, string name)
    {
        var previewPath = Environment.GetEnvironmentVariable("AION2_PREVIEW_PATH");
        if (string.IsNullOrWhiteSpace(previewPath)) return;
        foreach (var visual in content.GetVisualDescendants().OfType<Control>()) { visual.InvalidateMeasure(); visual.InvalidateVisual(); }
        content.InvalidateMeasure(); content.InvalidateVisual();
        content.Measure(content.Bounds.Size); content.Arrange(new Rect(new Point(), content.Bounds.Size));
        Dispatcher.UIThread.RunJobs();
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)content.Bounds.Width * 2, (int)content.Bounds.Height * 2), new Vector(192, 192));
        bitmap.Render(content); bitmap.Save(Path.Combine(Path.GetDirectoryName(previewPath)!, name), PngBitmapEncoderOptions.Default);
    }
}
