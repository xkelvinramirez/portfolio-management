using Application.Portfolios.Interfaces;
using Contracts.Common;
using Contracts.Portfolios;
using Domain.Entities;
using ErrorOr;
using MediatR;
using System.Linq.Expressions;

namespace Application.Portfolios.Query;

public sealed record GetPortfoliosQuery(long? UserId, PaginatorRequest Paginator) : IRequest<ErrorOr<PaginatorResponse<PortfolioResponse>>>;

public sealed class GetPortfoliosQueryHandler(
    IPortfolioRepository portfolioRepository
    ) : IRequestHandler<GetPortfoliosQuery, ErrorOr<PaginatorResponse<PortfolioResponse>>>
{
    public async Task<ErrorOr<PaginatorResponse<PortfolioResponse>>> Handle(GetPortfoliosQuery query, CancellationToken cancellationToken)
    {
        Expression<Func<Portfolio, bool>> predicate = query.UserId.HasValue
            ? portfolio => portfolio.UserId == query.UserId.Value
            : _ => true;

        var result = await portfolioRepository.GetAllAsync(query.Paginator.Page, query.Paginator.Limit, predicate, cancellationToken);

        return new PaginatorResponse<PortfolioResponse>
        {
            Page = result.Page,
            PageSize = result.PageSize,
            Total = result.Total,
            TotalPages = result.TotalPages,
            Data = result.Data.Select(p => p.ToPortfolioResponse())
        };
    }
}
