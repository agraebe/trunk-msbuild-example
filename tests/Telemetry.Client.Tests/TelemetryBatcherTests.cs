using Contoso.Common.Core;
using Contoso.Telemetry.Client;
using Xunit;

namespace Contoso.Telemetry.Client.Tests;

public class TelemetryBatcherTests
{
    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UnixEpoch;
    }

    private static TelemetryEvent MakeEvent(string name = "device.enrolled") =>
        new(name, new Dictionary<string, string> { ["source"] = "test" });

    [Fact]
    public void Add_BelowBatchSize_DoesNotFlush()
    {
        var clock = new FakeClock();
        var batcher = new TelemetryBatcher(clock, maxBatchSize: 3);

        var flushed = batcher.Add(MakeEvent());
        Assert.Empty(flushed);
    }

    [Fact]
    public void Add_ReachesBatchSize_FlushesAll()
    {
        var clock = new FakeClock();
        var batcher = new TelemetryBatcher(clock, maxBatchSize: 2);

        batcher.Add(MakeEvent("first"));
        var flushed = batcher.Add(MakeEvent("second"));

        Assert.Equal(2, flushed.Count);
    }

    [Fact]
    public void Add_AfterFlush_StartsNewBatch()
    {
        var clock = new FakeClock();
        var batcher = new TelemetryBatcher(clock, maxBatchSize: 1);

        batcher.Add(MakeEvent("first"));
        var flushed = batcher.Add(MakeEvent("second"));

        Assert.Single(flushed);
        Assert.Equal("second", flushed[0].Name);
    }

    [Fact]
    public void ShouldFlush_WhenBatchOlderThanMaxAge_ReturnsTrue()
    {
        var clock = new FakeClock();
        var batcher = new TelemetryBatcher(clock, maxBatchSize: 100, maxBatchAge: TimeSpan.FromSeconds(1));

        batcher.Add(MakeEvent());
        clock.UtcNow = clock.UtcNow.AddSeconds(2);

        Assert.True(batcher.ShouldFlush());
    }

    [Fact]
    public void ShouldFlush_FreshBatch_ReturnsFalse()
    {
        var clock = new FakeClock();
        var batcher = new TelemetryBatcher(clock, maxBatchSize: 100, maxBatchAge: TimeSpan.FromSeconds(30));

        batcher.Add(MakeEvent());
        Assert.False(batcher.ShouldFlush());
    }

    [Fact]
    public void Flush_EmptyBuffer_ReturnsEmptyList()
    {
        var clock = new FakeClock();
        var batcher = new TelemetryBatcher(clock);
        Assert.Empty(batcher.Flush());
    }

    [Fact]
    public void Flush_ClearsBufferForNextBatch()
    {
        var clock = new FakeClock();
        var batcher = new TelemetryBatcher(clock, maxBatchSize: 100);

        batcher.Add(MakeEvent());
        batcher.Flush();

        Assert.False(batcher.ShouldFlush());
    }
}
