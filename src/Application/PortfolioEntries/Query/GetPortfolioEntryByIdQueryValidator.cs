using FluentValidation;

namespace Application.PortfolioEntries.Query;

public sealed class GetPortfolioEntryByIdQueryValidator : AbstractValidator<GetPortfolioEntryByIdQuery>
{
    public GetPortfolioEntryByIdQueryValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0);
    }
}
