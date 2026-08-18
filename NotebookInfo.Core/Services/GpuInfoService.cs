using NotebookInfo.Core.Models;

namespace NotebookInfo.Core.Services;

/// <summary>Reads graphics adapter details from Win32_VideoController; VRAM can be approximate for shared-memory adapters.</summary>
public sealed class GpuInfoService(IWmiQueryService wmiQueryService) : IGpuInfoService
{
    public async Task<IReadOnlyList<GpuInfo>> GetGpuInfoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var rows = await wmiQueryService.QueryAsync("Win32_VideoController", cancellationToken);
            var gpus = rows.Select(row => new GpuInfo(
                row.Text("Name"),
                row.UInt64("AdapterRAM"),
                row.Text("DriverVersion"),
                row.Text("VideoProcessor"),
                row.UInt32("CurrentHorizontalResolution"),
                row.UInt32("CurrentVerticalResolution"),
                row.UInt32("CurrentRefreshRate"))).ToArray();
            return gpus.Length == 0 ? new[] { GpuInfo.Unknown } : gpus;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ServiceHelpers.LogFailure("Failed to query Win32_VideoController", ex);
            return new[] { GpuInfo.Unknown };
        }
    }
}
