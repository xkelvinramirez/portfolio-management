
namespace Contracts.Portfolios;

public sealed record UpdatePortfolioResponse(
    long Id,
    long UserId,
    string Name,
    string Description,
    DateTime CreatedAt,
    DateTime? UpdatedAt
    );
