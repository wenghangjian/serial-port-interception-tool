namespace SerialMitmProxy.Core.Models;

public sealed class RawBytesEventArgs : EventArgs
{
    public RawBytesEventArgs(Direction direction, byte[] payload, DateTimeOffset timestampUtc)
    {
        Direction = direction;
        Payload = payload;
        TimestampUtc = timestampUtc;
    }

    public Direction Direction { get; }

    public byte[] Payload { get; }

    public DateTimeOffset TimestampUtc { get; }
}

public sealed class FrameEventArgs : EventArgs
{
    public FrameEventArgs(Frame frame)
    {
        Frame = frame;
    }

    public Frame Frame { get; }
}

public sealed class ProxyErrorEventArgs : EventArgs
{
    public ProxyErrorEventArgs(Exception exception)
    {
        Exception = exception;
    }

    public Exception Exception { get; }
}
