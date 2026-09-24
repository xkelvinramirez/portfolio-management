using FluentValidation;

namespace Application.Portfolios.Query;

public sealed class GetPortfolioAllocationQueryValidator : AbstractValidator<GetPortfolioAllocationQuery>
{
    public GetPortfolioAllocationQueryValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0);

        RuleFor(x => x.GroupBy)
            .IsInEnum();
    }
}
