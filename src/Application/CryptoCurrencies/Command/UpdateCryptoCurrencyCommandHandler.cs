using Application.Common.UnitOfWork;
using Application.CryptoCurrencies.Interfaces;
using Contracts.CryptoCurrencies;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.CryptoCurrencies.Command;

public sealed record UpdateCryptoCurrencyCommand(long Id, UpdateCryptoCurrencyRequest Request) : IRequest<ErrorOr<UpdateCryptoCurrencyResponse>>;

public sealed class UpdateCryptoCurrencyCommandHandler(
    ILogger<UpdateCryptoCurrencyCommandHandler> logger,
    ICryptoCurrencyRepository cryptoCurrencyRepository,
    IUnitOfWork unitOfWork
    ) : IRequestHandler<UpdateCryptoCurrencyCommand, ErrorOr<UpdateCryptoCurrencyResponse>>
{
    public async Task<ErrorOr<UpdateCryptoCurrencyResponse>> Handle(UpdateCryptoCurrencyCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

        var cryptoCurrency = await cryptoCurrencyRepository.GetByIdAsync(command.Id, cancellationToken);
        if (cryptoCurrency is null)
        {
            return Error.NotFound("CryptoCurrency.NotFound", $"Crypto currency with ID '{command.Id}' was not found.");
        }

        var duplicate = await cryptoCurrencyRepository.GetBySymbolAsync(request.Symbol, cancellationToken);
        if (duplicate is not null && duplicate.Id != cryptoCurrency.Id)
        {
            return Error.Conflict("CryptoCurrency.AlreadyExists", $"A crypto currency with the symbol '{request.Symbol}' already exists.");
        }

        cryptoCurrency.Symbol = request.Symbol;
        cryptoCurrency.Name = request.Name;

        cryptoCurrencyRepository.Update(cryptoCurrency);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Updated crypto currency with ID {CryptoCurrencyId}", cryptoCurrency.Id);

        return cryptoCurrency.ToUpdateCryptoCurrencyResponse();
    }
}
