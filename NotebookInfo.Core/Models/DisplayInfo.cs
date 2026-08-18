namespace NotebookInfo.Core.Models;

/// <summary>Display identity and current mode information collected from EDID and Windows Display APIs.</summary>
public sealed record DisplayInfo(
    string ManufacturerCode,
    string Model,
    string SerialNumber,
    double PhysicalWidthCentimeters,
    double PhysicalHeightCentimeters,
    uint CurrentHorizontalResolution,
    uint CurrentVerticalResolution,
    uint CurrentRefreshRate,
    string DeviceName,
    string DeviceString)
{
    public string DisplayPhysicalSize => PhysicalWidthCentimeters <= 0 || PhysicalHeightCentimeters <= 0
        ? UnknownValue.Unknown
        : $"{PhysicalWidthCentimeters:0.#} x {PhysicalHeightCentimeters:0.#} cm";

    public string DisplayResolution => CurrentHorizontalResolution == 0 || CurrentVerticalResolution == 0
        ? UnknownValue.Unknown
        : $"{CurrentHorizontalResolution} x {CurrentVerticalResolution}";

    public string DisplayRefreshRate => CurrentRefreshRate == 0 ? UnknownValue.Unknown : $"{CurrentRefreshRate} Hz";

    public static DisplayInfo Unknown { get; } = new(
        UnknownValue.Unknown,
        UnknownValue.Unknown,
        UnknownValue.Unknown,
        0,
        0,
        0,
        0,
        0,
        UnknownValue.Unknown,
        UnknownValue.Unknown);
}
