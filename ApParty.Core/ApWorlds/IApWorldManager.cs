using ApParty.Core.Archipelago;

namespace ApParty.Core.ApWorlds;

public interface IApWorldManager
{
    IReadOnlyList<ApWorldCheck> Verify(ArchipelagoInstall install);

    IReadOnlyList<ApWorldRepair> Repair(ArchipelagoInstall install);
}
