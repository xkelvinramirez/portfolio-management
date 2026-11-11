using System.Net;
using Application.CryptoCurrencies.Interfaces;
using Application.MafAgent.Interfaces;
using Application.Reports.Interfaces;
using Infrastructure.Common.Persistence.Contexts;
using Infrastructure.MafAgent.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Azure.Cosmos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace WebApi.MinimalAPI.Tests.MafAgent.Http;

/// <summary>
/// In-process API host for MafAgent HTTP tests (plan T002). It runs with no external dependency and
/// without touching production code:
/// <list type="bullet">
/// <item>environment "Testing" (user-secrets are not loaded) and in-memory settings for every required section;</item>
/// <item>EF InMemory instead of Postgres, keeping the AddedEntityIdLog interceptor (analyze L3);</item>
/// <item>IPendingActionRepository double with in-memory conditional transitions (InMemory has no ExecuteUpdate, analyze G3);</item>
/// <item>CosmosClient double (startup initialization is caught and logged), and doubles for the agent runner,
/// the e-mail sender and CoinGecko so no test reaches a model, sends mail or calls an external API;</item>
/// <item>header-based test authentication and a controllable clock.</item>
/// </list>
/// </summary>
public sealed class MafAgentApiFactory : WebApplicationFactory<Program>
{
    private static readonly Dictionary<string, string?> Settings = new()
    {
        ["ConnectionStrings:Database"] = "Host=unused.invalid;Database=unused",
        ["ConnectionStrings:CosmosDb"] = "AccountEndpoint=https://unused.invalid:8081/;AccountKey=dGVzdA==",
        ["Jwt:Secret"] = "test-only-signing-secret-of-at-least-64-characters-0123456789abcdef",
        ["Jwt:Issuer"] = "maf-agent-tests",
        ["Jwt:Audience"] = "maf-agent-tests",
        ["Encryption:Key"] = Convert.ToBase64String(Enumerable.Range(1, 32).Select(i => (byte)i).ToArray()),
        ["CosmosDb:DatabaseName"] = "TestDb",
        ["CosmosDb:ContainerName"] = "Notes",
        ["CoinGecko:BaseUrl"] = "https://unused.invalid/",
        ["Ai:Provider"] = "Ollama",
        ["Ai:FastModel:ModelId"] = "test-model",
        ["Ai:FastModel:Endpoint"] = "http://unused.invalid/v1",
        ["Ai:ReasoningModel:ModelId"] = "test-model",
        ["Ai:ReasoningModel:Endpoint"] = "http://unused.invalid/v1",
        ["ApplicationInsights:ConnectionString"] = string.Empty,
    };

    private readonly string _databaseName = $"maf-api-{Guid.NewGuid():N}";

    public FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 11, 11, 10, 0, 0, TimeSpan.Zero));

    public IMafAgentRunner Runner { get; } = Substitute.For<IMafAgentRunner>();

    public IEmailSender EmailSender { get; } = Substitute.For<IEmailSender>();

    public ICoinGeckoClient CoinGecko { get; } = Substitute.For<ICoinGeckoClient>();

    public HttpClient CreateClientForUser(long userId)
    {
        HttpClient client = CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
        return client;
    }

    /// <summary>Runs <paramref name="work"/> against the shared in-memory AppDbContext (seeding, assertions).</summary>
    public async Task WithDbContextAsync(Func<AppDbContext, Task> work)
    {
        using IServiceScope scope = Services.CreateScope();
        await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        foreach ((string key, string? value) in Settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            // Database: EF InMemory, same interceptor as production.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>((sp, options) => options
                .UseInMemoryDatabase(_databaseName)
                .AddInterceptors(sp.GetRequiredService<AddedEntityIdLog>()));
            services.RemoveAll<IPendingActionRepository>();
            services.AddScoped<IPendingActionRepository, InMemoryTransitionsPendingActionRepository>();

            // External systems.
            services.RemoveAll<CosmosClient>();
            services.AddSingleton(CreateCosmosDouble());
            services.RemoveAll<IMafAgentRunner>();
            services.AddSingleton(Runner);
            services.RemoveAll<IEmailSender>();
            services.AddSingleton(EmailSender);
            services.RemoveAll<ICoinGeckoClient>();
            services.AddSingleton(CoinGecko);

            // HealthChecks UI polls /health in the background; tests don't need it (and in the test output
            // folder it fails to load IdentityModel). Only its hosted services are removed: the web server
            // itself is also an IHostedService and must stay.
            foreach (ServiceDescriptor collector in services
                .Where(d => d.ServiceType == typeof(IHostedService) &&
                            d.ImplementationType?.Namespace?.StartsWith("HealthChecks.UI", StringComparison.Ordinal) == true)
                .ToList())
            {
                services.Remove(collector);
            }

            // Clock.
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);

            // Authentication.
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

    // Startup calls EnsureCosmosDbInitializedAsync, which catches CosmosException and only logs a warning.
    private static CosmosClient CreateCosmosDouble()
    {
        CosmosClient cosmos = Substitute.For<CosmosClient>();
        cosmos.CreateDatabaseIfNotExistsAsync(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<RequestOptions>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new CosmosException("Cosmos is not available in tests.", HttpStatusCode.ServiceUnavailable, 0, string.Empty, 0));
        return cosmos;
    }
}
