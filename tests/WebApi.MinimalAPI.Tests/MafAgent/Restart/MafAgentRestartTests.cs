using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Application.CryptoCurrencies.Interfaces;
using Application.Reports.Interfaces;
using Domain.Entities;
using User = Domain.Entities.User;
using FluentAssertions;
using Infrastructure.Common.Persistence.Contexts;
using Infrastructure.MafAgent.Runner;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using WebApi.MinimalAPI.Tests.MafAgent.Http;
using WebApi.MinimalAPI.Tests.MafAgent.Persistence;
using WebApi.MinimalAPI.Tests.MafAgent.Runner;

namespace WebApi.MinimalAPI.Tests.MafAgent.Restart;

/// <summary>
/// Plan T033 (AC3, AC15) against the development Postgres. Everything is real except the model (a scripted
/// <see cref="IChatClient"/>) and the external systems: the MAF runner, its tools, the encrypted session, the
/// repositories with their atomic ExecuteUpdate claim, and the AddedEntityIdLog interceptor. A first host proposes
/// an entry from the chat; a SECOND host (a fresh ServiceProvider, as after an API restart) continues the
/// conversation and confirms the action.
/// </summary>
[Trait("Category", "Integration")]
[Collection(DevDatabaseCollection.Name)]
public sealed class MafAgentRestartTests : IAsyncLifetime
{
    private const string Base = "/api/v1/MafAgent";

    private readonly string _symbol = "IT" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
    private readonly string _exchangeName = "it-ex-" + Guid.NewGuid().ToString("N")[..8];
    private long _userId;
    private long _portfolioId;
    private long _cryptoId;
    private long _exchangeId;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await using AppDbContext db = new(DevDatabase.Options());
        User user = DevDatabase.NewTestUser("MafItRestart");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        _userId = user.Id;

        Portfolio portfolio = Portfolio.Create(_userId, $"it-{Guid.NewGuid():N}", "restart");
        CryptoCurrency crypto = CryptoCurrency.Create(_symbol, "Integration coin");
        Exchange exchange = Exchange.Create(_exchangeName, "not-a-key");
        db.AddRange(portfolio, crypto, exchange);
        await db.SaveChangesAsync();
        (_portfolioId, _cryptoId, _exchangeId) = (portfolio.Id, crypto.Id, exchange.Id);
    }

    public async ValueTask DisposeAsync()
    {
        await using AppDbContext db = new(DevDatabase.Options());
        // The user cascade removes its portfolio, entries, conversations and pending actions; the catalog rows go after.
        await db.Users.Where(u => u.Id == _userId).ExecuteDeleteAsync();
        await db.CryptoCurrencies.Where(c => c.Id == _cryptoId).ExecuteDeleteAsync();
        await db.Exchanges.Where(e => e.Id == _exchangeId).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task AConversationAndItsAction_SurviveARestart_AndConfirmingInsertsTheEntryWithItsRealId()
    {
        // ---- first host: the chat proposes an entry ----
        ScriptedChatClient firstModel = new((call, _) => call == 0
            ? ScriptedChatClient.ToolCall("ProposeCreateEntry", new Dictionary<string, object?>
            {
                ["portfolioId"] = _portfolioId,
                ["symbol"] = _symbol.ToLowerInvariant(),
                ["exchangeName"] = _exchangeName.ToUpperInvariant(),
                ["quantity"] = 0.25m,
                ["pricePerUnit"] = 64000m,
                ["recordedAt"] = "2026-11-10",
            })
            : ScriptedChatClient.Text("He propuesto la entrada; confírmala."));

        Guid conversationId;
        Guid actionId;
        using (PostgresHost first = new(firstModel))
        {
            HttpResponseMessage chat = await first.CreateClientForUser(_userId).PostAsJsonAsync(
                $"{Base}/chat", new { conversationId = (Guid?)null, message = "Registra 0.25 a 64000 del 10 de noviembre" }, Token);

            chat.StatusCode.Should().Be(HttpStatusCode.OK, await chat.Content.ReadAsStringAsync(Token));
            using JsonDocument body = JsonDocument.Parse(await chat.Content.ReadAsStringAsync(Token));
            conversationId = body.RootElement.GetProperty("conversationId").GetGuid();
            actionId = body.RootElement.GetProperty("pendingActions").EnumerateArray().Should().ContainSingle().Subject.GetProperty("id").GetGuid();
        }

        // Persisted in Postgres, encrypted, and nothing written yet.
        await using (AppDbContext db = new(DevDatabase.Options()))
        {
            MafConversation conversation = await db.MafConversations.AsNoTracking().SingleAsync(c => c.Id == conversationId, Token);
            conversation.UserId.Should().Be(_userId);
            conversation.EncryptedSessionState.Should().NotContain("Registra", "the session is encrypted at rest");
            PendingAction action = await db.PendingActions.AsNoTracking().SingleAsync(a => a.Id == actionId, Token);
            action.Status.Should().Be(PendingActionStatus.Pending);
            action.PayloadJson.Should().NotContain("CryptoCurrencyId", "the payload is encrypted at rest");
            (await db.PortfolioEntries.AsNoTracking().AnyAsync(e => e.PortfolioId == _portfolioId, Token)).Should().BeFalse("proposing writes nothing (AC2)");
        }

        // ---- second host, as after a restart ----
        ScriptedChatClient secondModel = new((_, _) => ScriptedChatClient.Text("Sigue pendiente de tu confirmación."));
        using PostgresHost second = new(secondModel);
        HttpClient client = second.CreateClientForUser(_userId);

        HttpResponseMessage resumed = await client.PostAsJsonAsync($"{Base}/chat", new { conversationId, message = "¿Sigue ahí?" }, Token);

        resumed.StatusCode.Should().Be(HttpStatusCode.OK, await resumed.Content.ReadAsStringAsync(Token));
        List<string> seen = secondModel.Calls[0].Messages.Select(m => m.Text).ToList();
        seen.Should().Contain("Registra 0.25 a 64000 del 10 de noviembre").And.Contain("He propuesto la entrada; confírmala.").And.Contain("¿Sigue ahí?");

        HttpResponseMessage confirmed = await client.PostAsync($"{Base}/pending-actions/{actionId}/confirm", null, Token);

        confirmed.StatusCode.Should().Be(HttpStatusCode.OK, await confirmed.Content.ReadAsStringAsync(Token));
        using JsonDocument result = JsonDocument.Parse(await confirmed.Content.ReadAsStringAsync(Token));
        result.RootElement.GetProperty("status").GetString().Should().Be("Succeeded");
        long entryId = result.RootElement.GetProperty("result").GetProperty("id").GetInt64();
        entryId.Should().BeGreaterThan(0);

        await using (AppDbContext db = new(DevDatabase.Options()))
        {
            PortfolioEntry entry = await db.PortfolioEntries.AsNoTracking().SingleAsync(e => e.PortfolioId == _portfolioId, Token);
            entry.Id.Should().Be(entryId, "the confirmed result carries the id Postgres assigned (AC3)");
            entry.CryptoCurrencyId.Should().Be(_cryptoId);
            entry.ExchangeId.Should().Be(_exchangeId);
            entry.Quantity.Should().Be(0.25m);
            entry.RecordedAt.Should().Be(new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc), "a date without time is that UTC day");
            (await db.PendingActions.AsNoTracking().SingleAsync(a => a.Id == actionId, Token)).Status.Should().Be(PendingActionStatus.Succeeded);
        }
    }

    /// <summary>The real app on the development Postgres; only the model and the external systems are doubles.</summary>
    private sealed class PostgresHost(IChatClient model) : WebApplicationFactory<Program>
    {
        public HttpClient CreateClientForUser(long userId)
        {
            HttpClient client = CreateClient();
            client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
            return client;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:Database", DevDatabase.ConnectionStringValue);
            builder.UseSetting("ConnectionStrings:CosmosDb", "AccountEndpoint=https://unused.invalid:8081/;AccountKey=dGVzdA==");
            builder.UseSetting("Jwt:Secret", "test-only-signing-secret-of-at-least-64-characters-0123456789abcdef");
            builder.UseSetting("Jwt:Issuer", "maf-agent-tests");
            builder.UseSetting("Jwt:Audience", "maf-agent-tests");
            builder.UseSetting("Encryption:Key", Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray()));
            builder.UseSetting("CosmosDb:DatabaseName", "TestDb");
            builder.UseSetting("CosmosDb:ContainerName", "Notes");
            builder.UseSetting("CoinGecko:BaseUrl", "https://unused.invalid/");
            builder.UseSetting("Ai:Provider", "Ollama");
            builder.UseSetting("Ai:ReasoningModel:ModelId", "scripted");
            builder.UseSetting("Ai:ReasoningModel:Endpoint", "http://unused.invalid/v1");
            builder.UseSetting("Ai:FastModel:ModelId", "scripted");
            builder.UseSetting("Ai:FastModel:Endpoint", "http://unused.invalid/v1");
            builder.UseSetting("ApplicationInsights:ConnectionString", string.Empty);

            builder.ConfigureTestServices(services =>
            {
                // The model: scripted. Everything else of the MAF agent is the real registration.
                IMafChatClientFactory clients = Substitute.For<IMafChatClientFactory>();
                clients.Create().Returns(model);
                services.RemoveAll<IMafChatClientFactory>();
                services.AddSingleton(clients);

                CosmosClient cosmos = Substitute.For<CosmosClient>();
                cosmos.CreateDatabaseIfNotExistsAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>())
                    .ThrowsAsync(new CosmosException("Cosmos is not available in tests.", HttpStatusCode.ServiceUnavailable, 0, string.Empty, 0));
                services.RemoveAll<CosmosClient>();
                services.AddSingleton(cosmos);
                services.RemoveAll<IEmailSender>();
                services.AddSingleton(Substitute.For<IEmailSender>());
                services.RemoveAll<ICoinGeckoClient>();
                services.AddSingleton(Substitute.For<ICoinGeckoClient>());
                foreach (ServiceDescriptor collector in services
                    .Where(d => d.ServiceType == typeof(IHostedService) &&
                                d.ImplementationType?.Namespace?.StartsWith("HealthChecks.UI", StringComparison.Ordinal) == true)
                    .ToList())
                {
                    services.Remove(collector);
                }

                services.AddAuthentication(TestAuthHandler.SchemeName)
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                });
            });
        }
    }
}
