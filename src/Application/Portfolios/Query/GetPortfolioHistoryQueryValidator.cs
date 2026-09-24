using FluentValidation;

namespace Application.Portfolios.Query;

public sealed class GetPortfolioHistoryQueryValidator : AbstractValidator<GetPortfolioHistoryQuery>
{
    public GetPortfolioHistoryQueryValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0);
    }
}
