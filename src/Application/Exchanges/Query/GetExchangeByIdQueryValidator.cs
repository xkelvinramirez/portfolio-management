using FluentValidation;

namespace Application.Exchanges.Query;

public sealed class GetExchangeByIdQueryValidator : AbstractValidator<GetExchangeByIdQuery>
{
    public GetExchangeByIdQueryValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0);
    }
}
