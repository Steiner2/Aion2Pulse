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
            var sink = SceneSinkFactory.CreateForLive(scene)();
            var started = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            PacketObservationSource Source(int offset) => new(started + offset, offset + 1, 0x0438, 30, offset, default);
            var classes = new[] { CharacterClass.Gladiator, CharacterClass.Sorcerer, CharacterClass.Ranger, CharacterClass.Cleric };
            var names = new[] { "Preview · You", "Preview · Mage", "Preview · Ranger", "Preview · Healer" };
            for (var i = 0; i < classes.Length; i++)
            {
                sink.AppendNickname(Source(0), 100 + i, names[i], characterClass: classes[i], isLocalPlayer: i == 0);
                sink.AppendPlayerGroupMember(Source(0), 100 + i, PlayerGroupMembership.Party((byte)i));
            }
            sink.AppendNpcKind(Source(0), 200, NpcKind.Monster);
            for (var offset = 0; offset <= 12_000; offset += 1_000)
            {
                for (var i = 0; i < classes.Length; i++)
                {
                    var wire = new CombatWireObservation { SkillCode = 11_000_010, Damage = 10_000 - i * 1_800, HitCount = 1, AttemptCount = 1 };
                    sink.AppendCombatWireObservation(Source(offset), 100 + i, 200, in wire);
                }
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
            Assert.Equal(440, content.Bounds.Width);
            Assert.InRange(content.Bounds.Height, 160, 400);
            using var bitmap = new RenderTargetBitmap(new PixelSize(880, (int)Math.Ceiling(content.Bounds.Height * 2)), new Vector(192, 192));
            bitmap.Render(content);
            var previewPath = Environment.GetEnvironmentVariable("AION2_PREVIEW_PATH");
            if (!string.IsNullOrWhiteSpace(previewPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(previewPath)!);
                bitmap.Save(previewPath, PngBitmapEncoderOptions.Default);
            }
            var heal = new CombatWireObservation { SkillCode = 14_000_010, Damage = 60_000, ResourceKind = CombatResourceKind.Health, HitCount = 1, AttemptCount = 1 };
            sink.AppendCombatWireObservation(Source(13_000), 103, 100, in heal);
            sink.CompleteFlush(13_001);
            vm.SettingsFlyout.CombatantSortMetric = CombatantSortMetric.HealingPerSecond;
            vm.RefreshCombatStatsForTesting(); frameBatch.FlushFrame();
            Assert.True(vm.SettingsFlyout.IsHealingMode);
            Assert.Equal(60_000, vm.TotalFilteredHealing);
            Assert.Equal(vm.TotalFilteredHealingPerSecond, vm.DisplayTotalRate);
            Assert.Equal(103, vm.Combatants[0].Id);
            Assert.Equal(vm.Combatants[0].Healing, vm.Combatants[0].DisplayAmount);
            Assert.Equal("— ms", vm.RoundTripTimeDisplay);
            var settingsWindow = new PulseSettingsWindow(vm.SettingsFlyout);
            settingsWindow.Show(); Dispatcher.UIThread.RunJobs();
            var settingsContent = Assert.IsAssignableFrom<Control>(settingsWindow.Content);
            settingsContent.Measure(new Size(760, 600)); settingsContent.Arrange(new Rect(0, 0, 760, 600)); Dispatcher.UIThread.RunJobs();
            var designPicker = settingsContent.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Tag, MeterDesign.FocusGlass));
            designPicker.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(MeterDesign.FocusGlass, vm.SettingsFlyout.MeterDesign);
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
            historyWindow.Close(); settingsWindow.Close();
            provider.DisposeAsync().AsTask().GetAwaiter().GetResult();
        });
    }
    private static void SavePreview(Control content, string name)
    {
        var previewPath = Environment.GetEnvironmentVariable("AION2_PREVIEW_PATH");
        if (string.IsNullOrWhiteSpace(previewPath)) return;
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)content.Bounds.Width * 2, (int)content.Bounds.Height * 2), new Vector(192, 192));
        bitmap.Render(content); bitmap.Save(Path.Combine(Path.GetDirectoryName(previewPath)!, name), PngBitmapEncoderOptions.Default);
    }
}
