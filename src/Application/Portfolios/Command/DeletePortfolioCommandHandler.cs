using Application.Common.Security;
using Application.Common.UnitOfWork;
using Application.Portfolios.Interfaces;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Portfolios.Command;

public sealed record DeletePortfolioCommand(long Id) : IRequest<ErrorOr<Deleted>>;

public sealed class DeletePortfolioCommandHandler(
    ILogger<DeletePortfolioCommandHandler> logger,
    IPortfolioRepository portfolioRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserProvider currentUserProvider
    ) : IRequestHandler<DeletePortfolioCommand, ErrorOr<Deleted>>
{
    public async Task<ErrorOr<Deleted>> Handle(DeletePortfolioCommand command, CancellationToken cancellationToken)
    {
        var portfolio = await portfolioRepository.GetByIdAsync(command.Id, cancellationToken);
        if (portfolio is null || portfolio.UserId != currentUserProvider.UserId)
        {
            return Error.NotFound("Portfolio.NotFound", $"Portfolio with ID '{command.Id}' was not found.");
        }

        portfolioRepository.RemoveRange([portfolio]);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Deleted portfolio with ID {PortfolioId}", command.Id);

        return Result.Deleted;
    }
}
