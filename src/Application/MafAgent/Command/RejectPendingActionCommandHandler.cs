using Application.Common.Security;
using Application.MafAgent.Interfaces;
using Domain.Entities;
using ErrorOr;
using MediatR;

namespace Application.MafAgent.Command;

/// <summary>
/// Not an <c>ICommand</c> on purpose: <see cref="IPendingActionRepository.TryRejectAsync"/> commits on its own,
/// and nothing else is staged for the unit of work to flush.
/// </summary>
public sealed record RejectPendingActionCommand(Guid Id) : IRequest<ErrorOr<Success>>;

/// <summary>
/// Rejects a pending action (spec Behaviour §Escritura 5): nothing is written to the business data. The
/// Pending → Rejected transition is the atomic conditional update of <c>TryRejectAsync</c>, never an update
/// of the loaded entity, so a reject and a confirm can never both win (plan §3, analyze G4).
/// </summary>
public sealed class RejectPendingActionCommandHandler(
    IPendingActionRepository pendingActionRepository,
    IMafConversationRepository conversationRepository,
    ICurrentUserProvider currentUserProvider,
    TimeProvider timeProvider
    ) : IRequestHandler<RejectPendingActionCommand, ErrorOr<Success>>
{
    public async Task<ErrorOr<Success>> Handle(RejectPendingActionCommand command, CancellationToken cancellationToken)
    {
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        long userId = currentUserProvider.UserId;

        PendingAction? action = await pendingActionRepository.GetByIdAsync(command.Id, cancellationToken);
        if (action is null || action.UserId != userId)
        {
            return MafAgentErrors.PendingActionNotFound(command.Id);
        }

        MafConversation? conversation = await conversationRepository.GetByIdAsync(action.ConversationId, cancellationToken);
        if (conversation is null || conversation.UserId != userId)
        {
            return MafAgentErrors.PendingActionNotFound(command.Id);
        }

        if (action.IsExpired(now) || conversation.IsExpired(now))
        {
            return MafAgentErrors.PendingActionExpired(command.Id);
        }

        if (action.Status != PendingActionStatus.Pending ||
            !await pendingActionRepository.TryRejectAsync(action.Id, userId, now, cancellationToken))
        {
            return MafAgentErrors.PendingActionAlreadyDecided(command.Id);
        }

        return Result.Success;
    }
}
