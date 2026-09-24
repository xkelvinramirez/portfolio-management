using FluentValidation;

namespace Application.CryptoCurrencies.Query;

public sealed class GetCryptoCurrencyByIdQueryValidator : AbstractValidator<GetCryptoCurrencyByIdQuery>
{
    public GetCryptoCurrencyByIdQueryValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0);
    }
}
