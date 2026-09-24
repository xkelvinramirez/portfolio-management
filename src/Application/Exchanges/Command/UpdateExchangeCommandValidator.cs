using FluentValidation;

namespace Application.Exchanges.Command;

public sealed class UpdateExchangeCommandValidator : AbstractValidator<UpdateExchangeCommand>
{
    public UpdateExchangeCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0);

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
