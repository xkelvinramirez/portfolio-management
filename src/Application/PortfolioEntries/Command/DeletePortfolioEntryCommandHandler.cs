using Application.Common.Security;
using Application.Common.UnitOfWork;
using Application.PortfolioEntries.Interfaces;
using Application.Portfolios.Interfaces;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.PortfolioEntries.Command;

public sealed record DeletePortfolioEntryCommand(long Id) : IRequest<ErrorOr<Deleted>>;

public sealed class DeletePortfolioEntryCommandHandler(
    ILogger<DeletePortfolioEntryCommandHandler> logger,
    IPortfolioEntryRepository portfolioEntryRepository,
    IPortfolioRepository portfolioRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserProvider currentUserProvider
    ) : IRequestHandler<DeletePortfolioEntryCommand, ErrorOr<Deleted>>
{
    public async Task<ErrorOr<Deleted>> Handle(DeletePortfolioEntryCommand command, CancellationToken cancellationToken)
    {
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

        portfolioEntryRepository.RemoveRange([entry]);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Deleted portfolio entry with ID {PortfolioEntryId}", command.Id);

        return Result.Deleted;
    }
}
