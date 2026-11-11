using Domain.Entities;

namespace Application.MafAgent.Interfaces;

public interface IPendingActionRepository
{
    Task<PendingAction?> GetByIdAsync(Guid id, CancellationToken ct);

    void Add(PendingAction action);

    void Update(PendingAction action);

    /// <summary>
    /// Atomic Pending → Executing for the owner's unexpired action. Runs immediately, outside the unit of
    /// work: it is the "at most once" guarantee (spec AC5). True only if exactly one row was claimed.
    /// </summary>
    Task<bool> TryClaimAsync(Guid id, long userId, DateTime nowUtc, CancellationToken ct);

    /// <summary>Atomic Pending → Rejected (sets DecidedAt), same guard as <see cref="TryClaimAsync"/>.</summary>
    Task<bool> TryRejectAsync(Guid id, long userId, DateTime nowUtc, CancellationToken ct);

    /// <summary>
    /// Atomic Executing → Failed with <paramref name="errorCode"/>. Used only by the confirm exception path,
    /// after <see cref="ITrackedChangesDiscarder.DiscardAll"/>.
    /// </summary>
    Task<bool> TryMarkFailedAsync(Guid id, string errorCode, DateTime nowUtc, CancellationToken ct);
}
