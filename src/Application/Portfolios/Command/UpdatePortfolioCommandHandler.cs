using Application.Common.UnitOfWork;
using Application.Portfolios.Interfaces;
using Contracts.Portfolios;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Portfolios.Command;

public sealed record UpdatePortfolioCommand(long Id, UpdatePortfolioRequest Request) : IRequest<ErrorOr<UpdatePortfolioResponse>>;

public sealed class UpdatePortfolioCommandHandler(
    ILogger<UpdatePortfolioCommandHandler> logger,
    IPortfolioRepository portfolioRepository,
    IUnitOfWork unitOfWork
    ) : IRequestHandler<UpdatePortfolioCommand, ErrorOr<UpdatePortfolioResponse>>
{
    public async Task<ErrorOr<UpdatePortfolioResponse>> Handle(UpdatePortfolioCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

        var portfolio = await portfolioRepository.GetByIdAsync(command.Id, cancellationToken);
        if (portfolio is null)
        {
            return Error.NotFound("Portfolio.NotFound", $"Portfolio with ID '{command.Id}' was not found.");
        }

        var duplicate = await portfolioRepository.GetPortfolioByNameAsync(portfolio.UserId, request.Name, cancellationToken);
        if (duplicate is not null && duplicate.Id != portfolio.Id)
        {
            return Error.Conflict("Portfolio.AlreadyExists", $"You already have a portfolio named '{request.Name}'.");
        }

        portfolio.Name = request.Name;
        portfolio.Description = request.Description;
        portfolio.UpdatedAt = DateTime.UtcNow;

        portfolioRepository.Update(portfolio);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Updated portfolio with ID {PortfolioId}", portfolio.Id);

        return portfolio.ToUpdatePortfolioResponse();
    }
}
