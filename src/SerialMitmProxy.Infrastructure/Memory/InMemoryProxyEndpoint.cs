using System.Threading.Channels;
using SerialMitmProxy.Core.Abstractions;

namespace SerialMitmProxy.Infrastructure.Memory;

public sealed class InMemoryProxyEndpoint : IProxyEndpoint
{
    private readonly Channel<ReadOnlyMemory<byte>> _incoming;
    private readonly Channel<ReadOnlyMemory<byte>> _written;
    private bool _opened;

    public InMemoryProxyEndpoint(string name)
    {
        Name = name;
        _incoming = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        _written = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
    }

    public string Name { get; }

    public Task OpenAsync(CancellationToken cancellationToken)
    {
        _opened = true;
        return Task.CompletedTask;
    }

    public IAsyncEnumerable<ReadOnlyMemory<byte>> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _incoming.Reader.ReadAllAsync(cancellationToken);
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        EnsureOpened();
        return _written.Writer.WriteAsync(payload.ToArray(), cancellationToken);
    }

    public ValueTask InjectIncomingAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        EnsureOpened();
        return _incoming.Writer.WriteAsync(payload.ToArray(), cancellationToken);
    }

    public Task<byte[]> WaitForWriteAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        return WaitForWriteCoreAsync(timeout, cancellationToken);
    }

    public bool TryReadWrite(out byte[] payload)
    {
        if (_written.Reader.TryRead(out var data))
        {
            payload = data.ToArray();
            return true;
        }

        payload = Array.Empty<byte>();
        return false;
    }

    public ValueTask DisposeAsync()
    {
        _incoming.Writer.TryComplete();
        _written.Writer.TryComplete();
        _opened = false;
        return ValueTask.CompletedTask;
    }

    private async Task<byte[]> WaitForWriteCoreAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

        try
        {
            var payload = await _written.Reader.ReadAsync(linked.Token).ConfigureAwait(false);
            return payload.ToArray();
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"No payload written to endpoint '{Name}' within {timeout}.");
        }
    }

    private void EnsureOpened()
    {
        if (!_opened)
        {
            throw new InvalidOperationException($"Endpoint '{Name}' is not opened.");
        }
    }
}
