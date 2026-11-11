namespace Contracts.MafAgent;

/// <summary>
/// A write the agent proposed and that only runs once the user confirms <see cref="Id"/>.
/// <see cref="Values"/> is the exact request that will be applied (dates as ISO-8601 UTC with Z);
/// <see cref="Before"/> is the current state for edits (null for creates and system actions);
/// <see cref="Summary"/> names the operation and changed fields without echoing user free text.
/// </summary>
public sealed record PendingActionDto(
    Guid Id,
    string Operation,
    string Summary,
    IReadOnlyDictionary<string, string?> Values,
    IReadOnlyDictionary<string, string?>? Before,
    string ExpiresAt);
