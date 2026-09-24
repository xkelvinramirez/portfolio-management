using Application.Portfolios.Interfaces;
using Application.PortfolioEntries.Interfaces;
using Contracts.Portfolios;
using ErrorOr;
using MediatR;

namespace Application.Portfolios.Query;

public sealed record GetPortfolioValueQuery(long Id, DateTime? Date) : IRequest<ErrorOr<PortfolioValueResponse>>;

public sealed class GetPortfolioValueQueryHandler(
    IPortfolioRepository portfolioRepository,
    IPortfolioEntryRepository portfolioEntryRepository
    ) : IRequestHandler<GetPortfolioValueQuery, ErrorOr<PortfolioValueResponse>>
{
    public async Task<ErrorOr<PortfolioValueResponse>> Handle(GetPortfolioValueQuery query, CancellationToken cancellationToken)
    {
        var portfolio = await portfolioRepository.GetByIdAsync(query.Id, cancellationToken);
        if (portfolio is null)
        {
            return Error.NotFound("Portfolio.NotFound", $"Portfolio with ID '{query.Id}' was not found.");
        }

        var entries = await portfolioEntryRepository.GetByPortfolioIdAsync(query.Id, cancellationToken);
        var date = query.Date ?? DateTime.UtcNow;
        var value = portfolio.GetPortfolioValueByDate(entries, date);

        return new PortfolioValueResponse(portfolio.Id, date.Date, value);
    }
}
