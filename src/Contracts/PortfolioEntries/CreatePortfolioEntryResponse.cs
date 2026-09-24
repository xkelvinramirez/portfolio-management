
namespace Contracts.PortfolioEntries;

public sealed record CreatePortfolioEntryResponse(
    long Id,
    long PortfolioId,
    long CryptoCurrencyId,
    long ExchangeId,
    decimal Quantity,
    decimal PricePerUnit,
    DateTime RecordedAt
    );
