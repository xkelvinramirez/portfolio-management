
namespace Contracts.Portfolios;

public enum PortfolioAllocationGroupBy
{
    Asset,
    Exchange
}

public sealed record PortfolioAllocationItemResponse(
    long GroupId,
    string Label,
    decimal Value,
    decimal Percentage
    );

public sealed record PortfolioAllocationResponse(
    long PortfolioId,
    DateTime Date,
    PortfolioAllocationGroupBy GroupBy,
    List<PortfolioAllocationItemResponse> Items
    );
