using Domain.Entities;

namespace Application.Common.Security;

public sealed record AuthToken(string AccessToken, DateTime ExpiresAtUtc);

public interface IJwtTokenGenerator
{
    AuthToken GenerateToken(User user);
}
