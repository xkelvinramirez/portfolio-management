using Application.Common.Security;
using Application.Common.UnitOfWork;
using Application.CryptoCurrencies.Interfaces;
using Application.Exchanges.Interfaces;
using Application.PortfolioEntries.Interfaces;
using Application.Portfolios.Interfaces;
using Contracts.PortfolioEntries;
using Domain.Entities;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.PortfolioEntries.Command;

public sealed record CreatePortfolioEntryCommand(CreatePortfolioEntryRequest Request) : IRequest<ErrorOr<CreatePortfolioEntryResponse>>;

public sealed class CreatePortfolioEntryCommandHandler(
    ILogger<CreatePortfolioEntryCommandHandler> logger,
    IPortfolioEntryRepository portfolioEntryRepository,
    IPortfolioRepository portfolioRepository,
    ICryptoCurrencyRepository cryptoCurrencyRepository,
    IExchangeRepository exchangeRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserProvider currentUserProvider
    ) : IRequestHandler<CreatePortfolioEntryCommand, ErrorOr<CreatePortfolioEntryResponse>>
{
    public async Task<ErrorOr<CreatePortfolioEntryResponse>> Handle(CreatePortfolioEntryCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        logger.LogInformation("Handling CreatePortfolioEntryCommand for Portfolio: {PortfolioId}", request.PortfolioId);

        var portfolio = await portfolioRepository.GetByIdAsync(request.PortfolioId, cancellationToken);
        if (portfolio is null || portfolio.UserId != currentUserProvider.UserId)
        {
            return Error.NotFound("Portfolio.NotFound", $"Portfolio with ID '{request.PortfolioId}' was not found.");
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

        var newEntry = PortfolioEntry.Create(
            request.PortfolioId,
            request.CryptoCurrencyId,
            request.ExchangeId,
            request.Quantity,
            request.PricePerUnit,
            request.RecordedAt);

        await portfolioEntryRepository.AddAsync(newEntry, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created new portfolio entry with ID {PortfolioEntryId}", newEntry.Id);

        return newEntry.ToCreatePortfolioEntryResponse();
    }
}
