using System.Globalization;
using SerialMitmProxy.Application.Common;

namespace SerialMitmProxy.Application.Intercept;

public enum ChecksumAlgorithmKind
{
    Sum8 = 0,
    Xor8 = 1,
    Crc8 = 2,
    Crc16Modbus = 3,
    Crc16Ibm = 4,
    Crc16CcittFalse = 5,
    Lrc8 = 6,
    Bcc = 7,
    Crc32 = 8,
}

public enum ChecksumByteOrder
{
    LittleEndian = 0,
    BigEndian = 1,
}

public sealed record ChecksumAlgorithmOption(
    ChecksumAlgorithmKind Kind,
    string Label,
    int Width,
    ChecksumByteOrder DefaultByteOrder);

public sealed record ChecksumByteOrderOption(ChecksumByteOrder Order, string Label);

public sealed class ChecksumToolViewModel : ViewModelBase
{
    private const int AutoOffset = -1;

    private readonly List<ChecksumAlgorithmOption> _algorithmOptions = new();
    private readonly List<ChecksumByteOrderOption> _byteOrderOptions = new();
    private Func<string, string> _translate;
    private ChecksumAlgorithmOption? _selectedAlgorithm;
    private ChecksumByteOrderOption? _selectedByteOrder;
    private InterceptQueueItemViewModel? _selectedItem;
    private string _computedHex = string.Empty;
    private string _scopeDescription = string.Empty;
    private string _statusText = string.Empty;
    private string _startOffsetText = "0";
    private string _endOffsetText = AutoOffset.ToString(CultureInfo.InvariantCulture);
    private string _writeOffsetText = AutoOffset.ToString(CultureInfo.InvariantCulture);

    public ChecksumToolViewModel(Func<string, string>? translate = null)
    {
        _translate = translate ?? (static key => key);
        RebuildByteOrderOptions();
        RebuildAlgorithmOptions();
        _selectedAlgorithm = _algorithmOptions.FirstOrDefault();
        _selectedByteOrder = ResolveDefaultByteOrder(_selectedAlgorithm);
        Recalculate();
    }

    public IReadOnlyList<ChecksumAlgorithmOption> AlgorithmOptions => _algorithmOptions;

    public IReadOnlyList<ChecksumByteOrderOption> ByteOrderOptions => _byteOrderOptions;

    public ChecksumAlgorithmOption? SelectedAlgorithm
    {
        get => _selectedAlgorithm;
        set
        {
            if (!SetProperty(ref _selectedAlgorithm, value))
            {
                return;
            }

            var defaultByteOrder = ResolveDefaultByteOrder(value);
            if (!Equals(_selectedByteOrder, defaultByteOrder))
            {
                _selectedByteOrder = defaultByteOrder;
                RaisePropertyChanged(nameof(SelectedByteOrder));
            }

            RaisePropertyChanged(nameof(IsReady));
            Recalculate();
        }
    }

    public ChecksumByteOrderOption? SelectedByteOrder
    {
        get => _selectedByteOrder;
        set
        {
            if (!SetProperty(ref _selectedByteOrder, value))
            {
                return;
            }

            Recalculate();
        }
    }

    public InterceptQueueItemViewModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (ReferenceEquals(_selectedItem, value))
            {
                return;
            }

            if (_selectedItem is not null)
            {
                _selectedItem.PropertyChanged -= SelectedItem_PropertyChanged;
            }

            _selectedItem = value;
            if (_selectedItem is not null)
            {
                _selectedItem.PropertyChanged += SelectedItem_PropertyChanged;
            }

            RaisePropertyChanged(nameof(IsReady));
            Recalculate();
        }
    }

    public bool IsReady => SelectedItem is not null && SelectedAlgorithm is not null && SelectedByteOrder is not null;

    public string StartOffsetText
    {
        get => _startOffsetText;
        set
        {
            if (!SetProperty(ref _startOffsetText, value))
            {
                return;
            }

            Recalculate();
        }
    }

    public string EndOffsetText
    {
        get => _endOffsetText;
        set
        {
            if (!SetProperty(ref _endOffsetText, value))
            {
                return;
            }

            Recalculate();
        }
    }

    public string WriteOffsetText
    {
        get => _writeOffsetText;
        set
        {
            if (!SetProperty(ref _writeOffsetText, value))
            {
                return;
            }

            Recalculate();
        }
    }

    public string ComputedHex
    {
        get => _computedHex;
        private set => SetProperty(ref _computedHex, value);
    }

    public string ScopeDescription
    {
        get => _scopeDescription;
        private set => SetProperty(ref _scopeDescription, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public void SetTranslator(Func<string, string> translate)
    {
        _translate = translate;

        var selectedKind = SelectedAlgorithm?.Kind ?? ChecksumAlgorithmKind.Crc16Modbus;
        var selectedOrder = SelectedByteOrder?.Order ?? ChecksumByteOrder.LittleEndian;

        RebuildByteOrderOptions();
        RebuildAlgorithmOptions();

        _selectedAlgorithm = _algorithmOptions.FirstOrDefault(option => option.Kind == selectedKind) ?? _algorithmOptions.FirstOrDefault();
        _selectedByteOrder = _byteOrderOptions.FirstOrDefault(option => option.Order == selectedOrder)
            ?? ResolveDefaultByteOrder(_selectedAlgorithm);

        RaisePropertyChanged(nameof(AlgorithmOptions));
        RaisePropertyChanged(nameof(ByteOrderOptions));
        RaisePropertyChanged(nameof(SelectedAlgorithm));
        RaisePropertyChanged(nameof(SelectedByteOrder));
        RaisePropertyChanged(nameof(IsReady));
        Recalculate();
    }

    public void Recalculate()
    {
        if (!TryBuildCalculationContext(out var context, out var error))
        {
            ComputedHex = string.Empty;
            ScopeDescription = string.Empty;
            StatusText = error;
            return;
        }

        ComputedHex = FormatHex(context.Checksum);
        ScopeDescription = context.EndOffset < context.StartOffset
            ? _translate("ChecksumScopeEmpty")
            : string.Format(_translate("ChecksumScopeBytes"), context.StartOffset, context.EndOffset);
        StatusText = BuildStatusText(context.PayloadLength, context.Algorithm);
    }

    public void AppendChecksum()
    {
        if (!TryBuildCalculationContext(out var context, out var error))
        {
            throw new InvalidOperationException(error);
        }

        var updated = context.Payload.Concat(context.Checksum).ToArray();
        SelectedItem!.EditedHex = FormatHex(updated);
        Recalculate();
    }

    public void OverwriteTailChecksum()
    {
        if (!TryBuildCalculationContext(out var context, out var error))
        {
            throw new InvalidOperationException(error);
        }

        var writeOffset = ResolveOverwriteOffset(context.PayloadLength, context.Algorithm.Width);
        var updated = context.Payload.ToArray();
        Array.Copy(context.Checksum, 0, updated, writeOffset, context.Algorithm.Width);
        SelectedItem!.EditedHex = FormatHex(updated);
        Recalculate();
    }

    private void SelectedItem_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InterceptQueueItemViewModel.EditedHex))
        {
            Recalculate();
        }
    }

    private bool TryBuildCalculationContext(out CalculationContext context, out string error)
    {
        context = default;
        error = string.Empty;

        if (!TryGetWorkingPayload(out var payload, out error))
        {
            return false;
        }

        var algorithm = SelectedAlgorithm!;
        if (!TryParseStartOffset(payload.Length, out var startOffset, out error))
        {
            return false;
        }

        if (!TryParseOffset(EndOffsetText, allowAuto: true, out var requestedEndOffset, out error))
        {
            return false;
        }

        if (!TryParseOffset(WriteOffsetText, allowAuto: true, out _, out error))
        {
            return false;
        }

        if (!TryResolveEndOffset(payload.Length, algorithm.Width, startOffset, requestedEndOffset, out var endOffset, out error))
        {
            return false;
        }

        var checksum = Calculate(algorithm.Kind, GetCalculationBytes(payload, startOffset, endOffset), SelectedByteOrder!.Order);
        context = new CalculationContext(payload, algorithm, startOffset, endOffset, checksum);
        return true;
    }

    private bool TryGetWorkingPayload(out byte[] payload, out string error)
    {
        payload = Array.Empty<byte>();
        error = string.Empty;

        if (SelectedItem is null || SelectedAlgorithm is null || SelectedByteOrder is null)
        {
            error = _translate("ChecksumSelectFrameFirst");
            return false;
        }

        try
        {
            payload = SelectedItem.ParseEditedPayload();
            return true;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            error = string.Format(_translate("ChecksumInvalidHex"), ex.Message);
            return false;
        }
    }

    private void RebuildAlgorithmOptions()
    {
        _algorithmOptions.Clear();
        _algorithmOptions.Add(new ChecksumAlgorithmOption(ChecksumAlgorithmKind.Sum8, _translate("ChecksumAlgoSum8"), 1, ChecksumByteOrder.LittleEndian));
        _algorithmOptions.Add(new ChecksumAlgorithmOption(ChecksumAlgorithmKind.Xor8, _translate("ChecksumAlgoXor8"), 1, ChecksumByteOrder.LittleEndian));
        _algorithmOptions.Add(new ChecksumAlgorithmOption(ChecksumAlgorithmKind.Crc8, _translate("ChecksumAlgoCrc8"), 1, ChecksumByteOrder.LittleEndian));
        _algorithmOptions.Add(new ChecksumAlgorithmOption(ChecksumAlgorithmKind.Crc16Modbus, _translate("ChecksumAlgoCrc16Modbus"), 2, ChecksumByteOrder.LittleEndian));
        _algorithmOptions.Add(new ChecksumAlgorithmOption(ChecksumAlgorithmKind.Crc16Ibm, _translate("ChecksumAlgoCrc16Ibm"), 2, ChecksumByteOrder.LittleEndian));
        _algorithmOptions.Add(new ChecksumAlgorithmOption(ChecksumAlgorithmKind.Crc16CcittFalse, _translate("ChecksumAlgoCrc16Ccitt"), 2, ChecksumByteOrder.BigEndian));
        _algorithmOptions.Add(new ChecksumAlgorithmOption(ChecksumAlgorithmKind.Lrc8, _translate("ChecksumAlgoLrc"), 1, ChecksumByteOrder.LittleEndian));
        _algorithmOptions.Add(new ChecksumAlgorithmOption(ChecksumAlgorithmKind.Bcc, _translate("ChecksumAlgoBcc"), 1, ChecksumByteOrder.LittleEndian));
        _algorithmOptions.Add(new ChecksumAlgorithmOption(ChecksumAlgorithmKind.Crc32, _translate("ChecksumAlgoCrc32"), 4, ChecksumByteOrder.LittleEndian));
        RaisePropertyChanged(nameof(AlgorithmOptions));
    }

    private void RebuildByteOrderOptions()
    {
        _byteOrderOptions.Clear();
        _byteOrderOptions.Add(new ChecksumByteOrderOption(ChecksumByteOrder.LittleEndian, _translate("ChecksumByteOrderLittle")));
        _byteOrderOptions.Add(new ChecksumByteOrderOption(ChecksumByteOrder.BigEndian, _translate("ChecksumByteOrderBig")));
        RaisePropertyChanged(nameof(ByteOrderOptions));
    }

    private ChecksumByteOrderOption? ResolveDefaultByteOrder(ChecksumAlgorithmOption? algorithm)
    {
        var order = algorithm?.DefaultByteOrder ?? ChecksumByteOrder.LittleEndian;
        return _byteOrderOptions.FirstOrDefault(option => option.Order == order) ?? _byteOrderOptions.FirstOrDefault();
    }

    private bool TryParseStartOffset(int payloadLength, out int startOffset, out string error)
    {
        if (!TryParseOffset(StartOffsetText, allowAuto: false, out startOffset, out error))
        {
            return false;
        }

        if (startOffset < 0 || startOffset > payloadLength)
        {
            error = string.Format(_translate("ChecksumInvalidRange"), StartOffsetText, EndOffsetText);
            return false;
        }

        return true;
    }

    private bool TryParseOffset(string text, bool allowAuto, out int value, out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            value = allowAuto ? AutoOffset : 0;
            return true;
        }

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            error = string.Format(_translate("ChecksumInvalidRange"), StartOffsetText, EndOffsetText);
            return false;
        }

        if (allowAuto && value == AutoOffset)
        {
            return true;
        }

        if (value < 0)
        {
            error = string.Format(_translate("ChecksumInvalidRange"), StartOffsetText, EndOffsetText);
            return false;
        }

        return true;
    }

    private bool TryResolveEndOffset(
        int payloadLength,
        int checksumWidth,
        int startOffset,
        int requestedEndOffset,
        out int endOffset,
        out string error)
    {
        error = string.Empty;

        if (payloadLength == 0)
        {
            endOffset = -1;
            return true;
        }

        endOffset = requestedEndOffset == AutoOffset
            ? ResolveAutoEndOffset(payloadLength, checksumWidth)
            : requestedEndOffset;

        if (endOffset >= payloadLength)
        {
            error = string.Format(_translate("ChecksumInvalidRange"), StartOffsetText, EndOffsetText);
            return false;
        }

        if (endOffset < startOffset - 1)
        {
            error = string.Format(_translate("ChecksumInvalidRange"), StartOffsetText, EndOffsetText);
            return false;
        }

        return true;
    }

    private int ResolveOverwriteOffset(int payloadLength, int checksumWidth)
    {
        if (!TryParseOffset(WriteOffsetText, allowAuto: true, out var requestedOffset, out _))
        {
            throw new InvalidOperationException(string.Format(_translate("ChecksumInvalidWriteOffset"), WriteOffsetText));
        }

        var writeOffset = requestedOffset == AutoOffset
            ? payloadLength - checksumWidth
            : requestedOffset;

        if (writeOffset < 0 || writeOffset + checksumWidth > payloadLength)
        {
            throw new InvalidOperationException(string.Format(_translate("ChecksumInvalidWriteOffset"), WriteOffsetText));
        }

        return writeOffset;
    }

    private string BuildStatusText(int payloadLength, ChecksumAlgorithmOption algorithm)
    {
        var autoRangeHint = string.Format(_translate("ChecksumAutoRangeHint"), algorithm.Width);
        var byteOrderHint = string.Format(_translate("ChecksumByteOrderHint"), SelectedByteOrder!.Label);

        try
        {
            var writeOffset = ResolveOverwriteOffset(payloadLength, algorithm.Width);
            return $"{autoRangeHint} {string.Format(_translate("ChecksumWriteHint"), writeOffset, writeOffset + algorithm.Width - 1)} {byteOrderHint}";
        }
        catch (InvalidOperationException)
        {
            return $"{autoRangeHint} {_translate("ChecksumAppendOnlyHint")} {byteOrderHint}";
        }
    }

    private static int ResolveAutoEndOffset(int payloadLength, int checksumWidth)
    {
        if (payloadLength == 0)
        {
            return -1;
        }

        return payloadLength > checksumWidth
            ? payloadLength - checksumWidth - 1
            : payloadLength - 1;
    }

    private static byte[] GetCalculationBytes(byte[] payload, int startOffset, int endOffset)
    {
        if (endOffset < startOffset)
        {
            return Array.Empty<byte>();
        }

        var length = endOffset - startOffset + 1;
        var data = new byte[length];
        Array.Copy(payload, startOffset, data, 0, length);
        return data;
    }

    private static byte[] Calculate(ChecksumAlgorithmKind kind, byte[] data, ChecksumByteOrder byteOrder)
    {
        return kind switch
        {
            ChecksumAlgorithmKind.Sum8 => new[] { ComputeSum8(data) },
            ChecksumAlgorithmKind.Xor8 => new[] { ComputeXor8(data) },
            ChecksumAlgorithmKind.Crc8 => new[] { ComputeCrc8(data) },
            ChecksumAlgorithmKind.Crc16Modbus => ToOrderedBytes(ComputeCrc16Modbus(data), byteOrder),
            ChecksumAlgorithmKind.Crc16Ibm => ToOrderedBytes(ComputeCrc16Ibm(data), byteOrder),
            ChecksumAlgorithmKind.Crc16CcittFalse => ToOrderedBytes(ComputeCrc16CcittFalse(data), byteOrder),
            ChecksumAlgorithmKind.Lrc8 => new[] { ComputeLrc8(data) },
            ChecksumAlgorithmKind.Bcc => new[] { ComputeBcc(data) },
            ChecksumAlgorithmKind.Crc32 => ToOrderedBytes(ComputeCrc32(data), byteOrder),
            _ => throw new InvalidOperationException($"Unsupported checksum algorithm: {kind}"),
        };
    }

    private static byte ComputeSum8(IEnumerable<byte> data)
    {
        var sum = 0;
        foreach (var value in data)
        {
            sum = (sum + value) & 0xFF;
        }

        return (byte)sum;
    }

    private static byte ComputeXor8(IEnumerable<byte> data)
    {
        var value = 0;
        foreach (var item in data)
        {
            value ^= item;
        }

        return (byte)value;
    }

    private static byte ComputeCrc8(IEnumerable<byte> data)
    {
        byte crc = 0x00;
        foreach (var item in data)
        {
            crc ^= item;
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 0x80) != 0
                    ? (byte)((crc << 1) ^ 0x07)
                    : (byte)(crc << 1);
            }
        }

        return crc;
    }

    private static ushort ComputeCrc16Modbus(IEnumerable<byte> data)
    {
        ushort crc = 0xFFFF;
        foreach (var item in data)
        {
            crc ^= item;
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 0x0001) != 0
                    ? (ushort)((crc >> 1) ^ 0xA001)
                    : (ushort)(crc >> 1);
            }
        }

        return crc;
    }

    private static ushort ComputeCrc16Ibm(IEnumerable<byte> data)
    {
        ushort crc = 0x0000;
        foreach (var item in data)
        {
            crc ^= item;
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 0x0001) != 0
                    ? (ushort)((crc >> 1) ^ 0xA001)
                    : (ushort)(crc >> 1);
            }
        }

        return crc;
    }

    private static ushort ComputeCrc16CcittFalse(IEnumerable<byte> data)
    {
        ushort crc = 0xFFFF;
        foreach (var item in data)
        {
            crc ^= (ushort)(item << 8);
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 0x8000) != 0
                    ? (ushort)((crc << 1) ^ 0x1021)
                    : (ushort)(crc << 1);
            }
        }

        return crc;
    }

    private static byte ComputeLrc8(IEnumerable<byte> data)
    {
        var sum = 0;
        foreach (var value in data)
        {
            sum = (sum + value) & 0xFF;
        }

        return unchecked((byte)((-sum) & 0xFF));
    }

    private static byte ComputeBcc(IEnumerable<byte> data)
    {
        return ComputeXor8(data);
    }

    private static uint ComputeCrc32(IEnumerable<byte> data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var item in data)
        {
            crc ^= item;
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 1) != 0
                    ? (crc >> 1) ^ 0xEDB88320
                    : crc >> 1;
            }
        }

        return crc ^ 0xFFFFFFFF;
    }

    private static byte[] ToOrderedBytes(ushort value, ChecksumByteOrder byteOrder)
    {
        return byteOrder == ChecksumByteOrder.LittleEndian
            ? new[] { (byte)(value & 0xFF), (byte)((value >> 8) & 0xFF) }
            : new[] { (byte)((value >> 8) & 0xFF), (byte)(value & 0xFF) };
    }

    private static byte[] ToOrderedBytes(uint value, ChecksumByteOrder byteOrder)
    {
        return byteOrder == ChecksumByteOrder.LittleEndian
            ? new[]
            {
                (byte)(value & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 24) & 0xFF),
            }
            : new[]
            {
                (byte)((value >> 24) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)(value & 0xFF),
            };
    }

    private static string FormatHex(IEnumerable<byte> payload)
    {
        return BitConverter.ToString(payload.ToArray()).Replace('-', ' ');
    }

    private readonly record struct CalculationContext(
        byte[] Payload,
        ChecksumAlgorithmOption Algorithm,
        int StartOffset,
        int EndOffset,
        byte[] Checksum)
    {
        public int PayloadLength => Payload.Length;
    }
}
