using System.Text;
using NotebookInfo.Core.Models;

namespace NotebookInfo.Core.Services;

/// <summary>Parses the base EDID block exposed by Windows monitor registry entries.</summary>
public static class EdidParser
{
    private const int BaseEdidLength = 128;
    private const int DescriptorStart = 54;
    private const int DescriptorLength = 18;
    private const byte MonitorSerialDescriptor = 0xFF;
    private const byte MonitorNameDescriptor = 0xFC;

    public static EdidInfo Parse(byte[]? edid)
    {
        if (edid is null || edid.Length < BaseEdidLength || !HasValidHeader(edid))
        {
            return EdidInfo.Unknown;
        }

        var manufacturer = ParseManufacturerCode(edid[8], edid[9]);
        var productCode = BitConverter.ToUInt16(edid, 10).ToString("X4");
        var numericSerial = BitConverter.ToUInt32(edid, 12);
        var serial = numericSerial == 0 ? UnknownValue.Unknown : numericSerial.ToString();
        var width = edid[21];
        var height = edid[22];
        var model = UnknownValue.Unknown;

        for (var offset = DescriptorStart; offset + DescriptorLength <= BaseEdidLength; offset += DescriptorLength)
        {
            if (edid[offset] != 0 || edid[offset + 1] != 0 || edid[offset + 2] != 0)
            {
                continue;
            }

            var descriptorType = edid[offset + 3];
            if (descriptorType == MonitorNameDescriptor)
            {
                model = ParseDescriptorText(edid, offset);
            }
            else if (descriptorType == MonitorSerialDescriptor)
            {
                var descriptorSerial = ParseDescriptorText(edid, offset);
                if (descriptorSerial != UnknownValue.Unknown)
                {
                    serial = descriptorSerial;
                }
            }
        }

        if (model == UnknownValue.Unknown)
        {
            model = productCode;
        }

        return new EdidInfo(manufacturer, model, serial, width, height, productCode);
    }

    private static bool HasValidHeader(IReadOnlyList<byte> edid)
    {
        ReadOnlySpan<byte> expected = stackalloc byte[] { 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00 };
        for (var i = 0; i < expected.Length; i++)
        {
            if (edid[i] != expected[i])
            {
                return false;
            }
        }

        return true;
    }

    private static string ParseManufacturerCode(byte first, byte second)
    {
        var value = (first << 8) | second;
        Span<char> chars = stackalloc char[3];
        chars[0] = DecodeManufacturerChar((value >> 10) & 0x1F);
        chars[1] = DecodeManufacturerChar((value >> 5) & 0x1F);
        chars[2] = DecodeManufacturerChar(value & 0x1F);
        var code = new string(chars);
        return code.Any(ch => ch < 'A' || ch > 'Z') ? UnknownValue.Unknown : code;
    }

    private static char DecodeManufacturerChar(int value) => (char)('A' + value - 1);

    private static string ParseDescriptorText(byte[] edid, int descriptorOffset)
    {
        var raw = Encoding.ASCII.GetString(edid, descriptorOffset + 5, 13);
        var value = raw.Replace("\0", string.Empty).Trim();
        var newline = value.IndexOf('\n', StringComparison.Ordinal);
        if (newline >= 0)
        {
            value = value[..newline].Trim();
        }

        return UnknownValue.Clean(value);
    }
}

/// <summary>Normalized fields extracted from an EDID base block.</summary>
public sealed record EdidInfo(
    string ManufacturerCode,
    string Model,
    string SerialNumber,
    double PhysicalWidthCentimeters,
    double PhysicalHeightCentimeters,
    string ProductCode)
{
    public static EdidInfo Unknown { get; } = new(
        UnknownValue.Unknown,
        UnknownValue.Unknown,
        UnknownValue.Unknown,
        0,
        0,
        UnknownValue.Unknown);
}
