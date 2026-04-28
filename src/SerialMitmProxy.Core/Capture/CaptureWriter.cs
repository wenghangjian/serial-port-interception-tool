using SerialMitmProxy.Core.Models;

namespace SerialMitmProxy.Core.Capture;

public sealed class CaptureWriter : IAsyncDisposable
{
    private readonly FileStream _binStream;
    private readonly FileStream _idxStream;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public CaptureWriter(string binPath, string idxPath)
    {
        BinPath = binPath;
        IdxPath = idxPath;

        Directory.CreateDirectory(Path.GetDirectoryName(binPath) ?? ".");
        Directory.CreateDirectory(Path.GetDirectoryName(idxPath) ?? ".");

        _binStream = new FileStream(binPath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
        _idxStream = new FileStream(idxPath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
    }

    public string BinPath { get; }

    public string IdxPath { get; }

    public static CaptureWriter Create(string folderPath)
    {
        Directory.CreateDirectory(folderPath);
        return new CaptureWriter(Path.Combine(folderPath, "capture.bin"), Path.Combine(folderPath, "capture.idx"));
    }

    public async ValueTask AppendAsync(Frame frame, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var offset = _binStream.Position;
            await _binStream.WriteAsync(frame.Payload, cancellationToken).ConfigureAwait(false);

            var entry = new CaptureEntry(frame.TimestampUtc, frame.Direction, offset, frame.Payload.Length);
            var buffer = new byte[24];
            Buffer.BlockCopy(BitConverter.GetBytes(entry.TimestampUtc.UtcTicks), 0, buffer, 0, 8);
            Buffer.BlockCopy(BitConverter.GetBytes((int)entry.Direction), 0, buffer, 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(entry.Offset), 0, buffer, 12, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(entry.Length), 0, buffer, 20, 4);
            await _idxStream.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _binStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            await _idxStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _binStream.DisposeAsync().ConfigureAwait(false);
        await _idxStream.DisposeAsync().ConfigureAwait(false);
        _gate.Dispose();
    }
}
