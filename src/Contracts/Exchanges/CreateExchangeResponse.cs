
namespace Contracts.Exchanges;

public sealed record CreateExchangeResponse(
    long Id,
    string Name,
    string ApiKey,
    DateTime CreatedAt
    );
