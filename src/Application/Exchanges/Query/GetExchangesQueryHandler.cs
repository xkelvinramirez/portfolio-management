using Application.Exchanges.Interfaces;
using Contracts.Common;
using Contracts.Exchanges;
using ErrorOr;
using MediatR;

namespace Application.Exchanges.Query;

public sealed record GetExchangesQuery(PaginatorRequest Paginator) : IRequest<ErrorOr<PaginatorResponse<ExchangeResponse>>>;

public sealed class GetExchangesQueryHandler(
    IExchangeRepository exchangeRepository
    ) : IRequestHandler<GetExchangesQuery, ErrorOr<PaginatorResponse<ExchangeResponse>>>
{
    public async Task<ErrorOr<PaginatorResponse<ExchangeResponse>>> Handle(GetExchangesQuery query, CancellationToken cancellationToken)
    {
        var result = await exchangeRepository.GetAllAsync(query.Paginator.Page, query.Paginator.Limit, _ => true, cancellationToken);

        return new PaginatorResponse<ExchangeResponse>
        {
            Page = result.Page,
            PageSize = result.PageSize,
            Total = result.Total,
            TotalPages = result.TotalPages,
            Data = result.Data.Select(e => e.ToExchangeResponse())
        };
    }
}
