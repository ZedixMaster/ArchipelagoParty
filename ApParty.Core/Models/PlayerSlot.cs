namespace ApParty.Core.Models;

public sealed record PlayerSlot(string Name, string GameId, string PresetId)
{
    public IReadOnlyDictionary<string, object> Overrides { get; init; } = new Dictionary<string, object>();
}
