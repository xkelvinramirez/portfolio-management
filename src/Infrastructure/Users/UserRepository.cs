using Application.Users.Interfaces;
using Domain.Entities;
using Infrastructure.Common.Persistence.Contexts;
using Infrastructure.Common.Repository;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Users;

internal sealed class UserRepository(AppDbContext dbContext) : Repository<User>(dbContext), IUserRepository
{
    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
        => dbContext.Set<User>().FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
}
