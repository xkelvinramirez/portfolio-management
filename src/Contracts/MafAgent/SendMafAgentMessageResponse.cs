namespace Contracts.MafAgent;

/// <summary>The agent's reply plus ONLY the pending actions proposed during this turn.</summary>
public sealed record SendMafAgentMessageResponse(
    Guid ConversationId,
    string Reply,
    IReadOnlyList<PendingActionDto> PendingActions);
