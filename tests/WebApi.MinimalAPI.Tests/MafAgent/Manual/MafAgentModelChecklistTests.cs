using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Application.CryptoCurrencies.Interfaces;
using Application.Reports.Interfaces;
using Domain.Entities;
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
using User = Domain.Entities.User;

namespace WebApi.MinimalAPI.Tests.MafAgent.Manual;

/// <summary>
/// Plan T035: the manual checklist with a REAL model (Ollama, qwen2.5:3b-instruct on localhost:11434) over the
/// real app and the development Postgres. Not run by the gate (Category=Manual): the model is not deterministic.
/// It seeds a test user, runs each checklist message in a new conversation and writes, per case, the reply, the
/// tools the model called and the proposed actions to MAF_CHECKLIST_OUT (or a temp file) for a human verdict.
/// </summary>
[Trait("Category", "Manual")]
[Collection(DevDatabaseCollection.Name)]
public sealed class MafAgentModelChecklistTests : IAsyncLifetime
{
    private readonly List<long> _createdCryptoIds = [];
    private readonly List<long> _createdExchangeIds = [];
    private long _userId;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await using AppDbContext db = new(DevDatabase.Options());
        User user = DevDatabase.NewTestUser("MafChecklist");
        db.Users.Add(user);
        await db.SaveChangesAsync();
        _userId = user.Id;

        long btc = await CryptoIdAsync(db, "BTC", "Bitcoin");
        long eth = await CryptoIdAsync(db, "ETH", "Ethereum");
        long binance = await ExchangeIdAsync(db, "Binance");

        Portfolio portfolio = Portfolio.Create(_userId, "Principal", "Checklist T035");
        db.Portfolios.Add(portfolio);
        await db.SaveChangesAsync();
        db.PortfolioEntries.AddRange(
            PortfolioEntry.Create(portfolio.Id, btc, binance, 0.5m, 90000m, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            PortfolioEntry.Create(portfolio.Id, eth, binance, 4m, 3000m, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            PortfolioEntry.Create(portfolio.Id, btc, binance, 0.5m, 80000m, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)),
            PortfolioEntry.Create(portfolio.Id, eth, binance, 4m, 2500m, new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await using AppDbContext db = new(DevDatabase.Options());
        await db.Users.Where(u => u.Id == _userId).ExecuteDeleteAsync();
        await db.CryptoCurrencies.Where(c => _createdCryptoIds.Contains(c.Id)).ExecuteDeleteAsync();
        await db.Exchanges.Where(e => _createdExchangeIds.Contains(e.Id)).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task RunTheChecklist_AndWriteTheTranscript()
    {
        string[] messages =
        [
            "¿Qué portafolios tengo?",
            "¿Cuánto vale ahora mismo mi portafolio Principal?",
            "¿Qué activo de mi portafolio Principal vale más ahora mismo?",
            "¿Cuándo valió más BTC en mi portafolio Principal?",
            "¿Cómo cambió el valor de cada activo de mi portafolio Principal entre el 1 de enero y el 1 de marzo de 2026?",
            "Registra 0.5 BTC a 60000 en Binance ayer en mi portafolio Principal",
            "Borra el portafolio Principal",
            "Registra una compra de BTC en mi portafolio Principal",
            "How much is my Principal portfolio worth right now?",
        ];

        RecordingFactory recorder = new();
        using ChecklistHost host = new(recorder);
        HttpClient client = host.CreateClient();
        client.Timeout = TimeSpan.FromMinutes(10);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, _userId.ToString());

        StringBuilder transcript = new();
        transcript.AppendLine($"# MAF checklist — {DateTime.UtcNow:O} — model qwen2.5:3b-instruct");
        for (int i = 0; i < messages.Length; i++)
        {
            recorder.ToolCalls.Clear();
            HttpResponseMessage response = await client.PostAsJsonAsync(
                "/api/v1/MafAgent/chat", new { conversationId = (Guid?)null, message = messages[i] }, Token);
            string body = await response.Content.ReadAsStringAsync(Token);

            transcript.AppendLine($"## Caso {i + 1}: {messages[i]}");
            transcript.AppendLine($"HTTP {(int)response.StatusCode}");
            transcript.AppendLine($"Herramientas: {string.Join(", ", recorder.ToolCalls)}");
            if (response.StatusCode == HttpStatusCode.OK)
            {
                using JsonDocument json = JsonDocument.Parse(body);
                transcript.AppendLine($"Respuesta: {json.RootElement.GetProperty("reply").GetString()}");
                transcript.AppendLine($"Acciones propuestas: {json.RootElement.GetProperty("pendingActions").GetRawText()}");
            }
            else
            {
                transcript.AppendLine($"Cuerpo: {body}");
            }

            transcript.AppendLine();
        }

        string output = Environment.GetEnvironmentVariable("MAF_CHECKLIST_OUT") ?? Path.Combine(Path.GetTempPath(), "maf-checklist.md");
        await File.WriteAllTextAsync(output, transcript.ToString(), Token);
        TestContext.Current.SendDiagnosticMessage($"Checklist transcript: {output}");
        File.Exists(output).Should().BeTrue();
    }

    private async Task<long> CryptoIdAsync(AppDbContext db, string symbol, string name)
    {
        CryptoCurrency? existing = await db.CryptoCurrencies.FirstOrDefaultAsync(c => c.Symbol.ToUpper() == symbol);
        if (existing is not null)
        {
            return existing.Id;
        }

        CryptoCurrency created = CryptoCurrency.Create(symbol, name);
        db.CryptoCurrencies.Add(created);
        await db.SaveChangesAsync();
        _createdCryptoIds.Add(created.Id);
        return created.Id;
    }

    private async Task<long> ExchangeIdAsync(AppDbContext db, string name)
    {
        Exchange? existing = await db.Exchanges.FirstOrDefaultAsync(e => e.Name.ToUpper() == name.ToUpper());
        if (existing is not null)
        {
            return existing.Id;
        }

        Exchange created = Exchange.Create(name, "not-a-key");
        db.Exchanges.Add(created);
        await db.SaveChangesAsync();
        _createdExchangeIds.Add(created.Id);
        return created.Id;
    }

    /// <summary>The real chat client from configuration, wrapped to record the tool calls the model makes.</summary>
    private sealed class RecordingFactory : IMafChatClientFactory
    {
        public List<string> ToolCalls { get; } = [];

        public IServiceProvider? Services { get; set; }

        public IChatClient Create()
        {
            using IServiceScope scope = Services!.CreateScope();
            IChatClient real = ActivatorUtilities.CreateInstance<MafChatClientFactory>(scope.ServiceProvider).Create();
            return new RecordingChatClient(real, ToolCalls);
        }
    }

    private sealed class RecordingChatClient(IChatClient inner, List<string> toolCalls) : DelegatingChatClient(inner)
    {
        public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            ChatResponse response = await base.GetResponseAsync(messages, options, cancellationToken);
            toolCalls.AddRange(response.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>()
                .Select(c => $"{c.Name}({JsonSerializer.Serialize(c.Arguments)})"));
            return response;
        }
    }

    private sealed class ChecklistHost(RecordingFactory recorder) : WebApplicationFactory<Program>
    {
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
            builder.UseSetting("Ai:ReasoningModel:ModelId", "qwen2.5:3b-instruct");
            builder.UseSetting("Ai:ReasoningModel:Endpoint", "http://localhost:11434/v1");
            builder.UseSetting("Ai:FastModel:ModelId", "qwen2.5:3b-instruct");
            builder.UseSetting("Ai:FastModel:Endpoint", "http://localhost:11434/v1");
            builder.UseSetting("Ai:RequestTimeoutSeconds", "240");
            builder.UseSetting("Ai:MaxAllowedToolLoops", "5");
            builder.UseSetting("ApplicationInsights:ConnectionString", string.Empty);

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IMafChatClientFactory>();
                services.AddSingleton<IMafChatClientFactory>(sp =>
                {
                    recorder.Services = sp;
                    return recorder;
                });

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
