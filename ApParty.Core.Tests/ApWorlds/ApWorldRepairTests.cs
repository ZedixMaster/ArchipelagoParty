using ApParty.Core.ApWorlds;
using ApParty.Core.Archipelago;
using ApParty.Core.Tests.TestSupport;

namespace ApParty.Core.Tests.ApWorlds;

public sealed class ApWorldRepairTests : IDisposable
{
    private const string PinnedContent = "world v1.0";
    private const string OldContent = "world v0.9";
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 21, 5, 0, TimeSpan.Zero);

    private readonly TempKit _kit = new();
    private readonly FakeArchipelagoInstall _fake = new();
    private readonly ArchipelagoInstall _install;

    public ApWorldRepairTests()
    {
        _install = new ArchipelagoInstall(_fake.Create("ap"), new Version(0, 6, 8));
    }

    public void Dispose()
    {
        _kit.Dispose();
        _fake.Dispose();
    }

    private string BackupRoot => Path.Combine(_install.CustomWorldsDirectory, ".apparty-backup");

    [Fact]
    public void Missing_world_is_installed_from_the_kit()
    {
        WithWorld("test");
        Directory.CreateDirectory(_install.CustomWorldsDirectory);

        var repair = Assert.Single(Repair());

        Assert.Equal(ApWorldRepairOutcome.Installed, repair.Outcome);
        Assert.Null(repair.BackupPath);
        Assert.Equal(PinnedContent, ReadInstalled("test"));
    }

    [Fact]
    public void Missing_custom_worlds_folder_is_created()
    {
        WithWorld("test");

        Assert.Equal(ApWorldRepairOutcome.Installed, Assert.Single(Repair()).Outcome);
        Assert.Equal(PinnedContent, ReadInstalled("test"));
    }

    [Fact]
    public void Wrong_version_is_backed_up_with_a_timestamp_then_replaced()
    {
        WithWorld("test");
        Install("test", OldContent);

        var repair = Assert.Single(Repair());

        var expectedBackup = Path.Combine(BackupRoot, "20261024-210500", "test.apworld");
        Assert.Equal(ApWorldRepairOutcome.Replaced, repair.Outcome);
        Assert.Equal(expectedBackup, repair.BackupPath);
        Assert.Equal(OldContent, File.ReadAllText(expectedBackup));
        Assert.Equal(PinnedContent, ReadInstalled("test"));
    }

    [Fact]
    public void World_that_is_already_ok_is_left_alone()
    {
        WithWorld("test");
        Install("test", PinnedContent);
        var installedPath = InstalledPath("test");
        var lastWrite = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(installedPath, lastWrite);

        var repair = Assert.Single(Repair());

        Assert.Equal(ApWorldRepairOutcome.AlreadyOk, repair.Outcome);
        Assert.Equal(lastWrite, File.GetLastWriteTimeUtc(installedPath));
        Assert.False(Directory.Exists(BackupRoot));
    }

    [Fact]
    public void World_with_a_kit_problem_is_not_touched()
    {
        WithWorld("test", kitContent: null);
        Install("test", OldContent);

        var repair = Assert.Single(Repair());

        Assert.Equal(ApWorldRepairOutcome.CannotRepair, repair.Outcome);
        Assert.Contains("test.apworld", repair.Problem);
        Assert.Equal(OldContent, ReadInstalled("test"));
        Assert.False(Directory.Exists(BackupRoot));
    }

    [Fact]
    public void After_repair_verify_reports_every_world_ok()
    {
        WithWorld("missing");
        WithWorld("wrong");
        Install("wrong", OldContent);

        var manager = Manager();
        manager.Repair(_install);

        Assert.All(manager.Verify(_install), check => Assert.Equal(ApWorldState.Ok, check.State));
    }

    [Fact]
    public void Backups_from_one_repair_share_one_timestamped_folder()
    {
        WithWorld("a_game");
        WithWorld("b_game");
        Install("a_game", OldContent);
        Install("b_game", OldContent);

        var repairs = Repair();

        Assert.Single(repairs.Select(r => Path.GetDirectoryName(r.BackupPath)).Distinct());
    }

    [Fact]
    public void A_second_repair_in_the_same_second_does_not_overwrite_earlier_backups()
    {
        WithWorld("test");
        Install("test", "first old version");
        var first = Assert.Single(Repair());
        Install("test", "second old version");

        var second = Assert.Single(Repair());

        Assert.NotEqual(first.BackupPath, second.BackupPath);
        Assert.Equal("first old version", File.ReadAllText(first.BackupPath!));
        Assert.Equal("second old version", File.ReadAllText(second.BackupPath!));
    }

    [Fact]
    public void Other_files_in_custom_worlds_are_untouched()
    {
        WithWorld("test");
        Install("test", OldContent);
        var otherPath = Path.Combine(_install.CustomWorldsDirectory, "someone_elses.apworld");
        File.WriteAllText(otherPath, "not ours");

        Repair();

        Assert.Equal("not ours", File.ReadAllText(otherPath));
    }

    private void WithWorld(string gameId, string? kitContent = PinnedContent) =>
        _kit.WithGame(gameId, gameId, new ApWorldSpec($"{gameId}.apworld", TempKit.Sha256(PinnedContent), kitContent));

    private string InstalledPath(string gameId) => Path.Combine(_install.CustomWorldsDirectory, $"{gameId}.apworld");

    private void Install(string gameId, string content)
    {
        Directory.CreateDirectory(_install.CustomWorldsDirectory);
        File.WriteAllText(InstalledPath(gameId), content);
    }

    private string ReadInstalled(string gameId) => File.ReadAllText(InstalledPath(gameId));

    private ApWorldManager Manager() => new(_kit.LoadCatalog(), new FixedTimeProvider(Now));

    private IReadOnlyList<ApWorldRepair> Repair() => Manager().Repair(_install);
}
