using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Application.Common.Behaviors;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    // Credentials, plus free text that is user-written or decrypted from storage (chat messages,
    // agent replies, pending-action payloads/results, note content): none of it may reach Seq.
    // Matched by property name at any depth, including inside nested collections.
    private static readonly string[] SensitivePropertyNames =
    [
        "Password", "AccessToken", "RefreshToken",
        "Message", "Reply", "Values", "Before", "Result", "Content", "Summary",
    ];

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { RedactSensitiveProperties }
        }
    };

    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Request {RequestName}, details: {RequestDetails}",
            typeof(TRequest).Name,
            JsonSerializer.Serialize(request, SerializerOptions));

        var response = await next(cancellationToken);

        _logger.LogInformation(
            "Response {ResponseName}, details: {ResponseDetails}",
            typeof(TResponse).Name,
            JsonSerializer.Serialize(response, SerializerOptions));

        return response;
    }

    private static void RedactSensitiveProperties(JsonTypeInfo typeInfo)
    {
        foreach (var property in typeInfo.Properties)
        {
            if (SensitivePropertyNames.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
            {
                property.ShouldSerialize = (_, _) => false;
            }
        }
    }
}