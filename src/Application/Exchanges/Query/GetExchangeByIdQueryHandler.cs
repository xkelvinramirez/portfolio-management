using Application.Exchanges.Interfaces;
using Contracts.Exchanges;
using ErrorOr;
using MediatR;

namespace Application.Exchanges.Query;

public sealed record GetExchangeByIdQuery(long Id) : IRequest<ErrorOr<ExchangeResponse>>;

public sealed class GetExchangeByIdQueryHandler(
    IExchangeRepository exchangeRepository
    ) : IRequestHandler<GetExchangeByIdQuery, ErrorOr<ExchangeResponse>>
{
    public async Task<ErrorOr<ExchangeResponse>> Handle(GetExchangeByIdQuery query, CancellationToken cancellationToken)
    {
        var exchange = await exchangeRepository.GetByIdAsync(query.Id, cancellationToken);
        if (exchange is null)
        {
            return Error.NotFound("Exchange.NotFound", $"Exchange with ID '{query.Id}' was not found.");
        }

        return exchange.ToExchangeResponse();
    }
}
