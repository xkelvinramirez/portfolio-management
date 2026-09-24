using Application.Common.Security;
using Application.Portfolios.Interfaces;
using Contracts.Common;
using Contracts.Portfolios;
using ErrorOr;
using MediatR;

namespace Application.Portfolios.Query;

public sealed record GetPortfoliosQuery(PaginatorRequest Paginator) : IRequest<ErrorOr<PaginatorResponse<PortfolioResponse>>>;

public sealed class GetPortfoliosQueryHandler(
    IPortfolioRepository portfolioRepository,
    ICurrentUserProvider currentUserProvider
    ) : IRequestHandler<GetPortfoliosQuery, ErrorOr<PaginatorResponse<PortfolioResponse>>>
{
    public async Task<ErrorOr<PaginatorResponse<PortfolioResponse>>> Handle(GetPortfoliosQuery query, CancellationToken cancellationToken)
    {
        var currentUserId = currentUserProvider.UserId;

        var result = await portfolioRepository.GetAllAsync(
            query.Paginator.Page,
            query.Paginator.Limit,
            portfolio => portfolio.UserId == currentUserId,
            cancellationToken);

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
