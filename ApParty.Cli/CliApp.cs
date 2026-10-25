using System.CommandLine;
using System.CommandLine.Invocation;
using ApParty.Core.Catalog;
using ApParty.Core.Models;
using ApParty.Core.Validation;
using ApParty.Core.Yaml;
using Microsoft.Extensions.DependencyInjection;

namespace ApParty.Cli;

public static class CliApp
{
    public static int Run(string[] args, IServiceProvider services, TextWriter output, TextWriter error)
    {
        var root = new RootCommand("Archipelago Party Launcher")
        {
            GamesCommand(services, output),
            YamlCommand(services, output, error),
        };

        var parseResult = root.Parse(args);
        var configuration = new InvocationConfiguration
        {
            Output = output,
            Error = error,
            EnableDefaultExceptionHandler = false,
        };

        if (parseResult.Action is ParseErrorAction)
        {
            parseResult.Invoke(configuration);
            return ExitCodes.UsageError;
        }

        return parseResult.Invoke(configuration);
    }

    private static Command GamesCommand(IServiceProvider services, TextWriter output)
    {
        var command = new Command("games", "List the games and presets in the kit.");

        command.SetAction(_ =>
        {
            var catalog = services.GetRequiredService<IGameCatalog>();
            foreach (var game in catalog.Games)
            {
                var presets = string.Join(", ", catalog.GetPresets(game.Id).Select(p => p.Id));
                output.WriteLine($"{game.Id,-20} {game.DisplayName}  [presets: {presets}]");
            }
            return ExitCodes.Success;
        });

        return command;
    }

    private static Command YamlCommand(IServiceProvider services, TextWriter output, TextWriter error)
    {
        var name = new Option<string>("--name") { Description = "Player name (at most 16 characters).", Required = true };
        var game = new Option<string>("--game") { Description = "Game id, as listed by 'games'.", Required = true };
        var preset = new Option<string>("--preset") { Description = "Preset id for that game.", Required = true };
        var outDir = new Option<string>("--out") { Description = "Folder to write the YAML file into.", Required = true };

        var command = new Command("yaml", "Write one player's YAML from a preset.") { name, game, preset, outDir };

        command.SetAction(parseResult =>
        {
            var player = new PlayerSlot(parseResult.GetValue(name)!, parseResult.GetValue(game)!, parseResult.GetValue(preset)!);

            var errors = services.GetRequiredService<IPlayerValidator>().Validate([player]);
            if (errors.Count > 0)
            {
                foreach (var validationError in errors)
                    error.WriteLine(validationError.Message);
                return ExitCodes.ValidationFailed;
            }

            var yaml = services.GetRequiredService<IYamlBuilder>().Build(player);
            var directory = parseResult.GetValue(outDir)!;
            Directory.CreateDirectory(directory);

            var path = Path.Combine(directory, YamlFileName(player));
            File.WriteAllText(path, yaml);
            output.WriteLine($"Wrote {path}");
            return ExitCodes.Success;
        });

        return command;
    }

    private static string YamlFileName(PlayerSlot player)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var safeName = string.Concat(player.Name.Select(c => invalidChars.Contains(c) ? '_' : c));
        return $"{safeName}_{player.GameId}.yaml";
    }
}
