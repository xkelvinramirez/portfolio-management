using Application.MafAgent.Interfaces;
using Infrastructure.Common.Persistence.Contexts;

namespace Infrastructure.MafAgent.Persistence;

/// <summary>Clears the scoped AppDbContext change tracker (plan §0 "Tras el reclamo", analyze H3).</summary>
internal sealed class TrackedChangesDiscarder(AppDbContext dbContext) : ITrackedChangesDiscarder
{
    public void DiscardAll() => dbContext.ChangeTracker.Clear();
}
