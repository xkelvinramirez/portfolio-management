using Application.Common.Security;
using Application.PortfolioEntries.Interfaces;
using Application.Portfolios.Interfaces;
using Contracts.Common;
using Contracts.PortfolioEntries;
using ErrorOr;
using MediatR;

namespace Application.PortfolioEntries.Query;

public sealed record GetPortfolioEntriesQuery(long PortfolioId, PaginatorRequest Paginator) : IRequest<ErrorOr<PaginatorResponse<PortfolioEntryResponse>>>;

public sealed class GetPortfolioEntriesQueryHandler(
    IPortfolioEntryRepository portfolioEntryRepository,
    IPortfolioRepository portfolioRepository,
    ICurrentUserProvider currentUserProvider
    ) : IRequestHandler<GetPortfolioEntriesQuery, ErrorOr<PaginatorResponse<PortfolioEntryResponse>>>
{
    public async Task<ErrorOr<PaginatorResponse<PortfolioEntryResponse>>> Handle(GetPortfolioEntriesQuery query, CancellationToken cancellationToken)
    {
        var portfolio = await portfolioRepository.GetByIdAsync(query.PortfolioId, cancellationToken);
        if (portfolio is null || portfolio.UserId != currentUserProvider.UserId)
        {
            return Error.NotFound("Portfolio.NotFound", $"Portfolio with ID '{query.PortfolioId}' was not found.");
        }

        var result = await portfolioEntryRepository.GetAllAsync(
            query.Paginator.Page,
            query.Paginator.Limit,
            entry => entry.PortfolioId == query.PortfolioId,
            cancellationToken);

        return new PaginatorResponse<PortfolioEntryResponse>
        {
            Page = result.Page,
            PageSize = result.PageSize,
            Total = result.Total,
            TotalPages = result.TotalPages,
            Data = result.Data.Select(e => e.ToPortfolioEntryResponse())
        };
    }
}
