using Domain.Entities;
using Contracts.Portfolios;

namespace Application.Portfolios;

public static class PortfoliosMapper
{
    public static CreatePortfolioResponse ToCreatePortfolioResponse(this Portfolio portfolio)
    {
        return new CreatePortfolioResponse(portfolio.Id, portfolio.Name);
    }
}
