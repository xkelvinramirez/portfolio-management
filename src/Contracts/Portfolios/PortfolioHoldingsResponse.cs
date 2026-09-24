
namespace Contracts.Portfolios;

public sealed record PortfolioHoldingItemResponse(
    long CryptoCurrencyId,
    string CryptoCurrencySymbol,
    string CryptoCurrencyName,
    long ExchangeId,
    string ExchangeName,
    decimal Quantity,
    decimal PricePerUnit,
    decimal Value,
    DateTime RecordedAt
    );

public sealed record PortfolioHoldingsResponse(
    long PortfolioId,
    DateTime Date,
    List<PortfolioHoldingItemResponse> Holdings
    );
