using Application.Common.Security;
using Application.Portfolios.Interfaces;
using Application.PortfolioEntries.Interfaces;
using Contracts.Portfolios;
using ErrorOr;
using MediatR;

namespace Application.Portfolios.Query;

public sealed record GetPortfolioHoldingsQuery(long Id, DateTime? Date) : IRequest<ErrorOr<PortfolioHoldingsResponse>>;

public sealed class GetPortfolioHoldingsQueryHandler(
    IPortfolioRepository portfolioRepository,
    IPortfolioEntryRepository portfolioEntryRepository,
    ICurrentUserProvider currentUserProvider
    ) : IRequestHandler<GetPortfolioHoldingsQuery, ErrorOr<PortfolioHoldingsResponse>>
{
    public async Task<ErrorOr<PortfolioHoldingsResponse>> Handle(GetPortfolioHoldingsQuery query, CancellationToken cancellationToken)
    {
        var portfolio = await portfolioRepository.GetByIdAsync(query.Id, cancellationToken);
        if (portfolio is null || portfolio.UserId != currentUserProvider.UserId)
        {
            return Error.NotFound("Portfolio.NotFound", $"Portfolio with ID '{query.Id}' was not found.");
        }

        var entries = await portfolioEntryRepository.GetByPortfolioIdAsync(query.Id, cancellationToken);
        var date = query.Date ?? DateTime.UtcNow;
        var holdings = portfolio.GetHoldingsAsOf(entries, date);

        var items = holdings
            .Select(e => new PortfolioHoldingItemResponse(
                e.CryptoCurrencyId,
                e.CryptoCurrency.Symbol,
                e.CryptoCurrency.Name,
                e.ExchangeId,
                e.Exchange.Name,
                e.Quantity,
                e.PricePerUnit,
                e.Quantity * e.PricePerUnit,
                e.RecordedAt))
            .OrderByDescending(i => i.Value)
            .ToList();

        return new PortfolioHoldingsResponse(portfolio.Id, date.Date, items);
    }
}
