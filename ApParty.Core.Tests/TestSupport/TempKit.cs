using ApParty.Core.Catalog;

namespace ApParty.Core.Tests.TestSupport;

internal sealed class TempKit : IDisposable
{
    public string GamesDirectory { get; } = Directory.CreateTempSubdirectory("apparty-kit-").FullName;

    public TempKit WithGame(string id, string archipelagoGameName, params (string Id, string Yaml)[] presets)
    {
        var gameDir = Path.Combine(GamesDirectory, id);
        Directory.CreateDirectory(Path.Combine(gameDir, "presets"));

        File.WriteAllText(Path.Combine(gameDir, "game.json"), $$"""
            {
              "id": "{{id}}",
              "displayName": "{{archipelagoGameName}}",
              "archipelagoGameName": "{{archipelagoGameName}}",
              "minArchipelagoVersion": "0.6.0"
            }
            """);

        foreach (var preset in presets)
            File.WriteAllText(Path.Combine(gameDir, "presets", preset.Id + ".yaml"), preset.Yaml);

        return this;
    }

    public string PresetPath(string gameId, string presetId) =>
        Path.Combine(GamesDirectory, gameId, "presets", presetId + ".yaml");

    public GameCatalog LoadCatalog() => GameCatalog.Load(GamesDirectory);

    public void Dispose() => Directory.Delete(GamesDirectory, recursive: true);
}
