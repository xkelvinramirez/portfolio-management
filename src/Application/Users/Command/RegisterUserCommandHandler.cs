using Application.Common.Security;
using Application.Common.UnitOfWork;
using Application.Users.Interfaces;
using Contracts.Users;
using Domain.Entities;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Users.Command;

public sealed record RegisterUserCommand(RegisterUserRequest Request) : IRequest<ErrorOr<RegisterUserResponse>>;

public sealed class RegisterUserCommandHandler(
    ILogger<RegisterUserCommandHandler> logger,
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork
    ) : IRequestHandler<RegisterUserCommand, ErrorOr<RegisterUserResponse>>
{
    public async Task<ErrorOr<RegisterUserResponse>> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        logger.LogInformation("Handling RegisterUserCommand for Email: {Email}", request.Email);

        var existingUser = await userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existingUser is not null)
        {
            return Error.Conflict("User.AlreadyExists", $"A user with the email '{request.Email}' already exists.");
        }

        var passwordHash = passwordHasher.Hash(request.Password);
        var newUser = User.Create(request.Email, request.FirstName, request.LastName, passwordHash);

        await userRepository.AddAsync(newUser, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Registered new user with ID {UserId}", newUser.Id);

        return newUser.ToRegisterUserResponse();
    }
}
