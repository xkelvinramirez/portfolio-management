using Application.MafAgent.Interfaces;
using FluentAssertions;
using Infrastructure;
using Infrastructure.MafAgent.Runner;
using Infrastructure.MafAgent.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using WebApi.MinimalAPI.Tests.MafAgent.Http;

namespace WebApi.MinimalAPI.Tests.MafAgent.Runner;

/// <summary>
/// Plan T030: the real host wires the MAF runner, its tools and its chat client factory, all scoped. The test host
/// replaces the <see cref="IMafAgentRunner"/> PORT with a double; the concrete runner stays registered.
/// </summary>
public sealed class MafAgentRunnerDiTests : IClassFixture<MafAgentApiFactory>
{
    private readonly MafAgentApiFactory _factory;

    public MafAgentRunnerDiTests(MafAgentApiFactory factory) => _factory = factory;

    [Fact]
    public void TheRunnerAndItsTools_ResolveScoped_FromTheRealRegistration()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        using IServiceScope other = _factory.Services.CreateScope();

        MafAgentRunner runner = scope.ServiceProvider.GetRequiredService<MafAgentRunner>();
        MafAgentProposeTools propose = scope.ServiceProvider.GetRequiredService<MafAgentProposeTools>();

        runner.Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<MafAgentReadTools>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<MafAgentProposeTools>().Should().BeSameAs(propose, "one turn's proposals live in one scope");
        other.ServiceProvider.GetRequiredService<MafAgentProposeTools>().Should().NotBeSameAs(propose, "proposals never leak across requests");
    }

    [Fact]
    public void TheRealChatClientFactory_BuildsAClientFromTheTestAiSection_WithoutNetwork()
    {
        using IServiceScope scope = _factory.Services.CreateScope();

        IChatClient client = scope.ServiceProvider.GetRequiredService<IMafChatClientFactory>().Create();

        client.GetService<ChatClientMetadata>()!.ProviderUri!.Host.Should().Be("unused.invalid");
    }

    [Fact]
    public void InProduction_ThePortResolvesToTheMafRunner_Scoped()
    {
        ServiceCollection services = new();
        services.AddMafAgent();

        ServiceDescriptor port = services.Single(d => d.ServiceType == typeof(IMafAgentRunner));
        port.Lifetime.Should().Be(ServiceLifetime.Scoped);

        // The real dependencies of the runner and its tools, as doubles: only the port mapping is under test.
        services.AddLogging();
        services.AddOptions<Infrastructure.Common.Options.AiOptions>();
        services.AddHttpClient();
        services.AddSingleton(TimeProvider.System);
        foreach (Type type in new[]
                 {
                     typeof(Application.Portfolios.Interfaces.IPortfolioRepository), typeof(Application.PortfolioEntries.Interfaces.IPortfolioEntryRepository),
                     typeof(MediatR.ISender), typeof(Application.Common.Security.ICurrentUserProvider), typeof(Application.Common.Security.IEncryptionService),
                     typeof(IPendingActionRepository), typeof(ICurrentValuesReader),
                 })
        {
            services.AddScoped(type, _ => NSubstitute.Substitute.For([type], []));
        }

        services.AddScoped<Application.MafAgent.PendingActionFactory>();
        using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using IServiceScope scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IMafAgentRunner>().Should().BeOfType<MafAgentRunner>()
            .And.BeSameAs(scope.ServiceProvider.GetRequiredService<MafAgentRunner>());
    }
}