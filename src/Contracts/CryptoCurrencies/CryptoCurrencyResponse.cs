
namespace Contracts.CryptoCurrencies;

public sealed record CryptoCurrencyResponse(
    long Id,
    string Symbol,
    string Name,
    DateTime CreatedAt
    );
