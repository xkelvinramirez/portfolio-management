using Application.Common.Behaviors;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace Application.Tests.Common.Behaviors;

/// <summary>
/// LoggingBehavior serializes every MediatR request/response to the log (Seq). Free text written by
/// the user or decrypted from storage (chat messages, agent replies, pending-action payloads, note
/// content) must never reach it (spec maf-tool-calling-agent, Behaviour §Conversación; plan §0 Logs; T040).
/// </summary>
public sealed class LoggingBehaviorRedactionTests
{
    private const string KeptValue = "KEEP-ME-VISIBLE";

    private static readonly string[] SensitiveSentinels =
    [
        "SECRET-MESSAGE",
        "SECRET-REPLY",
        "SECRET-VALUES",
        "SECRET-BEFORE",
        "SECRET-RESULT",
        "SECRET-CONTENT",
        "SECRET-SUMMARY",
        "SECRET-NESTED-SUMMARY",
        "SECRET-NESTED-VALUES",
        "SECRET-NESTED-BEFORE",
    ];

    private sealed record FakeRequest(
        string Kept,
        string Message,
        string Content,
        string Summary,
        string Values,
        string Before,
        string Result,
        string Password);

    private sealed record FakePendingAction(string Id, string Summary, string Values, string Before);

    private sealed record FakeResponse(string Kept, string Reply, IReadOnlyList<FakePendingAction> PendingActions);

    [Fact]
    public async Task Handle_NeverLogsSensitiveFreeTextProperties_IncludingNestedCollections()
    {
        var logger = new CapturingLogger<LoggingBehavior<FakeRequest, FakeResponse>>();
        var behavior = new LoggingBehavior<FakeRequest, FakeResponse>(logger);
        var request = new FakeRequest(
            Kept: KeptValue,
            Message: "SECRET-MESSAGE",
            Content: "SECRET-CONTENT",
            Summary: "SECRET-SUMMARY",
            Values: "SECRET-VALUES",
            Before: "SECRET-BEFORE",
            Result: "SECRET-RESULT",
            Password: "SECRET-PASSWORD");
        var response = new FakeResponse(
            Kept: KeptValue,
            Reply: "SECRET-REPLY",
            PendingActions: [new FakePendingAction("action-1", "SECRET-NESTED-SUMMARY", "SECRET-NESTED-VALUES", "SECRET-NESTED-BEFORE")]);

        await behavior.Handle(request, _ => Task.FromResult(response), TestContext.Current.CancellationToken);

        string logged = string.Join("\n", logger.Messages);
        logged.Should().NotContainAny(SensitiveSentinels);
    }

    [Fact]
    public async Task Handle_StillLogsNonSensitiveProperties()
    {
        var logger = new CapturingLogger<LoggingBehavior<FakeRequest, FakeResponse>>();
        var behavior = new LoggingBehavior<FakeRequest, FakeResponse>(logger);
        var request = new FakeRequest(KeptValue, "m", "c", "s", "v", "b", "r", "p");
        var response = new FakeResponse(KeptValue, "reply", [new FakePendingAction("action-1", "s", "v", "b")]);

        await behavior.Handle(request, _ => Task.FromResult(response), TestContext.Current.CancellationToken);

        logger.Messages.Should().HaveCount(2);
        logger.Messages[0].Should().Contain(nameof(FakeRequest)).And.Contain(KeptValue);
        logger.Messages[1].Should().Contain(nameof(FakeResponse)).And.Contain(KeptValue).And.Contain("action-1");
    }

    [Fact]
    public async Task Handle_KeepsRedactingExistingCredentialProperties()
    {
        var logger = new CapturingLogger<LoggingBehavior<FakeRequest, FakeResponse>>();
        var behavior = new LoggingBehavior<FakeRequest, FakeResponse>(logger);
        var request = new FakeRequest(KeptValue, "m", "c", "s", "v", "b", "r", "SECRET-PASSWORD");
        var response = new FakeResponse(KeptValue, "reply", []);

        await behavior.Handle(request, _ => Task.FromResult(response), TestContext.Current.CancellationToken);

        string.Join("\n", logger.Messages).Should().NotContain("SECRET-PASSWORD");
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
