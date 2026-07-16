using Contoso.Common.Core;

namespace Contoso.Telemetry.Client;

/// <summary>
/// Buffers telemetry events and flushes them once a batch size or age threshold is
/// hit. Deliberately depends on Common.Core only — this is the "isolated corner" of
/// the sample graph: a change here should never fan out into the service layer, so it
/// always lands in its own merge-queue lane.
/// </summary>
public sealed class TelemetryBatcher
{
    private readonly IClock _clock;
    private readonly int _maxBatchSize;
    private readonly TimeSpan _maxBatchAge;
    private readonly List<TelemetryEvent> _buffer = new();
    private DateTimeOffset _batchStartedAt;

    public TelemetryBatcher(IClock clock, int maxBatchSize = 50, TimeSpan? maxBatchAge = null)
    {
        _clock = clock;
        _maxBatchSize = maxBatchSize;
        _maxBatchAge = maxBatchAge ?? TimeSpan.FromSeconds(30);
        _batchStartedAt = clock.UtcNow;
    }

    public IReadOnlyList<TelemetryEvent> Add(TelemetryEvent evt)
    {
        if (_buffer.Count == 0)
        {
            _batchStartedAt = _clock.UtcNow;
        }

        _buffer.Add(evt);

        if (ShouldFlush())
        {
            return Flush();
        }

        return Array.Empty<TelemetryEvent>();
    }

    public bool ShouldFlush() =>
        _buffer.Count >= _maxBatchSize || _clock.UtcNow - _batchStartedAt >= _maxBatchAge;

    public IReadOnlyList<TelemetryEvent> Flush()
    {
        var flushed = _buffer.ToList();
        _buffer.Clear();
        return flushed;
    }
}

public sealed record TelemetryEvent(string Name, IReadOnlyDictionary<string, string> Properties);
// demo PR v2: touch Telemetry.Client for impacted-targets.yml
// re-trigger fresh impacted-targets upload 1784225256
// Stress 01: Telemetry.Client tweak (small lane) (1784238739912033000)
