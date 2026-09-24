using Application.Common.Repository;
using Domain.Entities;

namespace Application.PortfolioEntries.Interfaces;

public interface IPortfolioEntryRepository : IRepository<PortfolioEntry>
{
    Task<PortfolioEntry?> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<List<PortfolioEntry>> GetByPortfolioIdAsync(long portfolioId, CancellationToken cancellationToken);
}
