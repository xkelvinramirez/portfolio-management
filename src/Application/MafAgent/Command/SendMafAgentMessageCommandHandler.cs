using Application.Common.Messaging;
using Application.Common.Security;
using Application.MafAgent.Interfaces;
using Application.Portfolios.Interfaces;
using Contracts.MafAgent;
using Domain.Common.Exceptions;
using Domain.Entities;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.MafAgent.Command;

public sealed record SendMafAgentMessageCommand(SendMafAgentMessageRequest Request)
    : IRequest<ErrorOr<SendMafAgentMessageResponse>>, ICommand;

/// <summary>
/// One chat turn (plan §3 "Flujo principal A"). The agent can only PROPOSE: its tools stage pending actions,
/// and this handler returns them as DTOs. Nothing here can execute a write: there is no executor and no sender.
/// The unit of work commits the conversation and the staged actions together, and only on success: a failed
/// turn returns an error, so nothing is committed and no pending action is left behind (AC14).
/// </summary>
public sealed class SendMafAgentMessageCommandHandler(
    IMafConversationRepository conversationRepository,
    IMafAgentRunner agentRunner,
    IPortfolioRepository portfolioRepository,
    PendingActionFactory factory,
    IEncryptionService encryptionService,
    ICurrentUserProvider currentUserProvider,
    TimeProvider timeProvider,
    ILogger<SendMafAgentMessageCommandHandler> logger
    ) : IRequestHandler<SendMafAgentMessageCommand, ErrorOr<SendMafAgentMessageResponse>>
{
    // What the runner receives for a conversation that has no session yet.
    private const string NewSession = "";

    public async Task<ErrorOr<SendMafAgentMessageResponse>> Handle(SendMafAgentMessageCommand command, CancellationToken cancellationToken)
    {
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        long userId = currentUserProvider.UserId;
        Guid? conversationId = command.Request.ConversationId;

        MafConversation conversation;
        string session;
        if (conversationId is null)
        {
            conversation = MafConversation.Create(userId, encryptionService.Encrypt(NewSession), now);
            session = NewSession;
        }
        else
        {
            MafConversation? existing = await conversationRepository.GetByIdAsync(conversationId.Value, cancellationToken);
            if (existing is null || existing.UserId != userId)
            {
                return MafAgentErrors.ConversationNotFound(conversationId.Value);
            }

            if (existing.IsExpired(now))
            {
                return MafAgentErrors.ConversationExpired(conversationId.Value);
            }

            conversation = existing;
            // A session that cannot be decrypted is a server fault: let it reach the global handler (500).
            session = encryptionService.Decrypt(conversation.EncryptedSessionState);
        }

        ActivePortfolio? activePortfolio = null;
        if (command.Request.PortfolioId is long portfolioId)
        {
            Portfolio? portfolio = await portfolioRepository.GetByIdAsync(portfolioId, cancellationToken);
            if (portfolio is null || portfolio.UserId != userId)
            {
                return MafAgentErrors.PortfolioNotFound(portfolioId);
            }

            activePortfolio = new ActivePortfolio(portfolio.Id, portfolio.Name);
        }

        MafAgentTurnResult turn;
        try
        {
            turn = await agentRunner.RunTurnAsync(conversation, session, command.Request.Message, activePortfolio, cancellationToken);
        }
        catch (AgentLoopExhaustedException exception)
        {
            // Ids only: never the message text (plan §0 "Logs").
            logger.LogWarning(exception, "MAF agent turn halted in conversation {ConversationId}: tool loop cap exceeded", conversation.Id);
            return MafAgentErrors.LoopExhausted();
        }
        catch (AgentTimeoutException exception)
        {
            logger.LogWarning(exception, "MAF agent turn halted in conversation {ConversationId}: inference request timed out", conversation.Id);
            return MafAgentErrors.Timeout();
        }

        conversation.UpdateSession(encryptionService.Encrypt(turn.SerializedSessionJson), now);
        if (conversationId is null)
        {
            // Staged only after a successful turn, together with the actions the tools staged.
            conversationRepository.Add(conversation);
        }
        else
        {
            conversationRepository.Update(conversation);
        }

        // Built from the staged instances of THIS turn, never re-read from the database.
        List<PendingActionDto> proposed = turn.ProposedActions.Select(factory.ToDto).ToList();
        return new SendMafAgentMessageResponse(conversation.Id, turn.Reply, proposed);
    }
}
