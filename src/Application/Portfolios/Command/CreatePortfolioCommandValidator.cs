using FluentValidation;

namespace Application.Portfolios.Command;

public sealed class CreatePortfolioCommandValidator: AbstractValidator<CreatePortfolioCommand>
{
    public CreatePortfolioCommandValidator()
    {
        RuleFor(x => x.Request)
            .NotNull()
            .WithMessage("Request cannot be null.");
        RuleFor(x => x.Request.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Request.Description)
            .MaximumLength(500);
    }
}
