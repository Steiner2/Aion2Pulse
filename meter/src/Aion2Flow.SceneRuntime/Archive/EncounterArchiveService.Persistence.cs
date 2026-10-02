using System.Text.Json;

namespace Cloris.Aion2Flow.SceneRuntime.Archive;

public sealed partial class EncounterArchiveService
{
    private readonly string? _directory;
    private Task _pendingWrite = Task.CompletedTask;
    private readonly HashSet<string> _unreadableFiles = [];
    public string? StorageError { get; private set; }
    public event EventHandler? StorageStatusChanged;

    private void SetStorageError(string message)
    {
        StorageError = message;
        StorageStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public EncounterArchiveService() { }

    public EncounterArchiveService(string directory)
    {
        _directory = Path.GetFullPath(directory);
        try
        {
            Directory.CreateDirectory(_directory);
            foreach (var path in Directory.EnumerateFiles(_directory, "*.json"))
            {
                if (!Guid.TryParse(Path.GetFileNameWithoutExtension(path), out _)) continue;
                try
                {
                    using var stream = File.OpenRead(path);
                    var data = JsonSerializer.Deserialize(stream, EncounterJsonContext.Default.StoredEncounter);
                    if (data is null) continue;
                    var payload = SceneArchivePayload.FromStored(data);
                    if (_historyByEncounterId.ContainsKey(payload.Snapshot.EncounterId)) continue;
                    var record = new ArchivedEncounterRecord { Id = data.Id, ArchivedAt = data.ArchivedAt,
                        Trigger = data.Trigger, IsAutomatic = data.IsAutomatic, ScenePayload = payload };
                    _history.Add(record);
                    _historyByEncounterId[payload.Snapshot.EncounterId] = record;
                }
                catch (Exception e) when (e is IOException or JsonException or InvalidDataException or ArgumentException)
                { _unreadableFiles.Add(path); StorageError = $"Skipped damaged history file: {Path.GetFileName(path)} ({e.Message})"; }
            }
            _history.Sort((a, b) => b.ArchivedAt.CompareTo(a.ArchivedAt));
            foreach (var removed in _history.Skip(MaxHistoryCount))
                _historyByEncounterId.Remove(removed.ScenePayload.Snapshot.EncounterId);
            if (_history.Count > MaxHistoryCount) _history.RemoveRange(MaxHistoryCount, _history.Count - MaxHistoryCount);
            _historySnapshot = [.. _history];
            PruneFiles();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { StorageError = $"History storage unavailable: {e.Message}"; }
    }

    // Called under the history lock. Each immutable encounter is written once, off the UI thread.
    private void QueueSave(ArchivedEncounterRecord record)
    {
        if (_directory is null) return;
        _pendingWrite = _pendingWrite.ContinueWith(_ => SaveRecord(record), TaskScheduler.Default);
    }

    private void SaveRecord(ArchivedEncounterRecord record)
    {
        var path = Path.Combine(_directory!, $"{record.Id:D}.json");
        var temporary = path + ".tmp";
        try
        {
            Directory.CreateDirectory(_directory!);
            using (var stream = File.Create(temporary))
                JsonSerializer.Serialize(stream, record.ScenePayload.ToStored(record), EncounterJsonContext.Default.StoredEncounter);
            File.Move(temporary, path, overwrite: true);
            PruneFiles();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        { SetStorageError($"Could not save encounter history: {e.Message}"); }
    }

    private void PruneFiles()
    {
        HashSet<Guid> retained;
        lock (_lock) retained = _history.Select(r => r.Id).ToHashSet();
        foreach (var path in Directory.EnumerateFiles(_directory!, "*.json"))
            if (!_unreadableFiles.Contains(path) && Guid.TryParse(Path.GetFileNameWithoutExtension(path), out var id) && !retained.Contains(id))
                File.Delete(path);
    }

    public Task FlushAsync() { lock (_lock) return _pendingWrite; }
    public async ValueTask DisposeAsync() => await FlushAsync().ConfigureAwait(false);
}
