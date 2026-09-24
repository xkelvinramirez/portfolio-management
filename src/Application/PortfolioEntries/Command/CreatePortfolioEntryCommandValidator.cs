using FluentValidation;

namespace Application.PortfolioEntries.Command;

public sealed class CreatePortfolioEntryCommandValidator : AbstractValidator<CreatePortfolioEntryCommand>
{
    public CreatePortfolioEntryCommandValidator()
    {
        RuleFor(x => x.Request)
            .NotNull()
            .WithMessage("Request cannot be null.");

        RuleFor(x => x.Request.PortfolioId)
            .GreaterThan(0);

        RuleFor(x => x.Request.CryptoCurrencyId)
            .GreaterThan(0);

        RuleFor(x => x.Request.ExchangeId)
            .GreaterThan(0);

        RuleFor(x => x.Request.Quantity)
            .GreaterThan(0);

        RuleFor(x => x.Request.PricePerUnit)
            .GreaterThanOrEqualTo(0);

        RuleFor(x => x.Request.RecordedAt)
            .NotEqual(default(DateTime));
    }
}
