using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Application.Common.Behaviors;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        _logger.LogInformation($"Request {typeof(TRequest).Name}, details: {JsonSerializer.Serialize(request)}");
        var response = await next(cancellationToken);
        _logger.LogInformation($"Response {typeof(TResponse).Name} details: {JsonSerializer.Serialize(response)}");
        return response;
    }
}