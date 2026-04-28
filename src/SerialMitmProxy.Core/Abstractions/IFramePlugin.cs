using SerialMitmProxy.Core.Models;

namespace SerialMitmProxy.Core.Abstractions;

public interface IFramePlugin
{
    string Name { get; }

    byte[] Transform(Direction direction, byte[] payload);
}
