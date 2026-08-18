namespace NotebookInfo.Core.Models;

/// <summary>Processor information read from Windows hardware inventory.</summary>
public sealed record CpuInfo(
    string Name,
    string Manufacturer,
    uint NumberOfCores,
    uint NumberOfLogicalProcessors,
    uint MaxClockSpeedMHz,
    uint CurrentClockSpeedMHz,
    string SocketDesignation)
{
    public string CoreSummary => $"{NumberOfCores} cores / {NumberOfLogicalProcessors} threads";

    public static CpuInfo Unknown { get; } = new(
        UnknownValue.Unknown,
        UnknownValue.Unknown,
        0,
        0,
        0,
        0,
        UnknownValue.Unknown);
}
