using NotebookInfo.Core.Models;
using NotebookInfo.Core.Services;
using Xunit;

namespace NotebookInfo.Tests;

public sealed class HardwareModelTests
{
    [Fact]
    public void CpuModel_ProvidesCoreSummary()
    {
        var cpu = new CpuInfo("CPU", "Vendor", 8, 16, 4800, 1200, "Socket");
        Assert.Equal("8 cores / 16 threads", cpu.CoreSummary);
    }

    [Fact]
    public void MemoryCapacity_IsConvertedFromBytesToGb()
    {
        var module = new MemoryModuleInfo(16UL * 1024 * 1024 * 1024, 3200, "Vendor", "Part", "Serial", 26, "DDR4", 3200);
        var memory = new MemoryInfo(new[] { module, module });
        Assert.Equal("16 GB", module.DisplayCapacity);
        Assert.Equal("32 GB", memory.DisplayTotalCapacity);
    }

    [Theory]
    [InlineData(24, "DDR3")]
    [InlineData(26, "DDR4")]
    [InlineData(34, "DDR5")]
    [InlineData(30, "LPDDR4")]
    [InlineData(35, "LPDDR5")]
    [InlineData(0, "Unknown")]
    public void SmbiosMemoryType_IsConvertedToDisplayName(ushort type, string expected)
    {
        Assert.Equal(expected, MemoryTypeConverter.ToDisplayName(type));
    }

    [Fact]
    public void GpuModel_FormatsResolutionAndRefreshRate()
    {
        var gpu = new GpuInfo("GPU", 8UL * 1024 * 1024 * 1024, "1.2.3", "Processor", 2560, 1600, 120);
        Assert.Equal("8 GB", gpu.DisplayAdapterRAM);
        Assert.Equal("2560 x 1600", gpu.DisplayResolution);
        Assert.Equal("120 Hz", gpu.DisplayRefreshRate);
    }

    [Fact]
    public async Task QueryFailure_DoesNotCrashCpuService()
    {
        var service = new CpuInfoService(new ThrowingWmiQueryService());
        var cpus = await service.GetCpuInfoAsync();
        Assert.Single(cpus);
        Assert.Equal("Unknown", cpus[0].Name);
    }

    [Fact]
    public async Task EmptyValues_AreDisplayedAsUnknown()
    {
        var service = new CpuInfoService(new FakeWmiQueryService(new Dictionary<string, object?> { ["Name"] = null }));
        var cpus = await service.GetCpuInfoAsync();
        Assert.Equal("Unknown", cpus[0].Name);
        Assert.Equal("Unknown", cpus[0].Manufacturer);
    }

    [Fact]
    public async Task ComputerService_FallsBackAcrossOemSpecificValues()
    {
        var service = new ComputerInfoService(new ClassBasedWmiQueryService(new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>
        {
            ["Win32_ComputerSystem"] = new IReadOnlyDictionary<string, object?>[] { new Dictionary<string, object?> { ["Manufacturer"] = "To be filled by O.E.M.", ["Model"] = "" } },
            ["Win32_ComputerSystemProduct"] = new IReadOnlyDictionary<string, object?>[] { new Dictionary<string, object?> { ["Vendor"] = "Contoso", ["Name"] = "Notebook 14", ["IdentifyingNumber"] = "System Serial Number" } },
            ["Win32_BIOS"] = new IReadOnlyDictionary<string, object?>[] { new Dictionary<string, object?> { ["SerialNumber"] = "ABC123", ["BIOSVersion"] = new[] { "BIOS-A", "BIOS-B" } } }
        }));

        var computer = await service.GetComputerInfoAsync();

        Assert.Equal("Contoso", computer.Manufacturer);
        Assert.Equal("Notebook 14", computer.Model);
        Assert.Equal("ABC123", computer.SerialNumber);
        Assert.Equal("BIOS-A, BIOS-B", computer.BIOSVersion);
    }

    [Fact]
    public async Task NumericValues_AreParsedFromNativeWmiTypes()
    {
        var service = new GpuInfoService(new FakeWmiQueryService(new Dictionary<string, object?>
        {
            ["Name"] = "GPU",
            ["AdapterRAM"] = 4294967296UL,
            ["CurrentHorizontalResolution"] = 1920u,
            ["CurrentVerticalResolution"] = 1080,
            ["CurrentRefreshRate"] = (ushort)60
        }));

        var gpus = await service.GetGpuInfoAsync();

        Assert.Equal("4 GB", gpus[0].DisplayAdapterRAM);
        Assert.Equal("1920 x 1080", gpus[0].DisplayResolution);
        Assert.Equal("60 Hz", gpus[0].DisplayRefreshRate);
    }

    [Fact]
    public void EdidParser_ReadsManufacturerModelSerialAndPhysicalSize()
    {
        var edid = CreateEdid("DEL", "U2720Q", "SN12345", 60, 34);

        var parsed = EdidParser.Parse(edid);

        Assert.Equal("DEL", parsed.ManufacturerCode);
        Assert.Equal("U2720Q", parsed.Model);
        Assert.Equal("SN12345", parsed.SerialNumber);
        Assert.Equal(60, parsed.PhysicalWidthCentimeters);
        Assert.Equal(34, parsed.PhysicalHeightCentimeters);
    }

    [Fact]
    public void DisplayModel_FormatsSizeResolutionAndRefreshRate()
    {
        var display = new DisplayInfo("DEL", "U2720Q", "SN12345", 60, 34, 3840, 2160, 60, "\\\\.\\DISPLAY1", "Generic PnP Monitor");

        Assert.Equal("60 x 34 cm", display.DisplayPhysicalSize);
        Assert.Equal("3840 x 2160", display.DisplayResolution);
        Assert.Equal("60 Hz", display.DisplayRefreshRate);
    }

    private static byte[] CreateEdid(string manufacturer, string model, string serial, byte widthCentimeters, byte heightCentimeters)
    {
        var edid = new byte[128];
        var header = new byte[] { 0x00, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00 };
        Array.Copy(header, edid, header.Length);
        var manufacturerValue = EncodeManufacturer(manufacturer);
        edid[8] = (byte)(manufacturerValue >> 8);
        edid[9] = (byte)(manufacturerValue & 0xFF);
        edid[10] = 0x34;
        edid[11] = 0x12;
        edid[21] = widthCentimeters;
        edid[22] = heightCentimeters;
        WriteDescriptor(edid, 54, 0xFC, model);
        WriteDescriptor(edid, 72, 0xFF, serial);
        return edid;
    }

    private static ushort EncodeManufacturer(string manufacturer)
    {
        return (ushort)(((manufacturer[0] - 'A' + 1) << 10) | ((manufacturer[1] - 'A' + 1) << 5) | (manufacturer[2] - 'A' + 1));
    }

    private static void WriteDescriptor(byte[] edid, int offset, byte descriptorType, string value)
    {
        edid[offset] = 0;
        edid[offset + 1] = 0;
        edid[offset + 2] = 0;
        edid[offset + 3] = descriptorType;
        edid[offset + 4] = 0;
        var bytes = System.Text.Encoding.ASCII.GetBytes(value + "\n");
        Array.Copy(bytes, 0, edid, offset + 5, Math.Min(bytes.Length, 13));
    }

    private sealed class ThrowingWmiQueryService : IWmiQueryService
    {
        public Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(string className, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated WMI failure");
    }

    private sealed class FakeWmiQueryService(IReadOnlyDictionary<string, object?> row) : IWmiQueryService
    {
        public Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(string className, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(new[] { row });
    }

    private sealed class ClassBasedWmiQueryService(IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>> rowsByClass) : IWmiQueryService
    {
        public Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(string className, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(rowsByClass.TryGetValue(className, out var rows) ? rows : Array.Empty<IReadOnlyDictionary<string, object?>>());
    }
}
