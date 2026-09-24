using Application.Common.Security;
using Application.Common.UnitOfWork;
using Application.Portfolios.Interfaces;
using Contracts.Portfolios;
using Domain.Entities;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;


namespace Application.Portfolios.Command;

public sealed record CreatePortfolioCommand(CreatePortfolioRequest Request) : IRequest<ErrorOr<CreatePortfolioResponse>>;

public sealed class CreatePortfolioCommandHandler(
    ILogger<CreatePortfolioCommandHandler> logger,
    IPortfolioRepository portfolioRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserProvider currentUserProvider
    ) : IRequestHandler<CreatePortfolioCommand, ErrorOr<CreatePortfolioResponse>>
{
    public async Task<ErrorOr<CreatePortfolioResponse>> Handle(CreatePortfolioCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var userId = currentUserProvider.UserId;
        logger.LogInformation("Handling CreatePortfolioCommand for Portfolio: {PortfolioName}", request.Name);

        // Check if this user already has a portfolio with the same name
        var existingPortfolio = await portfolioRepository.GetPortfolioByNameAsync(userId, request.Name, cancellationToken);
        if (existingPortfolio is not null)
        {
            return Error.Conflict("Portfolio.AlreadyExists", $"You already have a portfolio named '{request.Name}'.");
        }
        // Create a new portfolio entity
        var newPortfolio = Portfolio.Create(
            userId,
            request.Name,
            request.Description);

        // Add the new portfolio to the repository
        await portfolioRepository.AddAsync(newPortfolio, cancellationToken);
        // Commit the changes using Unit of Work
        //await unitOfWork.CommitAsync(cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Created new portfolio with ID {PortfolioId} and Name {PortfolioName}", newPortfolio.Id, newPortfolio.Name);
        // Return the response
        return new CreatePortfolioResponse(newPortfolio.Id, newPortfolio.Name);
    }
}
