
namespace Contracts.Users;

public sealed record LoginUserResponse(
    string AccessToken,
    DateTime ExpiresAtUtc
    );
