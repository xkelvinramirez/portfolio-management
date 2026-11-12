namespace Contracts.MafAgent;

/// <summary>A chat turn with the MAF agent. A null <paramref name="ConversationId"/> starts a new conversation.</summary>
public sealed record SendMafAgentMessageRequest(Guid? ConversationId, string Message);
