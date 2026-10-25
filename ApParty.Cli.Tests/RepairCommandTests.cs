using ApParty.Core;
using ApParty.Core.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace ApParty.Cli.Tests;

public sealed class RepairCommandTests : IDisposable
{
    private const string WorldContent = "world v1.0";

    private readonly TempKit _kit = new();
    private readonly FakeArchipelagoInstall _fake = new();
    private readonly StringWriter _stdout = new();
    private readonly StringWriter _stderr = new();
    private readonly string _apDir;

    public RepairCommandTests()
    {
        _apDir = _fake.Create("ap");
    }

    public void Dispose()
    {
        _kit.Dispose();
        _fake.Dispose();
    }

    private string Output => _stdout.ToString();

    private string InstalledPath => Path.Combine(_apDir, "custom_worlds", "test.apworld");

    [Fact]
    public void Missing_world_is_installed_and_reported_as_fixed()
    {
        WithWorldGame();

        var exitCode = Repair("--archipelago", _apDir);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("[FIXED]", Output);
        Assert.Contains("test.apworld", Output);
        Assert.Equal(WorldContent, File.ReadAllText(InstalledPath));
    }

    [Fact]
    public void Replaced_world_reports_where_the_old_version_was_backed_up()
    {
        WithWorldGame();
        Install("world v0.9");

        var exitCode = Repair("--archipelago", _apDir);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("[FIXED]", Output);
        Assert.Contains(Path.Combine(_apDir, "custom_worlds", ".apparty-backup"), Output);
    }

    [Fact]
    public void World_that_is_already_ok_is_reported_as_ok()
    {
        WithWorldGame();
        Install(WorldContent);

        var exitCode = Repair("--archipelago", _apDir);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("[OK]", Output);
        Assert.DoesNotContain("[FIXED]", Output);
    }

    [Fact]
    public void World_with_a_kit_problem_fails_and_explains_why()
    {
        WithWorldGame(kitContent: null);

        var exitCode = Repair("--archipelago", _apDir);

        Assert.Equal(ExitCodes.ProblemsFound, exitCode);
        Assert.Contains("[FAIL]", Output);
        Assert.Contains("test.apworld", Output);
    }

    [Fact]
    public void Missing_archipelago_fails_and_shows_where_it_looked()
    {
        WithWorldGame();
        var missing = _fake.Path("missing");

        var exitCode = Repair("--archipelago", missing);

        Assert.Equal(ExitCodes.ProblemsFound, exitCode);
        Assert.Contains("[FAIL]", Output);
        Assert.Contains(missing, Output);
    }

    private void WithWorldGame(string? kitContent = WorldContent) =>
        _kit.WithGame("test", "Test Game", new ApWorldSpec("test.apworld", TempKit.Sha256(WorldContent), kitContent));

    private void Install(string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(InstalledPath)!);
        File.WriteAllText(InstalledPath, content);
    }

    private int Repair(params string[] options)
    {
        using var services = new ServiceCollection().AddApPartyCore(_kit.KitDirectory).BuildServiceProvider();
        return CliApp.Run(["repair", .. options], services, _stdout, _stderr);
    }
}
