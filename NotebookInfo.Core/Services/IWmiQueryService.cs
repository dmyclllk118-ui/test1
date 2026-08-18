namespace NotebookInfo.Core.Services;

/// <summary>Abstraction over WMI queries to make hardware services testable.</summary>
public interface IWmiQueryService
{
    Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(string className, CancellationToken cancellationToken = default);
}
