using FluentValidation;

namespace Application.PortfolioEntries.Command;

public sealed class DeletePortfolioEntryCommandValidator : AbstractValidator<DeletePortfolioEntryCommand>
{
    public DeletePortfolioEntryCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0);
    }
}
