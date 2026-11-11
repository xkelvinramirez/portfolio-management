using System.Text.Json;
using Application.Common.Messaging;
using Application.Common.Security;
using Application.MafAgent.Interfaces;
using Application.MafAgent.Payloads;
using Contracts.MafAgent;
using Domain.Entities;
using ErrorOr;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.MafAgent.Command;

public sealed record ConfirmPendingActionCommand(Guid Id) : IRequest<ErrorOr<PendingActionResultDto>>, ICommand;

/// <summary>
/// The ONLY way a proposed write runs (spec AC4): a deterministic confirmation by id, outside the chat.
/// Errors are returned only BEFORE the atomic claim. After a successful claim the action can never run
/// again (AC5), and every outcome is returned as a value (Succeeded, Failed or Stale) so that the unit of
/// work persists its final state (plan §0 "Commit" and "Tras el reclamo").
/// </summary>
public sealed class ConfirmPendingActionCommandHandler(
    IPendingActionRepository pendingActionRepository,
    IMafConversationRepository conversationRepository,
    ICurrentValuesReader currentValuesReader,
    PendingActionExecutor executor,
    PendingActionFactory factory,
    ITrackedChangesDiscarder trackedChangesDiscarder,
    IMafAgentRunner agentRunner,
    IEncryptionService encryptionService,
    ICurrentUserProvider currentUserProvider,
    TimeProvider timeProvider,
    ILogger<ConfirmPendingActionCommandHandler> logger
    ) : IRequestHandler<ConfirmPendingActionCommand, ErrorOr<PendingActionResultDto>>
{
    public async Task<ErrorOr<PendingActionResultDto>> Handle(ConfirmPendingActionCommand command, CancellationToken cancellationToken)
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
            !await pendingActionRepository.TryClaimAsync(action.Id, userId, now, cancellationToken))
        {
            return MafAgentErrors.PendingActionAlreadyDecided(command.Id);
        }

        // ---- Claimed. From here on: no error results, no request token. ----
        action.MarkExecuting();

        PendingActionResultDto outcome;
        try
        {
            outcome = await RunClaimedAsync(action, now);
        }
        // Documented exception to the "no bare catch (Exception)" canon (plan §0 "Tras el reclamo" 3, and
        // CLAUDE.md): after an irreversible claim the outcome must be persisted. Drop whatever the inner
        // command left in the tracker, then persist Executing → Failed atomically outside the unit of work.
        catch (Exception exception)
        {
            logger.LogError(exception, "Pending action {PendingActionId} ({Operation}) failed unexpectedly after being claimed", action.Id, action.Operation);
            trackedChangesDiscarder.DiscardAll();
            await pendingActionRepository.TryMarkFailedAsync(action.Id, MafAgentErrors.UnexpectedFailureCode, now, CancellationToken.None);
            return Result(action, "Failed", null, new PendingActionErrorDto(
                MafAgentErrors.UnexpectedFailureCode, "The action could not be completed because of an unexpected error."));
        }

        pendingActionRepository.Update(action);

        string session = encryptionService.Decrypt(conversation.EncryptedSessionState);
        string updatedSession = await agentRunner.AppendSystemNoteAsync(session, OutcomeNote(outcome), CancellationToken.None);
        conversation.UpdateSession(encryptionService.Encrypt(updatedSession), now);
        conversationRepository.Update(conversation);

        logger.LogInformation("Pending action {PendingActionId} ({Operation}) confirmed by user {UserId}: {Status}",
            action.Id, action.Operation, userId, outcome.Status);
        return outcome;
    }

    private async Task<PendingActionResultDto> RunClaimedAsync(PendingAction action, DateTime now)
    {
        object payload = factory.ReadPayload(action);

        if (PendingActionPayloads.IsEdit(action.Operation))
        {
            ErrorOr<IReadOnlyDictionary<string, string?>> current = await currentValuesReader.ReadAsync(
                action.Operation, PendingActionPayloads.TargetIdOf(payload)!, CancellationToken.None);
            if (current.IsError)
            {
                return Failed(action, current.FirstError, now);
            }

            if (StaleCheck.IsStale(factory.ReadBefore(action)!, current.Value))
            {
                action.MarkStale(now);
                return Result(action, "Stale", null, new PendingActionErrorDto(
                    MafAgentErrors.StaleCode, "The resource changed after this edit was proposed. Ask the agent to propose it again."));
            }
        }

        ErrorOr<object> executed = await executor.ExecuteAsync(action, CancellationToken.None);
        if (executed.IsError)
        {
            return Failed(action, executed.FirstError, now);
        }

        action.MarkSucceeded(encryptionService.Encrypt(JsonSerializer.Serialize(executed.Value, executed.Value.GetType())), now);
        return Result(action, "Succeeded", executed.Value, null);
    }

    private static PendingActionResultDto Failed(PendingAction action, Error error, DateTime now)
    {
        action.MarkFailed(error.Code, now);
        return Result(action, "Failed", null, new PendingActionErrorDto(error.Code, error.Description));
    }

    private static PendingActionResultDto Result(PendingAction action, string status, object? result, PendingActionErrorDto? error) =>
        new(action.Id, action.Operation.ToString(), status, result, error);

    // Ids, operation and status only: never user free text (plan §0 "Logs").
    private static string OutcomeNote(PendingActionResultDto outcome) =>
        $"[sistema] La acción pendiente {outcome.Id} ({outcome.Operation}) se confirmó: {outcome.Status}" +
        (outcome.Error is null ? "." : $" ({outcome.Error.Code}).");
}
