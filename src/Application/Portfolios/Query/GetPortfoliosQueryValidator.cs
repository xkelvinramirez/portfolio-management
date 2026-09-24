using FluentValidation;

namespace Application.Portfolios.Query;

public sealed class GetPortfoliosQueryValidator : AbstractValidator<GetPortfoliosQuery>
{
    public GetPortfoliosQueryValidator()
    {
        RuleFor(x => x.Paginator.Page)
            .GreaterThan(0);

        RuleFor(x => x.Paginator.Limit)
            .InclusiveBetween(1, 100);
    }
}
