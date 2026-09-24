using Application.PortfolioEntries.Interfaces;
using Contracts.Common;
using Contracts.PortfolioEntries;
using Domain.Entities;
using ErrorOr;
using MediatR;
using System.Linq.Expressions;

namespace Application.PortfolioEntries.Query;

public sealed record GetPortfolioEntriesQuery(long? PortfolioId, PaginatorRequest Paginator) : IRequest<ErrorOr<PaginatorResponse<PortfolioEntryResponse>>>;

public sealed class GetPortfolioEntriesQueryHandler(
    IPortfolioEntryRepository portfolioEntryRepository
    ) : IRequestHandler<GetPortfolioEntriesQuery, ErrorOr<PaginatorResponse<PortfolioEntryResponse>>>
{
    public async Task<ErrorOr<PaginatorResponse<PortfolioEntryResponse>>> Handle(GetPortfolioEntriesQuery query, CancellationToken cancellationToken)
    {
        Expression<Func<PortfolioEntry, bool>> predicate = query.PortfolioId.HasValue
            ? entry => entry.PortfolioId == query.PortfolioId.Value
            : _ => true;

        var result = await portfolioEntryRepository.GetAllAsync(query.Paginator.Page, query.Paginator.Limit, predicate, cancellationToken);

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
