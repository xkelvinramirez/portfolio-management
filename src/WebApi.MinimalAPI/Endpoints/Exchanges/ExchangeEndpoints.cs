using Application.Exchanges.Command;
using Application.Exchanges.Query;
using Asp.Versioning.Conventions;
using Contracts.Common;
using Contracts.Exchanges;
using ErrorOr;
using MediatR;
using WebApi.MinimalAPI.Endpoints.Common;

namespace WebApi.MinimalAPI.Endpoints.Exchanges;

public static class ExchangeEndpoints
{
    public static WebApplication RegisterExchangeEndpoints(this WebApplication app)
    {
        var versionSet = app.NewApiVersionSet()
           .HasApiVersion(1)
           .Build();
        var bases = app.MapGroup("/api/v{version:apiVersion}/Exchange/")
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(1)
            .WithTags(EndpointTags.Exchange)
            .RequireAuthorization();

        bases.MapPost("/", async (ISender mediatr, CreateExchangeRequest request) =>
        {
            var result = await mediatr.Send(new CreateExchangeCommand(request));
            return result.Match(Results.Ok, Results.BadRequest);
        })
           .Produces<CreateExchangeResponse>()
           .Produces<List<Error>>(400);

        bases.MapGet("/", async (ISender mediatr, [AsParameters] PaginatorRequest paginator) =>
        {
            var result = await mediatr.Send(new GetExchangesQuery(paginator));
            return result.Match(Results.Ok, Results.BadRequest);
        })
           .Produces<PaginatorResponse<ExchangeResponse>>()
           .Produces<List<Error>>(400);

        bases.MapGet("{id:long}", async (ISender mediatr, long id) =>
        {
            var result = await mediatr.Send(new GetExchangeByIdQuery(id));
            return result.Match(Results.Ok, errors => errors.ToProblemResult());
        })
           .Produces<ExchangeResponse>()
           .Produces(404)
           .Produces<List<Error>>(400);

        bases.MapPut("{id:long}", async (ISender mediatr, long id, UpdateExchangeRequest request) =>
        {
            var result = await mediatr.Send(new UpdateExchangeCommand(id, request));
            return result.Match(Results.Ok, errors => errors.ToProblemResult());
        })
           .Produces<UpdateExchangeResponse>()
           .Produces(404)
           .Produces<List<Error>>(400);

        bases.MapDelete("{id:long}", async (ISender mediatr, long id) =>
        {
            var result = await mediatr.Send(new DeleteExchangeCommand(id));
            return result.Match(_ => Results.NoContent(), errors => errors.ToProblemResult());
        })
           .Produces(204)
           .Produces(404)
           .Produces<List<Error>>(400);

        return app;
    }
}
