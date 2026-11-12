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

    // The four rules of plan §0 "Instrucciones del agente", plus how to read the tools. Written as short, explicit
    // rules: the T035 checklist showed a small local model inventing ids and values and switching language.
    internal const string Instructions =
        "You are a portfolio management assistant for the authenticated user. Use ONLY the provided tools for data, " +
        "and report numbers exactly as the tools give them.\n" +
        "1. LANGUAGE: answer in the language of the user's LAST message. If the user writes in Spanish, answer in " +
        "Spanish; if in English, in English. Never switch language.\n" +
        "2. NOT SUPPORTED: deleting anything (portfolios, entries, notes) and creating or editing cryptocurrencies or " +
        "exchanges is not supported in this version. Say so in one sentence and stop: propose nothing, and never " +
        "offer a confirmation phrase, a workaround or a deletion.\n" +
        "3. IDS: never guess an id. Portfolio ids are listed in the context of each turn (or call ListMyPortfolios); " +
        "use the id whose name matches what the user said; if the user names no portfolio, use the one the context " +
        "says the user is looking at. Entry ids come from GetPortfolioEntries, note ids from " +
        "ListNotes.\n" +
        "4. MISSING DATA: never fill in a value the user did not give (quantity, price, asset symbol, exchange, date, " +
        "name, title, content). Call the Propose* tool with only what the user gave; when it returns " +
        "MissingOrAmbiguous:<field>, ask the user for that field. An entry ALWAYS needs the asset symbol (e.g. BTC). " +
        "UnknownCatalogItem:<value> means that asset or exchange does not exist. NotFound means it does not exist or " +
        "is not the user's.\n" +
        "5. WRITES: you only PROPOSE them. Nothing changes until the user confirms the pending action with the " +
        "confirm button of the app, never by writing in this chat. Tell the user what you proposed.\n" +
        "6. DATES: resolve relative dates (yesterday, last Monday, this month) in the Europe/Madrid time zone from the " +
        "local time given in the context. For a day without a time pass yyyy-MM-dd; for an exact time pass the UTC " +
        "instant with a Z suffix.";

    // Marks the per-turn time message. Only the current one is kept in the history: older ones would leave the
    // model several conflicting "current" times (and grow the session on every turn).
    internal const string TimeContextMarker = "Context for this turn:";

    // Precedes the portfolio the user is looking at in the turn context.
    internal const string ActivePortfolioMarker = "The user is looking at portfolio";

    private int _toolCalls;
    private bool _loopExhausted;
    private Guid _conversationId;

    public async Task<MafAgentTurnResult> RunTurnAsync(
        MafConversation conversation, string decryptedSessionJson, string message, ActivePortfolio? activePortfolio,
        CancellationToken ct)
    {
        _toolCalls = 0;
        _loopExhausted = false;
        _conversationId = conversation.Id;
        proposeTools.BeginTurn(conversation.Id);

        AIAgent agent = BuildAgent();
        AgentSession session = await RestoreSessionAsync(agent, decryptedSessionJson, ct);
        DropPreviousTimeContexts(session);

        AgentResponse response;
        try
        {
            ChatMessage context = await TurnContextAsync(activePortfolio, ct);
            response = await agent.RunAsync([context, new ChatMessage(ChatRole.User, message)], session, cancellationToken: ct);
        }
        catch (Exception exception) when (IsInferenceTimeout(exception, ct))
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

    // The time of the turn (AC19), the user's own portfolios with their ids, so the model never has to guess an id, and
    // the portfolio the user is looking at, so a question that names none is not answered about another one.
    private async Task<ChatMessage> TurnContextAsync(ActivePortfolio? activePortfolio, CancellationToken ct)
    {
        DateTime utcNow = timeProvider.GetUtcNow().UtcDateTime;
        object listed = await readTools.ListMyPortfolios(ct);
        IReadOnlyList<MafPortfolioSummary> portfolios = listed as IReadOnlyList<MafPortfolioSummary> ?? [];
        string portfolioList = portfolios.Count == 0
            ? "the user has no portfolios yet"
            : string.Join("; ", portfolios.Select(p => $"id {p.Id} = \"{p.Name}\""));

        return new ChatMessage(ChatRole.System,
            $"{TimeContextMarker} current time {BusinessTime.FormatUtc(utcNow)} (UTC); " +
            $"local business time {BusinessTime.FormatLocal(utcNow)}; business time zone id {BusinessTime.ZoneId}. " +
            $"The user's portfolios: {portfolioList}." +
            (activePortfolio is null
                ? ""
                : $" {ActivePortfolioMarker} id {activePortfolio.Id} = \"{activePortfolio.Name}\". When the user does not " +
                  "name a portfolio, the question and any proposal are about this one."));
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

    private static void DropPreviousTimeContexts(AgentSession session)
    {
        if (session.TryGetInMemoryChatHistory(out List<ChatMessage>? history) &&
            history.RemoveAll(m => m.Role == ChatRole.System && m.Text.StartsWith(TimeContextMarker, StringComparison.Ordinal)) > 0)
        {
            session.SetInMemoryChatHistory(history);
        }
    }

    // A timeout is the inference pipeline's (Polly TimeoutRejectedException), or any timeout/cancellation that the
    // caller did NOT ask for. A cancellation the caller requested (ct) is not a timeout and propagates as is.
    private static bool IsInferenceTimeout(Exception exception, CancellationToken ct)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is TimeoutRejectedException or TimeoutException ||
                (current is OperationCanceledException && !ct.IsCancellationRequested))
            {
                return true;
            }
        }

        return false;
    }
}
