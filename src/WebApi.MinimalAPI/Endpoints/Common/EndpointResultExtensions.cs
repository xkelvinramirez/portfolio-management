using ErrorOr;

namespace WebApi.MinimalAPI.Endpoints.Common;

public static class EndpointResultExtensions
{
    public static IResult ToProblemResult(this List<Error> errors)
        => errors.Count > 0 && errors[0].Type == ErrorType.NotFound
            ? Results.NotFound(errors)
            : Results.BadRequest(errors);
}
