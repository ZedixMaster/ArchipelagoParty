using ApParty.Core.Catalog;
using ApParty.Core.Models;

namespace ApParty.Core.Validation;

public sealed class PlayerValidator(IGameCatalog catalog) : IPlayerValidator
{
    public const int MaxNameLength = 16;

    public IReadOnlyList<ValidationError> Validate(IReadOnlyList<PlayerSlot> players)
    {
        if (players.Count == 0)
            return [new ValidationError(null, "Add at least one player.")];

        var duplicateNames = players
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return players
            .SelectMany((player, index) => ValidatePlayer(player, duplicateNames)
                .Select(message => new ValidationError(index, message)))
            .ToList();
    }

    private IEnumerable<string> ValidatePlayer(PlayerSlot player, HashSet<string> duplicateNames)
    {
        var name = player.Name;

        if (string.IsNullOrWhiteSpace(name))
            yield return "Player name is required.";
        else if (name != name.Trim())
            yield return $"Player name '{name}' must not start or end with spaces.";

        var length = name.EnumerateRunes().Count();
        if (length > MaxNameLength)
            yield return $"Player name '{name}' is {length} characters; Archipelago allows at most {MaxNameLength}.";

        if (duplicateNames.Contains(name))
            yield return $"Another player is also named '{name}'.";

        if (!catalog.Games.Any(game => game.Id == player.GameId))
            yield return $"Unknown game '{player.GameId}'.";
        else if (!catalog.GetPresets(player.GameId).Any(preset => preset.Id == player.PresetId))
            yield return $"Game '{player.GameId}' has no preset '{player.PresetId}'.";
    }
}
