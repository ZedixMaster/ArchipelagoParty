using System.Globalization;
using System.Security.Cryptography;
using ApParty.Core.Archipelago;
using ApParty.Core.Catalog;
using ApParty.Core.Models;

namespace ApParty.Core.ApWorlds;

public sealed class ApWorldManager(IGameCatalog catalog, TimeProvider clock) : IApWorldManager
{
    public const string BackupFolderName = ".apparty-backup";

    public IReadOnlyList<ApWorldRepair> Repair(ArchipelagoInstall install)
    {
        string? backupDirectory = null;
        string BackupDirectory() => backupDirectory ??= CreateBackupDirectory(install);

        return Verify(install).Select(check => Fix(check, BackupDirectory)).ToList();
    }

    public IReadOnlyList<ApWorldCheck> Verify(ArchipelagoInstall install) =>
        catalog.Games
            .Where(game => game.ApWorldFile is not null)
            .Select(game => Check(game, install))
            .ToList();

    private ApWorldRepair Fix(ApWorldCheck check, Func<string> backupDirectory)
    {
        if (check.State == ApWorldState.Ok)
            return new ApWorldRepair(check.Game, ApWorldRepairOutcome.AlreadyOk, null, null);

        if (check.KitProblem is not null)
            return new ApWorldRepair(check.Game, ApWorldRepairOutcome.CannotRepair, null, check.KitProblem);

        Directory.CreateDirectory(Path.GetDirectoryName(check.InstalledPath)!);

        string? backupPath = null;
        if (check.State == ApWorldState.WrongVersion)
        {
            backupPath = Path.Combine(backupDirectory(), Path.GetFileName(check.InstalledPath));
            File.Move(check.InstalledPath, backupPath);
        }

        File.Copy(KitPath(check.Game), check.InstalledPath);

        var outcome = backupPath is null ? ApWorldRepairOutcome.Installed : ApWorldRepairOutcome.Replaced;
        return new ApWorldRepair(check.Game, outcome, backupPath, null);
    }

    private string CreateBackupDirectory(ArchipelagoInstall install)
    {
        var root = Path.Combine(install.CustomWorldsDirectory, BackupFolderName);
        var stamp = clock.GetLocalNow().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);

        var path = Path.Combine(root, stamp);
        for (var n = 2; Directory.Exists(path); n++)
            path = Path.Combine(root, $"{stamp}-{n}");

        Directory.CreateDirectory(path);
        return path;
    }

    private string KitPath(GameDefinition game) => Path.Combine(catalog.GetGameDirectory(game.Id), game.ApWorldFile!);

    private ApWorldCheck Check(GameDefinition game, ArchipelagoInstall install)
    {
        var fileName = game.ApWorldFile!;
        var pinnedHash = game.ApWorldSha256!;
        var installedPath = Path.Combine(install.CustomWorldsDirectory, fileName);
        var kitPath = KitPath(game);

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
