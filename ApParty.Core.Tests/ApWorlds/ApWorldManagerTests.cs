using ApParty.Core.ApWorlds;
using ApParty.Core.Archipelago;
using ApParty.Core.Tests.TestSupport;

namespace ApParty.Core.Tests.ApWorlds;

public sealed class ApWorldManagerTests : IDisposable
{
    private const string PinnedContent = "world v1.0";
    private static readonly string PinnedHash = TempKit.Sha256(PinnedContent);

    private readonly TempKit _kit = new();
    private readonly FakeArchipelagoInstall _fake = new();
    private readonly ArchipelagoInstall _install;

    public ApWorldManagerTests()
    {
        _install = new ArchipelagoInstall(_fake.Create("ap"), new Version(0, 6, 8));
    }

    public void Dispose()
    {
        _kit.Dispose();
        _fake.Dispose();
    }

    [Fact]
    public void Installed_world_matching_the_pinned_hash_is_ok()
    {
        WithWorld(kitContent: PinnedContent);
        Install(PinnedContent);

        var check = Assert.Single(Verify());

        Assert.Equal(ApWorldState.Ok, check.State);
        Assert.Null(check.KitProblem);
        Assert.False(check.CanRepair);
    }

    [Fact]
    public void Check_reports_the_game_and_the_installed_path()
    {
        WithWorld(kitContent: PinnedContent);

        var check = Assert.Single(Verify());

        Assert.Equal("test", check.Game.Id);
        Assert.Equal(Path.Combine(_install.CustomWorldsDirectory, "test.apworld"), check.InstalledPath);
    }

    [Fact]
    public void Missing_installed_file_is_not_installed_and_repairable()
    {
        WithWorld(kitContent: PinnedContent);
        Directory.CreateDirectory(_install.CustomWorldsDirectory);

        var check = Assert.Single(Verify());

        Assert.Equal(ApWorldState.NotInstalled, check.State);
        Assert.True(check.CanRepair);
    }

    [Fact]
    public void Missing_custom_worlds_folder_is_not_installed()
    {
        WithWorld(kitContent: PinnedContent);

        var check = Assert.Single(Verify());

        Assert.Equal(ApWorldState.NotInstalled, check.State);
    }

    [Fact]
    public void Installed_file_with_a_different_hash_is_the_wrong_version_and_repairable()
    {
        WithWorld(kitContent: PinnedContent);
        Install("world v0.9");

        var check = Assert.Single(Verify());

        Assert.Equal(ApWorldState.WrongVersion, check.State);
        Assert.True(check.CanRepair);
    }

    [Fact]
    public void Hash_comparison_ignores_hex_letter_case()
    {
        WithWorld(kitContent: PinnedContent, pinnedHash: PinnedHash.ToUpperInvariant());
        Install(PinnedContent);

        Assert.Equal(ApWorldState.Ok, Assert.Single(Verify()).State);
    }

    [Fact]
    public void Missing_kit_file_is_a_kit_problem_that_blocks_repair()
    {
        WithWorld(kitContent: null);

        var check = Assert.Single(Verify());

        Assert.Equal(ApWorldState.NotInstalled, check.State);
        Assert.False(check.CanRepair);
        Assert.Contains(Path.Combine(_kit.GamesDirectory, "test", "test.apworld"), check.KitProblem);
    }

    [Fact]
    public void Kit_file_not_matching_the_pinned_hash_is_a_kit_problem()
    {
        WithWorld(kitContent: "some other version");

        var check = Assert.Single(Verify());

        Assert.False(check.CanRepair);
        Assert.Contains("test.apworld", check.KitProblem);
    }

    [Fact]
    public void Kit_problem_is_reported_even_when_the_install_is_ok()
    {
        WithWorld(kitContent: null);
        Install(PinnedContent);

        var check = Assert.Single(Verify());

        Assert.Equal(ApWorldState.Ok, check.State);
        Assert.NotNull(check.KitProblem);
    }

    [Fact]
    public void Games_without_an_apworld_are_not_checked()
    {
        _kit.WithGame("core_game", "Core Game");
        WithWorld(kitContent: PinnedContent);

        var check = Assert.Single(Verify());

        Assert.Equal("test", check.Game.Id);
    }

    [Fact]
    public void Every_game_with_an_apworld_gets_a_check_in_catalog_order()
    {
        _kit.WithGame("b_game", "B Game", new ApWorldSpec("b.apworld", PinnedHash, PinnedContent));
        _kit.WithGame("a_game", "A Game", new ApWorldSpec("a.apworld", PinnedHash, PinnedContent));

        var checks = Verify();

        Assert.Equal(new[] { "a_game", "b_game" }, checks.Select(c => c.Game.Id));
    }

    private void WithWorld(string? kitContent, string? pinnedHash = null) =>
        _kit.WithGame("test", "Test Game", new ApWorldSpec("test.apworld", pinnedHash ?? PinnedHash, kitContent));

    private void Install(string content)
    {
        Directory.CreateDirectory(_install.CustomWorldsDirectory);
        File.WriteAllText(Path.Combine(_install.CustomWorldsDirectory, "test.apworld"), content);
    }

    private IReadOnlyList<ApWorldCheck> Verify() => new ApWorldManager(_kit.LoadCatalog(), TimeProvider.System).Verify(_install);
}
