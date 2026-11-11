using FluentValidation;

namespace Application.MafAgent.Command;

public sealed class ConfirmPendingActionCommandValidator : AbstractValidator<ConfirmPendingActionCommand>
{
    public ConfirmPendingActionCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}
