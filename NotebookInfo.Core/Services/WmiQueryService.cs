using System.Management;

namespace NotebookInfo.Core.Services;

/// <summary>Executes WMI queries against the local CIMV2 namespace.</summary>
public sealed class WmiQueryService : IWmiQueryService
{
    public Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(string className, CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rows = new List<IReadOnlyDictionary<string, object?>>();
            using var searcher = new ManagementObjectSearcher($"SELECT * FROM {className}");
            using var results = searcher.Get();
            foreach (ManagementObject item in results)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (PropertyData property in item.Properties)
                {
                    row[property.Name] = property.Value;
                }
                rows.Add(row);
                item.Dispose();
            }
            return rows;
        }, cancellationToken);
    }
}
