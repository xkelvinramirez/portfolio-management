using Domain.Common;

namespace Domain.Entities;

public sealed class User : Entity
{
    public required string Email { get; set; }
    public required string FirstName { get; set; }
    public string? LastName { get; set; }
    public required string PasswordHash { get; set; }
}

