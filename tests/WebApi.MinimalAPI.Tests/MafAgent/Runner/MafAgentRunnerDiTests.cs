using Application.MafAgent.Interfaces;
using FluentAssertions;
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
}
