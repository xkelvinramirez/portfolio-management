using Application.PortfolioEntries.Command;
using Application.PortfolioEntries.Query;
using Asp.Versioning.Conventions;
using Contracts.Common;
using Contracts.PortfolioEntries;
using ErrorOr;
using MediatR;
using WebApi.MinimalAPI.Endpoints.Common;

namespace WebApi.MinimalAPI.Endpoints.PortfolioEntries;

public static class PortfolioEntryEndpoints
{
    public static WebApplication RegisterPortfolioEntryEndpoints(this WebApplication app)
    {
        var versionSet = app.NewApiVersionSet()
           .HasApiVersion(1)
           .Build();
        var bases = app.MapGroup("/api/v{version:apiVersion}/PortfolioEntry/")
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(1)
            .WithTags(EndpointTags.PortfolioEntry)
            .RequireAuthorization();

        bases.MapPost("/", async (ISender mediatr, CreatePortfolioEntryRequest request) =>
        {
            var result = await mediatr.Send(new CreatePortfolioEntryCommand(request));
            return result.Match(Results.Ok, errors => errors.ToProblemResult());
        })
           .Produces<CreatePortfolioEntryResponse>()
           .Produces(404)
           .Produces<List<Error>>(400);

        bases.MapGet("/", async (ISender mediatr, [AsParameters] PaginatorRequest paginator, long portfolioId) =>
        {
            var result = await mediatr.Send(new GetPortfolioEntriesQuery(portfolioId, paginator));
            return result.Match(Results.Ok, errors => errors.ToProblemResult());
        })
           .Produces<PaginatorResponse<PortfolioEntryResponse>>()
           .Produces(404)
           .Produces<List<Error>>(400);

        bases.MapGet("{id:long}", async (ISender mediatr, long id) =>
        {
            var result = await mediatr.Send(new GetPortfolioEntryByIdQuery(id));
            return result.Match(Results.Ok, errors => errors.ToProblemResult());
        })
           .Produces<PortfolioEntryResponse>()
           .Produces(404)
           .Produces<List<Error>>(400);

        bases.MapPut("{id:long}", async (ISender mediatr, long id, UpdatePortfolioEntryRequest request) =>
        {
            var result = await mediatr.Send(new UpdatePortfolioEntryCommand(id, request));
            return result.Match(Results.Ok, errors => errors.ToProblemResult());
        })
           .Produces<UpdatePortfolioEntryResponse>()
           .Produces(404)
           .Produces<List<Error>>(400);

        bases.MapDelete("{id:long}", async (ISender mediatr, long id) =>
        {
            var result = await mediatr.Send(new DeletePortfolioEntryCommand(id));
            return result.Match(_ => Results.NoContent(), errors => errors.ToProblemResult());
        })
           .Produces(204)
           .Produces(404)
           .Produces<List<Error>>(400);

        return app;
    }
}
