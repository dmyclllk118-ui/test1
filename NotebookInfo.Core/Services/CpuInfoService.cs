using NotebookInfo.Core.Models;

namespace NotebookInfo.Core.Services;

/// <summary>Reads processor details from Win32_Processor.</summary>
public sealed class CpuInfoService(IWmiQueryService wmiQueryService) : ICpuInfoService
{
    public async Task<IReadOnlyList<CpuInfo>> GetCpuInfoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var rows = await wmiQueryService.QueryAsync("Win32_Processor", cancellationToken);
            var cpus = rows.Select(row => new CpuInfo(
                row.Text("Name"),
                row.Text("Manufacturer"),
                row.UInt32("NumberOfCores"),
                row.UInt32("NumberOfLogicalProcessors"),
                row.UInt32("MaxClockSpeed"),
                row.UInt32("CurrentClockSpeed"),
                row.Text("SocketDesignation"))).ToArray();
            return cpus.Length == 0 ? new[] { CpuInfo.Unknown } : cpus;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ServiceHelpers.LogFailure("Failed to query Win32_Processor", ex);
            return new[] { CpuInfo.Unknown };
        }
    }
}
