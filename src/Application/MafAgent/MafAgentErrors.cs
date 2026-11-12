using ErrorOr;

namespace Application.MafAgent;

/// <summary>The exact error codes of plan §0. A stale edit is NOT one of them: it is a 200 with Status=Stale.</summary>
public static class MafAgentErrors
{
    public const string StaleCode = "MafAgent.PendingAction.Stale";
    public const string UnexpectedFailureCode = "MafAgent.PendingAction.UnexpectedFailure";

    public static Error ConversationNotFound(Guid id) =>
        Error.NotFound("MafAgent.Conversation.NotFound", $"Conversation '{id}' was not found.");

    public static Error ConversationExpired(Guid id) =>
        Error.Conflict("MafAgent.Conversation.Expired", $"Conversation '{id}' expired after 24 hours without activity.");

    // Also for another user's action: the same answer as an unknown id (spec AC7).
    public static Error PendingActionNotFound(Guid id) =>
        Error.NotFound("MafAgent.PendingAction.NotFound", $"Pending action '{id}' was not found.");

    public static Error PendingActionExpired(Guid id) =>
        Error.Conflict("MafAgent.PendingAction.Expired", $"Pending action '{id}' has expired. Ask the agent to propose it again.");

    public static Error PendingActionAlreadyDecided(Guid id) =>
        Error.Conflict("MafAgent.PendingAction.AlreadyDecided", $"Pending action '{id}' was already confirmed or rejected.");

    // Also for another user's portfolio: the same answer as an unknown id.
    public static Error PortfolioNotFound(long id) =>
        Error.NotFound("MafAgent.Portfolio.NotFound", $"Portfolio '{id}' was not found.");

    public static Error LoopExhausted() =>
        Error.Failure("MafAgent.LoopExhausted", "The agent exceeded its step limit. Try a simpler request.");

    public static Error Timeout() =>
        Error.Failure("MafAgent.Timeout", "The agent took too long to answer. Try again.");
}
