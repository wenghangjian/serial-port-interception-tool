using SerialMitmProxy.Core.Decoders;
using SerialMitmProxy.Core.Intercept;
using SerialMitmProxy.Core.Models;
using SerialMitmProxy.Core.Proxy;
using SerialMitmProxy.Core.Rules;
using SerialMitmProxy.Infrastructure.Memory;

namespace SerialMitmProxy.Core.Tests.Proxy;

public sealed class ProxySessionTests
{
    [Fact]
    public async Task ProxySession_ForwardsBidirectionally()
    {
        await using var endpointA = new InMemoryProxyEndpoint("A");
        await using var endpointB = new InMemoryProxyEndpoint("B");

        await using var session = new ProxySession(
            endpointA,
            endpointB,
            new FixedLengthDecoder(4),
            new FixedLengthDecoder(4),
            new RuleEngine());

        await session.StartAsync();

        await endpointA.InjectIncomingAsync(new byte[] { 0x01, 0x02, 0x03, 0x04 });
        var bWritten = await endpointB.WaitForWriteAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 }, bWritten);

        await endpointB.InjectIncomingAsync(new byte[] { 0x05, 0x06, 0x07, 0x08 });
        var aWritten = await endpointA.WaitForWriteAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(new byte[] { 0x05, 0x06, 0x07, 0x08 }, aWritten);

        await session.StopAsync();
    }

    [Fact]
    public async Task ProxySession_RaisesFrameForwarded_WhenTrafficForwards()
    {
        await using var endpointA = new InMemoryProxyEndpoint("A");
        await using var endpointB = new InMemoryProxyEndpoint("B");

        await using var session = new ProxySession(
            endpointA,
            endpointB,
            new FixedLengthDecoder(4),
            new FixedLengthDecoder(4),
            new RuleEngine());

        FrameEventArgs? forwarded = null;
        session.FrameForwarded += (_, args) => forwarded = args;

        await session.StartAsync();

        await endpointA.InjectIncomingAsync(new byte[] { 0x01, 0x02, 0x03, 0x04 });
        await endpointB.WaitForWriteAsync(TimeSpan.FromSeconds(1));

        var deadline = DateTime.UtcNow.AddSeconds(1);
        while (forwarded is null && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.NotNull(forwarded);
        Assert.Equal(Direction.AtoB, forwarded!.Frame.Direction);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 }, forwarded.Frame.Payload);
        Assert.False(forwarded.Frame.IsModified);

        await session.StopAsync();
    }

    [Fact]
    public async Task ProxySession_InterceptWaitsForDecision()
    {
        await using var endpointA = new InMemoryProxyEndpoint("A");
        await using var endpointB = new InMemoryProxyEndpoint("B");

        var interceptManager = new InterceptManager();
        interceptManager.SetEnabled(true);
        var rules = new[]
        {
            new Rule
            {
                Name = "intercept",
                Matchers = new IRuleMatcher[] { new DirectionMatcher(Direction.AtoB) },
                Actions = new[] { new RuleAction { Type = RuleActionType.Intercept } },
            },
        };

        await using var session = new ProxySession(
            endpointA,
            endpointB,
            new FixedLengthDecoder(4),
            new FixedLengthDecoder(4),
            new RuleEngine(rules),
            interceptManager);

        FrameEventArgs? forwarded = null;
        session.FrameForwarded += (_, args) => forwarded = args;

        await session.StartAsync();

        await endpointA.InjectIncomingAsync(new byte[] { 0x01, 0x02, 0x03, 0x04 });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await foreach (var pending in interceptManager.ReadPendingAsync(cts.Token))
        {
            pending.Resolve(new InterceptDecision
            {
                Command = InterceptCommand.EditAndForward,
                EditedPayload = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD },
            });
            break;
        }

        var written = await endpointB.WaitForWriteAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD }, written);

        var deadline = DateTime.UtcNow.AddSeconds(1);
        while (forwarded is null && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.NotNull(forwarded);
        Assert.Equal(new byte[] { 0xAA, 0xBB, 0xCC, 0xDD }, forwarded!.Frame.Payload);
        Assert.True(forwarded.Frame.IsModified);

        await session.StopAsync();
    }

    [Fact]
    public async Task ProxySession_RemainsTransparentUntilInterceptionIsEnabled()
    {
        await using var endpointA = new InMemoryProxyEndpoint("A");
        await using var endpointB = new InMemoryProxyEndpoint("B");

        var interceptManager = new InterceptManager();
        var rules = new[]
        {
            new Rule
            {
                Name = "intercept",
                Matchers = new IRuleMatcher[] { new DirectionMatcher(Direction.AtoB) },
                Actions = new[] { new RuleAction { Type = RuleActionType.Intercept } },
            },
        };

        await using var session = new ProxySession(
            endpointA,
            endpointB,
            new FixedLengthDecoder(4),
            new FixedLengthDecoder(4),
            new RuleEngine(rules),
            interceptManager);

        await session.StartAsync();

        await endpointA.InjectIncomingAsync(new byte[] { 0x01, 0x02, 0x03, 0x04 });

        var written = await endpointB.WaitForWriteAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 }, written);
        Assert.False(interceptManager.TryDequeue(out _));

        await session.StopAsync();
    }

    [Fact]
    public async Task ProxySession_RaisesRawBytesObserved_WhenTrafficArrives()
    {
        await using var endpointA = new InMemoryProxyEndpoint("A");
        await using var endpointB = new InMemoryProxyEndpoint("B");

        await using var session = new ProxySession(
            endpointA,
            endpointB,
            new FixedLengthDecoder(4),
            new FixedLengthDecoder(4),
            new RuleEngine());

        RawBytesEventArgs? observed = null;
        session.RawBytesObserved += (_, args) => observed = args;

        await session.StartAsync();

        await endpointA.InjectIncomingAsync(new byte[] { 0x01, 0x02, 0x03, 0x04 });
        await endpointB.WaitForWriteAsync(TimeSpan.FromSeconds(1));

        var deadline = DateTime.UtcNow.AddSeconds(1);
        while (observed is null && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.NotNull(observed);
        Assert.Equal(Direction.AtoB, observed!.Direction);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x04 }, observed.Payload);

        await session.StopAsync();
    }
}
