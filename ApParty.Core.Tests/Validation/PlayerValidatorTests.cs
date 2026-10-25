using ApParty.Core.Models;
using ApParty.Core.Tests.TestSupport;
using ApParty.Core.Validation;

namespace ApParty.Core.Tests.Validation;

public sealed class PlayerValidatorTests : IDisposable
{
    private readonly TempKit _kit = new TempKit().WithGame("test", "Test Game", ("basic", "name: TEMPLATE\ngame: Test Game\nTest Game: {}\n"));

    public void Dispose() => _kit.Dispose();

    [Fact]
    public void Valid_players_produce_no_errors()
    {
        var errors = Validate(Player("Alice"), Player("Bob"));

        Assert.Empty(errors);
    }

    [Fact]
    public void Empty_player_list_is_a_session_error()
    {
        var error = Assert.Single(Validate());

        Assert.Null(error.PlayerIndex);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_name_is_an_error(string name)
    {
        var error = Assert.Single(Validate(Player(name)));

        Assert.Equal(0, error.PlayerIndex);
    }

    [Fact]
    public void Name_longer_than_16_characters_is_an_error()
    {
        var error = Assert.Single(Validate(Player("ABCDEFGHIJKLMNOPQ")));

        Assert.Equal(0, error.PlayerIndex);
        Assert.Contains("16", error.Message);
    }

    [Fact]
    public void Name_of_exactly_16_characters_is_allowed()
    {
        Assert.Empty(Validate(Player("ABCDEFGHIJKLMNOP")));
    }

    [Fact]
    public void Name_length_counts_characters_not_utf16_code_units()
    {
        var sixteenEmoji = string.Concat(Enumerable.Repeat("🎮", 16));

        Assert.Empty(Validate(Player(sixteenEmoji)));
    }

    [Theory]
    [InlineData(" Alice")]
    [InlineData("Alice ")]
    public void Name_with_leading_or_trailing_whitespace_is_an_error(string name)
    {
        var error = Assert.Single(Validate(Player(name)));

        Assert.Equal(0, error.PlayerIndex);
    }

    [Fact]
    public void Duplicate_names_are_reported_for_every_player_sharing_the_name_ignoring_case()
    {
        var errors = Validate(Player("Alice"), Player("Bob"), Player("alice"));

        Assert.Equal(new int?[] { 0, 2 }, errors.Select(e => e.PlayerIndex));
    }

    [Fact]
    public void Unknown_game_is_an_error()
    {
        var error = Assert.Single(Validate(new PlayerSlot("Alice", "missing_game", "basic")));

        Assert.Equal(0, error.PlayerIndex);
        Assert.Contains("missing_game", error.Message);
    }

    [Fact]
    public void Unknown_preset_is_an_error()
    {
        var error = Assert.Single(Validate(new PlayerSlot("Alice", "test", "missing_preset")));

        Assert.Equal(0, error.PlayerIndex);
        Assert.Contains("missing_preset", error.Message);
    }

    [Fact]
    public void Every_problem_is_reported_not_just_the_first()
    {
        var errors = Validate(
            Player("ABCDEFGHIJKLMNOPQ"),
            new PlayerSlot("Bob", "missing_game", "basic"),
            Player(""));

        Assert.Equal(new int?[] { 0, 1, 2 }, errors.Select(e => e.PlayerIndex));
    }

    private IReadOnlyList<ValidationError> Validate(params PlayerSlot[] players) =>
        new PlayerValidator(_kit.LoadCatalog()).Validate(players);

    private static PlayerSlot Player(string name) => new(name, "test", "basic");
}
