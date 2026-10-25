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
            RepairCommand(services, output),
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

    private static Option<string> ArchipelagoOption() => new("--archipelago")
    {
        Description = $"Archipelago install folder (default: {ArchipelagoLocator.DefaultInstallDirectory}).",
    };

    private static IReadOnlyList<string> SearchPaths(string? archipelagoPath) =>
        archipelagoPath is null ? ArchipelagoLocator.DefaultSearchPaths : [archipelagoPath];

    private static Command DoctorCommand(IServiceProvider services, TextWriter output)
    {
        var archipelago = ArchipelagoOption();
        var command = new Command("doctor", "Check that Archipelago and the kit's APWorlds are ready to generate.") { archipelago };

        command.SetAction(parseResult =>
        {
            var searchPaths = SearchPaths(parseResult.GetValue(archipelago));
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

    private static Command RepairCommand(IServiceProvider services, TextWriter output)
    {
        var archipelago = ArchipelagoOption();
        var command = new Command("repair", "Install or replace APWorlds in custom_worlds to match the kit's pinned versions.") { archipelago };

        command.SetAction(parseResult =>
        {
            var search = services.GetRequiredService<IArchipelagoLocator>().Locate(SearchPaths(parseResult.GetValue(archipelago)));
            if (WriteArchipelagoSection(search, output) is not { } install)
                return ExitCodes.ProblemsFound;

            var repairs = services.GetRequiredService<IApWorldManager>().Repair(install);
            if (repairs.Count > 0)
                output.WriteLine("APWorlds");

            foreach (var repair in repairs)
            {
                var file = repair.Game.ApWorldFile;
                var id = repair.Game.Id;
                switch (repair.Outcome)
                {
                    case ApWorldRepairOutcome.AlreadyOk:
                        Status(output, "[OK]", $"{id}: {file} already matches the pinned version");
                        break;
                    case ApWorldRepairOutcome.Installed:
                        Status(output, "[FIXED]", $"{id}: installed {file}");
                        break;
                    case ApWorldRepairOutcome.Replaced:
                        Status(output, "[FIXED]", $"{id}: replaced {file} (old version moved to {repair.BackupPath})");
                        break;
                    case ApWorldRepairOutcome.CannotRepair:
                        Status(output, "[FAIL]", $"{id}: {repair.Problem}");
                        break;
                }
            }

            var fixedCount = repairs.Count(r => r.Outcome is ApWorldRepairOutcome.Installed or ApWorldRepairOutcome.Replaced);
            var failedCount = repairs.Count(r => r.Outcome == ApWorldRepairOutcome.CannotRepair);

            output.WriteLine();
            output.WriteLine((fixedCount, failedCount) switch
            {
                (0, 0) => "Nothing to repair.",
                (_, 0) => $"Repaired {fixedCount} of {repairs.Count} APWorlds.",
                _ => $"{failedCount} of {repairs.Count} APWorlds could not be repaired.",
            });
            return failedCount == 0 ? ExitCodes.Success : ExitCodes.ProblemsFound;
        });

        return command;
    }

    private static void Status(TextWriter output, string marker, string message) =>
        output.WriteLine($"  {marker,-7} {message}");

    private static ArchipelagoInstall? WriteArchipelagoSection(ArchipelagoSearchResult search, TextWriter output)
    {
        output.WriteLine("Archipelago");
        if (search.Install is not { } install)
        {
            Status(output, "[FAIL]", "Archipelago not found.");
            foreach (var problem in search.Problems)
                output.WriteLine($"{"",10}{problem}");
            return null;
        }

        Status(output, "[OK]", $"Archipelago {install.Version} at {install.Directory}");
        foreach (var problem in search.Problems)
            Status(output, "[WARN]", problem);
        return install;
    }

    private static int WriteReport(EnvironmentReport report, TextWriter output)
    {
        var problems = 0;
        void Ok(string message) => Status(output, "[OK]", message);
        void Warn(string message) => Status(output, "[WARN]", message);
        void Fail(string message)
        {
            Status(output, "[FAIL]", message);
            problems++;
        }

        if (WriteArchipelagoSection(report.Archipelago, output) is not { } install)
            return 1;

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
