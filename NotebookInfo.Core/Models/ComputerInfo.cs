namespace NotebookInfo.Core.Models;

/// <summary>Basic computer and firmware identity information.</summary>
public sealed record ComputerInfo(
    string Manufacturer,
    string Model,
    string SerialNumber,
    string BIOSVersion,
    string BIOSSerialNumber,
    string WindowsMachineName)
{
    public static ComputerInfo Unknown { get; } = new(
        UnknownValue.Unknown,
        UnknownValue.Unknown,
        UnknownValue.Unknown,
        UnknownValue.Unknown,
        UnknownValue.Unknown,
        Environment.MachineName);
}
