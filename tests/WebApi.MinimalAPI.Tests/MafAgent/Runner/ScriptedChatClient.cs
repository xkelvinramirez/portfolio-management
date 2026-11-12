using Microsoft.Extensions.AI;

namespace WebApi.MinimalAPI.Tests.MafAgent.Runner;

/// <summary>
/// A model double: each call returns the next scripted response (a tool call or a final text). It records
/// every request so tests can inspect the messages, instructions and tools the agent sent. No network.
/// </summary>
internal sealed class ScriptedChatClient(Func<int, IReadOnlyList<ChatMessage>, ChatResponse> script) : IChatClient
{
    public List<(IReadOnlyList<ChatMessage> Messages, ChatOptions? Options)> Calls { get; } = [];

    public static ChatResponse Text(string text) => new(new ChatMessage(ChatRole.Assistant, text));

    public static ChatResponse ToolCall(string name, IDictionary<string, object?>? arguments = null) =>
        new(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent(Guid.NewGuid().ToString("N"), name, arguments)]));

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        List<ChatMessage> sent = messages.ToList();
        Calls.Add((sent, options));
        return Task.FromResult(script(Calls.Count - 1, sent));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The MAF runner does not stream.");

    public object? GetService(Type serviceType, object? serviceKey = null) => serviceType.IsInstanceOfType(this) ? this : null;

    public void Dispose()
    {
    }
}
