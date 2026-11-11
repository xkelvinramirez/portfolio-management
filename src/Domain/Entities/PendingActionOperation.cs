namespace Domain.Entities;

/// <summary>The eight write operations the MAF agent may propose (spec maf-tool-calling-agent, Behaviour §Escritura).</summary>
public enum PendingActionOperation
{
    CreatePortfolio,
    UpdatePortfolio,
    CreateEntry,
    UpdateEntry,
    CreateNote,
    UpdateNote,
    RefreshPrices,
    SendStatusReport,
}
