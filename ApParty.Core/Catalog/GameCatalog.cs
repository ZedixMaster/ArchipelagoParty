using System.Globalization;
using System.Text.Json;
using ApParty.Core.Models;

namespace ApParty.Core.Catalog;

public sealed class GameCatalog : IGameCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        RespectNullableAnnotations = true,
    };

    private readonly Dictionary<string, GameEntry> _entries;

    private GameCatalog(IEnumerable<GameEntry> entries)
    {
        _entries = entries.ToDictionary(e => e.Game.Id);
        Games = _entries.Values
            .Select(e => e.Game)
            .OrderBy(g => g.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<GameDefinition> Games { get; }

    public static GameCatalog Load(string gamesDirectory)
    {
        if (!Directory.Exists(gamesDirectory))
            throw new GameCatalogException($"Games directory not found: {gamesDirectory}");

        return new GameCatalog(Directory.EnumerateDirectories(gamesDirectory).Select(LoadGameFolder));
    }

    public GameDefinition GetGame(string gameId) => GetEntry(gameId).Game;

    public IReadOnlyList<Preset> GetPresets(string gameId) => GetEntry(gameId).Presets;

    public Preset GetPreset(string gameId, string presetId) =>
        GetPresets(gameId).FirstOrDefault(p => p.Id == presetId)
        ?? throw new KeyNotFoundException($"Game '{gameId}' has no preset '{presetId}'.");

    private GameEntry GetEntry(string gameId) =>
        _entries.TryGetValue(gameId, out var entry)
            ? entry
            : throw new KeyNotFoundException($"Unknown game '{gameId}'.");

    private static GameEntry LoadGameFolder(string gameDir)
    {
        var folderName = Path.GetFileName(gameDir);
        var game = ReadGameJson(Path.Combine(gameDir, "game.json"));

        if (game.Id != folderName)
            throw new GameCatalogException(
                $"Game folder '{folderName}' contains a game.json with id '{game.Id}'. They must match.");

        var presetsDir = Path.Combine(gameDir, "presets");
        var presets = LoadPresets(presetsDir, game.Id);

        if (presets.Count == 0)
            throw new GameCatalogException($"Game '{game.Id}' has no presets in {presetsDir}");

        return new GameEntry(game, presets);
    }

    private static GameDefinition ReadGameJson(string path)
    {
        if (!File.Exists(path))
            throw new GameCatalogException($"Missing game.json: {path}");

        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<GameDefinition>(stream, JsonOptions)
                ?? throw new GameCatalogException($"game.json is empty: {path}");
        }
        catch (JsonException ex)
        {
            throw new GameCatalogException($"Invalid game.json at {path}: {ex.Message}", ex);
        }
    }

    private static List<Preset> LoadPresets(string presetsDir, string gameId)
    {
        if (!Directory.Exists(presetsDir))
            return [];

        return Directory.EnumerateFiles(presetsDir, "*.yaml")
            .Select(path => Path.GetFileNameWithoutExtension(path))
            .Order(StringComparer.Ordinal)
            .Select(id => new Preset(id, ToDisplayName(id), gameId, Path.Combine(presetsDir, id + ".yaml")))
            .ToList();
    }

    private static string ToDisplayName(string presetId) =>
        CultureInfo.InvariantCulture.TextInfo.ToTitleCase(presetId.Replace('_', ' '));

    private sealed record GameEntry(GameDefinition Game, IReadOnlyList<Preset> Presets);
}
