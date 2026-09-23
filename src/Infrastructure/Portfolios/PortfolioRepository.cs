
using Application.Portfolios.Interfaces;
using Domain.Entities;
using Infrastructure.Common.Persistence.Contexts;
using Infrastructure.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Portfolios;

internal sealed class PortfolioRepository(AppDbContext dbContext) : Repository<Portfolio>(dbContext), IPortfolioRepository
{
    public Task<Portfolio?> GetPortfolioByNameAsync(string name, CancellationToken cancellationToken)
        => dbContext.Set<Portfolio>().FirstOrDefaultAsync(p => p.Name == name, cancellationToken);
}
