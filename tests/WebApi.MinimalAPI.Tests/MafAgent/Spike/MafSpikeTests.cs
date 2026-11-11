using System.ClientModel;
using System.ComponentModel;
using System.Text.Json;
using Azure.AI.OpenAI;
using FluentAssertions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI;

namespace WebApi.MinimalAPI.Tests.MafAgent.Spike;

/// <summary>
/// T001 spike (plan maf-tool-calling-agent §6.1): proves Microsoft Agent Framework 1.28 works with the
/// local Ollama model and coexists with Semantic Kernel 1.81 in the same process. Methods tagged
/// Category=Manual need Ollama on localhost:11434 with qwen2.5:3b-instruct; the rest run offline.
/// </summary>
public sealed class MafSpikeTests
{
    private const string OllamaEndpoint = "http://localhost:11434/v1";
    private const string OllamaModel = "qwen2.5:3b-instruct";
    private const decimal PortfolioValue = 1234.5m;

    // ---------- Live (Ollama) ----------

    [Fact]
    [Trait("Category", "Manual")]
    public async Task MafAgent_WithTwoTools_AnswersFromToolResult()
    {
        var tools = new SpikeTools();
        AIAgent agent = CreateMafAgent(CreateOllamaChatClient(), tools);
        AgentSession session = await agent.CreateSessionAsync(TestContext.Current.CancellationToken);

        AgentResponse response = await agent.RunAsync(
            "¿Cuánto vale ahora mismo mi portafolio llamado Principal?", session, cancellationToken: TestContext.Current.CancellationToken);

        tools.ValueCalls.Should().BeGreaterThanOrEqualTo(1, "the model must call the typed tool instead of inventing a value");
        response.Text.Should().ContainAny("1234", "1.234", "1,234");
    }

    [Fact]
    [Trait("Category", "Manual")]
    public async Task MafSession_SerializedAndRestoredInANewAgent_KeepsConversationHistory()
    {
        var tools = new SpikeTools();
        AIAgent firstAgent = CreateMafAgent(CreateOllamaChatClient(), tools);
        AgentSession session = await firstAgent.CreateSessionAsync(TestContext.Current.CancellationToken);
        await firstAgent.RunAsync("¿Cuánto vale mi portafolio Principal?", session, cancellationToken: TestContext.Current.CancellationToken);

        JsonElement serialized = await firstAgent.SerializeSessionAsync(session, cancellationToken: TestContext.Current.CancellationToken);
        string json = serialized.GetRawText();
        json.Should().Contain("Principal", "the persisted session must carry the previous turns");

        // A brand-new agent instance (as after an API restart) restores the session from JSON only.
        AIAgent secondAgent = CreateMafAgent(CreateOllamaChatClient(), new SpikeTools());
        AgentSession restored = await secondAgent.DeserializeSessionAsync(
            JsonDocument.Parse(json).RootElement, cancellationToken: TestContext.Current.CancellationToken);
        AgentResponse followUp = await secondAgent.RunAsync(
            "¿Qué valor exacto me acabas de dar? Responde solo con el número.", restored, cancellationToken: TestContext.Current.CancellationToken);

        followUp.Text.Should().ContainAny("1234", "1.234", "1,234");
    }

    [Fact]
    [Trait("Category", "Manual")]
    public async Task SkAgent_LiveFunctionCalling_StillWorksWithMafPackagesLoaded()
    {
        IKernelBuilder builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(modelId: OllamaModel, endpoint: new Uri(OllamaEndpoint), apiKey: "unused");
        Kernel kernel = builder.Build();
        kernel.Plugins.AddFromObject(new SkSpikePlugin(), "Spike");

        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0.1,
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
        };
        FunctionResult result = await kernel.InvokePromptAsync(
            "Use the available tool to get the current portfolio value and tell me the number.",
            new KernelArguments(settings),
            cancellationToken: TestContext.Current.CancellationToken);

        result.ToString().Should().ContainAny("4321", "4.321", "4,321");
    }

    // ---------- Offline ----------

    [Fact]
    public void AzureOpenAiChatClient_BuildsAMafAgentWithoutNetwork()
    {
        IChatClient chatClient = new AzureOpenAIClient(new Uri("https://example.openai.azure.com/"), new ApiKeyCredential("not-a-real-key"))
            .GetChatClient("gpt-4o-mini")
            .AsIChatClient();

        AIAgent agent = CreateMafAgent(chatClient, new SpikeTools());

        agent.Should().NotBeNull();
        chatClient.GetService<ChatClientMetadata>()!.ProviderName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task FunctionInvocationLimit_ObservedBehaviour_WhenTheModelNeverStopsCallingTools()
    {
        var looping = new AlwaysCallsToolChatClient();
        var tools = new SpikeTools();
        IChatClient chatClient = looping.AsBuilder()
            .UseFunctionInvocation(configure: f => f.MaximumIterationsPerRequest = 3)
            .Build();
        AIAgent agent = CreateMafAgent(chatClient, tools);

        Exception? thrown = null;
        AgentResponse? response = null;
        try
        {
            response = await agent.RunAsync("loop", cancellationToken: TestContext.Current.CancellationToken);
        }
        catch (Exception ex)
        {
            thrown = ex;
        }

        // Observation pinned for plan §0 "Límite de iteraciones" (analyze M10): reaching the cap does
        // NOT throw; the tool runs at most MaximumIterationsPerRequest times and a response comes back.
        thrown.Should().BeNull();
        response.Should().NotBeNull();
        tools.ValueCalls.Should().BeLessThanOrEqualTo(3);
    }

    // ---------- Helpers ----------

    private static IChatClient CreateOllamaChatClient() =>
        new OpenAIClient(new ApiKeyCredential("unused"), new OpenAIClientOptions { Endpoint = new Uri(OllamaEndpoint) })
            .GetChatClient(OllamaModel)
            .AsIChatClient();

    private static AIAgent CreateMafAgent(IChatClient chatClient, SpikeTools tools) =>
        chatClient.AsAIAgent(new ChatClientAgentOptions
        {
            Name = "PortfolioSpikeAgent",
            ChatOptions = new ChatOptions
            {
                Instructions = "You answer questions about the user's portfolios using ONLY the provided tools. " +
                               "Never invent ids or values. Report numbers exactly as the tools return them.",
                Temperature = 0.1f,
                Tools =
                [
                    AIFunctionFactory.Create(tools.ListMyPortfolios, nameof(SpikeTools.ListMyPortfolios)),
                    AIFunctionFactory.Create(tools.GetPortfolioValue, nameof(SpikeTools.GetPortfolioValue)),
                ],
            },
        });

    private sealed class SpikeTools
    {
        public int ValueCalls { get; private set; }

        [Description("Lists the authenticated user's portfolios (id and name).")]
        public string ListMyPortfolios() => """[{"id":1,"name":"Principal"},{"id":2,"name":"Trading"}]""";

        [Description("Gets the current total value of one of the user's portfolios, by portfolio id.")]
        public decimal GetPortfolioValue([Description("The portfolio id, as returned by ListMyPortfolios")] long portfolioId)
        {
            ValueCalls++;
            return portfolioId == 1 ? PortfolioValue : 10m;
        }
    }

    private sealed class SkSpikePlugin
    {
        [KernelFunction, Description("Gets the current portfolio value.")]
        public decimal GetCurrentPortfolioValue() => 4321m;
    }

    /// <summary>A fake model that answers every request with another call to GetPortfolioValue.</summary>
    private sealed class AlwaysCallsToolChatClient : IChatClient
    {
        private int _callId;

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var call = new Microsoft.Extensions.AI.FunctionCallContent($"call-{++_callId}", nameof(SpikeTools.GetPortfolioValue),
                new Dictionary<string, object?> { ["portfolioId"] = 1L });
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, new List<AIContent> { call })));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose()
        {
        }
    }
}
