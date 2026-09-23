
namespace Contracts.Portfolios;

public sealed class CreatePortfolioRequest
{
    public long UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
