using ApParty.Core.ApWorlds;
using ApParty.Core.Archipelago;
using ApParty.Core.Catalog;

namespace ApParty.Core.Health;

public sealed class EnvironmentChecker(IArchipelagoLocator locator, IApWorldManager apWorlds, IGameCatalog catalog)
    : IEnvironmentChecker
{
    public EnvironmentReport Check(IReadOnlyList<string> archipelagoSearchPaths)
    {
        var search = locator.Locate(archipelagoSearchPaths);
        if (search.Install is not { } install)
            return new EnvironmentReport(search, [], []);

        return new EnvironmentReport(
            search,
            apWorlds.Verify(install),
            catalog.Games.Where(game => game.MinArchipelagoVersion > install.Version).ToList());
    }
}
