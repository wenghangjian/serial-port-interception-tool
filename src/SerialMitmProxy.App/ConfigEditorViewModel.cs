using System.Globalization;
using System.IO;
using System.Text.Json;
using SerialMitmProxy.Application.Common;

namespace SerialMitmProxy.App;

public sealed class ConfigEditorViewModel : ViewModelBase
{
    private static readonly JsonSerializerOptions SaveOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private bool _sessionUseInMemory;
    private string _endpointAPortName = "COM1";
    private string _endpointABaudRate = "115200";
    private string _endpointADataBits = "8";
    private string _endpointAParity = "None";
    private string _endpointAStopBits = "One";
    private string _endpointAHandshake = "None";

    private string _endpointBPortName = "COM2";
    private string _endpointBBaudRate = "115200";
    private string _endpointBDataBits = "8";
    private string _endpointBParity = "None";
    private string _endpointBStopBits = "One";
    private string _endpointBHandshake = "None";

    private string _decoderMode = "TimeSlice";
    private string _decoderTimeSliceMs = "40";
    private string _decoderDelimiterHex = "0D 0A";
    private string _decoderFixedLength = "16";

    private bool _captureEnabled = true;
    private string _captureFolder = "captures";
    private string _monitorMaxFrames = "2000";
    private string _monitorUiThrottleMs = "50";
    private string _pluginsFolder = "plugins";

    public IReadOnlyList<string> ParityOptions { get; } = Enum.GetNames<System.IO.Ports.Parity>();

    public IReadOnlyList<string> StopBitsOptions { get; } = Enum.GetNames<System.IO.Ports.StopBits>();

    public IReadOnlyList<string> HandshakeOptions { get; } = Enum.GetNames<System.IO.Ports.Handshake>();

    public IReadOnlyList<string> DecoderModeOptions { get; } = new[] { "TimeSlice", "Delimiter", "FixedLength" };

    public bool SessionUseInMemory
    {
        get => _sessionUseInMemory;
        set => SetProperty(ref _sessionUseInMemory, value);
    }

    public string EndpointAPortName
    {
        get => _endpointAPortName;
        set => SetProperty(ref _endpointAPortName, value);
    }

    public string EndpointABaudRate
    {
        get => _endpointABaudRate;
        set => SetProperty(ref _endpointABaudRate, value);
    }

    public string EndpointADataBits
    {
        get => _endpointADataBits;
        set => SetProperty(ref _endpointADataBits, value);
    }

    public string EndpointAParity
    {
        get => _endpointAParity;
        set => SetProperty(ref _endpointAParity, value);
    }

    public string EndpointAStopBits
    {
        get => _endpointAStopBits;
        set => SetProperty(ref _endpointAStopBits, value);
    }

    public string EndpointAHandshake
    {
        get => _endpointAHandshake;
        set => SetProperty(ref _endpointAHandshake, value);
    }

    public string EndpointBPortName
    {
        get => _endpointBPortName;
        set => SetProperty(ref _endpointBPortName, value);
    }

    public string EndpointBBaudRate
    {
        get => _endpointBBaudRate;
        set => SetProperty(ref _endpointBBaudRate, value);
    }

    public string EndpointBDataBits
    {
        get => _endpointBDataBits;
        set => SetProperty(ref _endpointBDataBits, value);
    }

    public string EndpointBParity
    {
        get => _endpointBParity;
        set => SetProperty(ref _endpointBParity, value);
    }

    public string EndpointBStopBits
    {
        get => _endpointBStopBits;
        set => SetProperty(ref _endpointBStopBits, value);
    }

    public string EndpointBHandshake
    {
        get => _endpointBHandshake;
        set => SetProperty(ref _endpointBHandshake, value);
    }

    public string DecoderMode
    {
        get => _decoderMode;
        set => SetProperty(ref _decoderMode, value);
    }

    public string DecoderTimeSliceMs
    {
        get => _decoderTimeSliceMs;
        set => SetProperty(ref _decoderTimeSliceMs, value);
    }

    public string DecoderDelimiterHex
    {
        get => _decoderDelimiterHex;
        set => SetProperty(ref _decoderDelimiterHex, value);
    }

    public string DecoderFixedLength
    {
        get => _decoderFixedLength;
        set => SetProperty(ref _decoderFixedLength, value);
    }

    public bool CaptureEnabled
    {
        get => _captureEnabled;
        set => SetProperty(ref _captureEnabled, value);
    }

    public string CaptureFolder
    {
        get => _captureFolder;
        set => SetProperty(ref _captureFolder, value);
    }

    public string MonitorMaxFrames
    {
        get => _monitorMaxFrames;
        set => SetProperty(ref _monitorMaxFrames, value);
    }

    public string MonitorUiThrottleMs
    {
        get => _monitorUiThrottleMs;
        set => SetProperty(ref _monitorUiThrottleMs, value);
    }

    public string PluginsFolder
    {
        get => _pluginsFolder;
        set => SetProperty(ref _pluginsFolder, value);
    }

    public void LoadFrom(AppConfig config)
    {
        SessionUseInMemory = config.Session.UseInMemory;

        EndpointAPortName = config.Session.EndpointA.PortName;
        EndpointABaudRate = config.Session.EndpointA.BaudRate.ToString(CultureInfo.InvariantCulture);
        EndpointADataBits = config.Session.EndpointA.DataBits.ToString(CultureInfo.InvariantCulture);
        EndpointAParity = config.Session.EndpointA.Parity;
        EndpointAStopBits = config.Session.EndpointA.StopBits;
        EndpointAHandshake = config.Session.EndpointA.Handshake;

        EndpointBPortName = config.Session.EndpointB.PortName;
        EndpointBBaudRate = config.Session.EndpointB.BaudRate.ToString(CultureInfo.InvariantCulture);
        EndpointBDataBits = config.Session.EndpointB.DataBits.ToString(CultureInfo.InvariantCulture);
        EndpointBParity = config.Session.EndpointB.Parity;
        EndpointBStopBits = config.Session.EndpointB.StopBits;
        EndpointBHandshake = config.Session.EndpointB.Handshake;

        DecoderMode = config.Session.Decoders.Mode;
        DecoderTimeSliceMs = config.Session.Decoders.TimeSliceMs.ToString(CultureInfo.InvariantCulture);
        DecoderDelimiterHex = config.Session.Decoders.DelimiterHex;
        DecoderFixedLength = config.Session.Decoders.FixedLength.ToString(CultureInfo.InvariantCulture);

        CaptureEnabled = config.Capture.Enabled;
        CaptureFolder = config.Capture.Folder;
        MonitorMaxFrames = config.Monitor.MaxFrames.ToString(CultureInfo.InvariantCulture);
        MonitorUiThrottleMs = config.Monitor.UiThrottleMs.ToString(CultureInfo.InvariantCulture);
        PluginsFolder = config.Plugins.Folder;
    }

    public AppConfig BuildConfig()
    {
        return new AppConfig
        {
            Session = new SessionConfig
            {
                UseInMemory = SessionUseInMemory,
                EndpointA = new SerialEndpointConfig
                {
                    PortName = NormalizeRequired(EndpointAPortName, nameof(EndpointAPortName)),
                    BaudRate = ParsePositiveInt(EndpointABaudRate, nameof(EndpointABaudRate)),
                    DataBits = ParsePositiveInt(EndpointADataBits, nameof(EndpointADataBits)),
                    Parity = NormalizeRequired(EndpointAParity, nameof(EndpointAParity)),
                    StopBits = NormalizeRequired(EndpointAStopBits, nameof(EndpointAStopBits)),
                    Handshake = NormalizeRequired(EndpointAHandshake, nameof(EndpointAHandshake)),
                },
                EndpointB = new SerialEndpointConfig
                {
                    PortName = NormalizeRequired(EndpointBPortName, nameof(EndpointBPortName)),
                    BaudRate = ParsePositiveInt(EndpointBBaudRate, nameof(EndpointBBaudRate)),
                    DataBits = ParsePositiveInt(EndpointBDataBits, nameof(EndpointBDataBits)),
                    Parity = NormalizeRequired(EndpointBParity, nameof(EndpointBParity)),
                    StopBits = NormalizeRequired(EndpointBStopBits, nameof(EndpointBStopBits)),
                    Handshake = NormalizeRequired(EndpointBHandshake, nameof(EndpointBHandshake)),
                },
                Decoders = new DecoderConfig
                {
                    Mode = NormalizeRequired(DecoderMode, nameof(DecoderMode)),
                    TimeSliceMs = ParsePositiveInt(DecoderTimeSliceMs, nameof(DecoderTimeSliceMs)),
                    DelimiterHex = NormalizeRequired(DecoderDelimiterHex, nameof(DecoderDelimiterHex)),
                    FixedLength = ParsePositiveInt(DecoderFixedLength, nameof(DecoderFixedLength)),
                },
            },
            Capture = new CaptureConfig
            {
                Enabled = CaptureEnabled,
                Folder = NormalizeRequired(CaptureFolder, nameof(CaptureFolder)),
            },
            Monitor = new MonitorConfig
            {
                MaxFrames = ParsePositiveInt(MonitorMaxFrames, nameof(MonitorMaxFrames)),
                UiThrottleMs = ParsePositiveInt(MonitorUiThrottleMs, nameof(MonitorUiThrottleMs)),
            },
            Plugins = new PluginConfig
            {
                Folder = NormalizeRequired(PluginsFolder, nameof(PluginsFolder)),
            },
        };
    }

    public void SaveToFile(string path)
    {
        var config = BuildConfig();
        var json = JsonSerializer.Serialize(config, SaveOptions);
        File.WriteAllText(path, json);
    }

    private static int ParsePositiveInt(string text, string fieldName)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value <= 0)
        {
            throw new InvalidOperationException($"{fieldName} must be a positive integer.");
        }

        return value;
    }

    private static string NormalizeRequired(string text, string fieldName)
    {
        var trimmed = (text ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw new InvalidOperationException($"{fieldName} is required.");
        }

        return trimmed;
    }
}
