using System.Text.RegularExpressions;
using Application.Common.Security;
using Application.MafAgent;
using Application.MafAgent.Interfaces;
using Application.Notes.Query;
using Application.PortfolioEntries.Interfaces;
using Application.Portfolios.Interfaces;
using Contracts.Common;
using Contracts.Notes;
using Domain.Common.Exceptions;
using Domain.Entities;
using ErrorOr;
using FluentAssertions;
using Infrastructure.Common.Options;
using Infrastructure.MafAgent.Runner;
using Infrastructure.MafAgent.Tools;
using MediatR;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Polly.Timeout;

namespace WebApi.MinimalAPI.Tests.MafAgent.Runner;

/// <summary>
/// Plan T029 (AC8, AC9, AC14, AC15, AC19). The runner with a scripted model double: no network, no real model.
/// </summary>
public sealed class MafAgentRunnerTests
{
    private const long UserId = 7;

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 10, 23, 30, 0, TimeSpan.Zero));
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly IEncryptionService _encryption = Substitute.For<IEncryptionService>();
    private readonly ICurrentUserProvider _currentUser = Substitute.For<ICurrentUserProvider>();
    private readonly IPendingActionRepository _actions = Substitute.For<IPendingActionRepository>();
    private readonly ListLogger<MafAgentRunner> _logger = new();
    private readonly IPortfolioRepository _portfolios = Substitute.For<IPortfolioRepository>();
    private readonly AiOptions _options = new() { MaxAllowedToolLoops = 5 };
    private readonly MafConversation _conversation;

    public MafAgentRunnerTests()
    {
        _encryption.Encrypt(Arg.Any<string>()).Returns(c => "enc:" + c.Arg<string>());
        _encryption.Decrypt(Arg.Any<string>()).Returns(c => c.Arg<string>()["enc:".Length..]);
        _currentUser.UserId.Returns(UserId);
        _sender.Send(Arg.Any<GetNotesQuery>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<ErrorOr<PaginatorResponse<NoteResponse>>>(new PaginatorResponse<NoteResponse>
            {
                Page = 1,
                PageSize = 100,
                Total = 1,
                TotalPages = 1,
                Data = [new NoteResponse("n1", "Ideas", "Título", "contenido-de-nota-secreto", DateTime.UtcNow, true)],
            }));
        _conversation = MafConversation.Create(UserId, "enc:", _time.GetUtcNow().UtcDateTime);
        PortfoliosAre([]);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private MafAgentRunner Runner(IChatClient chatClient)
    {
        PendingActionFactory factory = new(_encryption, _currentUser, _time);
        MafAgentReadTools read = new(_portfolios, Substitute.For<IPortfolioEntryRepository>(), _sender, _currentUser, _time);
        MafAgentProposeTools propose = new(factory, _actions, Substitute.For<ICurrentValuesReader>(), _sender, _time);
        IMafChatClientFactory clients = Substitute.For<IMafChatClientFactory>();
        clients.Create().Returns(chatClient);
        return new MafAgentRunner(clients, read, propose, Options.Create(_options), _time, _logger);
    }

    private void PortfoliosAre(Portfolio[] portfolios) =>
        _portfolios.GetAllAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<System.Linq.Expressions.Expression<Func<Portfolio, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(new PaginatorResponse<Portfolio> { Page = 1, PageSize = 50, Total = portfolios.Length, TotalPages = 1, Data = portfolios });
    private static ScriptedChatClient AnswersWith(string text) => new((_, _) => ScriptedChatClient.Text(text));

    // ---- tools and instructions ----

    [Fact]
    public async Task TheModelGetsTheReadAndProposeTools_AndNoDeleteOrCatalogWriteTool()
    {
        ScriptedChatClient model = AnswersWith("hola");

        await Runner(model).RunTurnAsync(_conversation, "", "hola", Token);

        List<string> tools = model.Calls[0].Options!.Tools!.Select(t => t.Name).ToList();
        tools.Should().HaveCount(20);
        tools.Should().Contain(["ListMyPortfolios", "GetNote", "ProposeCreateEntry", "ProposeSendStatusReport"]);
        tools.Should().NotContain(name => Regex.IsMatch(name, "Delete|Remove|Purge", RegexOptions.IgnoreCase), "nothing can be deleted (AC8)");
        tools.Should().NotContain(name => Regex.IsMatch(name, @"^Propose\w*(CryptoCurrenc|Exchange)", RegexOptions.IgnoreCase), "the catalogs are read-only (AC8)");
    }

    [Fact]
    public async Task TheInstructions_CarryTheFourRulesOfThePlan()
    {
        ScriptedChatClient model = AnswersWith("hola");

        await Runner(model).RunTurnAsync(_conversation, "", "hola", Token);

        string instructions = model.Calls[0].Options!.Instructions!;
        instructions.Should().Contain("language of the user's LAST message", "rule 1: answer in the language of the user's last message");
        instructions.Should().Contain("not supported", "rule 2: deleting or writing catalogs is explained as unsupported");
        instructions.Should().Contain("MissingOrAmbiguous", "rule 3: ask for the missing field");
        instructions.Should().Contain("Europe/Madrid", "rule 4: relative dates are resolved in Madrid");
    }

    [Fact]
    public async Task EachTurn_InjectsTheUtcTime_TheMadridLocalTimeWithOffset_AndTheZoneId()
    {
        ScriptedChatClient model = AnswersWith("hola");

        await Runner(model).RunTurnAsync(_conversation, "", "¿qué pasó ayer?", Token);

        // 23:30 UTC on the 10th is already the 11th in Madrid.
        string sent = string.Join("\n", model.Calls[0].Messages.Select(m => m.Text));
        sent.Should().Contain("2026-11-10T23:30:00Z");
        sent.Should().Contain("2026-11-11T00:30:00+01:00");
        sent.Should().Contain("Europe/Madrid");
        model.Calls[0].Messages.Last().Text.Should().Be("¿qué pasó ayer?");
    }

    [Fact]
    public async Task EachTurn_ListsTheUsersPortfoliosWithTheirRealIds_SoTheModelNeverGuessesThem()
    {
        Portfolio main = Portfolio.Create(UserId, "Principal", "");
        typeof(Portfolio).BaseType!.GetProperty(nameof(Portfolio.Id))!.SetValue(main, 41L);
        PortfoliosAre([main]);
        ScriptedChatClient model = AnswersWith("hola");

        await Runner(model).RunTurnAsync(_conversation, "", "¿cuánto vale Principal?", Token);

        string context = model.Calls[0].Messages.Single(m => m.Text.StartsWith(MafAgentRunner.TimeContextMarker)).Text;
        context.Should().Contain("id 41 = \"Principal\"");
    }

    [Fact]
    public async Task AUserWithoutPortfolios_IsToldSoInTheContext()
    {
        ScriptedChatClient model = AnswersWith("hola");

        await Runner(model).RunTurnAsync(_conversation, "", "hola", Token);

        model.Calls[0].Messages.Single(m => m.Text.StartsWith(MafAgentRunner.TimeContextMarker)).Text
            .Should().Contain("the user has no portfolios yet");
    }
    [Fact]
    public async Task TheReply_IsTheModelsFinalText()
    {
        MafAgentTurnResult result = await Runner(AnswersWith("Tienes 2 portafolios.")).RunTurnAsync(_conversation, "", "hola", Token);

        result.Reply.Should().Be("Tienes 2 portafolios.");
    }

    // ---- session (AC15) ----

    [Fact]
    public async Task TheSerializedSession_IsRestoredWithItsPreviousTurns_ByANewRunner()
    {
        MafAgentTurnResult first = await Runner(AnswersWith("Primera respuesta")).RunTurnAsync(_conversation, "", "Primera pregunta", Token);
        first.SerializedSessionJson.Should().Contain("Primera pregunta");

        // A brand-new runner, as after an API restart, continues from the JSON only.
        ScriptedChatClient model = AnswersWith("Segunda respuesta");
        await Runner(model).RunTurnAsync(_conversation, first.SerializedSessionJson, "Segunda pregunta", Token);

        List<string> seen = model.Calls[0].Messages.Select(m => m.Text).ToList();
        seen.Should().Contain("Primera pregunta").And.Contain("Primera respuesta").And.Contain("Segunda pregunta");
    }

    [Fact]
    public async Task AppendSystemNote_AddsTheNoteToTheHistory_WithoutCallingTheModel()
    {
        MafAgentTurnResult first = await Runner(AnswersWith("ok")).RunTurnAsync(_conversation, "", "Primera", Token);
        ScriptedChatClient untouched = AnswersWith("no");

        string updated = await Runner(untouched).AppendSystemNoteAsync(first.SerializedSessionJson, "[sistema] La acción X se confirmó: Succeeded.", Token);

        untouched.Calls.Should().BeEmpty();
        ScriptedChatClient model = AnswersWith("vale");
        await Runner(model).RunTurnAsync(_conversation, updated, "¿Qué pasó?", Token);
        model.Calls[0].Messages.Select(m => m.Text).Should().Contain("[sistema] La acción X se confirmó: Succeeded.");
    }

    // ---- proposals ----

    [Fact]
    public async Task ProposedActions_AreTheOnesStagedInThisTurn_ForThisConversation()
    {
        ScriptedChatClient model = new((i, _) => i == 0
            ? ScriptedChatClient.ToolCall("ProposeCreatePortfolio", new Dictionary<string, object?> { ["name"] = "Ahorro" })
            : ScriptedChatClient.Text("Propuesto."));
        MafAgentRunner runner = Runner(model);

        MafAgentTurnResult first = await runner.RunTurnAsync(_conversation, "", "crea Ahorro", Token);
        MafAgentTurnResult second = await runner.RunTurnAsync(_conversation, first.SerializedSessionJson, "gracias", Token);

        first.ProposedActions.Should().ContainSingle().Which.ConversationId.Should().Be(_conversation.Id);
        _actions.Received(1).Add(first.ProposedActions[0]);
        second.ProposedActions.Should().BeEmpty("each turn returns only its own proposals");
    }

    // ---- limits (AC14) ----

    [Fact]
    public async Task SixToolCallsWithALimitOfFive_ThrowsLoopExhausted_AfterRunningTheToolFiveTimes()
    {
        ScriptedChatClient model = new((_, _) => ScriptedChatClient.ToolCall("ListNotes"));

        Func<Task> act = () => Runner(model).RunTurnAsync(_conversation, "", "bucle", Token);

        (await act.Should().ThrowAsync<AgentLoopExhaustedException>()).Which.MaxIterations.Should().Be(5);
        await _sender.Received(5).Send(Arg.Any<GetNotesQuery>(), Arg.Any<CancellationToken>());
        model.Calls.Should().HaveCount(6, "the sixth tool call stops the loop: the model is not asked again");
    }

    [Fact]
    public async Task ExactlyFiveToolCalls_IsWithinTheLimit()
    {
        ScriptedChatClient model = new((i, _) => i < 5 ? ScriptedChatClient.ToolCall("ListNotes") : ScriptedChatClient.Text("listo"));

        MafAgentTurnResult result = await Runner(model).RunTurnAsync(_conversation, "", "cinco", Token);

        result.Reply.Should().Be("listo");
    }

    [Fact]
    public async Task TheLimitCountsPerTurn_NotPerRunner()
    {
        int turnCalls = 0;
        ScriptedChatClient model = new((_, messages) =>
            messages.Last().Role == ChatRole.User ? Reset() : turnCalls++ < 3 ? ScriptedChatClient.ToolCall("ListNotes") : ScriptedChatClient.Text("ok"));
        MafAgentRunner runner = Runner(model);

        MafAgentTurnResult first = await runner.RunTurnAsync(_conversation, "", "uno", Token);
        Func<Task> second = () => runner.RunTurnAsync(_conversation, first.SerializedSessionJson, "dos", Token);

        await second.Should().NotThrowAsync("3 + 3 calls in two turns never exceed 5 in one turn");

        ChatResponse Reset()
        {
            turnCalls = 1;
            return ScriptedChatClient.ToolCall("ListNotes");
        }
    }

    [Fact]
    public async Task AnInferenceTimeout_IsAgentTimeoutException()
    {
        ScriptedChatClient model = new((_, _) => throw new TimeoutRejectedException("inference timed out"));

        Func<Task> act = () => Runner(model).RunTurnAsync(_conversation, "", "hola", Token);

        await act.Should().ThrowAsync<AgentTimeoutException>();
    }

    [Fact]
    public async Task AWrappedInferenceTimeout_IsAlsoAgentTimeoutException()
    {
        ScriptedChatClient model = new((_, _) => throw new HttpRequestException("send failed", new TimeoutRejectedException("inference timed out")));

        Func<Task> act = () => Runner(model).RunTurnAsync(_conversation, "", "hola", Token);

        await act.Should().ThrowAsync<AgentTimeoutException>();
    }

    [Fact]
    public async Task AnyOtherModelFailure_IsNotDisguisedAsATimeout()
    {
        ScriptedChatClient model = new((_, _) => throw new InvalidOperationException("boom"));

        Func<Task> act = () => Runner(model).RunTurnAsync(_conversation, "", "hola", Token);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ACancellationTheCallerDidNotAskFor_IsATimeout()
    {
        ScriptedChatClient model = new((_, _) => throw new TaskCanceledException("HttpClient.Timeout elapsed", new TimeoutException()));

        Func<Task> act = () => Runner(model).RunTurnAsync(_conversation, "", "hola", Token);

        await act.Should().ThrowAsync<AgentTimeoutException>();
    }

    [Fact]
    public async Task ABareTimeoutException_IsATimeout()
    {
        ScriptedChatClient model = new((_, _) => throw new TimeoutException("socket timeout"));

        Func<Task> act = () => Runner(model).RunTurnAsync(_conversation, "", "hola", Token);

        await act.Should().ThrowAsync<AgentTimeoutException>();
    }

    [Fact]
    public async Task AnOperationCanceledWhileTheCallerStillWaits_IsATimeout()
    {
        ScriptedChatClient model = new((_, _) => throw new OperationCanceledException());

        Func<Task> act = () => Runner(model).RunTurnAsync(_conversation, "", "hola", Token);

        await act.Should().ThrowAsync<AgentTimeoutException>();
    }

    [Fact]
    public async Task ACancellationTheCallerAskedFor_IsNotATimeout()
    {
        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(Token);
        ScriptedChatClient model = new((_, _) =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        });

        Func<Task> act = () => Runner(model).RunTurnAsync(_conversation, "", "hola", cts.Token);

        (await act.Should().ThrowAsync<OperationCanceledException>()).Which.Should().NotBeOfType<AgentTimeoutException>();
    }

    [Fact]
    public async Task OnlyTheCurrentTimeContext_IsSentAndKept_NotTheOnesOfPreviousTurns()
    {
        MafAgentTurnResult first = await Runner(AnswersWith("uno")).RunTurnAsync(_conversation, "", "primera", Token);
        _time.Advance(TimeSpan.FromDays(1));
        ScriptedChatClient model = AnswersWith("dos");

        MafAgentTurnResult second = await Runner(model).RunTurnAsync(_conversation, first.SerializedSessionJson, "segunda", Token);

        List<string> contexts = model.Calls[0].Messages.Select(m => m.Text).Where(t => t.StartsWith(MafAgentRunner.TimeContextMarker)).ToList();
        contexts.Should().ContainSingle().Which.Should().Contain("2026-11-11T23:30:00Z");
        second.SerializedSessionJson.Should().NotContain("2026-11-10T23:30:00Z", "the previous turn's time is dropped from the history");
        model.Calls[0].Messages.Select(m => m.Text).Should().Contain(["primera", "uno"]);
    }

    [Fact]
    public async Task AFailingTool_IsLoggedWithItsExceptionType_ButNotItsMessage()
    {
        _sender.Send(Arg.Any<GetNotesQuery>(), Arg.Any<CancellationToken>())
            .Returns<Task<ErrorOr<PaginatorResponse<NoteResponse>>>>(_ => throw new InvalidOperationException("detalle-secreto"));
        ScriptedChatClient model = new((i, _) => i == 0 ? ScriptedChatClient.ToolCall("ListNotes") : ScriptedChatClient.Text("no pude"));

        await Runner(model).RunTurnAsync(_conversation, "", "hola", Token);

        _logger.Entries.Should().Contain(e => e.Level == LogLevel.Warning && e.Message.Contains("ListNotes") && e.Message.Contains("InvalidOperationException"));
        string.Join("\n", _logger.Entries.Select(e => e.Message + e.Exception)).Should().NotContain("detalle-secreto");
    }
    // ---- logs (plan §0 "Logs") ----

    [Fact]
    public async Task TheToolLog_HasTheToolConversationAndOutcome_ButNeverArgumentsResultsOrMessageText()
    {
        ScriptedChatClient model = new((i, _) => i switch
        {
            0 => ScriptedChatClient.ToolCall("ProposeCreateNote", new Dictionary<string, object?>
            {
                ["category"] = "Ideas",
                ["title"] = "titulo-secreto",
                ["content"] = "argumento-secreto",
            }),
            1 => ScriptedChatClient.ToolCall("ListNotes"),
            _ => ScriptedChatClient.Text("hecho"),
        });

        await Runner(model).RunTurnAsync(_conversation, "", "mensaje-secreto-del-usuario", Token);

        _logger.Entries.Should().Contain(e => e.Message.Contains("ProposeCreateNote") && e.Message.Contains(_conversation.Id.ToString()));
        _logger.Entries.Should().Contain(e => e.Message.Contains("ListNotes"));
        string all = string.Join("\n", _logger.Entries.Select(e => e.Message + e.Exception));
        all.Should().NotContain("secreto", "no argument, tool result or message text reaches the logs");
    }
}

/// <summary>In-memory logger: keeps the level, message and exception of every entry.</summary>
internal sealed class ListLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, formatter(state, exception), exception));
}
