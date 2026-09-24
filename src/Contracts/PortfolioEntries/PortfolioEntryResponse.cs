
namespace Contracts.PortfolioEntries;

public sealed record PortfolioEntryResponse(
    long Id,
    long PortfolioId,
    long CryptoCurrencyId,
    long ExchangeId,
    decimal Quantity,
    decimal PricePerUnit,
    DateTime RecordedAt
    );
