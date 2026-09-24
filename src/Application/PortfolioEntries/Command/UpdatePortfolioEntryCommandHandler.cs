using Application.Common.Security;
using Application.Common.UnitOfWork;
using Application.CryptoCurrencies.Interfaces;
using Application.Exchanges.Interfaces;
using Application.PortfolioEntries.Interfaces;
using Application.Portfolios.Interfaces;
using Contracts.PortfolioEntries;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.PortfolioEntries.Command;

public sealed record UpdatePortfolioEntryCommand(long Id, UpdatePortfolioEntryRequest Request) : IRequest<ErrorOr<UpdatePortfolioEntryResponse>>;

public sealed class UpdatePortfolioEntryCommandHandler(
    ILogger<UpdatePortfolioEntryCommandHandler> logger,
    IPortfolioEntryRepository portfolioEntryRepository,
    IPortfolioRepository portfolioRepository,
    ICryptoCurrencyRepository cryptoCurrencyRepository,
    IExchangeRepository exchangeRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserProvider currentUserProvider
    ) : IRequestHandler<UpdatePortfolioEntryCommand, ErrorOr<UpdatePortfolioEntryResponse>>
{
    public async Task<ErrorOr<UpdatePortfolioEntryResponse>> Handle(UpdatePortfolioEntryCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

        var entry = await portfolioEntryRepository.GetByIdAsync(command.Id, cancellationToken);
        if (entry is null)
        {
            return Error.NotFound("PortfolioEntry.NotFound", $"Portfolio entry with ID '{command.Id}' was not found.");
        }

        var owningPortfolio = await portfolioRepository.GetByIdAsync(entry.PortfolioId, cancellationToken);
        if (owningPortfolio is null || owningPortfolio.UserId != currentUserProvider.UserId)
        {
            return Error.NotFound("PortfolioEntry.NotFound", $"Portfolio entry with ID '{command.Id}' was not found.");
        }

        var cryptoCurrency = await cryptoCurrencyRepository.GetByIdAsync(request.CryptoCurrencyId, cancellationToken);
        if (cryptoCurrency is null)
        {
            return Error.NotFound("CryptoCurrency.NotFound", $"Crypto currency with ID '{request.CryptoCurrencyId}' was not found.");
        }

        var exchange = await exchangeRepository.GetByIdAsync(request.ExchangeId, cancellationToken);
        if (exchange is null)
        {
            return Error.NotFound("Exchange.NotFound", $"Exchange with ID '{request.ExchangeId}' was not found.");
        }

        entry.CryptoCurrencyId = request.CryptoCurrencyId;
        entry.ExchangeId = request.ExchangeId;
        entry.Quantity = request.Quantity;
        entry.PricePerUnit = request.PricePerUnit;
        entry.RecordedAt = request.RecordedAt;

        portfolioEntryRepository.Update(entry);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Updated portfolio entry with ID {PortfolioEntryId}", entry.Id);

        return entry.ToUpdatePortfolioEntryResponse();
    }
}
