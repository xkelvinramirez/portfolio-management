using Domain.Common;

namespace Domain.Entities;

public sealed class User : Entity
{
    public required string Email { get; set; }
    public required string FirstName { get; set; }
    public string? LastName { get; set; }
    public required string PasswordHash { get; set; }

    public User() { }

    public static User Create(string email, string firstName, string? lastName, string passwordHash)
    {
        return new User
        {
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            PasswordHash = passwordHash
        };
    }
}

