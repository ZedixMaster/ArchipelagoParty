namespace ApParty.Core.Archipelago;

public interface IArchipelagoLocator
{
    ArchipelagoSearchResult Locate(IReadOnlyList<string> searchPaths);
}
