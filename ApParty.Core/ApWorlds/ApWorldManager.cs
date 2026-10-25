using System.Security.Cryptography;
using ApParty.Core.Archipelago;
using ApParty.Core.Catalog;
using ApParty.Core.Models;

namespace ApParty.Core.ApWorlds;

public sealed class ApWorldManager(IGameCatalog catalog) : IApWorldManager
{
    public IReadOnlyList<ApWorldCheck> Verify(ArchipelagoInstall install) =>
        catalog.Games
            .Where(game => game.ApWorldFile is not null)
            .Select(game => Check(game, install))
            .ToList();

    private ApWorldCheck Check(GameDefinition game, ArchipelagoInstall install)
    {
        var fileName = game.ApWorldFile!;
        var pinnedHash = game.ApWorldSha256!;
        var installedPath = Path.Combine(install.CustomWorldsDirectory, fileName);
        var kitPath = Path.Combine(catalog.GetGameDirectory(game.Id), fileName);

        var state = !File.Exists(installedPath) ? ApWorldState.NotInstalled
            : HashMatches(installedPath, pinnedHash) ? ApWorldState.Ok
            : ApWorldState.WrongVersion;

        var kitProblem = !File.Exists(kitPath) ? $"The kit is missing {kitPath}. Copy the pinned {fileName} there."
            : HashMatches(kitPath, pinnedHash) ? null
            : $"{kitPath} does not match the apWorldSha256 pinned for '{game.Id}'. It may be a different version.";

        return new ApWorldCheck(game, state, installedPath, kitProblem);
    }

    private static bool HashMatches(string path, string expectedSha256)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).Equals(expectedSha256, StringComparison.OrdinalIgnoreCase);
    }
}
