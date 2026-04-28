namespace SerialMitmProxy.Infrastructure.Serial;

public sealed class SerialPortOptions
{
    public string PortName { get; init; } = "COM1";

    public int BaudRate { get; init; } = 115200;

    public int DataBits { get; init; } = 8;

    public System.IO.Ports.Parity Parity { get; init; } = System.IO.Ports.Parity.None;

    public System.IO.Ports.StopBits StopBits { get; init; } = System.IO.Ports.StopBits.One;

    public System.IO.Ports.Handshake Handshake { get; init; } = System.IO.Ports.Handshake.None;

    public int ReadBufferSize { get; init; } = 4096;
}
