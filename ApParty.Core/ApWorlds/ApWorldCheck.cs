using ApParty.Core.Models;

namespace ApParty.Core.ApWorlds;

public sealed record ApWorldCheck(GameDefinition Game, ApWorldState State, string InstalledPath, string? KitProblem)
{
    public bool CanRepair => State != ApWorldState.Ok && KitProblem is null;
}
