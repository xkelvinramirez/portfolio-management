using FluentValidation;

namespace Application.MafAgent.Command;

public sealed class RejectPendingActionCommandValidator : AbstractValidator<RejectPendingActionCommand>
{
    public RejectPendingActionCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}
