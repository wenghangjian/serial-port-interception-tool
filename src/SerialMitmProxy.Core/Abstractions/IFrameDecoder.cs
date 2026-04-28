using SerialMitmProxy.Core.Models;

namespace SerialMitmProxy.Core.Abstractions;

public interface IFrameDecoder
{
    IReadOnlyList<DecodedFrame> Decode(ReadOnlyMemory<byte> chunk, DateTimeOffset timestampUtc);

    IReadOnlyList<DecodedFrame> Flush(DateTimeOffset timestampUtc);
}
