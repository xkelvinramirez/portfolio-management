using Domain.Common;

namespace Application.MafAgent.Interfaces;

/// <summary>
/// Database id of the last <typeparamref name="TEntity"/> inserted by a SaveChanges in this scope. Needed
/// because create handlers build their response before the unit of work commits, when the identity id is
/// still 0 (analyze G2).
/// </summary>
public interface IAddedEntityIdLog
{
    long? LastAddedId<TEntity>() where TEntity : Entity;
}
