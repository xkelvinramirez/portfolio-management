
using Application.PortfolioEntries.Interfaces;
using Domain.Entities;
using Infrastructure.Common.Persistence.Contexts;
using Infrastructure.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.PortfolioEntries;

internal sealed class PortfolioEntryRepository(AppDbContext dbContext) : Repository<PortfolioEntry>(dbContext), IPortfolioEntryRepository
{
    public Task<PortfolioEntry?> GetByIdAsync(long id, CancellationToken cancellationToken)
        => dbContext.Set<PortfolioEntry>().FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
}
