namespace Cloris.Aion2Flow.Services;

internal sealed class LatencyRefreshGate(TimeProvider timeProvider)
{
    private long? _lastRefresh;
    public bool TryRefresh()
    {
        var now = timeProvider.GetTimestamp();
        if (_lastRefresh is long previous && now >= previous && timeProvider.GetElapsedTime(previous, now) < TimeSpan.FromSeconds(5))
            return false;
        _lastRefresh = now;
        return true;
    }
    public void Reset() => _lastRefresh = null;
}
