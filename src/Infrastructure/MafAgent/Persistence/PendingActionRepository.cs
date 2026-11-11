using Application.MafAgent.Interfaces;
using Domain.Entities;
using Infrastructure.Common.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.MafAgent.Persistence;

internal sealed class PendingActionRepository(AppDbContext dbContext) : IPendingActionRepository
{
    public Task<PendingAction?> GetByIdAsync(Guid id, CancellationToken ct) =>
        dbContext.PendingActions.FirstOrDefaultAsync(a => a.Id == id, ct);

    public void Add(PendingAction action) => dbContext.PendingActions.Add(action);

    public void Update(PendingAction action) => dbContext.PendingActions.Update(action);

    // The three methods below are the ONLY writes outside the unit of work (plan §0 Commit). Each is a
    // single conditional UPDATE executed immediately: the database row lock makes the WHERE re-evaluate
    // for a concurrent caller, so exactly one transition can win (spec AC5).

    public async Task<bool> TryClaimAsync(Guid id, long userId, DateTime nowUtc, CancellationToken ct) =>
        await PendingOf(id, userId, nowUtc)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, PendingActionStatus.Executing), ct) == 1;

    public async Task<bool> TryRejectAsync(Guid id, long userId, DateTime nowUtc, CancellationToken ct) =>
        await PendingOf(id, userId, nowUtc)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.Status, PendingActionStatus.Rejected)
                .SetProperty(a => a.DecidedAt, nowUtc), ct) == 1;

    public async Task<bool> TryMarkFailedAsync(Guid id, string errorCode, DateTime nowUtc, CancellationToken ct) =>
        await dbContext.PendingActions
            .Where(a => a.Id == id && a.Status == PendingActionStatus.Executing)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.Status, PendingActionStatus.Failed)
                .SetProperty(a => a.ErrorCode, errorCode)
                .SetProperty(a => a.DecidedAt, nowUtc), ct) == 1;

    private IQueryable<PendingAction> PendingOf(Guid id, long userId, DateTime nowUtc) =>
        dbContext.PendingActions.Where(a =>
            a.Id == id &&
            a.UserId == userId &&
            a.Status == PendingActionStatus.Pending &&
            a.ExpiresAt > nowUtc);
}
