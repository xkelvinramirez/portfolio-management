using Domain.Entities;
using FluentAssertions;
using Infrastructure.Common.Persistence.Contexts;
using Infrastructure.MafAgent.Persistence;
using Microsoft.EntityFrameworkCore;

namespace WebApi.MinimalAPI.Tests.MafAgent.Persistence;

/// <summary>
/// Plan T037 against real Postgres. Unlike the EF InMemory provider (which assigns ids on Add), Postgres
/// assigns identity ids during the INSERT, so this is the only place that proves the interceptor reads the
/// id AFTER commit: before SaveChanges the entity id is 0 (exactly the analyze G2 problem).
/// </summary>
[Trait("Category", "Integration")]
[Collection(DevDatabaseCollection.Name)]
public sealed class AddedEntityIdLogIntegrationTests : IAsyncLifetime
{
    private long _userId;

    public async ValueTask InitializeAsync()
    {
        await using var db = new AppDbContext(DevDatabase.Options());
        User user = DevDatabase.NewTestUser("MafItIdLog");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        _userId = user.Id;
    }

    public async ValueTask DisposeAsync()
    {
        await using var db = new AppDbContext(DevDatabase.Options());
        await db.Users.Where(u => u.Id == _userId).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task LastAddedId_OnPostgres_IsTheIdentityAssignedByTheInsert()
    {
        var log = new AddedEntityIdLog();
        await using var db = new AppDbContext(DevDatabase.Options(log));
        Portfolio portfolio = Portfolio.Create(_userId, $"it-{Guid.NewGuid():N}", "added-id-log");
        db.Portfolios.Add(portfolio);

        portfolio.Id.Should().Be(0, "Postgres identity ids do not exist before the INSERT");
        log.LastAddedId<Portfolio>().Should().BeNull();

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        portfolio.Id.Should().BeGreaterThan(0);
        log.LastAddedId<Portfolio>().Should().Be(portfolio.Id);
    }
}
