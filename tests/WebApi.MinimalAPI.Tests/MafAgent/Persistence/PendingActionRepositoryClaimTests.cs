using Domain.Entities;
using FluentAssertions;
using Infrastructure.Common.Persistence.Contexts;
using Infrastructure.MafAgent.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace WebApi.MinimalAPI.Tests.MafAgent.Persistence;

/// <summary>
/// Atomic transitions of pending actions against the real development Postgres (plan T011; spec AC5,
/// AC6, AC7, AC12). The "at most once" guarantee lives in the conditional UPDATE, so an in-memory
/// provider would prove nothing here. Each test seeds its own throw-away user and removes it afterwards
/// (cascade deletes its conversations and actions).
/// Connection string: env var ConnectionStrings__Database, else the WebApi project's user-secrets.
/// </summary>
[Trait("Category", "Integration")]
public sealed class PendingActionRepositoryClaimTests : IAsyncLifetime
{
    private const string WebApiUserSecretsId = "daa01fcc-134f-4f9c-b600-67504a448d77";

    private readonly DbContextOptions<AppDbContext> _options = BuildOptions();
    private readonly DateTime _now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
    private long _userId;
    private long _otherUserId;
    private Guid _conversationId;

    public async ValueTask InitializeAsync()
    {
        await using AppDbContext db = NewContext();
        User owner = User.Create($"maf-it-{Guid.NewGuid():N}@test.local", "MafIt", null, "not-a-hash");
        User other = User.Create($"maf-it-{Guid.NewGuid():N}@test.local", "MafItOther", null, "not-a-hash");
        db.Users.AddRange(owner, other);
        await db.SaveChangesAsync();
        _userId = owner.Id;
        _otherUserId = other.Id;

        MafConversation conversation = MafConversation.Create(_userId, "cipher", _now);
        db.MafConversations.Add(conversation);
        await db.SaveChangesAsync();
        _conversationId = conversation.Id;
    }

    public async ValueTask DisposeAsync()
    {
        await using AppDbContext db = NewContext();
        await db.Users.Where(u => u.Id == _userId || u.Id == _otherUserId).ExecuteDeleteAsync();
    }

    // ---------- TryClaimAsync ----------

    [Fact]
    public async Task TryClaim_TwoConcurrentCallers_ExactlyOneWins()
    {
        Guid id = await SeedActionAsync(_now);

        bool[] results = await Task.WhenAll(ClaimInOwnContext(id), ClaimInOwnContext(id));

        results.Count(r => r).Should().Be(1, "the conditional UPDATE must let only one caller claim the action");
        (await StatusOf(id)).Should().Be(PendingActionStatus.Executing);
    }

    [Fact]
    public async Task TryClaim_AndTryReject_Concurrently_ExactlyOneWinsAndStateIsConsistent()
    {
        Guid id = await SeedActionAsync(_now);

        bool[] results = await Task.WhenAll(ClaimInOwnContext(id), RejectInOwnContext(id));

        results.Count(r => r).Should().Be(1);
        PendingActionStatus status = await StatusOf(id);
        status.Should().Be(results[0] ? PendingActionStatus.Executing : PendingActionStatus.Rejected);
    }

    [Fact]
    public async Task TryClaim_ActionOfAnotherUser_ReturnsFalseAndLeavesItPending()
    {
        Guid id = await SeedActionAsync(_now);

        await using AppDbContext db = NewContext();
        bool claimed = await new PendingActionRepository(db).TryClaimAsync(id, _otherUserId, _now, CancellationToken.None);

        claimed.Should().BeFalse();
        (await StatusOf(id)).Should().Be(PendingActionStatus.Pending);
    }

    [Fact]
    public async Task TryClaim_ExpiredAction_ReturnsFalse()
    {
        Guid id = await SeedActionAsync(_now.AddMinutes(-11));

        await using AppDbContext db = NewContext();
        bool claimed = await new PendingActionRepository(db).TryClaimAsync(id, _userId, _now, CancellationToken.None);

        claimed.Should().BeFalse();
        (await StatusOf(id)).Should().Be(PendingActionStatus.Pending);
    }

    [Fact]
    public async Task TryClaim_AlreadyClaimed_ReturnsFalse()
    {
        Guid id = await SeedActionAsync(_now);
        (await ClaimInOwnContext(id)).Should().BeTrue();

        (await ClaimInOwnContext(id)).Should().BeFalse();
    }

    // ---------- TryRejectAsync ----------

    [Fact]
    public async Task TryReject_Pending_MovesToRejectedWithDecisionTime()
    {
        Guid id = await SeedActionAsync(_now);

        (await RejectInOwnContext(id)).Should().BeTrue();

        PendingAction stored = await Load(id);
        stored.Status.Should().Be(PendingActionStatus.Rejected);
        stored.DecidedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task TryReject_OtherUserExpiredOrNotPending_ReturnsFalse()
    {
        Guid pending = await SeedActionAsync(_now);
        Guid expired = await SeedActionAsync(_now.AddMinutes(-11));
        Guid claimed = await SeedActionAsync(_now);
        (await ClaimInOwnContext(claimed)).Should().BeTrue();

        await using AppDbContext db = NewContext();
        var repository = new PendingActionRepository(db);
        (await repository.TryRejectAsync(pending, _otherUserId, _now, CancellationToken.None)).Should().BeFalse();
        (await repository.TryRejectAsync(expired, _userId, _now, CancellationToken.None)).Should().BeFalse();
        (await repository.TryRejectAsync(claimed, _userId, _now, CancellationToken.None)).Should().BeFalse();
    }

    // ---------- TryMarkFailedAsync ----------

    [Fact]
    public async Task TryMarkFailed_Executing_MovesToFailedWithErrorCodeAndDecisionTime()
    {
        Guid id = await SeedActionAsync(_now);
        (await ClaimInOwnContext(id)).Should().BeTrue();

        await using AppDbContext db = NewContext();
        bool failed = await new PendingActionRepository(db)
            .TryMarkFailedAsync(id, "MafAgent.PendingAction.UnexpectedFailure", _now, CancellationToken.None);

        failed.Should().BeTrue();
        PendingAction stored = await Load(id);
        stored.Status.Should().Be(PendingActionStatus.Failed);
        stored.ErrorCode.Should().Be("MafAgent.PendingAction.UnexpectedFailure");
        stored.DecidedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task TryMarkFailed_PendingOrAlreadyFinished_ReturnsFalse()
    {
        Guid pending = await SeedActionAsync(_now);
        Guid succeeded = await SeedActionAsync(_now);
        await using (AppDbContext setup = NewContext())
        {
            PendingAction action = await setup.PendingActions.SingleAsync(a => a.Id == succeeded, TestContext.Current.CancellationToken);
            action.MarkExecuting();
            action.MarkSucceeded("result-cipher", _now);
            await setup.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using AppDbContext db = NewContext();
        var repository = new PendingActionRepository(db);
        (await repository.TryMarkFailedAsync(pending, "X", _now, CancellationToken.None)).Should().BeFalse();
        (await repository.TryMarkFailedAsync(succeeded, "X", _now, CancellationToken.None)).Should().BeFalse();
        (await StatusOf(succeeded)).Should().Be(PendingActionStatus.Succeeded);
    }

    // ---------- Helpers ----------

    private async Task<Guid> SeedActionAsync(DateTime createdAt)
    {
        await using AppDbContext db = NewContext();
        PendingAction action = PendingAction.Create(
            _conversationId, _userId, PendingActionOperation.CreateEntry, "payload-cipher", null, createdAt);
        db.PendingActions.Add(action);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return action.Id;
    }

    private async Task<bool> ClaimInOwnContext(Guid id)
    {
        await using AppDbContext db = NewContext();
        return await new PendingActionRepository(db).TryClaimAsync(id, _userId, _now, CancellationToken.None);
    }

    private async Task<bool> RejectInOwnContext(Guid id)
    {
        await using AppDbContext db = NewContext();
        return await new PendingActionRepository(db).TryRejectAsync(id, _userId, _now, CancellationToken.None);
    }

    private async Task<PendingAction> Load(Guid id)
    {
        await using AppDbContext db = NewContext();
        return await db.PendingActions.AsNoTracking().SingleAsync(a => a.Id == id, TestContext.Current.CancellationToken);
    }

    private async Task<PendingActionStatus> StatusOf(Guid id) => (await Load(id)).Status;

    private AppDbContext NewContext() => new(_options);

    private static DbContextOptions<AppDbContext> BuildOptions()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddUserSecrets(WebApiUserSecretsId)
            .AddEnvironmentVariables()
            .Build();
        string connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException(
                "Integration tests need ConnectionStrings:Database (env var ConnectionStrings__Database or WebApi user-secrets).");

        return new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
    }
}
