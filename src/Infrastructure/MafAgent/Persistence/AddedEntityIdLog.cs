using Application.MafAgent.Interfaces;
using Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Infrastructure.MafAgent.Persistence;

/// <summary>
/// Records the database id of every <see cref="Entity"/> inserted by a successful SaveChanges in this
/// scope. Create handlers build their response before the unit of work commits, so the identity id they
/// return is still 0; the MAF confirm flow reads the real id from here instead (analyze G2).
/// Registered as a scoped interceptor on AppDbContext; one instance per request.
/// </summary>
internal sealed class AddedEntityIdLog : SaveChangesInterceptor, IAddedEntityIdLog
{
    private readonly Dictionary<Type, long> _lastAddedIdByType = [];
    private List<Entity> _pendingInserts = [];

    public long? LastAddedId<TEntity>() where TEntity : Entity =>
        _lastAddedIdByType.TryGetValue(typeof(TEntity), out long id) ? id : null;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        CaptureInserts(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        CaptureInserts(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        RecordIds();
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        RecordIds();
        return ValueTask.FromResult(result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => _pendingInserts = [];

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _pendingInserts = [];
        return Task.CompletedTask;
    }

    private void CaptureInserts(DbContext? context)
    {
        _pendingInserts = context is null
            ? []
            : context.ChangeTracker.Entries<Entity>()
                .Where(e => e.State == EntityState.Added)
                .Select(e => e.Entity)
                .ToList();
    }

    // After the save, identity ids are written back onto the entity instances.
    private void RecordIds()
    {
        foreach (Entity entity in _pendingInserts)
        {
            _lastAddedIdByType[entity.GetType()] = entity.Id;
        }

        _pendingInserts = [];
    }
}
