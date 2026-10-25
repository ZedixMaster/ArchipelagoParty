using System.Text.Json;

namespace ApParty.Core.Archipelago;

public sealed class ArchipelagoLocator(IReadOnlyList<string> searchPaths) : IArchipelagoLocator
{
    public static readonly string DefaultInstallDirectory = @"C:\ProgramData\Archipelago";

    private static readonly string[] RequiredExecutables = ["ArchipelagoGenerate.exe", "ArchipelagoServer.exe"];

    public ArchipelagoSearchResult Locate()
    {
        var problems = new List<string>();

        foreach (var directory in searchPaths)
        {
            var (install, problem) = Inspect(directory);
            if (install is not null)
                return new ArchipelagoSearchResult(install, problems);

            problems.Add(problem!);
        }

        return new ArchipelagoSearchResult(null, problems);
    }

    private static (ArchipelagoInstall? Install, string? Problem) Inspect(string directory)
    {
        if (!Directory.Exists(directory))
            return (null, $"No Archipelago install at {directory} (folder not found).");

        var missingExecutable = RequiredExecutables.FirstOrDefault(exe => !File.Exists(Path.Combine(directory, exe)));
        if (missingExecutable is not null)
            return (null, $"{directory} is missing {missingExecutable}.");

        var manifestPath = Path.Combine(directory, "manifest.json");
        if (!File.Exists(manifestPath))
            return (null, $"Missing Archipelago manifest: {manifestPath}");

        try
        {
            using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var version = ReadVersion(manifest.RootElement);

            return version is not null
                ? (new ArchipelagoInstall(directory, version), null)
                : (null, $"{manifestPath} has no usable version (expected e.g. \"version\": [0, 6, 8]).");
        }
        catch (JsonException ex)
        {
            return (null, $"Could not read {manifestPath}: {ex.Message}");
        }
    }

    private static Version? ReadVersion(JsonElement manifest)
    {
        if (manifest.ValueKind != JsonValueKind.Object
            || !manifest.TryGetProperty("version", out var versionArray)
            || versionArray.ValueKind != JsonValueKind.Array)
            return null;

        var parts = new List<int>();
        foreach (var part in versionArray.EnumerateArray())
        {
            if (part.ValueKind != JsonValueKind.Number || !part.TryGetInt32(out var number) || number < 0)
                return null;
            parts.Add(number);
        }

        return parts.Count switch
        {
            2 => new Version(parts[0], parts[1]),
            3 => new Version(parts[0], parts[1], parts[2]),
            4 => new Version(parts[0], parts[1], parts[2], parts[3]),
            _ => null,
        };
    }
}
