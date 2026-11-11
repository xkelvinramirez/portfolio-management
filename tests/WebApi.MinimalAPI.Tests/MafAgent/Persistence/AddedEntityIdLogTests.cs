using Domain.Entities;
using FluentAssertions;
using Infrastructure.Common.Persistence.Contexts;
using Infrastructure.MafAgent.Persistence;
using Microsoft.EntityFrameworkCore;

namespace WebApi.MinimalAPI.Tests.MafAgent.Persistence;

/// <summary>
/// Plan T037 (analyze G2): create handlers build their response before the unit of work commits, so the
/// identity id they report is 0. The interceptor records the real database id after SaveChanges.
/// </summary>
public sealed class AddedEntityIdLogTests
{
    private static readonly DateTime Now = new(2026, 11, 11, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task LastAddedId_AfterInsertingAnEntry_ReturnsTheAssignedId()
    {
        var log = new AddedEntityIdLog();
        await using AppDbContext db = NewContext(log);
        PortfolioEntry entry = PortfolioEntry.Create(1, 1, 1, 0.5m, 60000m, Now);
        db.PortfolioEntries.Add(entry);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        entry.Id.Should().BeGreaterThan(0);
        log.LastAddedId<PortfolioEntry>().Should().Be(entry.Id);
    }

    [Fact]
    public void LastAddedId_WithoutInserts_IsNull()
    {
        var log = new AddedEntityIdLog();

        log.LastAddedId<PortfolioEntry>().Should().BeNull();
    }

    [Fact]
    public async Task LastAddedId_AfterTwoInsertsOfTheSameType_ReturnsTheLatest()
    {
        var log = new AddedEntityIdLog();
        await using AppDbContext db = NewContext(log);
        Portfolio first = Portfolio.Create(7, "Primero", "a");
        db.Portfolios.Add(first);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        Portfolio second = Portfolio.Create(7, "Segundo", "b");
        db.Portfolios.Add(second);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        second.Id.Should().NotBe(first.Id);
        log.LastAddedId<Portfolio>().Should().Be(second.Id);
    }

    [Fact]
    public async Task LastAddedId_DoesNotMixEntityTypes()
    {
        var log = new AddedEntityIdLog();
        await using AppDbContext db = NewContext(log);
        Portfolio portfolio = Portfolio.Create(7, "Principal", "a");
        db.Portfolios.Add(portfolio);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        log.LastAddedId<Portfolio>().Should().Be(portfolio.Id);
        log.LastAddedId<PortfolioEntry>().Should().BeNull();
    }

    [Fact]
    public async Task LastAddedId_UpdatesAreNotInserts()
    {
        string databaseName = $"added-id-log-{Guid.NewGuid():N}";
        await using (AppDbContext db = NewContext(new AddedEntityIdLog(), databaseName))
        {
            db.Portfolios.Add(Portfolio.Create(7, "Principal", "a"));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var freshLog = new AddedEntityIdLog();
        await using AppDbContext second = NewContext(freshLog, databaseName);

        Portfolio loaded = await second.Portfolios.SingleAsync(TestContext.Current.CancellationToken);
        loaded.Name = "Renombrado";
        await second.SaveChangesAsync(TestContext.Current.CancellationToken);

        freshLog.LastAddedId<Portfolio>().Should().BeNull();
    }

    private static AppDbContext NewContext(AddedEntityIdLog log, string? databaseName = null) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? $"added-id-log-{Guid.NewGuid():N}")
            .AddInterceptors(log)
            .Options);
}
