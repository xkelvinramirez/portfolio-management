
using Application.CryptoCurrencies.Interfaces;
using Domain.Entities;
using Infrastructure.Common.Persistence.Contexts;
using Infrastructure.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.CryptoCurrencies;

internal sealed class CryptoCurrencyRepository(AppDbContext dbContext) : Repository<CryptoCurrency>(dbContext), ICryptoCurrencyRepository
{
    public Task<CryptoCurrency?> GetByIdAsync(long id, CancellationToken cancellationToken)
        => dbContext.Set<CryptoCurrency>().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<CryptoCurrency?> GetBySymbolAsync(string symbol, CancellationToken cancellationToken)
        => dbContext.Set<CryptoCurrency>().FirstOrDefaultAsync(c => c.Symbol == symbol, cancellationToken);
}
