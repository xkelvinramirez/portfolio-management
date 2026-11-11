using System.Globalization;
using Application.MafAgent.Payloads;

namespace Application.MafAgent;

/// <summary>
/// The comparable "field → invariant string" form of a resource (plan §3 "Desfase"): decimals "G29",
/// dates round-trip "O" in UTC (full precision), booleans "true"/"false". Built the same way when
/// proposing (Before) and when confirming (current values), so the two can be compared field by field.
/// </summary>
public static class FieldSnapshot
{
    public static string? Format(object? value) => value switch
    {
        null => null,
        decimal d => d.ToString("G29", CultureInfo.InvariantCulture),
        DateTime dt => AsUtc(dt).ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset dto => dto.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };

    /// <summary>The fields of the full update request carried by an edit payload (the target id excluded).</summary>
    public static IReadOnlyDictionary<string, string?> OfRequestFields(object payload) =>
        PendingActionPayloads.Properties(payload.GetType())
            .Where(p => !PendingActionPayloads.IsTarget(p))
            .ToDictionary(p => p.Name, p => Format(p.GetValue(payload)));

    // Npgsql returns timestamptz as Utc; a provider that drops the Kind (Unspecified) still stores UTC.
    private static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value.ToUniversalTime(),
    };
}
