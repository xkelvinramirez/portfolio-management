using Domain.Entities;
using FluentAssertions;
using Infrastructure.Common.Persistence.Contexts;
using Infrastructure.MafAgent.Persistence;
using Microsoft.EntityFrameworkCore;

namespace WebApi.MinimalAPI.Tests.MafAgent.Persistence;

/// <summary>Plan T012: after DiscardAll(), a SaveChanges persists nothing that was staged before it.</summary>
public sealed class TrackedChangesDiscarderTests
{
    [Fact]
    public async Task DiscardAll_DropsStagedChanges_SoSaveChangesPersistsNothing()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"discarder-{Guid.NewGuid():N}")
            .Options;
        DateTime now = new(2026, 11, 11, 10, 0, 0, DateTimeKind.Utc);

        await using (var db = new AppDbContext(options))
        {
            db.MafConversations.Add(MafConversation.Create(7, "cipher", now));

            new TrackedChangesDiscarder(db).DiscardAll();
            int written = await db.SaveChangesAsync(TestContext.Current.CancellationToken);

            written.Should().Be(0);
        }

        await using var verify = new AppDbContext(options);
        (await verify.MafConversations.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
    }

    [Fact]
    public async Task WithoutDiscard_TheSameStagedChangeIsPersisted()
    {
        DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"discarder-control-{Guid.NewGuid():N}")
            .Options;

        await using var db = new AppDbContext(options);
        db.MafConversations.Add(MafConversation.Create(7, "cipher", DateTime.UtcNow));

        int written = await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        written.Should().Be(1, "positive control: proves the discard test is not passing because nothing is ever saved");
    }
}
