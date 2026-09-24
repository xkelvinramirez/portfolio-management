using Application.Common.UnitOfWork;
using Application.PortfolioEntries.Interfaces;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.PortfolioEntries.Command;

public sealed record DeletePortfolioEntryCommand(long Id) : IRequest<ErrorOr<Deleted>>;

public sealed class DeletePortfolioEntryCommandHandler(
    ILogger<DeletePortfolioEntryCommandHandler> logger,
    IPortfolioEntryRepository portfolioEntryRepository,
    IUnitOfWork unitOfWork
    ) : IRequestHandler<DeletePortfolioEntryCommand, ErrorOr<Deleted>>
{
    public async Task<ErrorOr<Deleted>> Handle(DeletePortfolioEntryCommand command, CancellationToken cancellationToken)
    {
        var entry = await portfolioEntryRepository.GetByIdAsync(command.Id, cancellationToken);
        if (entry is null)
        {
            return Error.NotFound("PortfolioEntry.NotFound", $"Portfolio entry with ID '{command.Id}' was not found.");
        }

        portfolioEntryRepository.RemoveRange([entry]);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Deleted portfolio entry with ID {PortfolioEntryId}", command.Id);

        return Result.Deleted;
    }
}
