using System.Security.Cryptography;
using System.Text;
using ApParty.Core.Catalog;
using ApParty.Core.Kit;

namespace ApParty.Core.Tests.TestSupport;

internal sealed record ApWorldSpec(string FileName, string PinnedSha256, string? KitContent);

internal sealed class TempKit : IDisposable
{
    public string KitDirectory { get; } = Directory.CreateTempSubdirectory("apparty-kit-").FullName;

    public string GamesDirectory => Path.Combine(KitDirectory, "games");

    public string ManifestPath => Path.Combine(KitDirectory, KitManifest.FileName);

    public TempKit() => WithManifest("""{ "kitVersion": "0.1.0", "archipelagoVersion": "0.6.8" }""");

    public TempKit WithManifest(string json)
    {
        File.WriteAllText(ManifestPath, json);
        return this;
    }

    public TempKit WithKitArchipelagoVersion(string version) =>
        WithManifest($$"""{ "kitVersion": "0.1.0", "archipelagoVersion": "{{version}}" }""");

    public static string Sha256(string content) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));

    public TempKit WithGame(string id, string archipelagoGameName, params (string Id, string Yaml)[] presets) =>
        WithGame(id, archipelagoGameName, apWorld: null, presets);

    public TempKit WithGame(string id, string archipelagoGameName, ApWorldSpec? apWorld, params (string Id, string Yaml)[] presets)
    {
        var gameDir = Path.Combine(GamesDirectory, id);
        Directory.CreateDirectory(Path.Combine(gameDir, "presets"));

        var apWorldJson = apWorld is null
            ? ""
            : $$"""
                "apWorldFile": "{{apWorld.FileName}}",
                "apWorldSha256": "{{apWorld.PinnedSha256}}",
              """;

        File.WriteAllText(Path.Combine(gameDir, "game.json"), $$"""
            {
              "id": "{{id}}",
              "displayName": "{{archipelagoGameName}}",
              "archipelagoGameName": "{{archipelagoGameName}}",
              {{apWorldJson}}
              "minArchipelagoVersion": "0.6.0"
            }
            """);

        if (apWorld?.KitContent is not null)
            File.WriteAllText(Path.Combine(gameDir, apWorld.FileName), apWorld.KitContent);

        foreach (var preset in presets.DefaultIfEmpty((Id: "standard", Yaml: $"name: TEMPLATE\ngame: {archipelagoGameName}\n{archipelagoGameName}: {{}}\n")))
            File.WriteAllText(Path.Combine(gameDir, "presets", preset.Id + ".yaml"), preset.Yaml);

        return this;
    }

    public TempKit WithMinArchipelagoVersion(string gameId, string version)
    {
        var gameJsonPath = Path.Combine(GamesDirectory, gameId, "game.json");
        File.WriteAllText(gameJsonPath, File.ReadAllText(gameJsonPath)
            .Replace("\"minArchipelagoVersion\": \"0.6.0\"", $"\"minArchipelagoVersion\": \"{version}\""));
        return this;
    }

    public string PresetPath(string gameId, string presetId) =>
        Path.Combine(GamesDirectory, gameId, "presets", presetId + ".yaml");

    public GameCatalog LoadCatalog() => GameCatalog.Load(GamesDirectory);

    public KitManifest LoadManifest() => KitManifest.Load(KitDirectory);

    public void Dispose() => Directory.Delete(KitDirectory, recursive: true);
}
