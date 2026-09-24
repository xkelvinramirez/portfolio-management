using Application.Common.UnitOfWork;
using Application.CryptoCurrencies.Interfaces;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.CryptoCurrencies.Command;

public sealed record DeleteCryptoCurrencyCommand(long Id) : IRequest<ErrorOr<Deleted>>;

public sealed class DeleteCryptoCurrencyCommandHandler(
    ILogger<DeleteCryptoCurrencyCommandHandler> logger,
    ICryptoCurrencyRepository cryptoCurrencyRepository,
    IUnitOfWork unitOfWork
    ) : IRequestHandler<DeleteCryptoCurrencyCommand, ErrorOr<Deleted>>
{
    public async Task<ErrorOr<Deleted>> Handle(DeleteCryptoCurrencyCommand command, CancellationToken cancellationToken)
    {
        var cryptoCurrency = await cryptoCurrencyRepository.GetByIdAsync(command.Id, cancellationToken);
        if (cryptoCurrency is null)
        {
            return Error.NotFound("CryptoCurrency.NotFound", $"Crypto currency with ID '{command.Id}' was not found.");
        }

        cryptoCurrencyRepository.RemoveRange([cryptoCurrency]);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Deleted crypto currency with ID {CryptoCurrencyId}", command.Id);

        return Result.Deleted;
    }
}
