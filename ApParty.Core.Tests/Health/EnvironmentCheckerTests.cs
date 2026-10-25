using ApParty.Core.ApWorlds;
using ApParty.Core.Archipelago;
using ApParty.Core.Health;
using ApParty.Core.Tests.TestSupport;

namespace ApParty.Core.Tests.Health;

public sealed class EnvironmentCheckerTests : IDisposable
{
    private const string WorldContent = "world v1.0";

    private readonly TempKit _kit = new();
    private readonly FakeArchipelagoInstall _fake = new();

    public void Dispose()
    {
        _kit.Dispose();
        _fake.Dispose();
    }

    [Fact]
    public void Healthy_when_archipelago_is_found_worlds_match_and_versions_suffice()
    {
        WithWorldGame();
        var apDir = InstallArchipelago(withWorld: WorldContent);

        var report = Check(apDir);

        Assert.True(report.IsHealthy);
        Assert.Equal(apDir, report.Archipelago.Install?.Directory);
        Assert.Equal(ApWorldState.Ok, Assert.Single(report.ApWorlds).State);
        Assert.Empty(report.GamesNeedingNewerArchipelago);
    }

    [Fact]
    public void Search_paths_are_passed_to_the_locator()
    {
        WithWorldGame();
        var apDir = InstallArchipelago(withWorld: WorldContent);

        var report = Check(_fake.Path("missing"), apDir);

        Assert.Equal(apDir, report.Archipelago.Install?.Directory);
        Assert.Single(report.Archipelago.Problems);
    }

    [Fact]
    public void Archipelago_not_found_is_unhealthy_and_skips_the_install_dependent_checks()
    {
        WithWorldGame();

        var report = Check(_fake.Path("missing"));

        Assert.False(report.IsHealthy);
        Assert.Null(report.Archipelago.Install);
        Assert.NotEmpty(report.Archipelago.Problems);
        Assert.Empty(report.ApWorlds);
        Assert.Empty(report.GamesNeedingNewerArchipelago);
    }

    [Fact]
    public void World_not_installed_is_unhealthy()
    {
        WithWorldGame();
        var apDir = InstallArchipelago(withWorld: null);

        var report = Check(apDir);

        Assert.False(report.IsHealthy);
        Assert.Equal(ApWorldState.NotInstalled, Assert.Single(report.ApWorlds).State);
    }

    [Fact]
    public void World_with_the_wrong_version_is_unhealthy()
    {
        WithWorldGame();
        var apDir = InstallArchipelago(withWorld: "world v0.9");

        Assert.False(Check(apDir).IsHealthy);
    }

    [Fact]
    public void Kit_problem_alone_keeps_the_environment_healthy()
    {
        WithWorldGame(kitContent: null);
        var apDir = InstallArchipelago(withWorld: WorldContent);

        var report = Check(apDir);

        Assert.True(report.IsHealthy);
        Assert.NotNull(Assert.Single(report.ApWorlds).KitProblem);
    }

    [Fact]
    public void Game_requiring_a_newer_archipelago_is_unhealthy()
    {
        WithWorldGame();
        _kit.WithMinArchipelagoVersion("test", "0.7.0");
        var apDir = InstallArchipelago(withWorld: WorldContent);

        var report = Check(apDir);

        Assert.False(report.IsHealthy);
        Assert.Equal("test", Assert.Single(report.GamesNeedingNewerArchipelago).Id);
    }

    [Fact]
    public void Game_requiring_exactly_the_installed_version_is_fine()
    {
        WithWorldGame();
        _kit.WithMinArchipelagoVersion("test", "0.6.8");
        var apDir = InstallArchipelago(withWorld: WorldContent);

        Assert.Empty(Check(apDir).GamesNeedingNewerArchipelago);
    }

    [Fact]
    public void Version_requirement_applies_to_games_without_an_apworld_too()
    {
        _kit.WithGame("core_game", "Core Game").WithMinArchipelagoVersion("core_game", "0.7.0");
        var apDir = InstallArchipelago(withWorld: null);

        var report = Check(apDir);

        Assert.Equal("core_game", Assert.Single(report.GamesNeedingNewerArchipelago).Id);
    }

    private void WithWorldGame(string? kitContent = WorldContent) =>
        _kit.WithGame("test", "Test Game", new ApWorldSpec("test.apworld", TempKit.Sha256(WorldContent), kitContent));

    private string InstallArchipelago(string? withWorld)
    {
        var apDir = _fake.Create("ap");
        if (withWorld is not null)
        {
            Directory.CreateDirectory(Path.Combine(apDir, "custom_worlds"));
            File.WriteAllText(Path.Combine(apDir, "custom_worlds", "test.apworld"), withWorld);
        }
        return apDir;
    }

    private EnvironmentReport Check(params string[] searchPaths)
    {
        var catalog = _kit.LoadCatalog();
        return new EnvironmentChecker(new ArchipelagoLocator(), new ApWorldManager(catalog), catalog).Check(searchPaths);
    }
}
