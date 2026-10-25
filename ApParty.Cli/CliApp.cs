using System.CommandLine;
using System.CommandLine.Invocation;
using ApParty.Core.ApWorlds;
using ApParty.Core.Archipelago;
using ApParty.Core.Catalog;
using ApParty.Core.Health;
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
            DoctorCommand(services, output),
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

    private static Command DoctorCommand(IServiceProvider services, TextWriter output)
    {
        var archipelago = new Option<string>("--archipelago")
        {
            Description = $"Archipelago install folder (default: {ArchipelagoLocator.DefaultInstallDirectory}).",
        };

        var command = new Command("doctor", "Check that Archipelago and the kit's APWorlds are ready to generate.") { archipelago };

        command.SetAction(parseResult =>
        {
            var path = parseResult.GetValue(archipelago);
            var searchPaths = path is null ? ArchipelagoLocator.DefaultSearchPaths : [path];
            var report = services.GetRequiredService<IEnvironmentChecker>().Check(searchPaths);

            var problems = WriteReport(report, output);

            output.WriteLine();
            output.WriteLine(problems switch
            {
                0 => "All checks passed.",
                1 => "1 problem found.",
                _ => $"{problems} problems found.",
            });
            return problems == 0 ? ExitCodes.Success : ExitCodes.ProblemsFound;
        });

        return command;
    }

    private static int WriteReport(EnvironmentReport report, TextWriter output)
    {
        var problems = 0;
        void Ok(string message) => output.WriteLine($"  [OK]   {message}");
        void Warn(string message) => output.WriteLine($"  [WARN] {message}");
        void Fail(string message)
        {
            output.WriteLine($"  [FAIL] {message}");
            problems++;
        }

        output.WriteLine("Archipelago");
        if (report.Archipelago.Install is not { } install)
        {
            Fail("Archipelago not found.");
            foreach (var problem in report.Archipelago.Problems)
                output.WriteLine($"         {problem}");
            return problems;
        }

        Ok($"Archipelago {install.Version} at {install.Directory}");
        foreach (var problem in report.Archipelago.Problems)
            Warn(problem);

        if (report.ApWorlds.Count > 0)
        {
            output.WriteLine("APWorlds");
            foreach (var check in report.ApWorlds)
            {
                var file = check.Game.ApWorldFile;
                switch (check.State)
                {
                    case ApWorldState.Ok:
                        Ok($"{check.Game.Id}: {file} matches the pinned version");
                        break;
                    case ApWorldState.NotInstalled:
                        Fail($"{check.Game.Id}: {file} is not installed in {install.CustomWorldsDirectory}. Run 'repair' to install it.");
                        break;
                    case ApWorldState.WrongVersion:
                        Fail($"{check.Game.Id}: {file} is a different version than the one pinned. Run 'repair' to replace it.");
                        break;
                }

                if (check.KitProblem is not null)
                    Warn($"{check.Game.Id}: {check.KitProblem}");
            }
        }

        foreach (var game in report.GamesNeedingNewerArchipelago)
            Fail($"{game.Id}: needs Archipelago {game.MinArchipelagoVersion} or newer (installed: {install.Version}).");

        return problems;
    }

    private static string YamlFileName(PlayerSlot player)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var safeName = string.Concat(player.Name.Select(c => invalidChars.Contains(c) ? '_' : c));
        return $"{safeName}_{player.GameId}.yaml";
    }
}
