
namespace Contracts.Portfolios;

public sealed record PortfolioHistoryPointResponse(
    DateTime Date,
    decimal Value
    );

public sealed record PortfolioAssetHistorySeriesResponse(
    long CryptoCurrencyId,
    string Symbol,
    List<PortfolioHistoryPointResponse> Points
    );

public sealed record PortfolioHistoryResponse(
    long PortfolioId,
    List<PortfolioHistoryPointResponse> Points,
    List<PortfolioAssetHistorySeriesResponse> ByAsset
    );
