using System.IO;
using System.IO.Ports;
using System.Threading.Channels;
using SerialMitmProxy.Core.Abstractions;

namespace SerialMitmProxy.Infrastructure.Serial;

public sealed class SerialPortEndpoint : IProxyEndpoint
{
    private static readonly TimeSpan WorkerShutdownTimeout = TimeSpan.FromSeconds(2);
    private readonly SerialPortOptions _options;
    private readonly Channel<ReadOnlyMemory<byte>> _readChannel;
    private readonly Channel<ReadOnlyMemory<byte>> _writeChannel;
    private readonly List<Task> _workers = new();
    private SerialPort? _serialPort;
    private CancellationTokenSource? _lifetimeCts;

    public SerialPortEndpoint(string name, SerialPortOptions options)
    {
        Name = name;
        _options = options;
        _readChannel = Channel.CreateUnbounded<ReadOnlyMemory<byte>>(
            new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = true,
                AllowSynchronousContinuations = false,
            });
        _writeChannel = Channel.CreateUnbounded<ReadOnlyMemory<byte>>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = false,
            });
    }

    public string Name { get; }

    public Task OpenAsync(CancellationToken cancellationToken)
    {
        if (_serialPort is not null)
        {
            return Task.CompletedTask;
        }

        var availablePorts = SerialPort.GetPortNames();
        if (!availablePorts.Contains(_options.PortName, StringComparer.OrdinalIgnoreCase))
        {
            var knownPorts = availablePorts.Length == 0 ? "<none>" : string.Join(", ", availablePorts.OrderBy(static p => p));
            throw new InvalidOperationException($"Serial port '{_options.PortName}' was not found. Available ports: {knownPorts}.");
        }

        _lifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _serialPort = new SerialPort(_options.PortName, _options.BaudRate, _options.Parity, _options.DataBits, _options.StopBits)
        {
            Handshake = _options.Handshake,
            ReadTimeout = Timeout.Infinite,
            WriteTimeout = Timeout.Infinite,
            ReadBufferSize = _options.ReadBufferSize,
            WriteBufferSize = _options.ReadBufferSize,
        };

        _serialPort.Open();

        _workers.Add(Task.Run(() => ReadLoopAsync(_lifetimeCts.Token), _lifetimeCts.Token));
        _workers.Add(Task.Run(() => WriteLoopAsync(_lifetimeCts.Token), _lifetimeCts.Token));

        return Task.CompletedTask;
    }

    public IAsyncEnumerable<ReadOnlyMemory<byte>> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _readChannel.Reader.ReadAllAsync(cancellationToken);
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        return _writeChannel.Writer.WriteAsync(payload.ToArray(), cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        _lifetimeCts?.Cancel();
        _writeChannel.Writer.TryComplete();
        TryClosePort();

        try
        {
            if (_workers.Count > 0)
            {
                var allWorkers = Task.WhenAll(_workers);
                var completed = await Task.WhenAny(allWorkers, Task.Delay(WorkerShutdownTimeout)).ConfigureAwait(false);
                if (completed == allWorkers)
                {
                    await allWorkers.ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (IOException)
        {
        }
        finally
        {
            _workers.Clear();
        }

        _serialPort?.Dispose();
        _serialPort = null;

        _lifetimeCts?.Dispose();
        _lifetimeCts = null;
        _readChannel.Writer.TryComplete();
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            var port = _serialPort ?? throw new InvalidOperationException("Serial port is not opened.");
            var stream = port.BaseStream;
            var buffer = new byte[_options.ReadBufferSize];

            while (!cancellationToken.IsCancellationRequested)
            {
                var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                if (read <= 0)
                {
                    continue;
                }

                var chunk = buffer.AsMemory(0, read).ToArray();
                await _readChannel.Writer.WriteAsync(chunk, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _readChannel.Writer.TryComplete();
                return;
            }

            _readChannel.Writer.TryComplete(ex);
            return;
        }

        _readChannel.Writer.TryComplete();
    }

    private async Task WriteLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            var port = _serialPort ?? throw new InvalidOperationException("Serial port is not opened.");
            var stream = port.BaseStream;

            await foreach (var payload in _writeChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (IOException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void TryClosePort()
    {
        try
        {
            if (_serialPort is { IsOpen: true })
            {
                _serialPort.Close();
            }
        }
        catch
        {
        }
    }
}
