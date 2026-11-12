using System.Text.Json;
using Application.MafAgent;
using Application.MafAgent.Interfaces;
using Domain.Common.Exceptions;
using Domain.Entities;
using Infrastructure.Common.Options;
using Infrastructure.MafAgent.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.Timeout;

namespace Infrastructure.MafAgent.Runner;

/// <summary>
/// Runs one turn of the Microsoft Agent Framework agent (plan §3 "Puerto del agente", T030). Scoped: one per
/// request, with the request's tools (and so the authenticated user's identity).
/// <list type="bullet">
/// <item>The session is restored from the decrypted JSON (empty = new conversation) and returned serialized; the
/// handler encrypts it. Its in-memory history survives API restarts (AC15).</item>
/// <item>Each turn injects the UTC time, the Europe/Madrid local time with its offset and the zone id (AC19).</item>
/// <item>A function-invocation middleware COUNTS the tool calls of the turn: past
/// <see cref="AiOptions.MaxAllowedToolLoops"/> it stops the loop without running the tool, and the turn ends with
/// <see cref="AgentLoopExhaustedException"/> (plan §0 "Límite de iteraciones"; the built-in
/// MaximumIterationsPerRequest stops silently instead).</item>
/// <item>An inference timeout from the <c>InferenceClient</c> resilience pipeline becomes
/// <see cref="AgentTimeoutException"/> (AC14).</item>
/// <item>The tool log has the tool name, conversation, duration and outcome: never arguments, results or message
/// text (plan §0 "Logs").</item>
/// </list>
/// </summary>
internal sealed class MafAgentRunner(
    IMafChatClientFactory chatClientFactory,
    MafAgentReadTools readTools,
    MafAgentProposeTools proposeTools,
    IOptions<AiOptions> options,
    TimeProvider timeProvider,
    ILogger<MafAgentRunner> logger) : IMafAgentRunner
{
    internal const string AgentName = "PortfolioMafAgent";

    // The four rules of plan §0 "Instrucciones del agente", plus how to read the tools.
    internal const string Instructions =
        "You are a portfolio management assistant for the authenticated user. Use ONLY the provided tools for data; " +
        "never invent ids or values that a tool did not return, and report numbers exactly as the tools give them.\n" +
        "1. Always answer in the same language as the user's last message.\n" +
        "2. Deleting anything (portfolios, entries, notes) and creating or editing cryptocurrencies or exchanges is " +
        "not supported in this version: say so plainly and propose nothing.\n" +
        "3. Writes are only PROPOSED with the Propose* tools; nothing changes until the user confirms the returned " +
        "pending action by its id outside this chat. Tell the user what was proposed and that it needs confirmation. " +
        "If a Propose* tool returns MissingOrAmbiguous:<field>, ask the user for that field and propose nothing. " +
        "UnknownCatalogItem:<value> means the asset or exchange does not exist in the catalog. NotFound means the " +
        "resource does not exist or is not the user's.\n" +
        "4. Resolve relative dates (yesterday, last Monday, this month) in the Europe/Madrid time zone from the local " +
        "time given in the context, and pass tools the equivalent UTC instant with a Z suffix.";

    private int _toolCalls;
    private bool _loopExhausted;
    private Guid _conversationId;

    public async Task<MafAgentTurnResult> RunTurnAsync(
        MafConversation conversation, string decryptedSessionJson, string message, CancellationToken ct)
    {
        _toolCalls = 0;
        _loopExhausted = false;
        _conversationId = conversation.Id;
        proposeTools.BeginTurn(conversation.Id);

        AIAgent agent = BuildAgent();
        AgentSession session = await RestoreSessionAsync(agent, decryptedSessionJson, ct);

        AgentResponse response;
        try
        {
            response = await agent.RunAsync([TimeContext(), new ChatMessage(ChatRole.User, message)], session, cancellationToken: ct);
        }
        catch (Exception exception) when (IsInferenceTimeout(exception))
        {
            logger.LogWarning("MAF agent turn in conversation {ConversationId} timed out waiting for the model", conversation.Id);
            throw new AgentTimeoutException();
        }

        if (_loopExhausted)
        {
            throw new AgentLoopExhaustedException(options.Value.MaxAllowedToolLoops);
        }

        JsonElement serialized = await agent.SerializeSessionAsync(session, cancellationToken: ct);
        return new MafAgentTurnResult(response.Text, serialized.GetRawText(), proposeTools.ProposedActions.ToList());
    }

    public async Task<string> AppendSystemNoteAsync(string decryptedSessionJson, string note, CancellationToken ct)
    {
        AIAgent agent = BuildAgent();
        AgentSession session = await RestoreSessionAsync(agent, decryptedSessionJson, ct);

        List<ChatMessage> history = session.TryGetInMemoryChatHistory(out List<ChatMessage>? messages) ? messages : [];
        history.Add(new ChatMessage(ChatRole.System, note));
        session.SetInMemoryChatHistory(history);

        JsonElement serialized = await agent.SerializeSessionAsync(session, cancellationToken: ct);
        return serialized.GetRawText();
    }

    private AIAgent BuildAgent()
    {
        AIAgent agent = chatClientFactory.Create().AsAIAgent(new ChatClientAgentOptions
        {
            Name = AgentName,
            ChatOptions = new ChatOptions
            {
                Instructions = Instructions,
                Temperature = 0.1f,
                Tools = [.. readTools.GetTools(), .. proposeTools.GetTools()],
            },
        });

        return agent.AsBuilder().Use(CountAndLogToolCallAsync).Build();
    }

    private static async Task<AgentSession> RestoreSessionAsync(AIAgent agent, string decryptedSessionJson, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(decryptedSessionJson))
        {
            return await agent.CreateSessionAsync(ct);
        }

        using JsonDocument document = JsonDocument.Parse(decryptedSessionJson);
        return await agent.DeserializeSessionAsync(document.RootElement.Clone(), cancellationToken: ct);
    }

    private ChatMessage TimeContext()
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        return new ChatMessage(ChatRole.System,
            $"Context for this turn: current time {BusinessTime.FormatUtc(utcNow)} (UTC); " +
            $"local business time {BusinessTime.FormatLocal(utcNow)}; business time zone id {BusinessTime.ZoneId}.");
    }

    private async ValueTask<object?> CountAndLogToolCallAsync(
        AIAgent agent,
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, CancellationToken, ValueTask<object?>> next,
        CancellationToken cancellationToken)
    {
        string tool = context.Function.Name;
        int maxCalls = options.Value.MaxAllowedToolLoops;
        if (++_toolCalls > maxCalls)
        {
            _loopExhausted = true;
            context.Terminate = true;
            logger.LogWarning("MAF agent tool loop capped in conversation {ConversationId}: {Tool} was not run (limit {MaxToolCalls})",
                _conversationId, tool, maxCalls);
            return "Stopped: the tool call limit for this turn was reached.";
        }

        long started = timeProvider.GetTimestamp();
        try
        {
            object? result = await next(context, cancellationToken);
            logger.LogInformation("MAF agent tool {Tool} succeeded in conversation {ConversationId} in {ElapsedMs} ms",
                tool, _conversationId, timeProvider.GetElapsedTime(started).TotalMilliseconds);
            return result;
        }
        catch (Exception exception) when (LogToolFailure(tool, exception, started))
        {
            throw; // never reached: the filter only logs and returns false
        }
    }

    // Logs the exception TYPE only: its message could carry argument values.
    private bool LogToolFailure(string tool, Exception exception, long started)
    {
        logger.LogWarning("MAF agent tool {Tool} failed in conversation {ConversationId} after {ElapsedMs} ms: {ExceptionType}",
            tool, _conversationId, timeProvider.GetElapsedTime(started).TotalMilliseconds, exception.GetType().Name);
        return false;
    }

    private static bool IsInferenceTimeout(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is TimeoutRejectedException)
            {
                return true;
            }
        }

        return false;
    }
}
