using Application.Common.Repository;
using Domain.Entities;

namespace Application.CryptoCurrencies.Interfaces;

public interface ICryptoCurrencyRepository : IRepository<CryptoCurrency>
{
    Task<CryptoCurrency?> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<CryptoCurrency?> GetBySymbolAsync(string symbol, CancellationToken cancellationToken);
}
