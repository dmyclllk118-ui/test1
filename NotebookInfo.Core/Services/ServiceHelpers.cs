using System.Collections;
using System.Diagnostics;
using NotebookInfo.Core.Models;

namespace NotebookInfo.Core.Services;

internal static class ServiceHelpers
{
    private static readonly string[] PlaceholderValues =
    {
        "to be filled by o.e.m.",
        "to be filled by oem",
        "default string",
        "system serial number",
        "none",
        "not specified",
        "not available",
        "n/a",
        "unknown"
    };

    public static string Text(this IReadOnlyDictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value))
        {
            return UnknownValue.Unknown;
        }

        var text = ConvertToText(value);
        return IsPlaceholder(text) ? UnknownValue.Unknown : text;
    }

    public static uint UInt32(this IReadOnlyDictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value))
        {
            return 0;
        }

        try
        {
            return value switch
            {
                null => 0,
                uint unsignedValue => unsignedValue,
                int signedValue when signedValue >= 0 => (uint)signedValue,
                ushort unsignedShort => unsignedShort,
                short signedShort when signedShort >= 0 => (uint)signedShort,
                ulong unsignedLong when unsignedLong <= uint.MaxValue => (uint)unsignedLong,
                long signedLong when signedLong >= 0 && signedLong <= uint.MaxValue => (uint)signedLong,
                _ when uint.TryParse(value.ToString(), out var parsed) => parsed,
                _ => 0
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFailure($"Failed to parse {key} as UInt32", ex);
            return 0;
        }
    }

    public static ushort UInt16(this IReadOnlyDictionary<string, object?> row, string key)
    {
        var value = row.UInt32(key);
        return value <= ushort.MaxValue ? (ushort)value : (ushort)0;
    }

    public static ulong UInt64(this IReadOnlyDictionary<string, object?> row, string key)
    {
        if (!row.TryGetValue(key, out var value))
        {
            return 0;
        }

        try
        {
            return value switch
            {
                null => 0,
                ulong unsignedValue => unsignedValue,
                long signedValue when signedValue >= 0 => (ulong)signedValue,
                uint unsignedInt => unsignedInt,
                int signedInt when signedInt >= 0 => (ulong)signedInt,
                ushort unsignedShort => unsignedShort,
                short signedShort when signedShort >= 0 => (ulong)signedShort,
                _ when ulong.TryParse(value.ToString(), out var parsed) => parsed,
                _ => 0
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFailure($"Failed to parse {key} as UInt64", ex);
            return 0;
        }
    }

    public static void LogFailure(string message, Exception ex) => Trace.TraceWarning($"{message}: {ex.GetType().Name}");

    private static string ConvertToText(object? value)
    {
        if (value is null)
        {
            return UnknownValue.Unknown;
        }

        if (value is string text)
        {
            return UnknownValue.Clean(text);
        }

        if (value is IEnumerable enumerable and not byte[])
        {
            var values = enumerable.Cast<object?>()
                .Select(item => item?.ToString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!.Trim())
                .ToArray();

            return values.Length == 0 ? UnknownValue.Unknown : string.Join(", ", values);
        }

        return UnknownValue.Clean(value.ToString());
    }

    private static bool IsPlaceholder(string value)
    {
        return PlaceholderValues.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
