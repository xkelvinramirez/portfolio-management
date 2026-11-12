namespace Contracts.MafAgent;

/// <summary>
/// A chat turn with the MAF agent. A null <paramref name="ConversationId"/> starts a new conversation.
/// <paramref name="PortfolioId"/> is the portfolio the user is looking at (the dashboard's selection): the agent uses
/// it whenever the user does not name another one. Null when there is none.
/// </summary>
public sealed record SendMafAgentMessageRequest(Guid? ConversationId, string Message, long? PortfolioId = null);
