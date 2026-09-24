
namespace Contracts.PortfolioEntries;

public sealed record UpdatePortfolioEntryResponse(
    long Id,
    long PortfolioId,
    long CryptoCurrencyId,
    long ExchangeId,
    decimal Quantity,
    decimal PricePerUnit,
    DateTime RecordedAt
    );
