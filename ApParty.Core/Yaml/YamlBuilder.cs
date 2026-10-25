using System.Globalization;
using ApParty.Core.Catalog;
using ApParty.Core.Models;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace ApParty.Core.Yaml;

public sealed class YamlBuilder(IGameCatalog catalog) : IYamlBuilder
{
    private const string SelectedWeight = "50";

    private static readonly HashSet<string> Yaml11BoolAndNullWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "true", "false", "yes", "no", "on", "off", "y", "n", "null",
    };

    public string Build(PlayerSlot player)
    {
        var game = catalog.GetGame(player.GameId);
        var preset = catalog.GetPreset(player.GameId, player.PresetId);
        var document = LoadPreset(preset.FilePath);

        var presetGame = (document.Children.TryGetValue("game", out var gameNode) ? gameNode as YamlScalarNode : null)?.Value;
        if (presetGame != game.ArchipelagoGameName)
            throw new YamlBuildException(
                $"Preset '{preset.Id}' is for game '{presetGame}', but '{game.Id}' expects '{game.ArchipelagoGameName}'.");

        if (!document.Children.TryGetValue(game.ArchipelagoGameName, out var section) || section is not YamlMappingNode options)
            throw new YamlBuildException(
                $"Preset '{preset.Id}' has no '{game.ArchipelagoGameName}' options section: {preset.FilePath}");

        document.Children["name"] = NameNode(player.Name);

        foreach (var (option, value) in player.Overrides)
            options.Children[option] = ToOptionNode(option, value);

        return Serialize(document);
    }

    private static YamlScalarNode NameNode(string name) =>
        name.Length > 0 && char.IsLetter(name[0]) && !Yaml11BoolAndNullWords.Contains(name)
            ? new YamlScalarNode(name)
            : new YamlScalarNode(name) { Style = ScalarStyle.SingleQuoted };

    private static YamlNode ToOptionNode(string option, object value) => value switch
    {
        string choice => Selected(new YamlScalarNode(choice)),
        bool toggle => Selected(new YamlScalarNode(toggle ? "true" : "false") { Style = ScalarStyle.SingleQuoted }),
        int number => Selected(Number(number)),
        IReadOnlyDictionary<string, int> counts => new YamlMappingNode(
            counts.Select(kv => new KeyValuePair<YamlNode, YamlNode>(new YamlScalarNode(kv.Key), Number(kv.Value)))),
        IEnumerable<string> items => new YamlSequenceNode(items.Select(item => new YamlScalarNode(item))),
        _ => throw new ArgumentException(
            $"Override for option '{option}' has unsupported type {value.GetType().Name}.", nameof(value)),
    };

    private static YamlMappingNode Selected(YamlScalarNode value) =>
        new(value, new YamlScalarNode(SelectedWeight));

    private static YamlScalarNode Number(int value) =>
        new(value.ToString(CultureInfo.InvariantCulture));

    private static YamlMappingNode LoadPreset(string path)
    {
        var stream = new YamlStream();
        try
        {
            using var reader = File.OpenText(path);
            stream.Load(reader);
        }
        catch (YamlException ex)
        {
            throw new YamlBuildException($"Invalid YAML in preset {path}: {ex.Message}", ex);
        }

        return stream.Documents.FirstOrDefault()?.RootNode as YamlMappingNode
            ?? throw new YamlBuildException($"Preset is not a YAML mapping: {path}");
    }

    private static string Serialize(YamlMappingNode document)
    {
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        new YamlStream(new YamlDocument(document)).Save(writer, assignAnchors: false);
        return writer.ToString();
    }
}
