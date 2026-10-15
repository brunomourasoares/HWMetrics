using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using RMMonitor.Models;

namespace RMMonitor.Services;

/// <summary>
/// Queues telemetry history persistence away from the UI thread.
/// Pending writes are coalesced so slow disk I/O cannot build an unbounded backlog.
/// </summary>
public sealed class TelemetryHistoryWriter : IAsyncDisposable
{
    private readonly TelemetryHistoryStore _store;
    private readonly Channel<WriteRequest> _requests;
    private readonly Task _worker;
    private int _disposed;

    public TelemetryHistoryWriter(TelemetryHistoryStore? store = null)
    {
        _store = store ?? new TelemetryHistoryStore();
        _requests = Channel.CreateBounded<WriteRequest>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
        _worker = Task.Run(ProcessQueueAsync);
    }

    /// <summary>
    /// Captures the current history and schedules persistence without blocking the caller.
    /// </summary>
    public void Enqueue(IEnumerable<SensorSnapshot> snapshots, DateTime now, TimeSpan? retention = null)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        ArgumentNullException.ThrowIfNull(snapshots);

        var snapshotCopy = snapshots.ToArray();
        _requests.Writer.TryWrite(new WriteRequest(snapshotCopy, now, retention));
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            await _worker.ConfigureAwait(false);
            return;
        }

        _requests.Writer.TryComplete();
        await _worker.ConfigureAwait(false);
    }

    private async Task ProcessQueueAsync()
    {
        await foreach (var request in _requests.Reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                _store.Save(request.Snapshots, request.Now, request.Retention);
            }
            catch (Exception ex)
            {
                // Keep the worker alive so later snapshots can be persisted.
                System.Diagnostics.Debug.WriteLine($"[TelemetryHistoryWriter] Failed to persist telemetry history: {ex}");
            }
        }
    }

    private sealed record WriteRequest(
        IReadOnlyList<SensorSnapshot> Snapshots,
        DateTime Now,
        TimeSpan? Retention);
}
