using FluentValidation;

namespace Application.Exchanges.Command;

public sealed class CreateExchangeCommandValidator : AbstractValidator<CreateExchangeCommand>
{
    public CreateExchangeCommandValidator()
    {
        RuleFor(x => x.Request)
            .NotNull()
            .WithMessage("Request cannot be null.");

        RuleFor(x => x.Request.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Request.ApiKey)
            .NotEmpty()
            .MaximumLength(500);
    }
}
