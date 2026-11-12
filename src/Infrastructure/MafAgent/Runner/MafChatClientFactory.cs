using System.ClientModel;
using System.ClientModel.Primitives;
using Azure.AI.OpenAI;
using Infrastructure.Common.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;

namespace Infrastructure.MafAgent.Runner;

internal interface IMafChatClientFactory
{
    IChatClient Create();
}

/// <summary>
/// Builds the MAF agent's chat client from the existing <c>Ai</c> section (<see cref="AiOptions"/>), with the
/// reasoning model, over the <c>InferenceClient</c> HttpClient that already carries the inference timeout.
/// Ollama and OpenAI use the OpenAI-compatible client (Ollama through its <c>/v1</c> endpoint); Azure OpenAI uses
/// its own client and the deployment name. The agent, its tools and its instructions are the same for all.
/// </summary>
internal sealed class MafChatClientFactory(IOptions<AiOptions> options, IHttpClientFactory httpClientFactory) : IMafChatClientFactory
{
    public const string InferenceClientName = "InferenceClient";

    public IChatClient Create()
    {
        AiOptions ai = options.Value;
        ModelOptions model = ai.ReasoningModel;
        string modelId = string.IsNullOrWhiteSpace(model.ModelId) ? "llama3.2" : model.ModelId;
        ApiKeyCredential key = new(string.IsNullOrWhiteSpace(model.ApiKey) ? "unused" : model.ApiKey);
        // Only the "InferenceClient" resilience pipeline (Polly, Ai:RequestTimeoutSeconds) may time out or retry:
        // HttpClient's own 100 s timeout, System.ClientModel's 100 s network timeout and its retries would cut a
        // slow local model first, retry it, and surface as a TaskCanceledException instead of the mapped timeout.
        HttpClient httpClient = httpClientFactory.CreateClient(InferenceClientName);
        httpClient.Timeout = Timeout.InfiniteTimeSpan;
        HttpClientPipelineTransport transport = new(httpClient);

        if (ai.Provider == AiProviderType.AzureOpenAI)
        {
            if (string.IsNullOrWhiteSpace(model.Endpoint))
            {
                throw new InvalidOperationException("Ai:ReasoningModel:Endpoint is required when Ai:Provider is AzureOpenAI.");
            }

            return new AzureOpenAIClient(new Uri(model.Endpoint), key, new AzureOpenAIClientOptions { Transport = transport, NetworkTimeout = Timeout.InfiniteTimeSpan, RetryPolicy = new ClientRetryPolicy(0) })
                .GetChatClient(string.IsNullOrWhiteSpace(model.DeploymentName) ? modelId : model.DeploymentName)
                .AsIChatClient();
        }

        OpenAIClientOptions clientOptions = new() { Transport = transport, NetworkTimeout = Timeout.InfiniteTimeSpan, RetryPolicy = new ClientRetryPolicy(0) };
        if (!string.IsNullOrWhiteSpace(model.Endpoint))
        {
            clientOptions.Endpoint = new Uri(model.Endpoint);
        }
        else if (ai.Provider == AiProviderType.Ollama)
        {
            // Without it the OpenAI client would silently call api.openai.com.
            throw new InvalidOperationException("Ai:ReasoningModel:Endpoint is required when Ai:Provider is Ollama.");
        }

        return new OpenAIClient(key, clientOptions).GetChatClient(modelId).AsIChatClient();
    }
}
