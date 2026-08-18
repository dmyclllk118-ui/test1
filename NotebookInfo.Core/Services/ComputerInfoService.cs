using NotebookInfo.Core.Models;

namespace NotebookInfo.Core.Services;

/// <summary>Reads basic computer details from Win32_ComputerSystem, Win32_ComputerSystemProduct, and Win32_BIOS.</summary>
public sealed class ComputerInfoService(IWmiQueryService wmiQueryService) : IComputerInfoService
{
    public async Task<ComputerInfo> GetComputerInfoAsync(CancellationToken cancellationToken = default)
    {
        var info = ComputerInfo.Unknown;
        try
        {
            var systems = await SafeQueryAsync("Win32_ComputerSystem", cancellationToken);
            var products = await SafeQueryAsync("Win32_ComputerSystemProduct", cancellationToken);
            var biosRows = await SafeQueryAsync("Win32_BIOS", cancellationToken);
            var system = systems.FirstOrDefault();
            var product = products.FirstOrDefault();
            var bios = biosRows.FirstOrDefault();

            return new ComputerInfo(
                FirstKnown(system?.Text("Manufacturer"), product?.Text("Vendor")),
                FirstKnown(system?.Text("Model"), product?.Text("Name"), product?.Text("Version")),
                FirstKnown(product?.Text("IdentifyingNumber"), bios?.Text("SerialNumber")),
                FirstKnown(bios?.Text("SMBIOSBIOSVersion"), bios?.Text("Version"), bios?.Text("BIOSVersion")),
                FirstKnown(bios?.Text("SerialNumber"), product?.Text("IdentifyingNumber")),
                Environment.MachineName);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ServiceHelpers.LogFailure("Failed to query computer information", ex);
            return info;
        }
    }

    private async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> SafeQueryAsync(string className, CancellationToken cancellationToken)
    {
        try { return await wmiQueryService.QueryAsync(className, cancellationToken); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ServiceHelpers.LogFailure($"Failed to query {className}", ex);
            return Array.Empty<IReadOnlyDictionary<string, object?>>();
        }
    }

    private static string FirstKnown(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value) && value != UnknownValue.Unknown) ?? UnknownValue.Unknown;
    }
}
