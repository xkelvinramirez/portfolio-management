using Application.Common.Repository;
using Domain.Entities;

namespace Application.Portfolios.Interfaces;

public interface IPortfolioRepository : IRepository<Portfolio>
{
    Task<Portfolio?> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<Portfolio?> GetPortfolioByNameAsync(string name, CancellationToken cancellationToken);
}
