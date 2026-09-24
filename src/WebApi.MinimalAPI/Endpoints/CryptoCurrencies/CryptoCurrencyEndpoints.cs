using Application.CryptoCurrencies.Command;
using Application.CryptoCurrencies.Query;
using Asp.Versioning.Conventions;
using Contracts.Common;
using Contracts.CryptoCurrencies;
using ErrorOr;
using MediatR;
using WebApi.MinimalAPI.Endpoints.Common;

namespace WebApi.MinimalAPI.Endpoints.CryptoCurrencies;

public static class CryptoCurrencyEndpoints
{
    public static WebApplication RegisterCryptoCurrencyEndpoints(this WebApplication app)
    {
        var versionSet = app.NewApiVersionSet()
           .HasApiVersion(1)
           .Build();
        var bases = app.MapGroup("/api/v{version:apiVersion}/CryptoCurrency/")
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(1)
            .WithTags(EndpointTags.CryptoCurrency)
            .RequireAuthorization();

        bases.MapPost("/", async (ISender mediatr, CreateCryptoCurrencyRequest request) =>
        {
            var result = await mediatr.Send(new CreateCryptoCurrencyCommand(request));
            return result.Match(Results.Ok, Results.BadRequest);
        })
           .Produces<CreateCryptoCurrencyResponse>()
           .Produces<List<Error>>(400);

        bases.MapGet("/", async (ISender mediatr, [AsParameters] PaginatorRequest paginator) =>
        {
            var result = await mediatr.Send(new GetCryptoCurrenciesQuery(paginator));
            return result.Match(Results.Ok, Results.BadRequest);
        })
           .Produces<PaginatorResponse<CryptoCurrencyResponse>>()
           .Produces<List<Error>>(400);

        bases.MapGet("{id:long}", async (ISender mediatr, long id) =>
        {
            var result = await mediatr.Send(new GetCryptoCurrencyByIdQuery(id));
            return result.Match(Results.Ok, errors => errors.ToProblemResult());
        })
           .Produces<CryptoCurrencyResponse>()
           .Produces(404)
           .Produces<List<Error>>(400);

        bases.MapPut("{id:long}", async (ISender mediatr, long id, UpdateCryptoCurrencyRequest request) =>
        {
            var result = await mediatr.Send(new UpdateCryptoCurrencyCommand(id, request));
            return result.Match(Results.Ok, errors => errors.ToProblemResult());
        })
           .Produces<UpdateCryptoCurrencyResponse>()
           .Produces(404)
           .Produces<List<Error>>(400);

        bases.MapDelete("{id:long}", async (ISender mediatr, long id) =>
        {
            var result = await mediatr.Send(new DeleteCryptoCurrencyCommand(id));
            return result.Match(_ => Results.NoContent(), errors => errors.ToProblemResult());
        })
           .Produces(204)
           .Produces(404)
           .Produces<List<Error>>(400);

        return app;
    }
}
