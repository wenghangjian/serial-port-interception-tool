using SerialMitmProxy.Application.Intercept;
using SerialMitmProxy.Core.Intercept;
using SerialMitmProxy.Core.Models;

namespace SerialMitmProxy.Core.Tests.Proxy;

public sealed class ChecksumToolViewModelTests
{
    [Fact]
    public void ChecksumTool_ComputesCrc16ModbusForCommonFrame()
    {
        var item = CreateItem(Direction.AtoB, 0x01, 0x03, 0x00, 0x00, 0x00, 0x0A, 0x00, 0x00);
        var tool = new ChecksumToolViewModel
        {
            SelectedItem = item,
            SelectedAlgorithm = null,
        };

        tool.SelectedAlgorithm = tool.AlgorithmOptions.Single(option => option.Kind == ChecksumAlgorithmKind.Crc16Modbus);

        Assert.Equal("C5 CD", tool.ComputedHex);
    }

    [Fact]
    public void ChecksumTool_ComputesLrcAndBcc()
    {
        var item = CreateItem(Direction.BtoA, 0x01, 0x02, 0x03, 0x00);
        var tool = new ChecksumToolViewModel
        {
            SelectedItem = item,
        };

        tool.SelectedAlgorithm = tool.AlgorithmOptions.Single(option => option.Kind == ChecksumAlgorithmKind.Lrc8);
        Assert.Equal("FA", tool.ComputedHex);

        tool.SelectedAlgorithm = tool.AlgorithmOptions.Single(option => option.Kind == ChecksumAlgorithmKind.Bcc);
        Assert.Equal("00", tool.ComputedHex);
    }

    [Fact]
    public void ChecksumTool_ComputesCrc32ForKnownInput()
    {
        var item = CreateItem(Direction.AtoB, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39);
        var tool = new ChecksumToolViewModel
        {
            SelectedItem = item,
        };

        tool.SelectedAlgorithm = tool.AlgorithmOptions.Single(option => option.Kind == ChecksumAlgorithmKind.Crc32);
        tool.EndOffsetText = "8";

        Assert.Equal("26 39 F4 CB", tool.ComputedHex);
    }

    [Fact]
    public void ChecksumTool_OverwriteTailChecksum_UsesConfiguredOffsets()
    {
        var item = CreateItem(Direction.BtoA, 0xAA, 0x10, 0x20, 0x30, 0x99);
        var tool = new ChecksumToolViewModel
        {
            SelectedItem = item,
        };

        tool.SelectedAlgorithm = tool.AlgorithmOptions.Single(option => option.Kind == ChecksumAlgorithmKind.Sum8);
        tool.StartOffsetText = "1";
        tool.EndOffsetText = "3";
        tool.WriteOffsetText = "4";
        tool.OverwriteTailChecksum();

        Assert.Equal("AA 10 20 30 60", item.EditedHex);
    }

    [Fact]
    public void ChecksumTool_AppendChecksum_UsesSelectedByteOrder()
    {
        var item = CreateItem(Direction.AtoB, 0x01, 0x03, 0x00, 0x00, 0x00, 0x0A);
        var tool = new ChecksumToolViewModel
        {
            SelectedItem = item,
        };

        tool.SelectedAlgorithm = tool.AlgorithmOptions.Single(option => option.Kind == ChecksumAlgorithmKind.Crc16Modbus);
        tool.SelectedByteOrder = tool.ByteOrderOptions.Single(option => option.Order == ChecksumByteOrder.BigEndian);
        tool.EndOffsetText = "5";
        tool.AppendChecksum();

        Assert.Equal("01 03 00 00 00 0A CD C5", item.EditedHex);
    }

    private static InterceptQueueItemViewModel CreateItem(Direction direction, params byte[] payload)
    {
        var request = new InterceptRequest(Frame.Create(direction, payload, DateTimeOffset.UtcNow));
        return new InterceptQueueItemViewModel(request);
    }
}
