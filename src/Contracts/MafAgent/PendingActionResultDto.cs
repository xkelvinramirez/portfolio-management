namespace Contracts.MafAgent;

/// <summary>
/// Outcome of confirming a pending action. Once the action is claimed, every outcome is returned as
/// 200 with a <see cref="Status"/> of Succeeded, Failed or Stale, so its final state is persisted.
/// </summary>
public sealed record PendingActionResultDto(
    Guid Id,
    string Operation,
    string Status,
    object? Result,
    PendingActionErrorDto? Error);

public sealed record PendingActionErrorDto(string Code, string Description);
