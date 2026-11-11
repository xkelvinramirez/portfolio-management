namespace Domain.Entities;

/// <summary>
/// Lifecycle of a <see cref="PendingAction"/>: Pending → Executing → Succeeded | Failed | Stale, or Pending → Rejected.
/// Expiry is computed from ExpiresAt, not stored as a status.
/// </summary>
public enum PendingActionStatus
{
    Pending,
    Executing,
    Succeeded,
    Failed,
    Rejected,
    Stale,
}
