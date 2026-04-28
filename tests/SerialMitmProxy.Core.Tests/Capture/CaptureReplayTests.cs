using SerialMitmProxy.Core.Capture;
using SerialMitmProxy.Core.Models;
using SerialMitmProxy.Application.Replay;

namespace SerialMitmProxy.Core.Tests.Capture;

public sealed class CaptureReplayTests
{
    [Fact]
    public async Task CaptureRoundtrip_MatchesOriginalBytes()
    {
        var folder = Path.Combine(Path.GetTempPath(), "serial-mitm-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        await using var writer = CaptureWriter.Create(folder);
        var frameA = Frame.Create(Direction.AtoB, new byte[] { 0x01, 0x02, 0x03 }, DateTimeOffset.UtcNow);
        var frameB = Frame.Create(Direction.BtoA, new byte[] { 0x10, 0x11 }, DateTimeOffset.UtcNow.AddMilliseconds(25));

        await writer.AppendAsync(frameA, CancellationToken.None);
        await writer.AppendAsync(frameB, CancellationToken.None);
        await writer.FlushAsync(CancellationToken.None);

        var frames = await CaptureReader.ReadAllAsync(
            Path.Combine(folder, "capture.bin"),
            Path.Combine(folder, "capture.idx"),
            CancellationToken.None);

        Assert.Equal(2, frames.Count);
        Assert.Equal(frameA.Payload, frames[0].Payload);
        Assert.Equal(frameB.Payload, frames[1].Payload);
        Assert.Equal(frameA.Direction, frames[0].Entry.Direction);
        Assert.Equal(frameB.Direction, frames[1].Entry.Direction);
    }

    [Fact]
    public void ReplayPlayer_SupportsSingleStep()
    {
        var now = DateTimeOffset.UtcNow;
        var entries = new List<(CaptureEntry Entry, byte[] Payload)>
        {
            (new CaptureEntry(now, Direction.AtoB, 0, 2), new byte[] { 0xAA, 0xBB }),
            (new CaptureEntry(now.AddMilliseconds(10), Direction.BtoA, 2, 1), new byte[] { 0xCC }),
        };

        var player = new ReplayPlayer(entries);

        Assert.True(player.TryStep(out var first));
        Assert.NotNull(first);
        Assert.Equal(new byte[] { 0xAA, 0xBB }, first!.Payload);

        Assert.True(player.TryStep(out var second));
        Assert.NotNull(second);
        Assert.Equal(new byte[] { 0xCC }, second!.Payload);

        Assert.False(player.TryStep(out _));
    }

    [Fact]
    public async Task ReplayController_CanSaveFramesAsReplayCapturePackage()
    {
        var folder = Path.Combine(Path.GetTempPath(), "serial-mitm-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        var frames = new[]
        {
            Frame.Create(Direction.AtoB, new byte[] { 0x01, 0x02 }, DateTimeOffset.UtcNow),
            Frame.Create(Direction.BtoA, new byte[] { 0x03, 0x04, 0x05 }, DateTimeOffset.UtcNow.AddMilliseconds(5)),
        };

        var controller = new ReplayControllerViewModel
        {
            CaptureFolder = folder,
        };

        var count = await controller.SaveCaptureAsync(frames, CancellationToken.None);
        var loaded = await CaptureReader.ReadAllAsync(
            Path.Combine(folder, "capture.bin"),
            Path.Combine(folder, "capture.idx"),
            CancellationToken.None);

        Assert.Equal(2, count);
        Assert.Equal(2, loaded.Count);
        Assert.Equal(frames[0].Payload, loaded[0].Payload);
        Assert.Equal(frames[1].Payload, loaded[1].Payload);
        Assert.Equal(frames[0].Direction, loaded[0].Entry.Direction);
        Assert.Equal(frames[1].Direction, loaded[1].Entry.Direction);
    }
}
