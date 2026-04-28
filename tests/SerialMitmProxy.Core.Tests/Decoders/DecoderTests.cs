using SerialMitmProxy.Core.Decoders;

namespace SerialMitmProxy.Core.Tests.Decoders;

public sealed class DecoderTests
{
    [Fact]
    public void TimeSliceDecoder_EmitsFrameOnIdleGap()
    {
        var decoder = new TimeSliceDecoder(TimeSpan.FromMilliseconds(50));
        var start = DateTimeOffset.UtcNow;

        var first = decoder.Decode(new byte[] { 0x01, 0x02 }, start);
        var second = decoder.Decode(new byte[] { 0x03 }, start.AddMilliseconds(10));
        var third = decoder.Decode(new byte[] { 0x04 }, start.AddMilliseconds(70));
        var flushed = decoder.Flush(start.AddMilliseconds(80));

        Assert.Empty(first);
        Assert.Empty(second);
        Assert.Single(third);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, third[0].Payload);
        Assert.Single(flushed);
        Assert.Equal(new byte[] { 0x04 }, flushed[0].Payload);
    }

    [Fact]
    public void DelimiterDecoder_SplitsFramesAcrossChunks()
    {
        var decoder = new DelimiterDecoder(new byte[] { 0x0D, 0x0A });
        var t = DateTimeOffset.UtcNow;

        var first = decoder.Decode(new byte[] { 0x41, 0x0D }, t);
        var second = decoder.Decode(new byte[] { 0x0A, 0x42, 0x0D, 0x0A }, t);

        Assert.Empty(first);
        Assert.Equal(2, second.Count);
        Assert.Equal(new byte[] { 0x41, 0x0D, 0x0A }, second[0].Payload);
        Assert.Equal(new byte[] { 0x42, 0x0D, 0x0A }, second[1].Payload);
    }

    [Fact]
    public void FixedLengthDecoder_ProducesDeterministicFrames()
    {
        var decoder = new FixedLengthDecoder(3);
        var t = DateTimeOffset.UtcNow;

        var first = decoder.Decode(new byte[] { 0x01, 0x02, 0x03, 0x04 }, t);
        var second = decoder.Decode(new byte[] { 0x05, 0x06 }, t);

        Assert.Single(first);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, first[0].Payload);
        Assert.Single(second);
        Assert.Equal(new byte[] { 0x04, 0x05, 0x06 }, second[0].Payload);
    }
}
