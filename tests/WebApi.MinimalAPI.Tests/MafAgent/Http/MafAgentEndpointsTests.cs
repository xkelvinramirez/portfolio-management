using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Common.Security;
using Application.MafAgent.Command;
using Application.MafAgent.Interfaces;
using Application.MafAgent.Payloads;
using Contracts.MafAgent;
using Domain.Common.Exceptions;
using Domain.Entities;
using ErrorOr;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ClearExtensions;
using NSubstitute.ExceptionExtensions;

namespace WebApi.MinimalAPI.Tests.MafAgent.Http;

/// <summary>
/// Plan T031 (AC4, AC7, AC13): the MafAgent endpoints over the in-process host (T002). The agent runner is a
/// double, so no model runs. Every test uses its OWN user id and clears the shared runner double; the shared
/// clock is never advanced (an expired action is seeded in the past instead).
/// </summary>
public sealed class MafAgentEndpointsTests : IClassFixture<MafAgentApiFactory>
{
    private const string Base = "/api/v1/MafAgent";

    private readonly MafAgentApiFactory _factory;
    private readonly IEncryptionService _encryption;

    public MafAgentEndpointsTests(MafAgentApiFactory factory)
    {
        _factory = factory;
        _factory.Runner.ClearReceivedCalls();
        _factory.Runner.ClearSubstitute(ClearOptions.ReturnValues);
        _factory.Runner.AppendSystemNoteAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(c => c.ArgAt<string>(0) + " ");
        _encryption = _factory.Services.GetRequiredService<IEncryptionService>();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private DateTime Now => _factory.Time.GetUtcNow().UtcDateTime;

    private async Task<MafConversation> SeedConversationAsync(long userId, DateTime? lastActivity = null)
    {
        MafConversation conversation = MafConversation.Create(userId, _encryption.Encrypt(""), lastActivity ?? Now);
        await _factory.WithDbContextAsync(async db =>
        {
            db.MafConversations.Add(conversation);
            await db.SaveChangesAsync(Token);
        });
        return conversation;
    }

    private PendingAction NewAction(long userId, Guid conversationId, PendingActionOperation op, object payload, DateTime? createdAt = null) =>
        PendingAction.Create(conversationId, userId, op, _encryption.Encrypt(JsonSerializer.Serialize(payload, payload.GetType())), null, createdAt ?? Now);

    private async Task<PendingAction> SeedActionAsync(long userId, PendingActionOperation op, object payload, DateTime? createdAt = null)
    {
        MafConversation conversation = await SeedConversationAsync(userId);
        PendingAction action = NewAction(userId, conversation.Id, op, payload, createdAt);
        await _factory.WithDbContextAsync(async db =>
        {
            db.PendingActions.Add(action);
            await db.SaveChangesAsync(Token);
        });
        return action;
    }

    private Task<PendingAction> SeedCreatePortfolioAsync(long userId, DateTime? createdAt = null) =>
        SeedActionAsync(userId, PendingActionOperation.CreatePortfolio, new CreatePortfolioPayload("Ahorro", ""), createdAt);

    private async Task<(long PortfolioId, long CryptoId, long ExchangeId)> SeedEntryCatalogAsync(long userId)
    {
        Portfolio portfolio = Portfolio.Create(userId, $"P{userId}", "");
        CryptoCurrency crypto = CryptoCurrency.Create($"C{userId}", "Coin");
        Exchange exchange = Exchange.Create($"E{userId}", "key");
        await _factory.WithDbContextAsync(async db =>
        {
            db.AddRange(portfolio, crypto, exchange);
            await db.SaveChangesAsync(Token);
        });
        return (portfolio.Id, crypto.Id, exchange.Id);
    }

    private async Task<PendingActionStatus> StatusOfAsync(Guid id)
    {
        PendingActionStatus status = default;
        await _factory.WithDbContextAsync(async db =>
            status = (await db.PendingActions.AsNoTracking().SingleAsync(a => a.Id == id, Token)).Status);
        return status;
    }

    private static async Task<string> ErrorCodeAsync(HttpResponseMessage response)
    {
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        return body.RootElement[0].GetProperty("code").GetString()!;
    }

    private void RunnerAnswers(string reply, Func<MafConversation, IReadOnlyList<PendingAction>>? proposed = null) =>
        _factory.Runner.RunTurnAsync(Arg.Any<MafConversation>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(c => Task.FromResult(new MafAgentTurnResult(reply, "{}", proposed?.Invoke(c.ArgAt<MafConversation>(0)) ?? [])));

    // ---- chat ----

    [Fact]
    public async Task Chat_StartsAConversation_AndReturnsTheTurnsPendingActions()
    {
        const long user = 3101;
        PendingAction? proposed = null;
        RunnerAnswers("Propuesto.", conversation =>
        {
            proposed = NewAction(user, conversation.Id, PendingActionOperation.CreatePortfolio, new CreatePortfolioPayload("Ahorro", ""));
            return [proposed];
        });

        HttpResponseMessage response = await _factory.CreateClientForUser(user)
            .PostAsJsonAsync($"{Base}/chat", new { conversationId = (Guid?)null, message = "crea Ahorro" }, Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        Guid conversationId = body.RootElement.GetProperty("conversationId").GetGuid();
        body.RootElement.GetProperty("reply").GetString().Should().Be("Propuesto.");
        JsonElement action = body.RootElement.GetProperty("pendingActions").EnumerateArray().Should().ContainSingle().Subject;
        action.GetProperty("id").GetGuid().Should().Be(proposed!.Id);
        action.GetProperty("operation").GetString().Should().Be("CreatePortfolio");
        await _factory.WithDbContextAsync(async db =>
            (await db.MafConversations.AsNoTracking().SingleAsync(c => c.Id == conversationId, Token)).UserId.Should().Be(user));
    }

    [Theory]
    [InlineData("sí")]
    [InlineData("confirmo")]
    [InlineData("{id}")]
    public async Task Chat_SayingYesOrTheId_NeverConfirmsAnything(string text)
    {
        const long user = 3102;
        PendingAction action = await SeedCreatePortfolioAsync(user);
        RunnerAnswers("Para confirmar usa el botón.");

        HttpResponseMessage response = await _factory.CreateClientForUser(user).PostAsJsonAsync(
            $"{Base}/chat", new { conversationId = action.ConversationId, message = text.Replace("{id}", action.Id.ToString()) }, Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await StatusOfAsync(action.Id)).Should().Be(PendingActionStatus.Pending, "only the confirm endpoint runs a write (AC4)");
        await _factory.WithDbContextAsync(async db =>
            (await db.Portfolios.AsNoTracking().AnyAsync(p => p.UserId == user, Token)).Should().BeFalse());
    }

    [Fact]
    public async Task Chat_InAnotherUsersConversation_IsNotFound_WithoutRunningTheAgent()
    {
        MafConversation foreign = await SeedConversationAsync(3103);

        HttpResponseMessage response = await _factory.CreateClientForUser(3104)
            .PostAsJsonAsync($"{Base}/chat", new { conversationId = foreign.Id, message = "hola" }, Token);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ErrorCodeAsync(response)).Should().Be("MafAgent.Conversation.NotFound");
        await _factory.Runner.DidNotReceiveWithAnyArgs().RunTurnAsync(default!, default!, default!, Token);
    }

    [Fact]
    public async Task Chat_InAConversationIdleForMoreThan24Hours_IsConflictExpired()
    {
        MafConversation idle = await SeedConversationAsync(3105, Now - TimeSpan.FromHours(25));

        HttpResponseMessage response = await _factory.CreateClientForUser(3105)
            .PostAsJsonAsync($"{Base}/chat", new { conversationId = idle.Id, message = "hola" }, Token);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(response)).Should().Be("MafAgent.Conversation.Expired");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2001)]
    public async Task Chat_WithAnEmptyOrTooLongMessage_IsBadRequest(int length)
    {
        HttpResponseMessage response = await _factory.CreateClientForUser(3106)
            .PostAsJsonAsync($"{Base}/chat", new { conversationId = (Guid?)null, message = new string('a', length) }, Token);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Chat_WhenTheAgentExceedsItsSteps_IsBadRequestLoopExhausted_AndNothingIsSaved()
    {
        const long user = 3107;
        _factory.Runner.RunTurnAsync(Arg.Any<MafConversation>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AgentLoopExhaustedException(5));

        HttpResponseMessage response = await _factory.CreateClientForUser(user)
            .PostAsJsonAsync($"{Base}/chat", new { conversationId = (Guid?)null, message = "bucle" }, Token);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ErrorCodeAsync(response)).Should().Be("MafAgent.LoopExhausted");
        await _factory.WithDbContextAsync(async db =>
            (await db.MafConversations.AsNoTracking().AnyAsync(c => c.UserId == user, Token)).Should().BeFalse("a failed turn commits nothing (AC14)"));
    }

    // ---- confirm ----

    [Fact]
    public async Task Confirm_ACreateEntry_Succeeds_WithTheRealIdOfTheInsertedEntry()
    {
        const long user = 3108;
        (long portfolioId, long cryptoId, long exchangeId) = await SeedEntryCatalogAsync(user);
        PendingAction action = await SeedActionAsync(user, PendingActionOperation.CreateEntry,
            new CreateEntryPayload(portfolioId, cryptoId, exchangeId, 0.5m, 65000m, new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc)));

        HttpResponseMessage response = await _factory.CreateClientForUser(user).PostAsync($"{Base}/pending-actions/{action.Id}/confirm", null, Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token));
        body.RootElement.GetProperty("status").GetString().Should().Be("Succeeded");
        long entryId = body.RootElement.GetProperty("result").GetProperty("id").GetInt64();
        entryId.Should().BeGreaterThan(0);
        await _factory.WithDbContextAsync(async db =>
        {
            PortfolioEntry entry = await db.PortfolioEntries.AsNoTracking().SingleAsync(e => e.PortfolioId == portfolioId, Token);
            entry.Id.Should().Be(entryId);
            entry.Quantity.Should().Be(0.5m);
        });
        (await StatusOfAsync(action.Id)).Should().Be(PendingActionStatus.Succeeded);
    }

    [Fact]
    public async Task Confirm_AnUnknownId_AndAnotherUsersAction_AreTheSameNotFound()
    {
        PendingAction foreign = await SeedCreatePortfolioAsync(3109);
        HttpClient caller = _factory.CreateClientForUser(3110);

        HttpResponseMessage unknown = await caller.PostAsync($"{Base}/pending-actions/{Guid.NewGuid()}/confirm", null, Token);
        HttpResponseMessage other = await caller.PostAsync($"{Base}/pending-actions/{foreign.Id}/confirm", null, Token);

        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        other.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ErrorCodeAsync(other)).Should().Be(await ErrorCodeAsync(unknown)).And.Be("MafAgent.PendingAction.NotFound");
        (await StatusOfAsync(foreign.Id)).Should().Be(PendingActionStatus.Pending, "nothing is written (AC7)");
    }

    [Fact]
    public async Task Confirm_Twice_RunsOnce_AndTheSecondIsConflictAlreadyDecided()
    {
        const long user = 3111;
        PendingAction action = await SeedCreatePortfolioAsync(user);
        HttpClient client = _factory.CreateClientForUser(user);

        HttpResponseMessage first = await client.PostAsync($"{Base}/pending-actions/{action.Id}/confirm", null, Token);
        HttpResponseMessage second = await client.PostAsync($"{Base}/pending-actions/{action.Id}/confirm", null, Token);

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(second)).Should().Be("MafAgent.PendingAction.AlreadyDecided");
        await _factory.WithDbContextAsync(async db =>
            (await db.Portfolios.AsNoTracking().CountAsync(p => p.UserId == user, Token)).Should().Be(1, "AC5: the change exists once"));
    }

    [Fact]
    public async Task Confirm_AnExpiredAction_IsConflictExpired_AndWritesNothing()
    {
        const long user = 3112;
        PendingAction action = await SeedCreatePortfolioAsync(user, Now - TimeSpan.FromMinutes(11));

        HttpResponseMessage response = await _factory.CreateClientForUser(user).PostAsync($"{Base}/pending-actions/{action.Id}/confirm", null, Token);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(response)).Should().Be("MafAgent.PendingAction.Expired");
        (await StatusOfAsync(action.Id)).Should().Be(PendingActionStatus.Pending);
    }

    // ---- reject ----

    [Fact]
    public async Task Reject_IsNoContent_AndAfterwardsConfirmIsConflict()
    {
        const long user = 3113;
        PendingAction action = await SeedCreatePortfolioAsync(user);
        HttpClient client = _factory.CreateClientForUser(user);

        HttpResponseMessage rejected = await client.PostAsync($"{Base}/pending-actions/{action.Id}/reject", null, Token);
        HttpResponseMessage confirmed = await client.PostAsync($"{Base}/pending-actions/{action.Id}/confirm", null, Token);

        rejected.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await StatusOfAsync(action.Id)).Should().Be(PendingActionStatus.Rejected);
        confirmed.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await _factory.WithDbContextAsync(async db =>
            (await db.Portfolios.AsNoTracking().AnyAsync(p => p.UserId == user, Token)).Should().BeFalse("AC6"));
    }

    [Fact]
    public async Task Reject_AnotherUsersAction_IsNotFound()
    {
        PendingAction foreign = await SeedCreatePortfolioAsync(3114);

        HttpResponseMessage response = await _factory.CreateClientForUser(3115).PostAsync($"{Base}/pending-actions/{foreign.Id}/reject", null, Token);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await StatusOfAsync(foreign.Id)).Should().Be(PendingActionStatus.Pending);
    }

    [Fact]
    public async Task Reject_Twice_IsConflictAlreadyDecided_AndAnUnknownIdIsNotFound()
    {
        const long user = 3116;
        PendingAction action = await SeedCreatePortfolioAsync(user);
        HttpClient client = _factory.CreateClientForUser(user);

        HttpResponseMessage first = await client.PostAsync($"{Base}/pending-actions/{action.Id}/reject", null, Token);
        HttpResponseMessage second = await client.PostAsync($"{Base}/pending-actions/{action.Id}/reject", null, Token);
        HttpResponseMessage unknown = await client.PostAsync($"{Base}/pending-actions/{Guid.NewGuid()}/reject", null, Token);

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(second)).Should().Be("MafAgent.PendingAction.AlreadyDecided");
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // Plan §0 "Tras el reclamo" 2: once claimed, a client disconnect must not cancel the write or the commit of its
    // final state, so the endpoint sends the command WITHOUT the request's token.
    [Fact]
    public async Task Confirm_SendsTheCommandWithoutTheRequestCancellationToken()
    {
        const long user = 3117;
        PendingAction action = await SeedCreatePortfolioAsync(user);
        TokenProbe probe = new();
        using WebApplicationFactory<Program> host = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IPipelineBehavior<ConfirmPendingActionCommand, ErrorOr<PendingActionResultDto>>>(probe)));
        HttpClient client = host.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, user.ToString());

        HttpResponseMessage response = await client.PostAsync($"{Base}/pending-actions/{action.Id}/confirm", null, Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        probe.Seen.Should().BeTrue();
        probe.Token.CanBeCanceled.Should().BeFalse("the request's token can be cancelled by a disconnect; CancellationToken.None cannot");
    }

    private sealed class TokenProbe : IPipelineBehavior<ConfirmPendingActionCommand, ErrorOr<PendingActionResultDto>>
    {
        public bool Seen { get; private set; }

        public CancellationToken Token { get; private set; }

        public Task<ErrorOr<PendingActionResultDto>> Handle(
            ConfirmPendingActionCommand request, RequestHandlerDelegate<ErrorOr<PendingActionResultDto>> next, CancellationToken cancellationToken)
        {
            Seen = true;
            Token = cancellationToken;
            return next(cancellationToken);
        }
    }
    // ---- authentication ----

    [Theory]
    [InlineData("chat")]
    [InlineData("pending-actions/00000000-0000-0000-0000-000000000001/confirm")]
    [InlineData("pending-actions/00000000-0000-0000-0000-000000000001/reject")]
    public async Task WithoutIdentity_EveryEndpointIsUnauthorized(string path)
    {
        HttpResponseMessage response = await _factory.CreateClient().PostAsJsonAsync($"{Base}/{path}", new { message = "hola" }, Token);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
