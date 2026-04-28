using SerialMitmProxy.Application.Intercept;
using SerialMitmProxy.Core.Intercept;
using SerialMitmProxy.Core.Models;

namespace SerialMitmProxy.Core.Tests.Proxy;

public sealed class InterceptQueueViewModelTests
{
    [Fact]
    public async Task EditAndForward_UsesEditedHexPayload()
    {
        var manager = new InterceptManager();
        var viewModel = new InterceptQueueViewModel(manager);
        var request = new InterceptRequest(Frame.Create(Direction.AtoB, new byte[] { 0x01, 0x02 }, DateTimeOffset.UtcNow));
        var item = new InterceptQueueItemViewModel(request)
        {
            EditedHex = "AA BB CC DD",
        };

        viewModel.Pending.Add(item);
        viewModel.EditAndForward(item);

        var decision = await request.WaitAsync(CancellationToken.None);

        Assert.Equal(InterceptCommand.EditAndForward, decision.Command);
        Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD }, decision.EditedPayload);
        Assert.Empty(viewModel.Pending);
    }

    [Fact]
    public async Task Forward_UsesEditedPayloadWhenHexWasChanged()
    {
        var manager = new InterceptManager();
        var viewModel = new InterceptQueueViewModel(manager);
        var request = new InterceptRequest(Frame.Create(Direction.AtoB, new byte[] { 0x01, 0x02 }, DateTimeOffset.UtcNow));
        var item = new InterceptQueueItemViewModel(request)
        {
            EditedHex = "AA BB",
        };

        viewModel.Pending.Add(item);
        viewModel.Forward(item);

        var decision = await request.WaitAsync(CancellationToken.None);

        Assert.Equal(InterceptCommand.EditAndForward, decision.Command);
        Assert.Equal(new byte[] { 0xAA, 0xBB }, decision.EditedPayload);
        Assert.Empty(viewModel.Pending);
    }

    [Fact]
    public async Task Forward_UsesOriginalForwardWhenHexIsUnchanged()
    {
        var manager = new InterceptManager();
        var viewModel = new InterceptQueueViewModel(manager);
        var request = new InterceptRequest(Frame.Create(Direction.BtoA, new byte[] { 0x01, 0x02 }, DateTimeOffset.UtcNow));
        var item = new InterceptQueueItemViewModel(request)
        {
            EditedHex = "01 02",
        };

        viewModel.Pending.Add(item);
        viewModel.Forward(item);

        var decision = await request.WaitAsync(CancellationToken.None);

        Assert.Equal(InterceptCommand.Forward, decision.Command);
        Assert.Null(decision.EditedPayload);
        Assert.Empty(viewModel.Pending);
    }
}
