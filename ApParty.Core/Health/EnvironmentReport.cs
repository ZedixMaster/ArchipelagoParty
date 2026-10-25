using ApParty.Core.ApWorlds;
using ApParty.Core.Archipelago;
using ApParty.Core.Models;

namespace ApParty.Core.Health;

public sealed record EnvironmentReport(
    ArchipelagoSearchResult Archipelago,
    IReadOnlyList<ApWorldCheck> ApWorlds,
    IReadOnlyList<GameDefinition> GamesNeedingNewerArchipelago)
{
    public bool IsHealthy =>
        Archipelago.Install is not null
        && ApWorlds.All(check => check.State == ApWorldState.Ok)
        && GamesNeedingNewerArchipelago.Count == 0;
}
