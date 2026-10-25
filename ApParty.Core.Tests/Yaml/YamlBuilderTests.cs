using ApParty.Core.Catalog;
using ApParty.Core.Models;
using ApParty.Core.Tests.TestSupport;
using ApParty.Core.Yaml;

namespace ApParty.Core.Tests.Yaml;

public sealed class YamlBuilderTests : IDisposable
{
    private static readonly string ShippedGamesDir = Path.Combine(AppContext.BaseDirectory, "kit", "games");
    private static readonly string GoldenDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "golden");

    private const string TestPreset = """
        name: TEMPLATE
        description: Test preset
        game: Test Game
        requires:
          version: 0.6.0
        Test Game:
          logic_rules:
            glitchless: 50
            glitched: 0
          early_shadow_crystal:
            'false': 50
            'true': 0
          exclude_locations:
            - Location A
          start_inventory:
            Shadow Crystal: 1
        """;

    private readonly TempKit _kit = new TempKit().WithGame("test", "Test Game", ("basic", TestPreset));

    public void Dispose() => _kit.Dispose();

    [Fact]
    public void Golden_twilight_princess_matches_the_yaml_played_in_the_2026_10_21_seed()
    {
        var yaml = new YamlBuilder(GameCatalog.Load(ShippedGamesDir))
            .Build(new PlayerSlot("Josh", "twilight_princess", "streamlined"));

        YamlAssert.Equivalent(ReadGolden("Josh.yaml"), yaml, ignoredTopLevelKeys: "description");
    }

    [Fact]
    public void Golden_dk64_matches_the_yaml_played_in_the_2026_10_21_seed()
    {
        var yaml = new YamlBuilder(GameCatalog.Load(ShippedGamesDir))
            .Build(new PlayerSlot("Tamas", "dk64", "standard"));

        YamlAssert.Equivalent(ReadGolden("Tamas_DK64.yaml"), yaml, ignoredTopLevelKeys: "description");
    }

    [Fact]
    public void Without_overrides_only_the_name_changes()
    {
        var yaml = BuildTestPlayer();

        YamlAssert.Equivalent(TestPreset.Replace("name: TEMPLATE", "name: Josh"), yaml);
    }

    [Fact]
    public void Name_with_yaml_special_characters_round_trips()
    {
        var yaml = new YamlBuilder(_kit.LoadCatalog()).Build(new PlayerSlot("Sam: #1 'Ace'", "test", "basic"));

        YamlAssert.Equivalent(TestPreset.Replace("name: TEMPLATE", """name: "Sam: #1 'Ace'" """), yaml);
    }

    [Theory]
    [InlineData("1337")]
    [InlineData("-5")]
    [InlineData("1:20")]
    [InlineData(".inf")]
    [InlineData("true")]
    [InlineData("Yes")]
    [InlineData("OFF")]
    [InlineData("null")]
    [InlineData("~")]
    public void Name_that_yaml_would_read_as_a_number_bool_or_null_is_quoted(string name)
    {
        var yaml = new YamlBuilder(_kit.LoadCatalog()).Build(new PlayerSlot(name, "test", "basic"));

        YamlAssert.Equivalent(TestPreset.Replace("name: TEMPLATE", $"name: '{name}'"), yaml);
    }

    [Fact]
    public void String_override_replaces_the_whole_weight_mapping()
    {
        var yaml = BuildTestPlayer(("logic_rules", "glitched"));

        YamlAssert.OptionEquivalent("glitched: 50", yaml, "Test Game", "logic_rules");
    }

    [Fact]
    public void Bool_override_becomes_a_quoted_weight_key()
    {
        var yaml = BuildTestPlayer(("early_shadow_crystal", true));

        YamlAssert.OptionEquivalent("'true': 50", yaml, "Test Game", "early_shadow_crystal");
    }

    [Fact]
    public void Int_override_becomes_a_plain_weight_key()
    {
        var yaml = BuildTestPlayer(("blocker_max", 64));

        YamlAssert.OptionEquivalent("64: 50", yaml, "Test Game", "blocker_max");
    }

    [Fact]
    public void List_override_replaces_the_list()
    {
        var yaml = BuildTestPlayer(("exclude_locations", new[] { "Location B", "Location C" }));

        YamlAssert.OptionEquivalent("""
            - Location B
            - Location C
            """, yaml, "Test Game", "exclude_locations");
    }

    [Fact]
    public void Dictionary_override_replaces_the_dictionary()
    {
        var yaml = BuildTestPlayer(("start_inventory", new Dictionary<string, int> { ["Hookshot"] = 1 }));

        YamlAssert.OptionEquivalent("Hookshot: 1", yaml, "Test Game", "start_inventory");
    }

    [Fact]
    public void Override_for_an_option_missing_from_the_preset_is_added()
    {
        var yaml = BuildTestPlayer(("death_link", false));

        YamlAssert.OptionEquivalent("'false': 50", yaml, "Test Game", "death_link");
    }

    [Fact]
    public void Overriding_one_option_leaves_the_others_untouched()
    {
        var yaml = BuildTestPlayer(("logic_rules", "glitched"));

        YamlAssert.OptionEquivalent("""
            'false': 50
            'true': 0
            """, yaml, "Test Game", "early_shadow_crystal");
        YamlAssert.OptionEquivalent("- Location A", yaml, "Test Game", "exclude_locations");
    }

    [Fact]
    public void Building_does_not_modify_the_preset_file()
    {
        var before = File.ReadAllText(_kit.PresetPath("test", "basic"));

        BuildTestPlayer(("logic_rules", "glitched"));

        Assert.Equal(before, File.ReadAllText(_kit.PresetPath("test", "basic")));
    }

    [Fact]
    public void Unsupported_override_type_fails_and_names_the_option()
    {
        var ex = Assert.Throws<ArgumentException>(() => BuildTestPlayer(("logic_rules", DateTime.Now)));

        Assert.Contains("logic_rules", ex.Message);
    }

    [Fact]
    public void Preset_for_a_different_game_fails()
    {
        using var kit = new TempKit().WithGame("test", "Test Game", ("wrong", TestPreset.Replace("game: Test Game", "game: Other Game")));

        var ex = Assert.Throws<YamlBuildException>(() =>
            new YamlBuilder(kit.LoadCatalog()).Build(new PlayerSlot("Josh", "test", "wrong")));

        Assert.Contains("Test Game", ex.Message);
        Assert.Contains("Other Game", ex.Message);
    }

    [Fact]
    public void Preset_without_a_game_section_fails()
    {
        using var kit = new TempKit().WithGame("test", "Test Game", ("bare", "name: TEMPLATE\ngame: Test Game\n"));

        var ex = Assert.Throws<YamlBuildException>(() =>
            new YamlBuilder(kit.LoadCatalog()).Build(new PlayerSlot("Josh", "test", "bare")));

        Assert.Contains("Test Game", ex.Message);
    }

    [Fact]
    public void Unknown_preset_throws_key_not_found()
    {
        Assert.Throws<KeyNotFoundException>(() =>
            new YamlBuilder(_kit.LoadCatalog()).Build(new PlayerSlot("Josh", "test", "nope")));
    }

    private string BuildTestPlayer(params (string Option, object Value)[] overrides) =>
        new YamlBuilder(_kit.LoadCatalog()).Build(new PlayerSlot("Josh", "test", "basic")
        {
            Overrides = overrides.ToDictionary(o => o.Option, o => o.Value),
        });

    private static string ReadGolden(string fileName) => File.ReadAllText(Path.Combine(GoldenDir, fileName));
}
