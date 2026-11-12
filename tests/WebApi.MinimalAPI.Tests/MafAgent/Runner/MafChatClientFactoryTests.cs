using FluentAssertions;
using Infrastructure.Common.Options;
using Infrastructure.MafAgent.Runner;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace WebApi.MinimalAPI.Tests.MafAgent.Runner;

/// <summary>
/// Plan T029 (spec Non-goal "same behaviour whatever the provider"): the chat client comes from the existing
/// <c>Ai</c> section. Only construction is checked: no request is sent.
/// </summary>
public sealed class MafChatClientFactoryTests
{
    private static IChatClient Create(AiOptions options)
    {
        IHttpClientFactory http = Substitute.For<IHttpClientFactory>();
        http.CreateClient("InferenceClient").Returns(_ => new HttpClient());
        return new MafChatClientFactory(Options.Create(options), http).Create();
    }

    [Fact]
    public void Ollama_IsAnOpenAiCompatibleClient_OnTheConfiguredEndpoint_WithTheReasoningModel()
    {
        IChatClient client = Create(new AiOptions
        {
            Provider = AiProviderType.Ollama,
            ReasoningModel = new ModelOptions { ModelId = "qwen2.5:3b-instruct", Endpoint = "http://localhost:11434/v1" },
            FastModel = new ModelOptions { ModelId = "not-this-one", Endpoint = "http://elsewhere:1/v1" },
        });

        ChatClientMetadata metadata = client.GetService<ChatClientMetadata>()!;
        metadata.DefaultModelId.Should().Be("qwen2.5:3b-instruct");
        metadata.ProviderUri!.ToString().Should().StartWith("http://localhost:11434/v1");
    }

    [Fact]
    public void AzureOpenAI_IsAnAzureClient_UsingTheDeploymentName()
    {
        IChatClient client = Create(new AiOptions
        {
            Provider = AiProviderType.AzureOpenAI,
            ReasoningModel = new ModelOptions
            {
                ModelId = "gpt-4o-mini",
                DeploymentName = "my-deployment",
                Endpoint = "https://example.openai.azure.com/",
                ApiKey = "not-a-real-key",
            },
        });

        ChatClientMetadata metadata = client.GetService<ChatClientMetadata>()!;
        metadata.DefaultModelId.Should().Be("my-deployment");
        metadata.ProviderUri!.Host.Should().Be("example.openai.azure.com");
    }

    [Fact]
    public void OpenAI_WithoutEndpoint_UsesTheModelId()
    {
        IChatClient client = Create(new AiOptions
        {
            Provider = AiProviderType.OpenAI,
            ReasoningModel = new ModelOptions { ModelId = "gpt-4o-mini", ApiKey = "not-a-real-key" },
        });

        client.GetService<ChatClientMetadata>()!.DefaultModelId.Should().Be("gpt-4o-mini");
    }

    [Fact]
    public async Task TheInferenceHttpClient_IsTheTransport_WithoutItsOwnTimeoutOrClientRetries()
    {
        CountingHandler handler = new();
        HttpClient httpClient = new(handler);
        IHttpClientFactory http = Substitute.For<IHttpClientFactory>();
        http.CreateClient("InferenceClient").Returns(httpClient);
        IChatClient client = new MafChatClientFactory(Options.Create(new AiOptions
        {
            Provider = AiProviderType.Ollama,
            ReasoningModel = new ModelOptions { ModelId = "m", Endpoint = "http://localhost:11434/v1" },
        }), http).Create();

        Func<Task> call = () => client.GetResponseAsync("hola", cancellationToken: TestContext.Current.CancellationToken);

        await call.Should().ThrowAsync<Exception>();
        handler.Requests.Should().Be(1, "only the InferenceClient pipeline may retry, not the OpenAI client");
        handler.LastUri!.ToString().Should().StartWith("http://localhost:11434/v1");
        httpClient.Timeout.Should().Be(Timeout.InfiniteTimeSpan, "only Ai:RequestTimeoutSeconds (Polly) may time out");
    }

    [Fact]
    public void Ollama_WithoutEndpoint_IsAConfigurationError_InsteadOfCallingOpenAI()
    {
        Action act = () => Create(new AiOptions
        {
            Provider = AiProviderType.Ollama,
            ReasoningModel = new ModelOptions { ModelId = "qwen2.5:3b-instruct" },
        });

        act.Should().Throw<InvalidOperationException>().WithMessage("*Endpoint*");
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Requests { get; private set; }

        public Uri? LastUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            LastUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable));
        }
    }
    [Fact]
    public void AzureOpenAI_WithoutEndpoint_IsAConfigurationError()
    {
        Action act = () => Create(new AiOptions
        {
            Provider = AiProviderType.AzureOpenAI,
            ReasoningModel = new ModelOptions { ModelId = "gpt-4o-mini", DeploymentName = "d" },
        });

        act.Should().Throw<InvalidOperationException>().WithMessage("*Endpoint*");
    }
}
