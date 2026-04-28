using SerialMitmProxy.Application.Common;
using SerialMitmProxy.Core.Capture;
using SerialMitmProxy.Core.Models;

namespace SerialMitmProxy.Application.Replay;

public sealed class ReplayControllerViewModel : ViewModelBase
{
    private ReplayPlayer? _player;
    private string _captureFolder = ".";
    private double _speedFactor = 1.0;

    public string CaptureFolder
    {
        get => _captureFolder;
        set => SetProperty(ref _captureFolder, value);
    }

    public double SpeedFactor
    {
        get => _speedFactor;
        set => SetProperty(ref _speedFactor, value);
    }

    public async Task<int> LoadAsync(CancellationToken cancellationToken = default)
    {
        var binPath = Path.Combine(CaptureFolder, "capture.bin");
        var idxPath = Path.Combine(CaptureFolder, "capture.idx");
        var frames = await CaptureReader.ReadAllAsync(binPath, idxPath, cancellationToken).ConfigureAwait(false);
        _player = new ReplayPlayer(frames);
        return frames.Count;
    }

    public async Task<int> SaveCaptureAsync(IEnumerable<Frame> frames, CancellationToken cancellationToken = default)
    {
        var snapshot = frames.ToArray();
        await using var writer = CaptureWriter.Create(CaptureFolder);
        foreach (var frame in snapshot)
        {
            await writer.AppendAsync(frame, cancellationToken).ConfigureAwait(false);
        }

        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        return snapshot.Length;
    }

    public async Task ReplayAsync(Func<Frame, Task> onFrame, CancellationToken cancellationToken = default)
    {
        if (_player is null)
        {
            return;
        }

        await foreach (var frame in _player.ReplayAsync(SpeedFactor, cancellationToken).ConfigureAwait(false))
        {
            await onFrame(frame).ConfigureAwait(false);
        }
    }

    public bool TryStep(out Frame? frame)
    {
        if (_player is null)
        {
            frame = null;
            return false;
        }

        return _player.TryStep(out frame);
    }
}
