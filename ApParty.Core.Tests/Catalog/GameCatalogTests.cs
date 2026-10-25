using ApParty.Core.Catalog;

namespace ApParty.Core.Tests.Catalog;

public sealed class GameCatalogTests : IDisposable
{
    private static readonly string ShippedGamesDir = Path.Combine(AppContext.BaseDirectory, "kit", "games");

    private readonly string _tempDir = Directory.CreateTempSubdirectory("apparty-tests-").FullName;

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public void Loads_every_shipped_game_sorted_by_display_name()
    {
        var catalog = GameCatalog.Load(ShippedGamesDir);

        Assert.Equal(new[] { "dk64", "twilight_princess" }, catalog.Games.Select(g => g.Id));
    }

    [Fact]
    public void Reads_game_definition_fields_from_game_json()
    {
        var tp = GameCatalog.Load(ShippedGamesDir).GetGame("twilight_princess");

        Assert.Equal("The Legend of Zelda: Twilight Princess", tp.DisplayName);
        Assert.Equal("Twilight Princess", tp.ArchipelagoGameName);
        Assert.Equal("Twilight Princess.apworld", tp.ApWorldFile);
        Assert.Equal("0.6.1", tp.MinArchipelagoVersion);
        Assert.Equal(".aptp", tp.PatchExtension);
    }

    [Fact]
    public void Reads_the_pinned_apworld_hash_from_game_json()
    {
        var tp = GameCatalog.Load(ShippedGamesDir).GetGame("twilight_princess");

        Assert.Equal("4490b9101ecdaee5a9aab88aaf88b2e71305028aa3bed35a2f1d4ab69295a8e7", tp.ApWorldSha256);
    }

    [Fact]
    public void Game_directory_is_the_folder_the_game_was_loaded_from()
    {
        var catalog = GameCatalog.Load(ShippedGamesDir);

        Assert.Equal(Path.Combine(ShippedGamesDir, "dk64"), catalog.GetGameDirectory("dk64"));
        Assert.Throws<KeyNotFoundException>(() => catalog.GetGameDirectory("nope"));
    }

    [Fact]
    public void Apworld_file_without_a_pinned_hash_fails_and_names_the_file()
    {
        WriteGame("test", """
            { "id": "test", "displayName": "Test", "archipelagoGameName": "Test",
              "apWorldFile": "test.apworld", "minArchipelagoVersion": "0.6.0" }
            """, "standard");

        var ex = Assert.Throws<GameCatalogException>(() => GameCatalog.Load(_tempDir));

        Assert.Contains(Path.Combine(_tempDir, "test", "game.json"), ex.Message);
        Assert.Contains("apWorldSha256", ex.Message);
    }

    [Theory]
    [InlineData("abc123")]
    [InlineData("zz90b9101ecdaee5a9aab88aaf88b2e71305028aa3bed35a2f1d4ab69295a8e7")]
    public void Pinned_hash_that_is_not_64_hex_characters_fails(string badHash)
    {
        WriteGame("test", $$"""
            { "id": "test", "displayName": "Test", "archipelagoGameName": "Test",
              "apWorldFile": "test.apworld", "apWorldSha256": "{{badHash}}", "minArchipelagoVersion": "0.6.0" }
            """, "standard");

        var ex = Assert.Throws<GameCatalogException>(() => GameCatalog.Load(_tempDir));

        Assert.Contains("apWorldSha256", ex.Message);
    }

    [Fact]
    public void Finds_presets_in_the_game_presets_folder()
    {
        var preset = GameCatalog.Load(ShippedGamesDir).GetPreset("twilight_princess", "streamlined");

        Assert.Equal("streamlined", preset.Id);
        Assert.Equal("Streamlined", preset.DisplayName);
        Assert.Equal("twilight_princess", preset.GameId);
        Assert.True(File.Exists(preset.FilePath));
    }

    [Fact]
    public void Preset_display_names_come_from_file_names_and_presets_are_sorted_by_id()
    {
        WriteGame("test", ValidGameJson("test"), "glitched_logic", "chaos");

        var presets = GameCatalog.Load(_tempDir).GetPresets("test");

        Assert.Equal(new[] { "chaos", "glitched_logic" }, presets.Select(p => p.Id));
        Assert.Equal(new[] { "Chaos", "Glitched Logic" }, presets.Select(p => p.DisplayName));
    }

    [Fact]
    public void Optional_fields_may_be_omitted()
    {
        WriteGame("test", ValidGameJson("test"), "standard");

        var game = GameCatalog.Load(_tempDir).GetGame("test");

        Assert.Null(game.ApWorldFile);
        Assert.Null(game.PatchExtension);
    }

    [Fact]
    public void Missing_required_field_fails_and_names_the_file()
    {
        WriteGame("test", """{ "id": "test", "displayName": "Test", "minArchipelagoVersion": "0.6.0" }""", "standard");

        var ex = Assert.Throws<GameCatalogException>(() => GameCatalog.Load(_tempDir));

        Assert.Contains(Path.Combine(_tempDir, "test", "game.json"), ex.Message);
    }

    [Fact]
    public void Malformed_json_fails_and_names_the_file()
    {
        WriteGame("test", "{ not json", "standard");

        var ex = Assert.Throws<GameCatalogException>(() => GameCatalog.Load(_tempDir));

        Assert.Contains(Path.Combine(_tempDir, "test", "game.json"), ex.Message);
    }

    [Fact]
    public void Folder_name_must_match_game_id()
    {
        WriteGame("folder_name", ValidGameJson("different_id"), "standard");

        var ex = Assert.Throws<GameCatalogException>(() => GameCatalog.Load(_tempDir));

        Assert.Contains("folder_name", ex.Message);
        Assert.Contains("different_id", ex.Message);
    }

    [Fact]
    public void Game_folder_without_game_json_fails()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "empty"));

        var ex = Assert.Throws<GameCatalogException>(() => GameCatalog.Load(_tempDir));

        Assert.Contains("empty", ex.Message);
    }

    [Fact]
    public void Game_without_presets_fails()
    {
        WriteGame("test", ValidGameJson("test"));

        var ex = Assert.Throws<GameCatalogException>(() => GameCatalog.Load(_tempDir));

        Assert.Contains("test", ex.Message);
    }

    [Fact]
    public void Missing_games_directory_fails()
    {
        var missing = Path.Combine(_tempDir, "does-not-exist");

        var ex = Assert.Throws<GameCatalogException>(() => GameCatalog.Load(missing));

        Assert.Contains(missing, ex.Message);
    }

    [Fact]
    public void Unknown_game_id_throws_key_not_found()
    {
        var catalog = GameCatalog.Load(ShippedGamesDir);

        Assert.Throws<KeyNotFoundException>(() => catalog.GetGame("nope"));
        Assert.Throws<KeyNotFoundException>(() => catalog.GetPresets("nope"));
    }

    [Fact]
    public void Unknown_preset_id_throws_key_not_found()
    {
        var catalog = GameCatalog.Load(ShippedGamesDir);

        Assert.Throws<KeyNotFoundException>(() => catalog.GetPreset("twilight_princess", "nope"));
    }

    private void WriteGame(string folder, string gameJson, params string[] presetIds)
    {
        var gameDir = Path.Combine(_tempDir, folder);
        Directory.CreateDirectory(Path.Combine(gameDir, "presets"));
        File.WriteAllText(Path.Combine(gameDir, "game.json"), gameJson);

        foreach (var presetId in presetIds)
            File.WriteAllText(Path.Combine(gameDir, "presets", presetId + ".yaml"), "name: TEMPLATE");
    }

    private static string ValidGameJson(string id) => $$"""
        {
          "id": "{{id}}",
          "displayName": "Test Game {{id}}",
          "archipelagoGameName": "Test Game {{id}}",
          "minArchipelagoVersion": "0.6.0"
        }
        """;
}
