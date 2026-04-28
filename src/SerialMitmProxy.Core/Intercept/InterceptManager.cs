using System.Threading.Channels;
using SerialMitmProxy.Core.Models;

namespace SerialMitmProxy.Core.Intercept;

public sealed class InterceptManager
{
    private readonly object _gate = new();
    private readonly Channel<InterceptRequest> _queue = Channel.CreateUnbounded<InterceptRequest>(
        new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        });
    private readonly HashSet<InterceptRequest> _activeRequests = new();
    private bool _isEnabled;

    public event Action<bool>? InterceptionEnabledChanged;

    public bool IsEnabled
    {
        get
        {
            lock (_gate)
            {
                return _isEnabled;
            }
        }
    }

    public bool SetEnabled(bool enabled)
    {
        List<InterceptRequest>? requestsToRelease = null;

        lock (_gate)
        {
            if (_isEnabled == enabled)
            {
                return false;
            }

            _isEnabled = enabled;
            if (!enabled && _activeRequests.Count > 0)
            {
                requestsToRelease = _activeRequests.ToList();
            }
        }

        if (requestsToRelease is not null)
        {
            foreach (var request in requestsToRelease)
            {
                request.Resolve(new InterceptDecision { Command = InterceptCommand.Forward });
            }
        }

        InterceptionEnabledChanged?.Invoke(enabled);
        return true;
    }

    public async Task<InterceptDecision> RequestDecisionAsync(Frame frame, CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return new InterceptDecision { Command = InterceptCommand.Forward };
        }

        var request = new InterceptRequest(frame);
        lock (_gate)
        {
            if (!_isEnabled)
            {
                return new InterceptDecision { Command = InterceptCommand.Forward };
            }

            _activeRequests.Add(request);
        }

        try
        {
            await _queue.Writer.WriteAsync(request, cancellationToken).ConfigureAwait(false);
            return await request.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lock (_gate)
            {
                _activeRequests.Remove(request);
            }
        }
    }

    public IAsyncEnumerable<InterceptRequest> ReadPendingAsync(CancellationToken cancellationToken)
    {
        return _queue.Reader.ReadAllAsync(cancellationToken);
    }

    public bool TryDequeue(out InterceptRequest? request)
    {
        return _queue.Reader.TryRead(out request);
    }
}
