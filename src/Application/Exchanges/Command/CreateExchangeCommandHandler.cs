using Application.Common.UnitOfWork;
using Application.Exchanges.Interfaces;
using Contracts.Exchanges;
using Domain.Entities;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Exchanges.Command;

public sealed record CreateExchangeCommand(CreateExchangeRequest Request) : IRequest<ErrorOr<CreateExchangeResponse>>;

public sealed class CreateExchangeCommandHandler(
    ILogger<CreateExchangeCommandHandler> logger,
    IExchangeRepository exchangeRepository,
    IUnitOfWork unitOfWork
    ) : IRequestHandler<CreateExchangeCommand, ErrorOr<CreateExchangeResponse>>
{
    public async Task<ErrorOr<CreateExchangeResponse>> Handle(CreateExchangeCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        logger.LogInformation("Handling CreateExchangeCommand for Exchange: {ExchangeName}", request.Name);

        var existingExchange = await exchangeRepository.GetByNameAsync(request.Name, cancellationToken);
        if (existingExchange is not null)
        {
            return Error.Conflict("Exchange.AlreadyExists", $"An exchange with the name '{request.Name}' already exists.");
        }

        var newExchange = Exchange.Create(request.Name, request.ApiKey);

        await exchangeRepository.AddAsync(newExchange, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created new exchange with ID {ExchangeId}", newExchange.Id);

        return newExchange.ToCreateExchangeResponse();
    }
}
