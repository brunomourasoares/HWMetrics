namespace WaterCoolerApp.Services;

/// <summary>
/// Thread-safe, bounded rate limiter for repeated diagnostic messages.
/// </summary>
internal sealed class DiagnosticMessageThrottle
{
    private readonly object _sync = new();
    private readonly Dictionary<string, DateTime> _lastWrites = new(StringComparer.Ordinal);
    private readonly TimeSpan _repeatInterval;
    private readonly int _maxTrackedMessages;

    public DiagnosticMessageThrottle(TimeSpan repeatInterval, int maxTrackedMessages)
    {
        if (repeatInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(repeatInterval));
        if (maxTrackedMessages < 1)
            throw new ArgumentOutOfRangeException(nameof(maxTrackedMessages));

        _repeatInterval = repeatInterval;
        _maxTrackedMessages = maxTrackedMessages;
    }

    public bool ShouldWrite(string message, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(message);

        lock (_sync)
        {
            if (_lastWrites.TryGetValue(message, out var lastWrite) &&
                nowUtc >= lastWrite &&
                nowUtc - lastWrite < _repeatInterval)
            {
                return false;
            }

            // Keep memory bounded even when exception messages vary on every poll.
            if (!_lastWrites.ContainsKey(message) && _lastWrites.Count >= _maxTrackedMessages)
                _lastWrites.Clear();

            _lastWrites[message] = nowUtc;
            return true;
        }
    }
}
