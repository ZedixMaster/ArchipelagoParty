using ApParty.Core;
using ApParty.Core.Models;
using ApParty.Core.Yaml;
using Microsoft.Extensions.DependencyInjection;

namespace ApParty.Cli.Tests;

public sealed class CliAppTests : IDisposable
{
    private static readonly string ShippedKitDir = Path.Combine(AppContext.BaseDirectory, "kit");

    private readonly ServiceProvider _services = new ServiceCollection().AddApPartyCore(ShippedKitDir).BuildServiceProvider();
    private readonly string _tempRoot = Directory.CreateTempSubdirectory("apparty-cli-").FullName;
    private readonly StringWriter _stdout = new();
    private readonly StringWriter _stderr = new();

    private string OutDir => Path.Combine(_tempRoot, "out");

    public void Dispose()
    {
        _services.Dispose();
        Directory.Delete(_tempRoot, recursive: true);
    }

    [Fact]
    public void Games_lists_every_game_with_its_presets()
    {
        var exitCode = Run("games");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("twilight_princess", _stdout.ToString());
        Assert.Contains("streamlined", _stdout.ToString());
        Assert.Contains("dk64", _stdout.ToString());
        Assert.Contains("standard", _stdout.ToString());
    }

    [Fact]
    public void Yaml_writes_a_file_named_after_the_player_and_game_and_reports_its_path()
    {
        var exitCode = RunYaml("Alice", "twilight_princess", "streamlined");

        var expectedPath = Path.Combine(OutDir, "Alice_twilight_princess.yaml");
        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.True(File.Exists(expectedPath));
        Assert.Contains(expectedPath, _stdout.ToString());
    }

    [Fact]
    public void Yaml_file_contents_are_exactly_the_yaml_builder_output()
    {
        RunYaml("Alice", "twilight_princess", "streamlined");

        var expected = _services.GetRequiredService<IYamlBuilder>()
            .Build(new PlayerSlot("Alice", "twilight_princess", "streamlined"));
        Assert.Equal(expected, File.ReadAllText(Path.Combine(OutDir, "Alice_twilight_princess.yaml")));
    }

    [Fact]
    public void Yaml_creates_nested_output_directories()
    {
        var nested = Path.Combine(OutDir, "a", "b");

        var exitCode = Run("yaml", "--name", "Alice", "--game", "twilight_princess", "--preset", "streamlined", "--out", nested);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.True(File.Exists(Path.Combine(nested, "Alice_twilight_princess.yaml")));
    }

    [Fact]
    public void Yaml_overwrites_an_existing_file()
    {
        Directory.CreateDirectory(OutDir);
        var path = Path.Combine(OutDir, "Alice_twilight_princess.yaml");
        File.WriteAllText(path, "stale");

        RunYaml("Alice", "twilight_princess", "streamlined");

        Assert.Contains("name: Alice", File.ReadAllText(path));
    }

    [Fact]
    public void Characters_invalid_in_file_names_are_replaced_in_the_file_name_only()
    {
        var exitCode = RunYaml("Carol: #1", "twilight_princess", "streamlined");

        var path = Path.Combine(OutDir, "Carol_ #1_twilight_princess.yaml");
        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.True(File.Exists(path));
        Assert.Contains("Carol: #1", File.ReadAllText(path));
    }

    [Fact]
    public void Invalid_player_reports_validation_errors_and_writes_nothing()
    {
        var exitCode = RunYaml("ABCDEFGHIJKLMNOPQ", "twilight_princess", "streamlined");

        Assert.Equal(ExitCodes.ValidationFailed, exitCode);
        Assert.Contains("16", _stderr.ToString());
        Assert.False(Directory.Exists(OutDir));
    }

    [Fact]
    public void Unknown_game_is_a_validation_error_not_a_crash()
    {
        var exitCode = RunYaml("Alice", "nope", "streamlined");

        Assert.Equal(ExitCodes.ValidationFailed, exitCode);
        Assert.Contains("Unknown game 'nope'", _stderr.ToString());
        Assert.False(Directory.Exists(OutDir));
    }

    [Fact]
    public void Missing_required_option_is_a_usage_error_naming_the_option()
    {
        var exitCode = Run("yaml", "--game", "twilight_princess", "--preset", "streamlined", "--out", OutDir);

        Assert.Equal(ExitCodes.UsageError, exitCode);
        Assert.Contains("--name", _stderr.ToString());
        Assert.False(Directory.Exists(OutDir));
    }

    [Fact]
    public void Unknown_command_is_a_usage_error()
    {
        Assert.Equal(ExitCodes.UsageError, Run("frobnicate"));
    }

    [Fact]
    public void Help_lists_the_commands_and_succeeds()
    {
        var exitCode = Run("--help");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("games", _stdout.ToString());
        Assert.Contains("yaml", _stdout.ToString());
    }

    private int Run(params string[] args) => CliApp.Run(args, _services, _stdout, _stderr);

    private int RunYaml(string name, string game, string preset) =>
        Run("yaml", "--name", name, "--game", game, "--preset", preset, "--out", OutDir);
}
