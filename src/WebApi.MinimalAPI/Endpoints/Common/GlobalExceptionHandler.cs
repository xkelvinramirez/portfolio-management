using Microsoft.AspNetCore.Diagnostics;
using System.Net;
using System.Runtime.Serialization;

namespace WebApi.MinimalAPI.Endpoints.Common;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(HttpContext context, Exception ex, CancellationToken cancellationToken)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = 500;
        string text;
        if (ex is CustomException ex2)
        {
            context.Response.StatusCode = (int)ex2.StatusCode;
            text = ex2.ExceptionBody;
        }
        else
        {
            context.Response.StatusCode = 500;
            text = ex.Message;
        }

        logger.LogError($"Unhandled Exception: {ex}");
        //HttpResponseWritingExtensions.WriteAsync(text: (!(ex is CustomException)) ? new
        //{
        //    success = false,
        //    error_code = context.Response.StatusCode,
        //    error_msg = text
        //}.ToJsonString() : new
        //{
        //    Error = text
        //}.ToJsonString(), response: context.Response, cancellationToken: cancellationToken);
        return new ValueTask<bool>(result: true);
    }
}


[Serializable]
public class CustomException : BaseException
{
    public string ExceptionBody { get; set; } = string.Empty;

    public CustomException()
    {
    }

    public CustomException(string message)
        : base(message)
    {
    }

    public CustomException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    [Obsolete("Obsolete")]
    protected CustomException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }

    public CustomException(HttpStatusCode statusCode, string exceptionBody)
    {
        base.StatusCode = statusCode;
        ExceptionBody = exceptionBody;
    }
}

[Serializable]
public class BaseException : Exception
{
    public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.InternalServerError;

    public BaseException()
    {
    }

    public BaseException(string message)
        : base(message)
    {
    }

    public BaseException(string format, params object[] args)
        : base(string.Format(format, args))
    {
    }

    public BaseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public BaseException(string format, Exception innerException, params object[] args)
        : base(string.Format(format, args), innerException)
    {
    }

    [Obsolete("Obsolete")]
    protected BaseException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }

    public BaseException(HttpStatusCode statusCode)
    {
        StatusCode = statusCode;
    }

    public BaseException(string message, HttpStatusCode statusCode)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public BaseException(string format, HttpStatusCode statusCode, params object[] args)
        : base(string.Format(format, args))
    {
        StatusCode = statusCode;
    }

    public BaseException(string message, HttpStatusCode statusCode, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public BaseException(string format, HttpStatusCode statusCode, Exception innerException, params object[] args)
        : base(string.Format(format, args), innerException)
    {
        StatusCode = statusCode;
    }
}