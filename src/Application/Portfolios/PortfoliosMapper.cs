using Domain.Entities;
using Contracts.Portfolios;

namespace Application.Portfolios;

public static class PortfoliosMapper
{
    public static CreatePortfolioResponse ToCreatePortfolioResponse(this Portfolio portfolio)
    {
        return new CreatePortfolioResponse(portfolio.Id, portfolio.Name);
    }

    public static UpdatePortfolioResponse ToUpdatePortfolioResponse(this Portfolio portfolio)
    {
        return new UpdatePortfolioResponse(portfolio.Id, portfolio.UserId, portfolio.Name, portfolio.Description, portfolio.CreatedAt, portfolio.UpdatedAt);
    }

    public static PortfolioResponse ToPortfolioResponse(this Portfolio portfolio)
    {
        return new PortfolioResponse(portfolio.Id, portfolio.UserId, portfolio.Name, portfolio.Description, portfolio.CreatedAt, portfolio.UpdatedAt);
    }
}
