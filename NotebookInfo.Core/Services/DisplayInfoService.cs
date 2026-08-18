using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using NotebookInfo.Core.Models;

namespace NotebookInfo.Core.Services;

/// <summary>Reads display EDID identity from the registry and current display modes from the Windows Display API.</summary>
public sealed class DisplayInfoService : IDisplayInfoService
{
    public Task<IReadOnlyList<DisplayInfo>> GetDisplayInfoAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<DisplayInfo>>(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var modes = EnumerateDisplayModes();
                var edids = ReadMonitorEdids();
                var unmatchedEdids = edids.Where(edid => modes.All(mode => !string.Equals(MonitorHardwareKey(mode.MonitorDeviceId), MonitorHardwareKey(edid.DeviceInstanceId), StringComparison.OrdinalIgnoreCase))).ToArray();
                var count = modes.Count + unmatchedEdids.Length;
                if (count == 0)
                {
                    return new[] { DisplayInfo.Unknown };
                }

                var displays = new List<DisplayInfo>(count);
                for (var index = 0; index < count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var mode = index < modes.Count ? modes[index] : DisplayModeInfo.Unknown;
                    var edid = mode.MonitorDeviceId == UnknownValue.Unknown
                        ? EdidRegistryInfo.Unknown
                        : edids.FirstOrDefault(item => string.Equals(MonitorHardwareKey(item.DeviceInstanceId), MonitorHardwareKey(mode.MonitorDeviceId), StringComparison.OrdinalIgnoreCase), EdidRegistryInfo.Unknown);
                    if (index >= modes.Count)
                    {
                        edid = unmatchedEdids[index - modes.Count];
                    }
                    displays.Add(new DisplayInfo(
                        edid.Edid.ManufacturerCode,
                        edid.Edid.Model,
                        edid.Edid.SerialNumber,
                        edid.Edid.PhysicalWidthCentimeters,
                        edid.Edid.PhysicalHeightCentimeters,
                        mode.Width,
                        mode.Height,
                        mode.RefreshRate,
                        mode.DeviceName,
                        mode.DeviceString));
                }

                return displays;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ServiceHelpers.LogFailure("Failed to query display information", ex);
                return new[] { DisplayInfo.Unknown };
            }
        }, cancellationToken);
    }

    private static IReadOnlyList<DisplayModeInfo> EnumerateDisplayModes()
    {
        var displays = new List<DisplayModeInfo>();
        for (uint index = 0; ; index++)
        {
            var device = DisplayDevice.Create();
            if (!NativeMethods.EnumDisplayDevices(null, index, ref device, 0))
            {
                break;
            }

            if ((device.StateFlags & DisplayDeviceStateFlags.AttachedToDesktop) == 0)
            {
                continue;
            }

            var mode = DevMode.Create();
            var width = 0u;
            var height = 0u;
            var refreshRate = 0u;
            if (NativeMethods.EnumDisplaySettings(device.DeviceName, NativeMethods.EnumCurrentSettings, ref mode))
            {
                width = mode.PelsWidth;
                height = mode.PelsHeight;
                refreshRate = mode.DisplayFrequency;
            }
            else
            {
                ServiceHelpers.LogFailure($"Failed to query display mode for display index {index}", new Win32Exception(Marshal.GetLastWin32Error()));
            }

            displays.Add(new DisplayModeInfo(
                UnknownValue.Clean(device.DeviceName),
                UnknownValue.Clean(device.DeviceString),
                GetMonitorDeviceId(device.DeviceName),
                width,
                height,
                refreshRate));
        }

        return displays;
    }

    private static IReadOnlyList<EdidRegistryInfo> ReadMonitorEdids()
    {
        var monitors = new List<EdidRegistryInfo>();
        try
        {
            using var displayRoot = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\DISPLAY");
            if (displayRoot is null)
            {
                return monitors;
            }

            foreach (var manufacturerKeyName in displayRoot.GetSubKeyNames().Order(StringComparer.OrdinalIgnoreCase))
            {
                using var manufacturerKey = displayRoot.OpenSubKey(manufacturerKeyName);
                if (manufacturerKey is null)
                {
                    continue;
                }

                foreach (var monitorKeyName in manufacturerKey.GetSubKeyNames().Order(StringComparer.OrdinalIgnoreCase))
                {
                    using var monitorKey = manufacturerKey.OpenSubKey(monitorKeyName);
                    using var parametersKey = monitorKey?.OpenSubKey("Device Parameters");
                    if (parametersKey?.GetValue("EDID") is byte[] edid)
                    {
                        monitors.Add(new EdidRegistryInfo($@"MONITOR\{manufacturerKeyName}\{monitorKeyName}", EdidParser.Parse(edid)));
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ServiceHelpers.LogFailure("Failed to read monitor EDID registry data", ex);
        }

        return monitors;
    }

    private static string MonitorHardwareKey(string deviceInstanceId)
    {
        if (deviceInstanceId == UnknownValue.Unknown)
        {
            return UnknownValue.Unknown;
        }

        var parts = deviceInstanceId.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? $"{parts[0]}\\{parts[1]}" : deviceInstanceId;
    }

    private static string GetMonitorDeviceId(string displayDeviceName)
    {
        try
        {
            var monitor = DisplayDevice.Create();
            return NativeMethods.EnumDisplayDevices(displayDeviceName, 0, ref monitor, 0)
                ? UnknownValue.Clean(monitor.DeviceId)
                : UnknownValue.Unknown;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ServiceHelpers.LogFailure("Failed to query monitor device id", ex);
            return UnknownValue.Unknown;
        }
    }

    private sealed record EdidRegistryInfo(string DeviceInstanceId, EdidInfo Edid)
    {
        public static EdidRegistryInfo Unknown { get; } = new(UnknownValue.Unknown, EdidInfo.Unknown);
    }

    private sealed record DisplayModeInfo(string DeviceName, string DeviceString, string MonitorDeviceId, uint Width, uint Height, uint RefreshRate)
    {
        public static DisplayModeInfo Unknown { get; } = new(
            UnknownValue.Unknown,
            UnknownValue.Unknown,
            UnknownValue.Unknown,
            0,
            0,
            0);
    }

    [Flags]
    private enum DisplayDeviceStateFlags : int
    {
        AttachedToDesktop = 0x00000001
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public int Cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public DisplayDeviceStateFlags StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;

        public static DisplayDevice Create() => new() { Cb = Marshal.SizeOf<DisplayDevice>() };
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DevMode
    {
        private const int CchDeviceName = 32;
        private const int CchFormName = 32;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchDeviceName)] public string DeviceName;
        public ushort SpecVersion;
        public ushort DriverVersion;
        public ushort Size;
        public ushort DriverExtra;
        public uint Fields;
        public int PositionX;
        public int PositionY;
        public uint DisplayOrientation;
        public uint DisplayFixedOutput;
        public short Color;
        public short Duplex;
        public short YResolution;
        public short TTOption;
        public short Collate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchFormName)] public string FormName;
        public ushort LogPixels;
        public uint BitsPerPel;
        public uint PelsWidth;
        public uint PelsHeight;
        public uint DisplayFlags;
        public uint DisplayFrequency;
        public uint ICMMethod;
        public uint ICMIntent;
        public uint MediaType;
        public uint DitherType;
        public uint Reserved1;
        public uint Reserved2;
        public uint PanningWidth;
        public uint PanningHeight;

        public static DevMode Create() => new() { Size = (ushort)Marshal.SizeOf<DevMode>() };
    }

    private static class NativeMethods
    {
        public const int EnumCurrentSettings = -1;

        [DllImport("user32.dll", EntryPoint = "EnumDisplayDevicesW", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DisplayDevice lpDisplayDevice, uint dwFlags);

        [DllImport("user32.dll", EntryPoint = "EnumDisplaySettingsW", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DevMode lpDevMode);
    }
}
