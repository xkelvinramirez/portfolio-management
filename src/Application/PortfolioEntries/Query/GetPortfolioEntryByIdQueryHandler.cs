using Application.Common.Security;
using Application.PortfolioEntries.Interfaces;
using Application.Portfolios.Interfaces;
using Contracts.PortfolioEntries;
using ErrorOr;
using MediatR;

namespace Application.PortfolioEntries.Query;

public sealed record GetPortfolioEntryByIdQuery(long Id) : IRequest<ErrorOr<PortfolioEntryResponse>>;

public sealed class GetPortfolioEntryByIdQueryHandler(
    IPortfolioEntryRepository portfolioEntryRepository,
    IPortfolioRepository portfolioRepository,
    ICurrentUserProvider currentUserProvider
    ) : IRequestHandler<GetPortfolioEntryByIdQuery, ErrorOr<PortfolioEntryResponse>>
{
    public async Task<ErrorOr<PortfolioEntryResponse>> Handle(GetPortfolioEntryByIdQuery query, CancellationToken cancellationToken)
    {
        var entry = await portfolioEntryRepository.GetByIdAsync(query.Id, cancellationToken);
        if (entry is null)
        {
            return Error.NotFound("PortfolioEntry.NotFound", $"Portfolio entry with ID '{query.Id}' was not found.");
        }

        var portfolio = await portfolioRepository.GetByIdAsync(entry.PortfolioId, cancellationToken);
        if (portfolio is null || portfolio.UserId != currentUserProvider.UserId)
        {
            return Error.NotFound("PortfolioEntry.NotFound", $"Portfolio entry with ID '{query.Id}' was not found.");
        }

        return entry.ToPortfolioEntryResponse();
    }
}
