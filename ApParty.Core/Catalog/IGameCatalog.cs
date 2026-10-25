using ApParty.Core.Models;

namespace ApParty.Core.Catalog;

public interface IGameCatalog
{
    IReadOnlyList<GameDefinition> Games { get; }

    GameDefinition GetGame(string gameId);

    string GetGameDirectory(string gameId);

    IReadOnlyList<Preset> GetPresets(string gameId);

    Preset GetPreset(string gameId, string presetId);
}
