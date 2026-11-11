namespace Application.MafAgent;

/// <summary>Spec AC18: an edit must not silently overwrite a change the user never saw.</summary>
public static class StaleCheck
{
    /// <summary>True if any field of <paramref name="before"/> is missing from, or different in, <paramref name="current"/>.</summary>
    /// <remarks>
    /// Only the fields of <paramref name="before"/> are compared. That is complete because PendingActionFactory
    /// only accepts a Before with exactly the fields of the full update request.
    /// </remarks>
    public static bool IsStale(IReadOnlyDictionary<string, string?> before, IReadOnlyDictionary<string, string?> current) =>
        before.Any(field => !current.TryGetValue(field.Key, out string? now) || !string.Equals(now, field.Value, StringComparison.Ordinal));
}
