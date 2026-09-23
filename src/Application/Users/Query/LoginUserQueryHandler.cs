using Application.Common.Security;
using Application.Users.Interfaces;
using Contracts.Users;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Users.Query;

public sealed record LoginUserQuery(LoginUserRequest Request) : IRequest<ErrorOr<LoginUserResponse>>;

public sealed class LoginUserQueryHandler(
    ILogger<LoginUserQueryHandler> logger,
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwtTokenGenerator
    ) : IRequestHandler<LoginUserQuery, ErrorOr<LoginUserResponse>>
{
    public async Task<ErrorOr<LoginUserResponse>> Handle(LoginUserQuery query, CancellationToken cancellationToken)
    {
        var request = query.Request;

        var user = await userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            logger.LogWarning("Failed login attempt for Email: {Email}", request.Email);
            return Error.Unauthorized("User.InvalidCredentials", "Invalid email or password.");
        }

        var token = jwtTokenGenerator.GenerateToken(user);

        return new LoginUserResponse(token.AccessToken, token.ExpiresAtUtc);
    }
}
