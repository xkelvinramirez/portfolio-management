using System.Net;
using Application.CryptoCurrencies.Interfaces;
using Application.MafAgent.Interfaces;
using Application.Reports.Interfaces;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Common.Persistence.Contexts;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace WebApi.MinimalAPI.Tests.MafAgent.Http;

/// <summary>Plan T002: the MafAgent test host boots with no external dependency and its doubles are wired in.</summary>
public sealed class MafAgentApiFactorySmokeTests : IClassFixture<MafAgentApiFactory>
{
    private readonly MafAgentApiFactory _factory;

    public MafAgentApiFactorySmokeTests(MafAgentApiFactory factory) => _factory = factory;

    [Fact]
    public async Task AuthenticatedRequest_ToAProtectedEndpoint_Returns200()
    {
        HttpClient client = _factory.CreateClientForUser(7);

        HttpResponseMessage response = await client.GetAsync(
            "/api/v1/Portfolio/?page=1&limit=10", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RequestWithoutTestIdentity_Returns401()
    {
        HttpClient client = _factory.CreateClient();

        HttpResponseMessage response = await client.GetAsync(
            "/api/v1/Portfolio/?page=1&limit=10", TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public void Services_UseTheTestDoublesAndAnInMemoryDatabase()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IServiceProvider services = scope.ServiceProvider;

        services.GetRequiredService<AppDbContext>().Database.ProviderName.Should().Be("Microsoft.EntityFrameworkCore.InMemory");
        services.GetRequiredService<IPendingActionRepository>().Should().BeOfType<InMemoryTransitionsPendingActionRepository>();
        services.GetRequiredService<TimeProvider>().Should().BeSameAs(_factory.Time);
        services.GetRequiredService<IMafAgentRunner>().Should().BeSameAs(_factory.Runner);
        services.GetRequiredService<IEmailSender>().Should().BeSameAs(_factory.EmailSender);
        services.GetRequiredService<ICoinGeckoClient>().Should().BeSameAs(_factory.CoinGecko);
        services.GetRequiredService<CosmosClient>().GetType().Name.Should().Contain("Proxy", "Cosmos must be a test double");
        services.GetRequiredService<IAddedEntityIdLog>().Should().NotBeNull();
    }

    [Fact]
    public async Task TransitionDouble_ClaimsOnce_AndMarksFailedOnlyFromExecuting()
    {
        DateTime now = _factory.Time.GetUtcNow().UtcDateTime;
        Guid actionId = Guid.Empty;
        await _factory.WithDbContextAsync(async db =>
        {
            MafConversation conversation = MafConversation.Create(7, "cipher", now);
            PendingAction action = PendingAction.Create(conversation.Id, 7, PendingActionOperation.CreateEntry, "payload", null, now);
            db.MafConversations.Add(conversation);
            db.PendingActions.Add(action);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            actionId = action.Id;
        });

        using IServiceScope scope = _factory.Services.CreateScope();
        IPendingActionRepository repository = scope.ServiceProvider.GetRequiredService<IPendingActionRepository>();

        (await repository.TryMarkFailedAsync(actionId, "X", now, TestContext.Current.CancellationToken)).Should().BeFalse("still Pending");
        (await repository.TryClaimAsync(actionId, 8, now, TestContext.Current.CancellationToken)).Should().BeFalse("another user");
        (await repository.TryClaimAsync(actionId, 7, now, TestContext.Current.CancellationToken)).Should().BeTrue();
        (await repository.TryClaimAsync(actionId, 7, now, TestContext.Current.CancellationToken)).Should().BeFalse("already claimed");
        (await repository.TryMarkFailedAsync(actionId, "X", now, TestContext.Current.CancellationToken)).Should().BeTrue();

        await _factory.WithDbContextAsync(async db =>
        {
            PendingAction stored = await db.PendingActions.AsNoTracking().SingleAsync(a => a.Id == actionId, TestContext.Current.CancellationToken);
            stored.Status.Should().Be(PendingActionStatus.Failed);
            stored.ErrorCode.Should().Be("X");
        });
    }
}
