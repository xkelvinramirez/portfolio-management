using FluentValidation;

namespace Application.MafAgent.Command;

public sealed class SendMafAgentMessageCommandValidator : AbstractValidator<SendMafAgentMessageCommand>
{
    public const int MaxMessageLength = 2000;

    public SendMafAgentMessageCommandValidator()
    {
        RuleFor(c => c.Request).NotNull();
        RuleFor(c => c.Request.Message)
            .NotEmpty()
            .MaximumLength(MaxMessageLength)
            .When(c => c.Request is not null);
    }
}
