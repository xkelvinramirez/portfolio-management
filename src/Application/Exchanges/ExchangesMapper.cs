using Contracts.Exchanges;
using Domain.Entities;

namespace Application.Exchanges;

public static class ExchangesMapper
{
    public static CreateExchangeResponse ToCreateExchangeResponse(this Exchange exchange)
        => new(exchange.Id, exchange.Name, exchange.ApiKey, exchange.CreatedAt);

    public static UpdateExchangeResponse ToUpdateExchangeResponse(this Exchange exchange)
        => new(exchange.Id, exchange.Name, exchange.ApiKey, exchange.CreatedAt);

    public static ExchangeResponse ToExchangeResponse(this Exchange exchange)
        => new(exchange.Id, exchange.Name, exchange.ApiKey, exchange.CreatedAt);
}
