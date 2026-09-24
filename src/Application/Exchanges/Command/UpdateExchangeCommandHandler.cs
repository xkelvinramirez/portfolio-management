using Application.Common.UnitOfWork;
using Application.Exchanges.Interfaces;
using Contracts.Exchanges;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Exchanges.Command;

public sealed record UpdateExchangeCommand(long Id, UpdateExchangeRequest Request) : IRequest<ErrorOr<UpdateExchangeResponse>>;

public sealed class UpdateExchangeCommandHandler(
    ILogger<UpdateExchangeCommandHandler> logger,
    IExchangeRepository exchangeRepository,
    IUnitOfWork unitOfWork
    ) : IRequestHandler<UpdateExchangeCommand, ErrorOr<UpdateExchangeResponse>>
{
    public async Task<ErrorOr<UpdateExchangeResponse>> Handle(UpdateExchangeCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

        var exchange = await exchangeRepository.GetByIdAsync(command.Id, cancellationToken);
        if (exchange is null)
        {
            return Error.NotFound("Exchange.NotFound", $"Exchange with ID '{command.Id}' was not found.");
        }

        var duplicate = await exchangeRepository.GetByNameAsync(request.Name, cancellationToken);
        if (duplicate is not null && duplicate.Id != exchange.Id)
        {
            return Error.Conflict("Exchange.AlreadyExists", $"An exchange with the name '{request.Name}' already exists.");
        }

        exchange.Name = request.Name;
        exchange.ApiKey = request.ApiKey;

        exchangeRepository.Update(exchange);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Updated exchange with ID {ExchangeId}", exchange.Id);

        return exchange.ToUpdateExchangeResponse();
    }
}
