using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Markup.Xaml.Styling;
using Cloris.Aion2Flow.ViewModels;
using Cloris.Aion2Flow.Services.Hotkeys;
using Cloris.Aion2Flow.Presentation;

namespace Cloris.Aion2Flow.Views;

public sealed class PulseSettingsWindow : Window
{
    public PulseSettingsWindow(SettingsFlyoutViewModel vm)
    {
        Title = "Aion2Pulse / Settings";
        Width = 760; Height = 640; MinWidth = 620; MinHeight = 480;
        Background = Brush.Parse("#10161E"); Foreground = Brush.Parse("#EDF3F8");
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new Cloris.Aion2Flow.Styles.PulseWindowTheme());
        DataContext = vm;
        var tabs = new TabControl { TabStripPlacement = Dock.Left, Margin = new Thickness(8) };
        tabs.Items.Add(Page("Appearance", "Choose a meter style. History has its own layout.",
            DesignChoices(vm),
            EnumChoice<HistoryLayout>("History layout", CompiledBinding.Create<SettingsFlyoutViewModel, HistoryLayout>(x => x.HistoryLayout, mode: BindingMode.TwoWay)),
            Range("Player row height", CompiledBinding.Create<SettingsFlyoutViewModel, int>(x => x.PlayerRowHeight, mode: BindingMode.TwoWay), 26, 46),
            Range("Surface intensity", CompiledBinding.Create<SettingsFlyoutViewModel, int>(x => x.SurfaceIntensity, mode: BindingMode.TwoWay), 40, 100),
            Check("Use class colors", CompiledBinding.Create<SettingsFlyoutViewModel, bool>(x => x.UseClassColors, mode: BindingMode.TwoWay)),
            Check("Short numbers · 2.48m / 29.5k", CompiledBinding.Create<SettingsFlyoutViewModel, bool>(x => x.UseCompactMainMetrics, mode: BindingMode.TwoWay)),
            Range("Interface scale (%)", CompiledBinding.Create<SettingsFlyoutViewModel, int>(x => x.UiScalePercent, mode: BindingMode.TwoWay), 50, 200)));
        tabs.Items.Add(Page("Overlay", "A 24 px title bar keeps the playfield visible.",
            Range("Overlay width · 0 = automatic", CompiledBinding.Create<SettingsFlyoutViewModel, int>(x => x.OverlayWidth, mode: BindingMode.TwoWay), 0, 1000),
            Text("Automatic uses a compact width. Drag the bottom-right grip while unlocked to resize. Combined DPS/HPS keeps four readable columns."),
            EnumChoice<TopmostMode>("Keep on top", CompiledBinding.Create<SettingsFlyoutViewModel, TopmostMode>(x => x.TopmostMode, mode: BindingMode.TwoWay)),
            Choice("Visible rows", CompiledBinding.Create<SettingsFlyoutViewModel, int>(x => x.MaxVisibleCombatantRows, mode: BindingMode.TwoWay), vm.RowCountOptions),
            EnumChoice<CombatantSortMetric>("Display and sorting", CompiledBinding.Create<SettingsFlyoutViewModel, CombatantSortMetric>(x => x.CombatantSortMetric, mode: BindingMode.TwoWay)),
            Check("Show DPS and HPS together", CompiledBinding.Create<SettingsFlyoutViewModel, bool>(x => x.ShowBothMetrics, mode: BindingMode.TwoWay)),
            EnumChoice<Cloris.Aion2Flow.SceneRuntime.Model.CombatantStatisticsScope>("Players", CompiledBinding.Create<SettingsFlyoutViewModel, Cloris.Aion2Flow.SceneRuntime.Model.CombatantStatisticsScope>(x => x.CombatantStatisticsScope, mode: BindingMode.TwoWay)),
            Check("Show total and per-second columns", CompiledBinding.Create<SettingsFlyoutViewModel, bool>(x => x.ShowDamageColumn, mode: BindingMode.TwoWay)),
            Check("Show per-second column", CompiledBinding.Create<SettingsFlyoutViewModel, bool>(x => x.ShowDamagePerSecondColumn, mode: BindingMode.TwoWay)),
            Check("Show player names", CompiledBinding.Create<SettingsFlyoutViewModel, bool>(x => x.ShowPlayerNames, mode: BindingMode.TwoWay)),
            Check("Show server names", CompiledBinding.Create<SettingsFlyoutViewModel, bool>(x => x.ShowPlayerShortServerName, mode: BindingMode.TwoWay)),
            Check("Show legion names", CompiledBinding.Create<SettingsFlyoutViewModel, bool>(x => x.ShowPlayerLegionName, mode: BindingMode.TwoWay)),
            Check("Hide title when click-through", CompiledBinding.Create<SettingsFlyoutViewModel, bool>(x => x.HideHeaderWhenClickThrough, mode: BindingMode.TwoWay)),
            Hotkey("Reset fight", vm, GlobalHotkeyAction.BattleReset, CompiledBinding.Create<SettingsFlyoutViewModel, string>(x => x.ResetHotkeyDisplay, mode: BindingMode.TwoWay)),
            Hotkey("Cycle click-through / locked / unlocked", vm, GlobalHotkeyAction.CycleOverlayInteraction, CompiledBinding.Create<SettingsFlyoutViewModel, string>(x => x.OverlayInteractionHotkeyDisplay, mode: BindingMode.TwoWay))));
        tabs.Items.Add(Page("Combat", "Damage and healing share the same combat duration. Results freeze between pulls.",
            EnumChoice<Cloris.Aion2Flow.SceneRuntime.Model.CombatTrackingBehavior>("Tracking behavior", CompiledBinding.Create<SettingsFlyoutViewModel, Cloris.Aion2Flow.SceneRuntime.Model.CombatTrackingBehavior>(x => x.CombatTracking, mode: BindingMode.TwoWay)),
            Text("Separate pulls: current behavior. Combat chain: combine normal mobs until the chosen break. Manual: keep collecting until Reset or a map change. Known boss deaths still end chains."),
            Range("Combat chain break (seconds)", CompiledBinding.Create<SettingsFlyoutViewModel, int>(x => x.CombatChainSeconds, mode: BindingMode.TwoWay), 5, 180),
            Range("Inactivity timeout (seconds)", CompiledBinding.Create<SettingsFlyoutViewModel, int>(x => x.CombatIdleSeconds, mode: BindingMode.TwoWay), 2, 30),
            Check("Exclude long damage pauses from DPS / HPS time", CompiledBinding.Create<SettingsFlyoutViewModel, bool>(x => x.PauseDamageTime, mode: BindingMode.TwoWay)),
            Range("Damage pause threshold (seconds)", CompiledBinding.Create<SettingsFlyoutViewModel, int>(x => x.DamagePauseSeconds, mode: BindingMode.TwoWay), 1, 10),
            Text("Uses group damage activity, not a verified cutscene or invulnerability signal. Disable to include all combat time."),
            Text("All enemies follows the selected behavior. Boss-only collection uses the legacy boss boundaries instead."),
            EnumChoice<Cloris.Aion2Flow.SceneRuntime.Model.SceneKind>("Collection scope", CompiledBinding.Create<SettingsFlyoutViewModel, Cloris.Aion2Flow.SceneRuntime.Model.SceneKind>(x => x.SceneKind, mode: BindingMode.TwoWay)),
            EnumChoice<EncounterTimeDisplayFormat>("Time format", CompiledBinding.Create<SettingsFlyoutViewModel, EncounterTimeDisplayFormat>(x => x.EncounterTimeDisplayFormat, mode: BindingMode.TwoWay)),
            Check("Show boss health", CompiledBinding.Create<SettingsFlyoutViewModel, bool>(x => x.ShowFocusStatusBar, mode: BindingMode.TwoWay)),
            Check("Enable skill monitor", CompiledBinding.Create<SettingsFlyoutViewModel, bool>(x => x.SkillMonitorEnabled, mode: BindingMode.TwoWay))));
        tabs.Items.Add(Page("History", "Saved locally · retains 50 bosses and 50 normal pulls independently.",
            EnumChoice<HistoryLayout>("History layout", CompiledBinding.Create<SettingsFlyoutViewModel, HistoryLayout>(x => x.HistoryLayout, mode: BindingMode.TwoWay)),
            Text("Filter by boss, incomplete result or encounter name. Select a player to inspect damage, healing and original skill icons."),
            Text("Instance totals use combined combat time, excluding breaks between fights.")));
        tabs.Items.Add(Page("Capture", "Passive network packet recording.", Text("Global Early Access · network only"),
            Text("Capture starts with the meter. Driver, game port and connection status remain visible in the footer."),
            Text("Healing shows amounts reported by the packets. Shield absorption is kept separate in player details.")));
        tabs.Items.Add(Page("About", "Aion2Pulse Preview", Text("Version 0.4.0-preview.2 · Experimental"), Text("Based on the Aion2Flow packet engine. GPL-3.0 license and upstream credits are included in the package.")));
        var done = new Button { Content = "Done", HorizontalAlignment = HorizontalAlignment.Right, Padding = new Thickness(22, 7) };
        done.Click += (_, _) => Close();
        var footer = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(20, 10) };
        footer.Children.Add(Text("Changes are saved automatically")); Grid.SetColumn(done, 1); footer.Children.Add(done);
        var categories = new StackPanel { Spacing = 5, Margin = new Thickness(8, 16) };
        var pageHost = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
        object? firstPage = null;
        foreach (var page in tabs.Items.OfType<TabItem>())
        {
            var pageContent = page.Content; page.Content = null;
            firstPage ??= pageContent;
            var button = new Button { Content = page.Header }; button.Classes.Add("PulseNav");
            button.Click += (_, _) => { foreach (var item in categories.Children) item.Classes.Set("selected", ReferenceEquals(item, button)); pageHost.Content = pageContent; };
            categories.Children.Add(button);
        }
        categories.Children[0].Classes.Add("selected"); pageHost.Content = firstPage;
        var sidebar = new Border { Background = Brush.Parse("#18212C"), Child = categories };
        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("146,*") }; layout.Children.Add(sidebar); Grid.SetColumn(pageHost, 1); layout.Children.Add(pageHost);
        var shell = new Grid { RowDefinitions = new RowDefinitions("*,Auto"), Background = Brush.Parse("#10161E") }; shell.Children.Add(layout); Grid.SetRow(footer, 1); shell.Children.Add(footer); Content = shell;
    }

    private Control DesignChoices(SettingsFlyoutViewModel vm)
    {
        var choices = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 8 };
        var buttons = new List<Button>();
        foreach (var design in Enum.GetValues<MeterDesign>())
        {
            var preview = new StackPanel { Spacing = 5, Margin = new Thickness(0, 0, 0, 8) };
            for (var index = 0; index < 3; index++) preview.Children.Add(new Border { Height = design == MeterDesign.RaidClassic ? 8 : 2, Width = 100 - index * 22, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, design == MeterDesign.RaidClassic ? 0 : 5, 0, 0), Background = Brush.Parse(new[] { "#D8A572", "#A4A5F0", "#9FCE8D" }[index]) });
            var panel = new StackPanel { Spacing = 6 }; panel.Children.Add(preview);
            panel.Children.Add(new TextBlock { Text = design switch { MeterDesign.RaidClassic => "Raid Classic", MeterDesign.FocusGlass => "Focus Glass", _ => "Combat Studio" }, FontSize = 11, TextWrapping = TextWrapping.Wrap });
            var button = new Button { Content = panel, Tag = design, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            button.Click += (_, _) => vm.MeterDesign = design;
            Grid.SetColumn(button, buttons.Count); choices.Children.Add(button); buttons.Add(button);
        }
        void Refresh() { foreach (var button in buttons) button.BorderBrush = Brush.Parse(Equals(button.Tag, vm.MeterDesign) ? "#71DBC4" : "#303E4D"); }
        System.ComponentModel.PropertyChangedEventHandler changed = (_, e) => { if (e.PropertyName == nameof(vm.MeterDesign)) Refresh(); };
        vm.PropertyChanged += changed; Closed += (_, _) => vm.PropertyChanged -= changed; Refresh();
        var section = new StackPanel { Spacing = 10 }; section.Children.Add(new TextBlock { Text = "Meter design", FontSize = 12 }); section.Children.Add(choices); return section;
    }

    private static TabItem Page(string title, string description, params Control[] controls)
    {
        var panel = new StackPanel { Spacing = 15, Margin = new Thickness(22, 18) };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 22, FontWeight = FontWeight.SemiBold }); panel.Children.Add(Text(description));
        foreach (var control in controls) panel.Children.Add(control);
        return new TabItem { Header = title, Content = new ScrollViewer { Content = panel, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled } };
    }
    private static TextBlock Text(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = Brush.Parse("#AAB7C6"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
    private static Control Row(string label, Control input)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 16 };
        row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(input, 1); row.Children.Add(input); return row;
    }
    private static Control EnumChoice<T>(string label, BindingBase property) where T : struct, Enum => Choice(label, property, Enum.GetValues<T>());
    private static Control Choice(string label, BindingBase property, System.Collections.IEnumerable values)
    {
        var combo = new ComboBox { ItemsSource = values, MinWidth = 160, MaxWidth = 230 };
        combo.ItemTemplate = new FuncDataTemplate<object>((value, _) => new TextBlock { Text = value switch
        {
            MeterDesign.RaidClassic => "Raid Classic", MeterDesign.FocusGlass => "Focus Glass", MeterDesign.CombatStudio => "Combat Studio",
            Cloris.Aion2Flow.SceneRuntime.Model.CombatTrackingBehavior.SeparatePulls => "Separate pulls",
            Cloris.Aion2Flow.SceneRuntime.Model.CombatTrackingBehavior.CombatChain => "Combat chain",
            Cloris.Aion2Flow.SceneRuntime.Model.CombatTrackingBehavior.Manual => "Manual reset",
            Cloris.Aion2Flow.SceneRuntime.Model.SceneKind.Standard => "All enemies", Cloris.Aion2Flow.SceneRuntime.Model.SceneKind.Boss => "Boss only",
            EncounterTimeDisplayFormat.DecimalSeconds => "Seconds", EncounterTimeDisplayFormat.MinutesSeconds => "Minutes : seconds",
            HistoryLayout.SplitView => "Split view", HistoryLayout.CompactList => "Compact list", HistoryLayout.Cards => "Cards",
            CombatantSortMetric.DamagePerSecond => "Damage per second (DPS)", CombatantSortMetric.TotalDamage => "Total damage",
            CombatantSortMetric.HealingPerSecond => "Healing per second (HPS)", CombatantSortMetric.TotalHealing => "Total healing",
            TopmostMode.GameForeground => "While game is active", TopmostMode.Always => "Always", TopmostMode.Never => "Never",
            _ => value?.ToString() ?? string.Empty
        }});
        combo.Bind(SelectingItemsControl.SelectedItemProperty, property); return Row(label, combo);
    }
    private static Control Range(string label, BindingBase property, int min, int max)
    {
        var input = new Slider { Minimum = min, Maximum = max, TickFrequency = 1, IsSnapToTickEnabled = true, Width = 155 };
        input.Bind(Slider.ValueProperty, property);
        var value = new TextBlock { MinWidth = 36, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        value.Bind(TextBlock.TextProperty, property);
        var controls = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        controls.Children.Add(input); controls.Children.Add(value); return Row(label, controls);
    }
    private static Control Check(string label, BindingBase property)
    {
        var check = new CheckBox { Content = label }; check.Bind(CheckBox.IsCheckedProperty, property); return check;
    }
    private static Control Hotkey(string label, SettingsFlyoutViewModel vm, GlobalHotkeyAction action, BindingBase display)
    {
        var capture = new Button(); capture.Bind(ContentControl.ContentProperty, display);
        capture.Click += (_, _) => { vm.BeginCaptureHotkey(action); capture.Focus(); };
        capture.KeyDown += (_, e) =>
        {
            if (vm.CapturingHotkeyAction != action) return;
            if (e.Key == Avalonia.Input.Key.Escape) vm.CancelCaptureHotkey(action);
            else if (HotkeyDefinition.FromKeyEvent(e.KeyModifiers, e.Key) is { } definition) vm.ApplyCapturedHotkey(action, definition);
            e.Handled = true;
        };
        capture.LostFocus += (_, _) => vm.CancelCaptureHotkey(action);
        var clear = new Button { Content = "Clear" }; clear.Click += (_, _) => vm.ClearHotkey(action);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 }; buttons.Children.Add(capture); buttons.Children.Add(clear);
        return Row(label, buttons);
    }
}
