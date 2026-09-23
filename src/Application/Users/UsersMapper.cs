using Contracts.Users;
using Domain.Entities;

namespace Application.Users;

public static class UsersMapper
{
    public static RegisterUserResponse ToRegisterUserResponse(this User user)
    {
        return new RegisterUserResponse(user.Id, user.Email);
    }
}
