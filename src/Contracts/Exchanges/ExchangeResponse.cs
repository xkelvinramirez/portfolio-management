
namespace Contracts.Exchanges;

public sealed record ExchangeResponse(
    long Id,
    string Name,
    string ApiKey,
    DateTime CreatedAt
    );
