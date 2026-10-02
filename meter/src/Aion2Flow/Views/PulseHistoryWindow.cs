using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using Cloris.Aion2Flow.ViewModels;
using Cloris.Aion2Flow.SceneRuntime.Combat;
using Cloris.Aion2Flow.SceneRuntime.Archive;

namespace Cloris.Aion2Flow.Views;

public sealed class PulseHistoryWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly StackPanel _fights = new() { Spacing = 4 };
    private readonly StackPanel _players = new() { Spacing = 5 };
    private readonly TextBlock _selected = new() { FontSize = 16, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _status = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap };
    private readonly TextBox _search = new() { PlaceholderText = "Find a fight…", Width = 220 };
    private readonly ComboBox _filter = new() { ItemsSource = new[] { "All fights", "Bosses", "Incomplete", "Normal pulls" }, SelectedIndex = 1, Width = 145 };
    private readonly Grid _body = new() { Margin = new Thickness(16) };
    private readonly ScrollViewer _list;
    private readonly StackPanel _detail = new() { Spacing = 12, Margin = new Thickness(16, 0, 0, 0) };
    private readonly ContentControl _skills = new();
    private readonly Expander _playerPicker = new() { Header = "Players", IsExpanded = true, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly GridSplitter _splitter = new() { Width = 6, HorizontalAlignment = HorizontalAlignment.Stretch, ResizeDirection = GridResizeDirection.Columns, Background = Brush.Parse("#303E4D") };

    public PulseHistoryWindow(MainViewModel vm)
    {
        _vm = vm; Title = "Aion2Pulse / Combat history"; Width = 1050; Height = 720; MinWidth = 740; MinHeight = 500;
        Background = Brush.Parse("#10161E"); Foreground = Brush.Parse("#EDF3F8"); RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new Cloris.Aion2Flow.Styles.PulseWindowTheme());
        _list = new ScrollViewer { Content = _fights };
        var toolbar = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(16) };
        toolbar.Children.Add(_filter); toolbar.Children.Add(_search);
        var live = new Button { Content = "Live", Command = vm.ReturnToLiveCommand }; toolbar.Children.Add(live);
        var overall = new Button { Content = "Instance totals", Command = vm.ShowOverallCommand }; toolbar.Children.Add(overall);
        toolbar.Children.Add(new Button { Content = "New total", Command = vm.ResetOverallCommand });
        foreach (var child in toolbar.Children) child.Margin = new Thickness(0, 0, 8, 4);
        _playerPicker.Content = _players;
        _detail.Children.Add(_selected); _detail.Children.Add(_playerPicker); _detail.Children.Add(_skills);
        _body.Children.Add(_list); _body.Children.Add(new ScrollViewer { Content = _detail });
        _body.Children.Add(_splitter);
        SizeChanged += (_, _) => ApplyLayout();
        var shell = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), Background = Brush.Parse("#10161E") }; shell.Children.Add(toolbar); Grid.SetRow(_body, 1); shell.Children.Add(_body);
        _status.Margin = new Thickness(16, 8); Grid.SetRow(_status, 2); shell.Children.Add(_status); Content = shell;
        _filter.SelectionChanged += (_, _) => RefreshList(); _search.TextChanged += (_, _) => RefreshList();
        vm.EncounterHistory.CollectionChanged += OnHistoryChanged;
        vm.SettingsFlyout.PropertyChanged += OnSettingsChanged;
        vm.PropertyChanged += OnMainChanged;
        Closed += (_, _) => { vm.EncounterHistory.CollectionChanged -= OnHistoryChanged; vm.SettingsFlyout.PropertyChanged -= OnSettingsChanged; vm.PropertyChanged -= OnMainChanged; };
        ApplyLayout(); RefreshList(); RefreshDetail();
    }
    private void OnHistoryChanged(object? sender, NotifyCollectionChangedEventArgs e) => Dispatcher.UIThread.Post(RefreshList);
    private void OnMainChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.SelectedEncounterHistory) or nameof(MainViewModel.IsViewingOverall) or nameof(MainViewModel.IsViewingArchivedEncounter)) { RefreshList(); RefreshDetail(); }
    }
    private void OnSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsFlyoutViewModel.HistoryLayout)) { ApplyLayout(); RefreshList(); }
        if (e.PropertyName is nameof(SettingsFlyoutViewModel.CombatantSortMetric) or nameof(SettingsFlyoutViewModel.CombatantStatisticsScope)) Dispatcher.UIThread.Post(RefreshDetail);
    }
    private void ApplyLayout()
    {
        var split = _vm.SettingsFlyout.HistoryLayout == HistoryLayout.SplitView && Width >= 950;
        if (split && _body.ColumnDefinitions.Count == 3 || !split && _body.ColumnDefinitions.Count == 1) return;
        _body.ColumnDefinitions = new ColumnDefinitions(split ? "270,6,*" : "*");
        if (split) { _body.ColumnDefinitions[0].MinWidth = 200; _body.ColumnDefinitions[2].MinWidth = 500; }
        _body.RowDefinitions = new RowDefinitions(split ? "*" : "*,*");
        Grid.SetColumn(_body.Children[1], split ? 2 : 0); Grid.SetRow(_body.Children[1], split ? 0 : 1);
        Grid.SetColumn(_splitter, split ? 1 : 0); _splitter.IsVisible = split;
        _detail.Margin = split ? new Thickness(16, 0, 0, 0) : new Thickness(0, 16, 0, 0);
    }
    internal static (double Damage, double Healing, double Dps, double Hps) Totals(SceneCombatSnapshot snapshot)
    {
        double damage = 0, healing = 0;
        foreach (var entry in snapshot.Combatants)
            if (entry.Metrics.IsVisiblePlayerCombatant) { damage += entry.Metrics.DamageAmount; healing += entry.Metrics.HealingAmount; }
        var seconds = snapshot.EncounterTime / 1000d;
        return (damage, healing, seconds > 0 ? damage / seconds : 0, seconds > 0 ? healing / seconds : 0);
    }
    private static string Number(double n) => n >= 1_000_000 ? $"{n / 1_000_000:0.00}m" : n >= 1000 ? $"{n / 1000:0.0}k" : $"{n:0}";
    internal static string EndReason(string reason) => reason switch
    {
        "capture-interrupted" => "Capture interrupted · incomplete", "idle-heuristic" => "Inactivity timeout",
        "enemies-inactive" => "Combat ended", "recording" => "In progress", "manual-reset" => "Manual reset",
        "map-transition" => "Map changed", _ => string.IsNullOrWhiteSpace(reason) ? "Saved fight" : reason
    };
    private void RefreshList()
    {
        _fights.Children.Clear();
        string? group = null;
        foreach (var item in _vm.EncounterHistory)
        {
            var snapshot = item.Record.ScenePayload.Snapshot;
            var incomplete = snapshot.Encounter.Reason == "capture-interrupted";
            var boss = EncounterArchiveService.IsBossEncounter(snapshot);
            var reason = snapshot.Encounter.Reason == "recording" ? item.Record.Trigger switch { "manual" => "Manually saved", "overall-reset" => "Instance totals reset", _ => "Saved fight" } : EndReason(snapshot.Encounter.Reason);
            if (_filter.SelectedIndex == 1 && !boss || _filter.SelectedIndex == 2 && !incomplete || _filter.SelectedIndex == 3 && boss) continue;
            if (!($"{item.SceneName} {item.ArchivedAtText} {reason}").Contains(_search.Text ?? "", StringComparison.OrdinalIgnoreCase)) continue;
            var visit = $"{item.Record.ArchivedAt.ToLocalTime():yyyy-MM-dd}/{snapshot.MapId}/{snapshot.MapInstanceId}";
            if (visit != group)
            {
                var location = snapshot.MapId == 0 ? "Location unavailable" : item.DisplayContext.ResolveMapName(snapshot.MapId);
                _fights.Children.Add(new TextBlock { Text = $"{item.Record.ArchivedAt.ToLocalTime():dd MMM} · {location}" + (snapshot.MapInstanceId == 0 ? "" : $" · instance {snapshot.MapInstanceId}"), Foreground = Brush.Parse("#AAB7C6"), FontSize = 11, Margin = new Thickness(2, 13, 2, 5), TextWrapping = TextWrapping.Wrap });
                group = visit;
            }
            var t = Totals(snapshot);
            var card = _vm.SettingsFlyout.HistoryLayout == HistoryLayout.Cards;
            var row = new StackPanel { Spacing = card ? 6 : 2 };
            row.Children.Add(new TextBlock { Text = $"{item.Record.ArchivedAt.ToLocalTime():HH:mm} · {(boss ? "Boss" : "Pull")} · {item.SceneName}", FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
            row.Children.Add(new TextBlock { Text = $"{TimeSpan.FromMilliseconds(snapshot.EncounterTime):m\\:ss} · {Number(t.Damage)} damage · {Number(t.Dps)} DPS", FontSize = 12, TextWrapping = TextWrapping.Wrap });
            row.Children.Add(new TextBlock { Text = $"{Number(t.Healing)} healing · {Number(t.Hps)} HPS · {reason}", FontSize = 11, Foreground = Brush.Parse("#AAB7C6"), TextWrapping = TextWrapping.Wrap });
            var button = new Button { Content = row, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(10, card ? 14 : 8), Background = Brush.Parse(_vm.SelectedEncounterHistory?.Record.Id == item.Record.Id ? "#203B3D" : "#18212C") };
            button.Click += (_, _) => { _vm.SelectedEncounterHistory = item; RefreshDetail(); }; _fights.Children.Add(button);
        }
        if (_fights.Children.Count == 0) _fights.Children.Add(new TextBlock { Text = _filter.SelectedIndex == 1 ? "No saved bosses. Select Normal pulls or All fights to see other encounters." : "No matching fights", TextWrapping = TextWrapping.Wrap });
        var bossCount = _vm.EncounterHistory.Count(item => EncounterArchiveService.IsBossEncounter(item.Record.ScenePayload.Snapshot));
        _status.Text = $"{bossCount} / 50 bosses · {_vm.EncounterHistory.Count - bossCount} / 50 pulls · {_vm.HistoryStorageStatus}";
    }
    private void RefreshDetail()
    {
        _players.Children.Clear(); _skills.Content = null;
        _playerPicker.Header = "Players"; _playerPicker.IsExpanded = true;
        var item = _vm.SelectedEncounterHistory;
        if (item is null) { _selected.Text = "Select a saved fight"; return; }
        _selected.Text = $"{item.SceneName} · {item.Record.ArchivedAt.ToLocalTime():HH:mm}";
        Cloris.Aion2Flow.Controls.DisplayContextProvider.SetDisplayContext(_body, item.DisplayContext);
        var headings = new Grid { ColumnDefinitions = new ColumnDefinitions("*,110,110"), ColumnSpacing = 6 };
        foreach (var (label, column) in new[] { ("PLAYER", 0), ("DAMAGE / DPS", 1), ("HEALING / HPS", 2) })
        {
            var heading = new TextBlock { Text = label, FontSize = 10, Foreground = Brush.Parse("#AAB7C6"), HorizontalAlignment = column == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right }; Grid.SetColumn(heading, column); headings.Children.Add(heading);
        }
        _players.Children.Add(headings);
        foreach (var row in _vm.Combatants)
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,110,110"), ColumnSpacing = 6 };
            grid.Children.Add(new Cloris.Aion2Flow.Controls.PcDisplay { EntityId = row.Id, IconSize = 18, IconSpacing = 5, VerticalAlignment = VerticalAlignment.Center });
            var damage = new TextBlock { Text = $"{Number(row.Damage)} / {Number(row.DamagePerSecond)}", HorizontalAlignment = HorizontalAlignment.Right, FontSize = 12 }; Grid.SetColumn(damage, 1); grid.Children.Add(damage);
            var healing = new TextBlock { Text = $"{Number(row.Healing)} / {Number(row.HealingPerSecond)}", HorizontalAlignment = HorizontalAlignment.Right, FontSize = 12 }; Grid.SetColumn(healing, 2); grid.Children.Add(healing);
            var button = new Button { Content = grid, HorizontalContentAlignment = HorizontalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Stretch };
            button.Click += (_, _) =>
            {
                _vm.SelectedCombatant = row;
                _playerPicker.Header = $"Players · {item.DisplayContext.ResolvePcName(row.Id)}";
                _playerPicker.IsExpanded = false;
                var tabs = new TabControl { Height = 520 };
                tabs.Items.Add(new TabItem { Header = "Skills", Content = new CombatantDetailsView { DataContext = _vm.CombatantDetails }, Height = double.NaN });
                tabs.Items.Add(new TabItem { Header = "Timeline", Content = new SkillEventTimelineView(item.Record.ScenePayload, row.Id, item.DisplayContext) });
                _skills.Content = tabs;
            };
            _players.Children.Add(button);
        }
        _players.Children.Add(new TextBlock { Text = "Select a player to see damage and healing skills", FontSize = 11, Foreground = Brush.Parse("#AAB7C6"), TextWrapping = TextWrapping.Wrap });
    }
}
