using Application.MafAgent.Interfaces;
using Domain.Entities;
using Infrastructure.Common.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace WebApi.MinimalAPI.Tests.MafAgent.Http;

/// <summary>
/// Test double for IPendingActionRepository on EF InMemory, which does not support ExecuteUpdate
/// (analyze G3). CRUD goes through the request's AppDbContext like the real repository. The three
/// transitions mimic the real conditional UPDATE: same guards, applied immediately in a SEPARATE
/// context (so nothing staged on the request's context is flushed) and serialized by a lock (so
/// concurrent callers get "exactly one wins"). The real atomicity is proven against Postgres in
/// PendingActionRepositoryClaimTests; this double only makes HTTP tests runnable.
/// </summary>
internal sealed class InMemoryTransitionsPendingActionRepository(
    AppDbContext dbContext,
    DbContextOptions<AppDbContext> options) : IPendingActionRepository
{
    private static readonly object Gate = new();

    public Task<PendingAction?> GetByIdAsync(Guid id, CancellationToken ct) =>
        dbContext.PendingActions.FirstOrDefaultAsync(a => a.Id == id, ct);

    public void Add(PendingAction action) => dbContext.PendingActions.Add(action);

    public void Update(PendingAction action) => dbContext.PendingActions.Update(action);

    public Task<bool> TryClaimAsync(Guid id, long userId, DateTime nowUtc, CancellationToken ct) =>
        Task.FromResult(Transition(id, a => IsClaimable(a, userId, nowUtc), a => a.MarkExecuting()));

    public Task<bool> TryRejectAsync(Guid id, long userId, DateTime nowUtc, CancellationToken ct) =>
        Task.FromResult(Transition(id, a => IsClaimable(a, userId, nowUtc), a => a.MarkRejected(nowUtc)));

    public Task<bool> TryMarkFailedAsync(Guid id, string errorCode, DateTime nowUtc, CancellationToken ct) =>
        Task.FromResult(Transition(
            id,
            a => a.Status == PendingActionStatus.Executing,
            a => a.MarkFailed(errorCode, nowUtc)));

    // Same WHERE as the real PendingActionRepository: owner, Pending and not expired (ExpiresAt > now).
    private static bool IsClaimable(PendingAction action, long userId, DateTime nowUtc) =>
        action.UserId == userId &&
        action.Status == PendingActionStatus.Pending &&
        action.ExpiresAt > nowUtc;

    private bool Transition(Guid id, Func<PendingAction, bool> guard, Action<PendingAction> apply)
    {
        lock (Gate)
        {
            using var isolated = new AppDbContext(options);
            PendingAction? action = isolated.PendingActions.SingleOrDefault(a => a.Id == id);
            if (action is null || !guard(action))
            {
                return false;
            }

            apply(action);
            isolated.SaveChanges();
            return true;
        }
    }
}
