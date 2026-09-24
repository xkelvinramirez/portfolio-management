using FluentValidation;

namespace Application.Portfolios.Query;

public sealed class GetPortfolioHoldingsQueryValidator : AbstractValidator<GetPortfolioHoldingsQuery>
{
    public GetPortfolioHoldingsQueryValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0);
    }
}
