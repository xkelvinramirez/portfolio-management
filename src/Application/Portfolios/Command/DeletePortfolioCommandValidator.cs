using FluentValidation;

namespace Application.Portfolios.Command;

public sealed class DeletePortfolioCommandValidator : AbstractValidator<DeletePortfolioCommand>
{
    public DeletePortfolioCommandValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0);
    }
}
