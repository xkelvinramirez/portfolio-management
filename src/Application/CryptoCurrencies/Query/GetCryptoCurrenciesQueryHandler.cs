using Application.CryptoCurrencies.Interfaces;
using Contracts.Common;
using Contracts.CryptoCurrencies;
using ErrorOr;
using MediatR;

namespace Application.CryptoCurrencies.Query;

public sealed record GetCryptoCurrenciesQuery(PaginatorRequest Paginator) : IRequest<ErrorOr<PaginatorResponse<CryptoCurrencyResponse>>>;

public sealed class GetCryptoCurrenciesQueryHandler(
    ICryptoCurrencyRepository cryptoCurrencyRepository
    ) : IRequestHandler<GetCryptoCurrenciesQuery, ErrorOr<PaginatorResponse<CryptoCurrencyResponse>>>
{
    public async Task<ErrorOr<PaginatorResponse<CryptoCurrencyResponse>>> Handle(GetCryptoCurrenciesQuery query, CancellationToken cancellationToken)
    {
        var result = await cryptoCurrencyRepository.GetAllAsync(query.Paginator.Page, query.Paginator.Limit, _ => true, cancellationToken);

        return new PaginatorResponse<CryptoCurrencyResponse>
        {
            Page = result.Page,
            PageSize = result.PageSize,
            Total = result.Total,
            TotalPages = result.TotalPages,
            Data = result.Data.Select(c => c.ToCryptoCurrencyResponse())
        };
    }
}
