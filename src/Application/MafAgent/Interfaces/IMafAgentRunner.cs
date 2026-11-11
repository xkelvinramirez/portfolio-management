using Domain.Entities;

namespace Application.MafAgent.Interfaces;

/// <summary>
/// Port to the Microsoft Agent Framework agent (implemented in Infrastructure). The handler decrypts the
/// session before calling and encrypts what comes back: the runner never sees IEncryptionService.
/// Throws AgentLoopExhaustedException or AgentTimeoutException; the handler maps them to errors.
/// </summary>
public interface IMafAgentRunner
{
    Task<MafAgentTurnResult> RunTurnAsync(
        MafConversation conversation, string decryptedSessionJson, string message, CancellationToken ct);

    /// <summary>Appends a system note (e.g. the outcome of a confirmed action) and returns the updated session.</summary>
    Task<string> AppendSystemNoteAsync(string decryptedSessionJson, string note, CancellationToken ct);
}

/// <summary>
/// One agent turn. <see cref="ProposedActions"/> are the same instances staged with
/// <see cref="IPendingActionRepository.Add"/> during the turn, not yet committed: build DTOs from them,
/// never re-read them from the database.
/// </summary>
public sealed record MafAgentTurnResult(
    string Reply,
    string SerializedSessionJson,
    IReadOnlyList<PendingAction> ProposedActions);
