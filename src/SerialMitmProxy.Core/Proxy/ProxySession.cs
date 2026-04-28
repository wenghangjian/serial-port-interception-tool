using SerialMitmProxy.Core.Abstractions;
using SerialMitmProxy.Core.Capture;
using SerialMitmProxy.Core.Intercept;
using SerialMitmProxy.Core.Models;
using SerialMitmProxy.Core.Rules;

namespace SerialMitmProxy.Core.Proxy;

public sealed class ProxySession : IAsyncDisposable
{
    private static readonly TimeSpan StopWorkerTimeout = TimeSpan.FromSeconds(2);
    private readonly IProxyEndpoint _endpointA;
    private readonly IProxyEndpoint _endpointB;
    private readonly IFrameDecoder _decoderAtoB;
    private readonly IFrameDecoder _decoderBtoA;
    private readonly RuleEngine _ruleEngine;
    private readonly InterceptManager? _interceptManager;
    private readonly CaptureWriter? _captureWriter;
    private readonly IReadOnlyList<IFramePlugin> _plugins;
    private readonly List<Task> _workers = new();
    private CancellationTokenSource? _lifetimeCts;
    private bool _endpointsDisposed;

    public ProxySession(
        IProxyEndpoint endpointA,
        IProxyEndpoint endpointB,
        IFrameDecoder decoderAtoB,
        IFrameDecoder decoderBtoA,
        RuleEngine ruleEngine,
        InterceptManager? interceptManager = null,
        CaptureWriter? captureWriter = null,
        IEnumerable<IFramePlugin>? plugins = null)
    {
        _endpointA = endpointA;
        _endpointB = endpointB;
        _decoderAtoB = decoderAtoB;
        _decoderBtoA = decoderBtoA;
        _ruleEngine = ruleEngine;
        _interceptManager = interceptManager;
        _captureWriter = captureWriter;
        _plugins = plugins?.ToArray() ?? Array.Empty<IFramePlugin>();
    }

    public event EventHandler<RawBytesEventArgs>? RawBytesObserved;

    public event EventHandler<FrameEventArgs>? FrameObserved;

    public event EventHandler<FrameEventArgs>? FrameForwarded;

    public event EventHandler<ProxyErrorEventArgs>? Error;

    public bool IsRunning => _lifetimeCts is { IsCancellationRequested: false };

    public void ReplaceRules(IEnumerable<Rule> rules)
    {
        _ruleEngine.ReplaceAll(rules);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_lifetimeCts is not null)
        {
            throw new InvalidOperationException("Proxy session is already running.");
        }

        _endpointsDisposed = false;
        _lifetimeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        await _endpointA.OpenAsync(_lifetimeCts.Token).ConfigureAwait(false);
        await _endpointB.OpenAsync(_lifetimeCts.Token).ConfigureAwait(false);

        _workers.Add(Task.Run(() => RunDirectionAsync(_endpointA, _endpointB, Direction.AtoB, _decoderAtoB, _lifetimeCts.Token), _lifetimeCts.Token));
        _workers.Add(Task.Run(() => RunDirectionAsync(_endpointB, _endpointA, Direction.BtoA, _decoderBtoA, _lifetimeCts.Token), _lifetimeCts.Token));
    }

    public async Task StopAsync()
    {
        if (_lifetimeCts is null)
        {
            return;
        }

        var cts = _lifetimeCts;
        _lifetimeCts = null;
        cts.Cancel();

        try
        {
            await DisposeEndpointsAsync().ConfigureAwait(false);

            if (_workers.Count > 0)
            {
                var allWorkers = Task.WhenAll(_workers);
                var completed = await Task.WhenAny(allWorkers, Task.Delay(StopWorkerTimeout)).ConfigureAwait(false);
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
        finally
        {
            _workers.Clear();
            cts.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        await DisposeEndpointsAsync().ConfigureAwait(false);

        if (_captureWriter is not null)
        {
            await _captureWriter.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task DisposeEndpointsAsync()
    {
        if (_endpointsDisposed)
        {
            return;
        }

        _endpointsDisposed = true;
        await SafeDisposeAsync(_endpointA).ConfigureAwait(false);
        await SafeDisposeAsync(_endpointB).ConfigureAwait(false);
    }

    private static async Task SafeDisposeAsync(IProxyEndpoint endpoint)
    {
        try
        {
            await endpoint.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private async Task RunDirectionAsync(
        IProxyEndpoint source,
        IProxyEndpoint destination,
        Direction direction,
        IFrameDecoder decoder,
        CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var chunk in source.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                var timestamp = DateTimeOffset.UtcNow;
                var rawPayload = chunk.ToArray();
                RawBytesObserved?.Invoke(this, new RawBytesEventArgs(direction, rawPayload, timestamp));

                var decodedFrames = decoder.Decode(rawPayload, timestamp);
                foreach (var decoded in decodedFrames)
                {
                    var frame = Frame.Create(direction, decoded.Payload, decoded.TimestampUtc);
                    await ProcessFrameAsync(frame, destination, cancellationToken).ConfigureAwait(false);
                }
            }

            foreach (var decoded in decoder.Flush(DateTimeOffset.UtcNow))
            {
                var frame = Frame.Create(direction, decoded.Payload, decoded.TimestampUtc);
                await ProcessFrameAsync(frame, destination, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Error?.Invoke(this, new ProxyErrorEventArgs(ex));
        }
    }

    private async Task ProcessFrameAsync(Frame frame, IProxyEndpoint destination, CancellationToken cancellationToken)
    {
        FrameObserved?.Invoke(this, new FrameEventArgs(frame));

        var evaluation = _ruleEngine.Evaluate(frame.Direction, frame.Payload);
        var effectivePayload = evaluation.Payload;

        foreach (var plugin in _plugins)
        {
            effectivePayload = plugin.Transform(frame.Direction, effectivePayload);
        }

        if (evaluation.Intercept && _interceptManager is not null)
        {
            var decision = await _interceptManager.RequestDecisionAsync(frame, cancellationToken).ConfigureAwait(false);
            ApplyInterceptDecision(evaluation, decision, ref effectivePayload);
        }

        if (evaluation.Delay > TimeSpan.Zero)
        {
            await Task.Delay(evaluation.Delay, cancellationToken).ConfigureAwait(false);
        }

        if (!evaluation.Drop)
        {
            var isModified = !effectivePayload.AsSpan().SequenceEqual(frame.Payload);
            for (var i = 0; i < evaluation.DuplicateCount; i++)
            {
                var forwarded = Frame.Create(
                    frame.Direction,
                    effectivePayload,
                    DateTimeOffset.UtcNow,
                    frame.IsSynthetic,
                    isModified);
                await destination.WriteAsync(forwarded.Payload, cancellationToken).ConfigureAwait(false);
                if (_captureWriter is not null)
                {
                    await _captureWriter.AppendAsync(forwarded, cancellationToken).ConfigureAwait(false);
                }

                FrameForwarded?.Invoke(this, new FrameEventArgs(forwarded));
            }
        }

        foreach (var injection in evaluation.Injections)
        {
            var injectedFrame = Frame.Create(frame.Direction, injection, DateTimeOffset.UtcNow, isSynthetic: true);
            await destination.WriteAsync(injectedFrame.Payload, cancellationToken).ConfigureAwait(false);
            if (_captureWriter is not null)
            {
                await _captureWriter.AppendAsync(injectedFrame, cancellationToken).ConfigureAwait(false);
            }

            FrameForwarded?.Invoke(this, new FrameEventArgs(injectedFrame));
        }
    }

    private static void ApplyInterceptDecision(RuleEvaluationResult evaluation, InterceptDecision decision, ref byte[] effectivePayload)
    {
        switch (decision.Command)
        {
            case InterceptCommand.Forward:
                return;
            case InterceptCommand.Drop:
                evaluation.Drop = true;
                return;
            case InterceptCommand.EditAndForward:
                if (decision.EditedPayload is not null)
                {
                    effectivePayload = decision.EditedPayload.ToArray();
                }

                return;
            case InterceptCommand.Repeat:
                evaluation.DuplicateCount = Math.Max(evaluation.DuplicateCount, Math.Max(decision.RepeatCount, 2));
                return;
            case InterceptCommand.Inject:
                if (decision.InjectPayload is not null && decision.InjectPayload.Length > 0)
                {
                    evaluation.Injections.Add(decision.InjectPayload.ToArray());
                }

                return;
            default:
                throw new InvalidOperationException($"Unsupported intercept command: {decision.Command}");
        }
    }
}
