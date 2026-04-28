using SerialMitmProxy.Core.Models;

namespace SerialMitmProxy.Core.Intercept;

public sealed class InterceptRequest
{
    private readonly TaskCompletionSource<InterceptDecision> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public InterceptRequest(Frame frame)
    {
        Frame = frame;
    }

    public Frame Frame { get; }

    public bool IsCompleted => _completion.Task.IsCompleted;

    public Task<InterceptDecision> WaitAsync(CancellationToken cancellationToken)
    {
        return _completion.Task.WaitAsync(cancellationToken);
    }

    public bool Resolve(InterceptDecision decision)
    {
        return _completion.TrySetResult(decision);
    }

    public bool Reject(Exception ex)
    {
        return _completion.TrySetException(ex);
    }
}
