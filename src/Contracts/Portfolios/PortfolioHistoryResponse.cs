
namespace Contracts.Portfolios;

public sealed record PortfolioHistoryPointResponse(
    DateTime Date,
    decimal Value
    );

public sealed record PortfolioHistoryResponse(
    long PortfolioId,
    List<PortfolioHistoryPointResponse> Points
    );
