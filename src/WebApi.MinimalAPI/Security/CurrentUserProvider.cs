using System.IdentityModel.Tokens.Jwt;
using Application.Common.Security;

namespace WebApi.MinimalAPI.Security;

internal sealed class CurrentUserProvider(IHttpContextAccessor httpContextAccessor) : ICurrentUserProvider
{
    public long UserId
    {
        get
        {
            var sub = httpContextAccessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            if (sub is null || !long.TryParse(sub, out var userId))
            {
                throw new InvalidOperationException("No authenticated user id available on the current request.");
            }

            return userId;
        }
    }
}
