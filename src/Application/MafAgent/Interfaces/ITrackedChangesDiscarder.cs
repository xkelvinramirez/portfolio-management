namespace Application.MafAgent.Interfaces;

/// <summary>
/// Drops every change tracked in the request's unit of work, so nothing left behind by a command that
/// threw can be saved afterwards (plan §0 "Tras el reclamo", analyze H3).
/// </summary>
public interface ITrackedChangesDiscarder
{
    void DiscardAll();
}
