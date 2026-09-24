
namespace Contracts.Exchanges;

public sealed record UpdateExchangeResponse(
    long Id,
    string Name,
    string ApiKey,
    DateTime CreatedAt
    );
