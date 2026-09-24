using FluentValidation;

namespace Application.Exchanges.Query;

public sealed class GetExchangesQueryValidator : AbstractValidator<GetExchangesQuery>
{
    public GetExchangesQueryValidator()
    {
        RuleFor(x => x.Paginator.Page)
            .GreaterThan(0);

        RuleFor(x => x.Paginator.Limit)
            .InclusiveBetween(1, 100);
    }
}
