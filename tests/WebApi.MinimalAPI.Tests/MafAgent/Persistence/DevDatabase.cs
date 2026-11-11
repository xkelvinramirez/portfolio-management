using Domain.Entities;
using Infrastructure.Common.Persistence.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace WebApi.MinimalAPI.Tests.MafAgent.Persistence;

/// <summary>
/// Every test class that touches the shared development database joins this collection, so they never
/// run in parallel with each other: one class's leftover sweep can't delete another class's live users.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DevDatabaseCollection
{
    public const string Name = "DevDatabase";
}

/// <summary>
/// Shared access to the development Postgres for Category=Integration tests (plan T011/T037/T033).
/// Connection string: env var ConnectionStrings__Database, else the WebApi project's user-secrets.
/// Test users are created with the <see cref="TestUserEmailPrefix"/> prefix and deleted afterwards
/// (cascade removes their portfolios, conversations and pending actions).
/// </summary>
internal static class DevDatabase
{
    public const string TestUserEmailPrefix = "maf-it-";
    public const string TestUserEmailDomain = "@test.local";
    private const string WebApiUserSecretsId = "daa01fcc-134f-4f9c-b600-67504a448d77";

    private static readonly Lazy<string> ConnectionString = new(() =>
        new ConfigurationBuilder()
            .AddUserSecrets(WebApiUserSecretsId)
            .AddEnvironmentVariables()
            .Build()
            .GetConnectionString("Database")
        ?? throw new InvalidOperationException(
            "Integration tests need ConnectionStrings:Database (env var ConnectionStrings__Database or WebApi user-secrets)."));

    public static DbContextOptions<AppDbContext> Options(params IInterceptor[] interceptors) =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString.Value)
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(interceptors)
            .Options;

    public static User NewTestUser(string firstName) =>
        User.Create($"{TestUserEmailPrefix}{Guid.NewGuid():N}{TestUserEmailDomain}", firstName, null, "not-a-hash");

    /// <summary>Removes test users left behind by a run that was killed before its cleanup.</summary>
    public static Task SweepTestUsersAsync(AppDbContext db, CancellationToken ct) =>
        db.Users
            .Where(u => u.Email.StartsWith(TestUserEmailPrefix) && u.Email.EndsWith(TestUserEmailDomain))
            .ExecuteDeleteAsync(ct);
}
