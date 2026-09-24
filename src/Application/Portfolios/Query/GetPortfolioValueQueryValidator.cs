using FluentValidation;

namespace Application.Portfolios.Query;

public sealed class GetPortfolioValueQueryValidator : AbstractValidator<GetPortfolioValueQuery>
{
    public GetPortfolioValueQueryValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0);
    }
}
