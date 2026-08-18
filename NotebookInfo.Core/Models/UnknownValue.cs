namespace NotebookInfo.Core.Models;

/// <summary>Shared display constants for unavailable hardware data.</summary>
public static class UnknownValue
{
    public const string Unknown = "Unknown";
    public const string Unavailable = "Unavailable";

    public static string Clean(string? value) => string.IsNullOrWhiteSpace(value) ? Unknown : value.Trim();
}
