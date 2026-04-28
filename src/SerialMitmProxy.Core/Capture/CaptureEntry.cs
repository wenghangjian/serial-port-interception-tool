using SerialMitmProxy.Core.Models;

namespace SerialMitmProxy.Core.Capture;

public readonly record struct CaptureEntry(
    DateTimeOffset TimestampUtc,
    Direction Direction,
    long Offset,
    int Length);
