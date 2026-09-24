using Application.PortfolioEntries.Interfaces;
using Contracts.PortfolioEntries;
using ErrorOr;
using MediatR;

namespace Application.PortfolioEntries.Query;

public sealed record GetPortfolioEntryByIdQuery(long Id) : IRequest<ErrorOr<PortfolioEntryResponse>>;

public sealed class GetPortfolioEntryByIdQueryHandler(
    IPortfolioEntryRepository portfolioEntryRepository
    ) : IRequestHandler<GetPortfolioEntryByIdQuery, ErrorOr<PortfolioEntryResponse>>
{
    public async Task<ErrorOr<PortfolioEntryResponse>> Handle(GetPortfolioEntryByIdQuery query, CancellationToken cancellationToken)
    {
        var entry = await portfolioEntryRepository.GetByIdAsync(query.Id, cancellationToken);
        if (entry is null)
        {
            return Error.NotFound("PortfolioEntry.NotFound", $"Portfolio entry with ID '{query.Id}' was not found.");
        }

        return entry.ToPortfolioEntryResponse();
    }
}
