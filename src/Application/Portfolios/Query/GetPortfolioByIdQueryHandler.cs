using Application.Portfolios.Interfaces;
using Contracts.Portfolios;
using ErrorOr;
using MediatR;

namespace Application.Portfolios.Query;

public sealed record GetPortfolioByIdQuery(long Id) : IRequest<ErrorOr<PortfolioResponse>>;

public sealed class GetPortfolioByIdQueryHandler(
    IPortfolioRepository portfolioRepository
    ) : IRequestHandler<GetPortfolioByIdQuery, ErrorOr<PortfolioResponse>>
{
    public async Task<ErrorOr<PortfolioResponse>> Handle(GetPortfolioByIdQuery query, CancellationToken cancellationToken)
    {
        var portfolio = await portfolioRepository.GetByIdAsync(query.Id, cancellationToken);
        if (portfolio is null)
        {
            return Error.NotFound("Portfolio.NotFound", $"Portfolio with ID '{query.Id}' was not found.");
        }

        return portfolio.ToPortfolioResponse();
    }
}
