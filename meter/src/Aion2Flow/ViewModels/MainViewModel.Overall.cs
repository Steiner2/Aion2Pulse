using Cloris.Aion2Flow.SceneRuntime.Combat;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cloris.Aion2Flow.ViewModels;

public sealed partial class MainViewModel
{
    [ObservableProperty]
    public partial bool IsViewingOverall { get; set; }
    public double TotalFilteredDamage { get; private set => SetFrameProperty(ref field, value); }
    public double TotalFilteredHealing { get; private set => SetFrameProperty(ref field, value); }
    public double TotalFilteredHealingPerSecond { get; private set => SetFrameProperty(ref field, value); }
    public double DisplayTotalAmount { get; private set => SetFrameProperty(ref field, value); }
    public double DisplayTotalRate { get; private set => SetFrameProperty(ref field, value); }
    public string FooterToolTip => $"{DriverIndicatorToolTip}\n{GamePortIndicatorToolTip}\n{CaptureLockIndicatorToolTip}\n{LatencyToolTip}";
    public string RoundTripTimeDisplay => RoundTripTimeMilliseconds > 0 ? $"{RoundTripTimeMilliseconds} ms" : "— ms";
    public string HistoryButtonToolTip => $"Open saved fights, player skills, the event timeline and encounter analysis.\n{HistoryStorageStatus}";
    public string HistoryStorageStatus => _encounterArchiveService.StorageError ?? "History saved locally";
    private void OnHistoryStorageStatusChanged(object? sender, EventArgs e)
        => Avalonia.Threading.Dispatcher.UIThread.Post(() => { OnPropertyChanged(nameof(HistoryStorageStatus)); OnPropertyChanged(nameof(HistoryButtonToolTip)); });

    [RelayCommand]
    private void ShowOverall()
    {
        SelectedEncounterHistory = null;
        IsViewingArchivedEncounter = false;
        IsViewingOverall = true;
        RefreshOverall();
    }

    [RelayCommand]
    private void ResetOverall()
    {
        // Archive the current pull once, then begin a new manual total scope.
        CloseCurrentArchiveScope("overall-reset", isAutomatic: false);
        _encounterArchiveService.Overall.Reset();
        ResetLivePresentation();
        ReturnToLive();
    }

    private void RefreshOverall()
    {
        _displayedSnapshot = _encounterArchiveService.Overall.CreateSnapshot(_latestLiveSnapshot, _latestLiveFrame.MetadataRegistry);
        ApplySnapshot(_displayedSnapshot);
    }

    private void UpdateOverallMap(SceneCombatSnapshot snapshot)
        => _encounterArchiveService.Overall.ObserveMap(snapshot.MapId, snapshot.MapInstanceId);
}
