namespace SerialMitmProxy.Core.Abstractions;

public interface IProxyEndpoint : IAsyncDisposable
{
    string Name { get; }

    Task OpenAsync(CancellationToken cancellationToken);

    IAsyncEnumerable<ReadOnlyMemory<byte>> ReadAllAsync(CancellationToken cancellationToken);

    ValueTask WriteAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);
}
