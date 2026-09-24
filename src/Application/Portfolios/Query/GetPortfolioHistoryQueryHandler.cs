using Application.Common.Security;
using Application.Portfolios.Interfaces;
using Application.PortfolioEntries.Interfaces;
using Contracts.Portfolios;
using ErrorOr;
using MediatR;

namespace Application.Portfolios.Query;

public sealed record GetPortfolioHistoryQuery(long Id) : IRequest<ErrorOr<PortfolioHistoryResponse>>;

public sealed class GetPortfolioHistoryQueryHandler(
    IPortfolioRepository portfolioRepository,
    IPortfolioEntryRepository portfolioEntryRepository,
    ICurrentUserProvider currentUserProvider
    ) : IRequestHandler<GetPortfolioHistoryQuery, ErrorOr<PortfolioHistoryResponse>>
{
    public async Task<ErrorOr<PortfolioHistoryResponse>> Handle(GetPortfolioHistoryQuery query, CancellationToken cancellationToken)
    {
        var portfolio = await portfolioRepository.GetByIdAsync(query.Id, cancellationToken);
        if (portfolio is null || portfolio.UserId != currentUserProvider.UserId)
        {
            return Error.NotFound("Portfolio.NotFound", $"Portfolio with ID '{query.Id}' was not found.");
        }

        var entries = await portfolioEntryRepository.GetByPortfolioIdAsync(query.Id, cancellationToken);
        var history = portfolio.GetValueHistory(entries);
        var byAsset = portfolio.GetValueHistoryByAsset(entries);

        return new PortfolioHistoryResponse(
            portfolio.Id,
            history.Select(p => new PortfolioHistoryPointResponse(p.Date, p.Value)).ToList(),
            byAsset
                .Select(series => new PortfolioAssetHistorySeriesResponse(
                    series.CryptoCurrencyId,
                    series.Symbol,
                    series.Points.Select(p => new PortfolioHistoryPointResponse(p.Date, p.Value)).ToList()))
                .ToList());
    }
}
