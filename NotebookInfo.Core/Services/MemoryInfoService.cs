using NotebookInfo.Core.Models;

namespace NotebookInfo.Core.Services;

/// <summary>Reads physical memory modules from Win32_PhysicalMemory.</summary>
public sealed class MemoryInfoService(IWmiQueryService wmiQueryService) : IMemoryInfoService
{
    public async Task<MemoryInfo> GetMemoryInfoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var rows = await wmiQueryService.QueryAsync("Win32_PhysicalMemory", cancellationToken);
            var modules = rows.Select(row =>
            {
                var type = row.UInt16("SMBIOSMemoryType");
                return new MemoryModuleInfo(
                    row.UInt64("Capacity"),
                    row.UInt32("Speed"),
                    row.Text("Manufacturer"),
                    row.Text("PartNumber"),
                    row.Text("SerialNumber"),
                    type,
                    MemoryTypeConverter.ToDisplayName(type),
                    row.UInt32("ConfiguredClockSpeed"));
            }).ToArray();
            return new MemoryInfo(modules);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ServiceHelpers.LogFailure("Failed to query Win32_PhysicalMemory", ex);
            return MemoryInfo.Unknown;
        }
    }
}

/// <summary>Converts SMBIOS memory type numbers to user-friendly labels.</summary>
public static class MemoryTypeConverter
{
    public static string ToDisplayName(ushort smbiosMemoryType) => smbiosMemoryType switch
    {
        24 => "DDR3",
        26 => "DDR4",
        34 => "DDR5",
        30 => "LPDDR4",
        35 => "LPDDR5",
        _ => UnknownValue.Unknown
    };
}
