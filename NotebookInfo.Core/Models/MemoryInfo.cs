namespace NotebookInfo.Core.Models;

/// <summary>A physical memory module.</summary>
public sealed record MemoryModuleInfo(
    ulong CapacityBytes,
    uint SpeedMHz,
    string Manufacturer,
    string PartNumber,
    string SerialNumber,
    ushort SMBIOSMemoryType,
    string MemoryTypeName,
    uint ConfiguredClockSpeedMHz)
{
    public double CapacityGB => HardwareFormat.BytesToGiB(CapacityBytes);
    public string DisplayCapacity => HardwareFormat.FormatGiB(CapacityBytes);
}

/// <summary>Aggregate memory information for all detected physical modules.</summary>
public sealed record MemoryInfo(IReadOnlyList<MemoryModuleInfo> Modules)
{
    public ulong TotalCapacityBytes => Modules.Aggregate(0UL, (total, module) => total + module.CapacityBytes);
    public double TotalCapacityGB => HardwareFormat.BytesToGiB(TotalCapacityBytes);
    public string DisplayTotalCapacity => HardwareFormat.FormatGiB(TotalCapacityBytes);

    public static MemoryInfo Unknown { get; } = new(Array.Empty<MemoryModuleInfo>());
}

/// <summary>Formatting and conversion helpers for hardware quantities.</summary>
public static class HardwareFormat
{
    public static double BytesToGiB(ulong bytes) => Math.Round(bytes / 1024d / 1024d / 1024d, 2);

    public static string FormatGiB(ulong bytes)
    {
        if (bytes == 0)
        {
            return UnknownValue.Unknown;
        }

        var value = BytesToGiB(bytes);
        return Math.Abs(value - Math.Round(value)) < 0.01 ? $"{value:0} GB" : $"{value:0.##} GB";
    }
}
