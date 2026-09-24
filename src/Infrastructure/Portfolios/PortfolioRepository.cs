
using Application.Portfolios.Interfaces;
using Domain.Entities;
using Infrastructure.Common.Persistence.Contexts;
using Infrastructure.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Portfolios;

internal sealed class PortfolioRepository(AppDbContext dbContext) : Repository<Portfolio>(dbContext), IPortfolioRepository
{
    public Task<Portfolio?> GetByIdAsync(long id, CancellationToken cancellationToken)
        => dbContext.Set<Portfolio>().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<Portfolio?> GetPortfolioByNameAsync(long userId, string name, CancellationToken cancellationToken)
        => dbContext.Set<Portfolio>().FirstOrDefaultAsync(p => p.UserId == userId && p.Name == name, cancellationToken);
}
