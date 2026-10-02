namespace Cloris.Aion2Flow.Capture;

/// <summary>Passive transport RTT fallback. Only client data and server ACKs on the admitted game connection are used.</summary>
internal sealed class TcpRoundTripEstimator
{
    private readonly object _gate = new();
    private readonly List<SentSegment> _pending = [];
    private long _generation;
    private double? _milliseconds;
    private long _sampleTime;
    private uint? _highestEnd;
    private sealed record SentSegment(uint Start, uint End, long Time) { public bool Retransmitted { get; set; } }
    internal void Observe(long generation, bool outbound, uint sequence, int payloadLength, bool ack, uint acknowledgment, long timestampMilliseconds)
    {
        lock (_gate)
        {
            if (generation != _generation) { _generation = generation; _pending.Clear(); _milliseconds = null; _highestEnd = null; }
            _pending.RemoveAll(s => timestampMilliseconds - s.Time > 8000);
            if (outbound && payloadLength > 0)
            {
                var end = unchecked(sequence + (uint)payloadLength);
                if (_highestEnd is { } highest && Before(sequence, highest))
                {
                    foreach (var segment in _pending)
                        if (Before(sequence, segment.End) && Before(segment.Start, end)) segment.Retransmitted = true;
                    return;
                }
                _highestEnd = end;
                if (_pending.Count >= 128) { _pending.Clear(); return; }
                _pending.Add(new SentSegment(sequence, end, timestampMilliseconds));
            }
            if (outbound || !ack || _highestEnd is not { } highestEnd || Before(highestEnd, acknowledgment)) return;
            var acknowledged = _pending.Where(s => !Before(acknowledgment, s.End)).ToArray();
            if (acknowledged.Length == 0) return;
            _pending.RemoveAll(s => !Before(acknowledgment, s.End));
            // Karn: an ACK covering a retransmission cannot provide an unambiguous timing sample.
            if (acknowledged.Any(s => s.Retransmitted)) return;
            var elapsed = timestampMilliseconds - acknowledged[^1].Time;
            if (elapsed <= 0 || elapsed > 8000) return;
            _milliseconds = _milliseconds is { } previous ? previous * .8 + elapsed * .2 : elapsed;
            _sampleTime = timestampMilliseconds;
        }
    }
    internal double? GetCurrentMilliseconds(long generation, long nowMilliseconds)
    {
        lock (_gate) return generation == _generation && nowMilliseconds - _sampleTime <= 20000 ? _milliseconds : null;
    }
    internal void Clear()
    {
        lock (_gate) { _pending.Clear(); _milliseconds = null; _highestEnd = null; _generation = 0; }
    }
    private static bool Before(uint left, uint right) => unchecked((int)(left - right)) < 0;
}
