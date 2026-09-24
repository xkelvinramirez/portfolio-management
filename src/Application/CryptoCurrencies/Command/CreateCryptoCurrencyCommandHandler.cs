using Application.Common.UnitOfWork;
using Application.CryptoCurrencies.Interfaces;
using Contracts.CryptoCurrencies;
using Domain.Entities;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.CryptoCurrencies.Command;

public sealed record CreateCryptoCurrencyCommand(CreateCryptoCurrencyRequest Request) : IRequest<ErrorOr<CreateCryptoCurrencyResponse>>;

public sealed class CreateCryptoCurrencyCommandHandler(
    ILogger<CreateCryptoCurrencyCommandHandler> logger,
    ICryptoCurrencyRepository cryptoCurrencyRepository,
    IUnitOfWork unitOfWork
    ) : IRequestHandler<CreateCryptoCurrencyCommand, ErrorOr<CreateCryptoCurrencyResponse>>
{
    public async Task<ErrorOr<CreateCryptoCurrencyResponse>> Handle(CreateCryptoCurrencyCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        logger.LogInformation("Handling CreateCryptoCurrencyCommand for CryptoCurrency: {Symbol}", request.Symbol);

        var existingCryptoCurrency = await cryptoCurrencyRepository.GetBySymbolAsync(request.Symbol, cancellationToken);
        if (existingCryptoCurrency is not null)
        {
            return Error.Conflict("CryptoCurrency.AlreadyExists", $"A crypto currency with the symbol '{request.Symbol}' already exists.");
        }

        var newCryptoCurrency = CryptoCurrency.Create(request.Symbol, request.Name);

        await cryptoCurrencyRepository.AddAsync(newCryptoCurrency, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created new crypto currency with ID {CryptoCurrencyId}", newCryptoCurrency.Id);

        return newCryptoCurrency.ToCreateCryptoCurrencyResponse();
    }
}
