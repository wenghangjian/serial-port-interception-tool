using System.Runtime.CompilerServices;
using SerialMitmProxy.Core.Models;

namespace SerialMitmProxy.Core.Capture;

public sealed class ReplayPlayer
{
    private readonly IReadOnlyList<(CaptureEntry Entry, byte[] Payload)> _frames;
    private int _stepIndex;

    public ReplayPlayer(IReadOnlyList<(CaptureEntry Entry, byte[] Payload)> frames)
    {
        _frames = frames;
    }

    public async IAsyncEnumerable<Frame> ReplayAsync(
        double speedFactor = 1.0,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_frames.Count == 0)
        {
            yield break;
        }

        if (speedFactor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(speedFactor));
        }

        DateTimeOffset? previousTimestamp = null;
        foreach (var item in _frames)
        {
            if (previousTimestamp.HasValue)
            {
                var delta = item.Entry.TimestampUtc - previousTimestamp.Value;
                if (delta > TimeSpan.Zero)
                {
                    var adjusted = TimeSpan.FromTicks((long)(delta.Ticks / speedFactor));
                    if (adjusted > TimeSpan.Zero)
                    {
                        await Task.Delay(adjusted, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            previousTimestamp = item.Entry.TimestampUtc;
            yield return Frame.Create(item.Entry.Direction, item.Payload, item.Entry.TimestampUtc, isSynthetic: true);
        }
    }

    public bool TryStep(out Frame? frame)
    {
        if (_stepIndex >= _frames.Count)
        {
            frame = null;
            return false;
        }

        var current = _frames[_stepIndex++];
        frame = Frame.Create(current.Entry.Direction, current.Payload, current.Entry.TimestampUtc, isSynthetic: true);
        return true;
    }
}
