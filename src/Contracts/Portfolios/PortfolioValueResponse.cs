
namespace Contracts.Portfolios;

public sealed record PortfolioValueResponse(
    long PortfolioId,
    DateTime Date,
    decimal Value
    );
