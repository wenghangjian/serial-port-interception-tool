using SerialMitmProxy.Core.Abstractions;
using SerialMitmProxy.Core.Models;

namespace SerialMitmProxy.Plugins;

public sealed class XorMaskPlugin : IFramePlugin
{
    public byte Mask { get; init; }

    public string Name => "XorMaskPlugin";

    public byte[] Transform(Direction direction, byte[] payload)
    {
        if (Mask == 0)
        {
            return payload.ToArray();
        }

        var output = payload.ToArray();
        for (var i = 0; i < output.Length; i++)
        {
            output[i] ^= Mask;
        }

        return output;
    }
}
