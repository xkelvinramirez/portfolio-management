using Application.Common.UnitOfWork;
using Application.Portfolios.Interfaces;
using Contracts.Portfolios;
using Domain.Entities;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Reflection;


namespace Application.Portfolios.Command;

public sealed record CreatePortfolioCommand(CreatePortfolioRequest Request) : IRequest<ErrorOr<CreatePortfolioResponse>>;

public sealed class CreatePortfolioCommandHandler(
    ILogger<CreatePortfolioCommandHandler> logger,
    IPortfolioRepository portfolioRepository,
    IUnitOfWork unitOfWork
    ) : IRequestHandler<CreatePortfolioCommand, ErrorOr<CreatePortfolioResponse>>
{
    public async Task<ErrorOr<CreatePortfolioResponse>> Handle(CreatePortfolioCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        logger.LogInformation("Handling CreatePortfolioCommand for Portfolio: {PortfolioName}", request.Name);

        // Check if a portfolio with the same name already exists
        var existingPortfolio = await portfolioRepository.GetPortfolioByNameAsync(request.Name, cancellationToken);
        if (existingPortfolio is not null)
        {
            return Error.Conflict("Portfolio.AlreadyExists", $"A portfolio with the name '{request.Name}' already exists.");
        }
        // Create a new portfolio entity
        var newPortfolio = Portfolio.Create(
            request.UserId,
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
