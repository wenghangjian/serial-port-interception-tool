using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SerialMitmProxy.Infrastructure.Serial;

namespace SerialMitmProxy.App;

public sealed class AppConfig
{
    public SessionConfig Session { get; init; } = new();

    public CaptureConfig Capture { get; init; } = new();

    public MonitorConfig Monitor { get; init; } = new();

    public PluginConfig Plugins { get; init; } = new();
}

public sealed class SessionConfig
{
    public bool UseInMemory { get; init; }

    public SerialEndpointConfig EndpointA { get; init; } = new();

    public SerialEndpointConfig EndpointB { get; init; } = new();

    public DecoderConfig Decoders { get; init; } = new();
}

public sealed class SerialEndpointConfig
{
    public string PortName { get; init; } = "COM1";

    public int BaudRate { get; init; } = 115200;

    public int DataBits { get; init; } = 8;

    public string Parity { get; init; } = "None";

    public string StopBits { get; init; } = "One";

    public string Handshake { get; init; } = "None";

    public SerialPortOptions ToOptions()
    {
        return new SerialPortOptions
        {
            PortName = PortName,
            BaudRate = BaudRate,
            DataBits = DataBits,
            Parity = Enum.Parse<System.IO.Ports.Parity>(Parity, ignoreCase: true),
            StopBits = Enum.Parse<System.IO.Ports.StopBits>(StopBits, ignoreCase: true),
            Handshake = Enum.Parse<System.IO.Ports.Handshake>(Handshake, ignoreCase: true),
        };
    }
}

public sealed class DecoderConfig
{
    public string Mode { get; init; } = "TimeSlice";

    public int TimeSliceMs { get; init; } = 40;

    public string DelimiterHex { get; init; } = "0D 0A";

    public int FixedLength { get; init; } = 16;
}

public sealed class CaptureConfig
{
    public bool Enabled { get; init; } = true;

    public string Folder { get; init; } = "captures";
}

public sealed class MonitorConfig
{
    public int MaxFrames { get; init; } = 2000;

    public int UiThrottleMs { get; init; } = 50;
}

public sealed class PluginConfig
{
    public string Folder { get; init; } = "plugins";
}

public static class AppConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AppConfig Load(string baseDirectory)
    {
        var configPath = Path.Combine(baseDirectory, "serialmitmproxy.json");
        var templatePath = Path.Combine(baseDirectory, "serialmitmproxy.template.json");

        if (File.Exists(configPath))
        {
            return Deserialize(configPath);
        }

        if (File.Exists(templatePath))
        {
            return Deserialize(templatePath);
        }

        return new AppConfig();
    }

    private static AppConfig Deserialize(string path)
    {
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<AppConfig>(stream, JsonOptions) ?? new AppConfig();
    }
}
