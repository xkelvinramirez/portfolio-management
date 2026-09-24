using FluentValidation;

namespace Application.CryptoCurrencies.Command;

public sealed class DeleteCryptoCurrencyCommandValidator : AbstractValidator<DeleteCryptoCurrencyCommand>
{
    public DeleteCryptoCurrencyCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0);
    }
}
