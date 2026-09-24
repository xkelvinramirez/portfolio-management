using FluentValidation;

namespace Application.Exchanges.Command;

public sealed class DeleteExchangeCommandValidator : AbstractValidator<DeleteExchangeCommand>
{
    public DeleteExchangeCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0);
    }
}
