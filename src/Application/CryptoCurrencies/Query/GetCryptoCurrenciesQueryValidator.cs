using FluentValidation;

namespace Application.CryptoCurrencies.Query;

public sealed class GetCryptoCurrenciesQueryValidator : AbstractValidator<GetCryptoCurrenciesQuery>
{
    public GetCryptoCurrenciesQueryValidator()
    {
        RuleFor(x => x.Paginator.Page)
            .GreaterThan(0);

        RuleFor(x => x.Paginator.Limit)
            .InclusiveBetween(1, 100);
    }
}
