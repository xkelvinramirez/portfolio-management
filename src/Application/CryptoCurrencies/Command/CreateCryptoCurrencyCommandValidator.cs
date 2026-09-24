using FluentValidation;

namespace Application.CryptoCurrencies.Command;

public sealed class CreateCryptoCurrencyCommandValidator : AbstractValidator<CreateCryptoCurrencyCommand>
{
    public CreateCryptoCurrencyCommandValidator()
    {
        RuleFor(x => x.Request)
            .NotNull()
            .WithMessage("Request cannot be null.");

        RuleFor(x => x.Request.Symbol)
            .NotEmpty()
            .MaximumLength(20);

        RuleFor(x => x.Request.Name)
            .NotEmpty()
            .MaximumLength(100);
    }
}
