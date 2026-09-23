using Application.Portfolios.Command;
using Asp.Versioning.Conventions;
using Contracts.Portfolios;
using ErrorOr;
using MediatR;
using WebApi.MinimalAPI.Endpoints.Common;

namespace WebApi.MinimalAPI.Endpoints.Portfolios;

public static class PortfolioEndPoints
{
    public static WebApplication RegisterPortfolioEndpoints(this WebApplication app)
    {
        var versionSet = app.NewApiVersionSet()
           .HasApiVersion(1)
           .Build();
        var bases = app.MapGroup("/api/v{version:apiVersion}/Portfolio/")
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(1)
            .WithTags(EndpointTags.Portfolio);

        bases.MapPost("/", async (ISender mediatr, CreatePortfolioRequest request) =>
        {
            var result = await mediatr.Send(new CreatePortfolioCommand(request));
            return result.Match(Results.Ok, Results.BadRequest);
        })
           .Produces<CreatePortfolioResponse>()
           .Produces<List<Error>>(400);

        //bases.MapGet("/{id}", async (ISender mediatr, uint id) =>
        //{
        //    var result = await mediatr.Send(new GetAgentByIdQuery(id));
        //    return result.Match(Results.Ok, Results.BadRequest);
        //})
        //    .Produces<GetAgentByIdResponse>()
        //    .Produces<List<Error>>(400);

        //bases.MapPost("/{id}", async (ISender mediatr, uint id, UpdateAgentRequest request) =>
        //{
        //    var result = await mediatr.Send(new UpdateAgentCommand(id, request));
        //    return result.Match(Results.Ok, Results.BadRequest);
        //})
        //    .Produces<UpdateAgentResponse>()
        //    .Produces<List<Error>>(400);

        return app;
    }
}
