using SerialMitmProxy.Application.Monitoring;
using SerialMitmProxy.Application.Session;

namespace SerialMitmProxy.Core.Tests.Proxy;

public sealed class SessionManagerViewModelTests
{
    [Fact]
    public async Task StartAsync_ReturnsFalseWhenSessionFactoryThrows()
    {
        using var monitor = new LiveMonitorViewModel();
        var vm = new SessionManagerViewModel(
            () => throw new InvalidOperationException("bad config"),
            monitor);

        var started = await vm.StartAsync();

        Assert.False(started);
        Assert.False(vm.IsRunning);
        Assert.Contains("StatusStartFailed", vm.Status);
    }
}
