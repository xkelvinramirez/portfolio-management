
using Application.Common.UnitOfWork;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Infrastructure.Common.Options;
using Infrastructure.Common.Persistence.Contexts;
using Infrastructure.Common.Security;
using Infrastructure.Portfolios;
using Infrastructure.Users;
using Infrastructure.Exchanges;
using Infrastructure.CryptoCurrencies;
using Infrastructure.PortfolioEntries;
using Infrastructure.Notes;
using Infrastructure.Reports;
using Infrastructure.Agent;
using Application.MafAgent.Interfaces;
using Infrastructure.MafAgent.Persistence;
using Infrastructure.MafAgent.Runner;
using Infrastructure.MafAgent.Tools;
using Infrastructure.Agent.Filters;
using Infrastructure.Agent.Plugins;
using Infrastructure.Resilience;
using Application.Reports.Interfaces;
using Application.Common.Security;
using Application.Agent.Interfaces;
using Application.Portfolios.Interfaces;
using Application.Users.Interfaces;
using Application.Exchanges.Interfaces;
using Application.CryptoCurrencies.Interfaces;
using Application.PortfolioEntries.Interfaces;
using Application.Notes.Interfaces;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services,
        ConfigurationManager configuration, IHostEnvironment environment)
    {
        services
            .AddConfigurationOptions(configuration)
            .AddBackgroundServices(configuration)
            .AddDbContexts(configuration)
            .AddCosmosDb(configuration)
            .AddRepositories()
            .AddPackages(configuration)
            .AddAdapters()
            .AddSecurity()
            .AddCaching()
            .AddAgent(configuration)
            .AddMafAgent()
            .AddHealthChecksForDependencies(configuration);

        return services;
    }

    // Semantic Kernel tool-calling plumbing: transient Kernel per conversation (never a
    // singleton - plugins/filters below carry the authenticated user's ambient identity),
    // guardrail filters wired into the kernel itself rather than around the HttpClient, and
    // plugins that never accept tenant/user ids as LLM-controlled parameters.
    private static IServiceCollection AddAgent(this IServiceCollection services, IConfiguration configuration)
    {
        var aiOptions = configuration.GetSection(AiOptions.SectionName).Get<AiOptions>() ?? new AiOptions();

        services.AddScoped<LoopCappingFilter>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<LoopCappingFilter>>();
            var maxLoops = sp.GetRequiredService<IOptions<AiOptions>>().Value.MaxAllowedToolLoops;
            return new LoopCappingFilter(logger, maxLoops);
        });
        services.AddScoped<ToolInvocationAuditFilter>();

        services.AddScoped<PortfolioAgentPlugin>();

        services.AddScoped<IKernelFactory, KernelFactory>();
        services.AddScoped<IAgentOrchestrator, AgentOrchestrator>();

        services.AddHttpClient("InferenceClient")
            .AddInferenceResilience(TimeSpan.FromSeconds(aiOptions.RequestTimeoutSeconds));

        return services;
    }

    // Microsoft Agent Framework agent (plan T030). Scoped like the rest of the request: its tools carry the
    // authenticated user's identity and the turn's proposed actions. It reuses the "InferenceClient" HttpClient
    // registered above, so the inference timeout is the same as the Semantic Kernel agent's.
    internal static IServiceCollection AddMafAgent(this IServiceCollection services)
    {
        services.AddScoped<MafAgentReadTools>();
        services.AddScoped<MafAgentProposeTools>();
        services.AddScoped<IMafChatClientFactory, MafChatClientFactory>();
        services.AddScoped<MafAgentRunner>();
        services.AddScoped<IMafAgentRunner>(sp => sp.GetRequiredService<MafAgentRunner>());

        return services;
    }
    private static IServiceCollection AddCosmosDb(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("CosmosDb");
        services.AddSingleton(_ => new CosmosClient(connectionString));
        services.AddScoped<INoteRepository, NoteRepository>();

        return services;
    }

    // Called once from Program.cs after the app is built. Idempotent — no-op if the database/
    // container already exist. Unlike EF migrations (applied explicitly via the dotnet-ef CLI),
    // this is safe to run on every startup: there's no schema to diff, just "ensure this
    // container is there".
    //
    // Deliberately non-fatal: provisioning here can fail for reasons that are an Azure account
    // setting, not a code bug (e.g. the account's configured total-throughput cap). That must
    // not take down the whole API — Portfolio/Exchange/etc. have nothing to do with Cosmos DB.
    // If this fails, the API still starts; only the /Note endpoints will error until the
    // container is provisioned (manually, or by fixing the throughput budget) and the app restarted.
    public static async Task EnsureCosmosDbInitializedAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<CosmosClient>>();
        var client = scope.ServiceProvider.GetRequiredService<CosmosClient>();
        var cosmosOptions = scope.ServiceProvider.GetRequiredService<IOptions<CosmosDbOptions>>().Value;

        try
        {
            var database = await client.CreateDatabaseIfNotExistsAsync(cosmosOptions.DatabaseName);
            // Matches the partition key of the pre-existing "Notes" container this app was
            // pointed at (DashboardDB/Notes) — only takes effect if the container has to be
            // created from scratch, e.g. in a different environment.
            await database.Database.CreateContainerIfNotExistsAsync(cosmosOptions.ContainerName, partitionKeyPath: "/category");
        }
        catch (CosmosException ex)
        {
            logger.LogWarning(ex,
                "Could not ensure Cosmos DB database/container '{DatabaseName}/{ContainerName}' exist. " +
                "The API will still start, but /Note endpoints will fail until this is resolved " +
                "(see docs/notes-cosmos-db.md).",
                cosmosOptions.DatabaseName, cosmosOptions.ContainerName);
        }
    }

    private static IServiceCollection AddBackgroundServices(this IServiceCollection services,
        IConfiguration configuration)
    {
        return services;
    }

    private static IServiceCollection AddDbContexts(this IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("Database");

        // One AddedEntityIdLog per request: it records the real ids of inserted entities after commit,
        // because create handlers build their response while the identity id is still 0.
        services.AddScoped<AddedEntityIdLog>();
        services.AddScoped<IAddedEntityIdLog>(sp => sp.GetRequiredService<AddedEntityIdLog>());

        services.AddDbContext<AppDbContext>(
            (sp, options) => options
                .UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions
                    .MigrationsHistoryTable(HistoryRepository.DefaultTableName, Schemas.Default)
                    .EnableRetryOnFailure())
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(sp.GetRequiredService<AddedEntityIdLog>()));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        return services;
    }

    private static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        services.AddScoped<IPortfolioRepository, PortfolioRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IExchangeRepository, ExchangeRepository>();
        services.AddScoped<ICryptoCurrencyRepository, CryptoCurrencyRepository>();
        services.AddScoped<IPortfolioEntryRepository, PortfolioEntryRepository>();
        services.AddScoped<IMafConversationRepository, MafConversationRepository>();
        services.AddScoped<IPendingActionRepository, PendingActionRepository>();
        services.AddScoped<ITrackedChangesDiscarder, TrackedChangesDiscarder>();

        return services;
    }

    private static IServiceCollection AddAdapters(this IServiceCollection services)
    {
        services.AddHttpClient<ICoinGeckoClient, CoinGeckoClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<CoinGeckoOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("PortfolioManagementMinimalApi/1.0");
            if (!string.IsNullOrEmpty(options.ApiKey))
            {
                client.DefaultRequestHeaders.Add("x-cg-demo-api-key", options.ApiKey);
            }
        });

        // Registered here (not just in Worker's AddReportScheduling) so that MediatR's handler for
        // SendPortfolioStatusReportCommand - scanned from the shared Application assembly - stays
        // structurally resolvable in the API process too. Only the Worker actually binds/validates
        // EmailOptions and runs the background service that sends through it.
        services.AddScoped<IEmailSender, MailKitEmailSender>();

        return services;
    }

    private static IServiceCollection AddSecurity(this IServiceCollection services)
    {
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<IEncryptionService, AesEncryptionService>();
        return services;
    }

    private static IServiceCollection AddCaching(this IServiceCollection services)
    {
        // In-process only for now (no IDistributedCache/Redis registered): still gives every
        // cached read stampede protection and a shared TTL. Adding Redis later as an L2 tier
        // is a config-only change; HybridCache picks it up automatically once registered.
        services.AddHybridCache(options =>
        {
            options.DefaultEntryOptions = new()
            {
                Expiration = TimeSpan.FromMinutes(10),
                LocalCacheExpiration = TimeSpan.FromMinutes(10),
            };
        });

        return services;
    }



    private static IServiceCollection AddPackages(this IServiceCollection services, IConfigurationRoot configuration)
    {
        return services;
    }

    private static IServiceCollection AddConfigurationOptions(this IServiceCollection services,
        ConfigurationManager configuration)
    {
        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetRequiredSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<CoinGeckoOptions>()
            .Bind(configuration.GetRequiredSection(CoinGeckoOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<CosmosDbOptions>()
            .Bind(configuration.GetRequiredSection(CosmosDbOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<EncryptionOptions>()
            .Bind(configuration.GetRequiredSection(EncryptionOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services
            .AddOptions<AiOptions>()
            .Bind(configuration.GetRequiredSection(AiOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Bound here (not required/validated, unlike the sections above) so that MailKitEmailSender
        // reads real values in BOTH processes - the API needs this for the manual "send now" endpoint,
        // even though only the Worker's AddReportScheduling enforces (GetRequiredSection + ValidateOnStart)
        // that Email is actually configured before it starts its scheduled sweep.
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));

        return services;
    }

    // Only the base checks - no AddHealthChecksUI() here. That's an ASP.NET Core web dashboard
    // (needs IServer, middleware, MapHealthChecksUI()) and belongs in WebApi.MinimalAPI's
    // AddPresentation(); registering it here broke the Worker (no IServer in a non-web Generic
    // Host) as soon as ASP.NET Core's strict DI validation kicked in under Development.
    private static void AddHealthChecksForDependencies(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHealthChecks()
            .AddNpgSql(configuration.GetConnectionString("Database")!, name: "postgres")
            .AddAzureCosmosDB(name: "cosmosdb");
    }
}