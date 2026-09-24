
namespace Contracts.Portfolios;

public sealed class UpdatePortfolioRequest
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
