using Application.Common.Security;
using Application.MafAgent;
using Application.MafAgent.Command;
using Application.MafAgent.Interfaces;
using Contracts.MafAgent;
using ErrorOr;
using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Application.Tests.MafAgent;

/// <summary>
/// Plan T024: the real <c>AddApplication</c> wires the MafAgent services. The ports of other layers are doubles,
/// exactly as Infrastructure and WebApi will provide them.
/// </summary>
public class MafAgentApplicationDiTests
{
    private static ServiceProvider BuildProvider()
    {
        ServiceCollection services = new();
        // The host registers logging before AddApplication; MediatR requires it.
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddApplication(new ConfigurationManager());
        services.AddScoped(_ => Substitute.For<IEncryptionService>());
        services.AddScoped(_ => Substitute.For<ICurrentUserProvider>());
        services.AddScoped(_ => Substitute.For<IAddedEntityIdLog>());
        services.AddScoped(_ => Substitute.For<IPendingActionRepository>());
        services.AddScoped(_ => Substitute.For<IMafConversationRepository>());
        services.AddScoped(_ => Substitute.For<ITrackedChangesDiscarder>());
        services.AddScoped(_ => Substitute.For<IMafAgentRunner>());
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = false });
    }

    [Fact]
    public void ResolvesTheMafAgentServices_AsScoped()
    {
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();
        using IServiceScope other = provider.CreateScope();

        PendingActionFactory factory = scope.ServiceProvider.GetRequiredService<PendingActionFactory>();
        scope.ServiceProvider.GetRequiredService<ICurrentValuesReader>().Should().BeOfType<CurrentValuesReader>();
        scope.ServiceProvider.GetRequiredService<PendingActionExecutor>().Should().NotBeNull();

        scope.ServiceProvider.GetRequiredService<PendingActionFactory>().Should().BeSameAs(factory);
        other.ServiceProvider.GetRequiredService<PendingActionFactory>().Should().NotBeSameAs(factory);
    }

    [Fact]
    public void ResolvesTheThreeMafAgentCommandHandlers()
    {
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IRequestHandler<SendMafAgentMessageCommand, ErrorOr<SendMafAgentMessageResponse>>>()
            .Should().BeOfType<SendMafAgentMessageCommandHandler>();
        scope.ServiceProvider.GetRequiredService<IRequestHandler<ConfirmPendingActionCommand, ErrorOr<PendingActionResultDto>>>()
            .Should().BeOfType<ConfirmPendingActionCommandHandler>();
        scope.ServiceProvider.GetRequiredService<IRequestHandler<RejectPendingActionCommand, ErrorOr<Success>>>()
            .Should().BeOfType<RejectPendingActionCommandHandler>();
    }

    [Fact]
    public void TheRegisteredChatValidator_RejectsA2001CharacterMessage()
    {
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();

        IValidator<SendMafAgentMessageCommand> validator = scope.ServiceProvider.GetRequiredService<IValidator<SendMafAgentMessageCommand>>();

        validator.Validate(new SendMafAgentMessageCommand(new SendMafAgentMessageRequest(null, new string('a', 2001))))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void ConfirmAndRejectValidators_AreRegistered()
    {
        using ServiceProvider provider = BuildProvider();
        using IServiceScope scope = provider.CreateScope();

        scope.ServiceProvider.GetServices<IValidator<ConfirmPendingActionCommand>>().Should().ContainSingle();
        scope.ServiceProvider.GetServices<IValidator<RejectPendingActionCommand>>().Should().ContainSingle();
    }
}
