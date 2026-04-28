using SerialMitmProxy.Core.Models;

namespace SerialMitmProxy.Core.Capture;

public static class CaptureReader
{
    private const int EntrySize = 24;

    public static async Task<IReadOnlyList<CaptureEntry>> ReadIndexAsync(string idxPath, CancellationToken cancellationToken = default)
    {
        var entries = new List<CaptureEntry>();
        await using var stream = new FileStream(idxPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, useAsync: true);

        var buffer = new byte[EntrySize];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (read != EntrySize)
            {
                throw new InvalidDataException("capture.idx is corrupted.");
            }

            var ticks = BitConverter.ToInt64(buffer, 0);
            var direction = (Direction)BitConverter.ToInt32(buffer, 8);
            var offset = BitConverter.ToInt64(buffer, 12);
            var length = BitConverter.ToInt32(buffer, 20);
            entries.Add(new CaptureEntry(new DateTimeOffset(ticks, TimeSpan.Zero), direction, offset, length));
        }

        return entries;
    }

    public static async Task<byte[]> ReadPayloadAsync(string binPath, CaptureEntry entry, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(binPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, useAsync: true);
        stream.Seek(entry.Offset, SeekOrigin.Begin);

        var payload = new byte[entry.Length];
        var read = 0;
        while (read < entry.Length)
        {
            var count = await stream.ReadAsync(payload.AsMemory(read), cancellationToken).ConfigureAwait(false);
            if (count == 0)
            {
                throw new EndOfStreamException("capture.bin ended unexpectedly.");
            }

            read += count;
        }

        return payload;
    }

    public static async Task<IReadOnlyList<(CaptureEntry Entry, byte[] Payload)>> ReadAllAsync(
        string binPath,
        string idxPath,
        CancellationToken cancellationToken = default)
    {
        var entries = await ReadIndexAsync(idxPath, cancellationToken).ConfigureAwait(false);
        var frames = new List<(CaptureEntry Entry, byte[] Payload)>(entries.Count);
        foreach (var entry in entries)
        {
            var payload = await ReadPayloadAsync(binPath, entry, cancellationToken).ConfigureAwait(false);
            frames.Add((entry, payload));
        }

        return frames;
    }
}
