using Application.CryptoCurrencies.Interfaces;
using Contracts.CryptoCurrencies;
using ErrorOr;
using MediatR;

namespace Application.CryptoCurrencies.Query;

public sealed record GetCryptoCurrencyByIdQuery(long Id) : IRequest<ErrorOr<CryptoCurrencyResponse>>;

public sealed class GetCryptoCurrencyByIdQueryHandler(
    ICryptoCurrencyRepository cryptoCurrencyRepository
    ) : IRequestHandler<GetCryptoCurrencyByIdQuery, ErrorOr<CryptoCurrencyResponse>>
{
    public async Task<ErrorOr<CryptoCurrencyResponse>> Handle(GetCryptoCurrencyByIdQuery query, CancellationToken cancellationToken)
    {
        var cryptoCurrency = await cryptoCurrencyRepository.GetByIdAsync(query.Id, cancellationToken);
        if (cryptoCurrency is null)
        {
            return Error.NotFound("CryptoCurrency.NotFound", $"Crypto currency with ID '{query.Id}' was not found.");
        }

        return cryptoCurrency.ToCryptoCurrencyResponse();
    }
}
