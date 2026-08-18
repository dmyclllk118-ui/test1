namespace NotebookInfo.Core.Models;

/// <summary>Graphics adapter information. WMI adapter memory may be approximate on shared-memory GPUs.</summary>
public sealed record GpuInfo(
    string Name,
    ulong AdapterRAMBytes,
    string DriverVersion,
    string VideoProcessor,
    uint CurrentHorizontalResolution,
    uint CurrentVerticalResolution,
    uint CurrentRefreshRate)
{
    public string DisplayAdapterRAM => HardwareFormat.FormatGiB(AdapterRAMBytes);
    public string DisplayResolution => CurrentHorizontalResolution == 0 || CurrentVerticalResolution == 0
        ? UnknownValue.Unknown
        : $"{CurrentHorizontalResolution} x {CurrentVerticalResolution}";
    public string DisplayRefreshRate => CurrentRefreshRate == 0 ? UnknownValue.Unknown : $"{CurrentRefreshRate} Hz";

    public static GpuInfo Unknown { get; } = new(
        UnknownValue.Unknown,
        0,
        UnknownValue.Unknown,
        UnknownValue.Unknown,
        0,
        0,
        0);
}
