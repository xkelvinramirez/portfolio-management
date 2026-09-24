using FluentValidation;

namespace Application.Portfolios.Query;

public sealed class GetPortfolioByIdQueryValidator : AbstractValidator<GetPortfolioByIdQuery>
{
    public GetPortfolioByIdQueryValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0);
    }
}
