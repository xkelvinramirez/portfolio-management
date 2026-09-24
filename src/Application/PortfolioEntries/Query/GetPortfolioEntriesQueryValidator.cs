using FluentValidation;

namespace Application.PortfolioEntries.Query;

public sealed class GetPortfolioEntriesQueryValidator : AbstractValidator<GetPortfolioEntriesQuery>
{
    public GetPortfolioEntriesQueryValidator()
    {
        RuleFor(x => x.PortfolioId)
            .GreaterThan(0);

        RuleFor(x => x.Paginator.Page)
            .GreaterThan(0);

        RuleFor(x => x.Paginator.Limit)
            .InclusiveBetween(1, 100);
    }
}
