
namespace Contracts.CryptoCurrencies;

public sealed record UpdateCryptoCurrencyResponse(
    long Id,
    string Symbol,
    string Name,
    DateTime CreatedAt
    );
