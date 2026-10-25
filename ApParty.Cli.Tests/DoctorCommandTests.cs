using ApParty.Core;
using ApParty.Core.Archipelago;
using ApParty.Core.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace ApParty.Cli.Tests;

public sealed class DoctorCommandTests : IDisposable
{
    private const string WorldContent = "world v1.0";

    private readonly TempKit _kit = new();
    private readonly FakeArchipelagoInstall _fake = new();
    private readonly StringWriter _stdout = new();
    private readonly StringWriter _stderr = new();

    public void Dispose()
    {
        _kit.Dispose();
        _fake.Dispose();
    }

    [Fact]
    public void Healthy_environment_reports_ok_and_exits_with_success()
    {
        WithWorldGame();
        var apDir = InstallArchipelago(withWorld: WorldContent);

        var exitCode = Doctor("--archipelago", apDir);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("[OK]", Output);
        Assert.Contains("0.6.8", Output);
        Assert.Contains(apDir, Output);
        Assert.Contains("test.apworld", Output);
        Assert.DoesNotContain("[FAIL]", Output);
        Assert.Contains("All checks passed", Output);
    }

    [Fact]
    public void Missing_archipelago_fails_and_shows_where_it_looked()
    {
        WithWorldGame();
        var missing = _fake.Path("missing");

        var exitCode = Doctor("--archipelago", missing);

        Assert.Equal(ExitCodes.ProblemsFound, exitCode);
        Assert.Contains("[FAIL]", Output);
        Assert.Contains(missing, Output);
    }

    [Fact]
    public void World_that_is_not_installed_fails_and_suggests_repair()
    {
        WithWorldGame();
        var apDir = InstallArchipelago(withWorld: null);

        var exitCode = Doctor("--archipelago", apDir);

        Assert.Equal(ExitCodes.ProblemsFound, exitCode);
        Assert.Contains("[FAIL]", Output);
        Assert.Contains("test.apworld", Output);
        Assert.Contains("repair", Output);
        Assert.Contains("1 problem", Output);
    }

    [Fact]
    public void Kit_problem_is_a_warning_not_a_failure()
    {
        WithWorldGame(kitContent: null);
        var apDir = InstallArchipelago(withWorld: WorldContent);

        var exitCode = Doctor("--archipelago", apDir);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("[WARN]", Output);
        Assert.DoesNotContain("[FAIL]", Output);
    }

    [Fact]
    public void Game_needing_a_newer_archipelago_fails_naming_the_required_version()
    {
        WithWorldGame();
        _kit.WithMinArchipelagoVersion("test", "0.7.0");
        var apDir = InstallArchipelago(withWorld: WorldContent);

        var exitCode = Doctor("--archipelago", apDir);

        Assert.Equal(ExitCodes.ProblemsFound, exitCode);
        Assert.Contains("0.7.0", Output);
    }

    [Fact]
    public void Without_the_archipelago_option_the_default_install_location_is_checked()
    {
        WithWorldGame();

        Doctor();

        Assert.Contains(ArchipelagoLocator.DefaultInstallDirectory, Output);
    }

    private string Output => _stdout.ToString();

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

    private int Doctor(params string[] options)
    {
        using var services = new ServiceCollection().AddApPartyCore(_kit.KitDirectory).BuildServiceProvider();
        return CliApp.Run(["doctor", .. options], services, _stdout, _stderr);
    }
}
