using Application.Common.Security;
using Application.Portfolios.Interfaces;
using Application.PortfolioEntries.Interfaces;
using Contracts.Portfolios;
using ErrorOr;
using MediatR;

namespace Application.Portfolios.Query;

public sealed record GetPortfolioAllocationQuery(long Id, PortfolioAllocationGroupBy GroupBy, DateTime? Date) : IRequest<ErrorOr<PortfolioAllocationResponse>>;

public sealed class GetPortfolioAllocationQueryHandler(
    IPortfolioRepository portfolioRepository,
    IPortfolioEntryRepository portfolioEntryRepository,
    ICurrentUserProvider currentUserProvider
    ) : IRequestHandler<GetPortfolioAllocationQuery, ErrorOr<PortfolioAllocationResponse>>
{
    public async Task<ErrorOr<PortfolioAllocationResponse>> Handle(GetPortfolioAllocationQuery query, CancellationToken cancellationToken)
    {
        var portfolio = await portfolioRepository.GetByIdAsync(query.Id, cancellationToken);
        if (portfolio is null || portfolio.UserId != currentUserProvider.UserId)
        {
            return Error.NotFound("Portfolio.NotFound", $"Portfolio with ID '{query.Id}' was not found.");
        }

        var entries = await portfolioEntryRepository.GetByPortfolioIdAsync(query.Id, cancellationToken);
        var date = query.Date ?? DateTime.UtcNow;
        var holdings = portfolio.GetHoldingsAsOf(entries, date);
        var totalValue = holdings.Sum(e => e.Quantity * e.PricePerUnit);

        var grouped = query.GroupBy == PortfolioAllocationGroupBy.Asset
            ? holdings.GroupBy(e => (Id: e.CryptoCurrencyId, Label: e.CryptoCurrency.Symbol))
            : holdings.GroupBy(e => (Id: e.ExchangeId, Label: e.Exchange.Name));

        var items = grouped
            .Select(g =>
            {
                var value = g.Sum(e => e.Quantity * e.PricePerUnit);
                var percentage = totalValue == 0 ? 0 : Math.Round(value / totalValue * 100, 2);
                return new PortfolioAllocationItemResponse(g.Key.Id, g.Key.Label, value, percentage);
            })
            .OrderByDescending(i => i.Value)
            .ToList();

        return new PortfolioAllocationResponse(portfolio.Id, date.Date, query.GroupBy, items);
    }
}
