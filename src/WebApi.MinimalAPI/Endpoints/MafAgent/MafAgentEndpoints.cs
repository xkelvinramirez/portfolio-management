using Application.MafAgent.Command;
using Asp.Versioning.Conventions;
using Contracts.MafAgent;
using ErrorOr;
using MediatR;
using WebApi.MinimalAPI.Endpoints.Common;

namespace WebApi.MinimalAPI.Endpoints.MafAgent;

/// <summary>
/// The Microsoft Agent Framework agent (plan §3). The chat only PROPOSES writes; a write runs only through the
/// confirm endpoint, by pending action id (AC4). None of these POSTs takes an Idempotency-Key: confirm and reject are
/// idempotent by id, and a duplicated chat only duplicates harmless proposals (accepted deviation, analyze F5).
/// </summary>
public static class MafAgentEndpoints
{
    public static WebApplication RegisterMafAgentEndpoints(this WebApplication app)
    {
        var versionSet = app.NewApiVersionSet()
           .HasApiVersion(1)
           .Build();

        var bases = app.MapGroup("/api/v{version:apiVersion}/MafAgent/")
            .WithApiVersionSet(versionSet)
            .MapToApiVersion(1)
            .WithTags(EndpointTags.MafAgent)
            .RequireAuthorization();

        bases.MapPost("chat", async (ISender mediatr, SendMafAgentMessageRequest request) =>
        {
            var result = await mediatr.Send(new SendMafAgentMessageCommand(request));
            return result.Match(Results.Ok, errors => errors.ToProblemResult());
        })
           .Produces<SendMafAgentMessageResponse>()
           .Produces<List<Error>>(404)
           .Produces<List<Error>>(409)
           .Produces<List<Error>>(400);

        // Sent WITHOUT the request's cancellation token: once the action is claimed, a client disconnect must not
        // cancel the write or the commit of its final state (plan §0 "Tras el reclamo" 2).
        bases.MapPost("pending-actions/{id:guid}/confirm", async (ISender mediatr, Guid id) =>
        {
            var result = await mediatr.Send(new ConfirmPendingActionCommand(id));
            return result.Match(Results.Ok, errors => errors.ToProblemResult());
        })
           .Produces<PendingActionResultDto>()
           .Produces<List<Error>>(404)
           .Produces<List<Error>>(409)
           .Produces<List<Error>>(400);

        bases.MapPost("pending-actions/{id:guid}/reject", async (ISender mediatr, Guid id) =>
        {
            var result = await mediatr.Send(new RejectPendingActionCommand(id));
            return result.Match(_ => Results.NoContent(), errors => errors.ToProblemResult());
        })
           .Produces(204)
           .Produces<List<Error>>(404)
           .Produces<List<Error>>(409)
           .Produces<List<Error>>(400);

        return app;
    }
}
