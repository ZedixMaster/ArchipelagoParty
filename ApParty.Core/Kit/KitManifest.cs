using System.Text.Json;

namespace ApParty.Core.Kit;

public sealed record KitManifest
{
    public const string FileName = "kit.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        RespectNullableAnnotations = true,
    };

    public required Version KitVersion { get; init; }
    public required Version ArchipelagoVersion { get; init; }

    public static KitManifest Load(string kitDirectory)
    {
        var path = Path.Combine(kitDirectory, FileName);
        if (!File.Exists(path))
            throw new KitManifestException($"Missing {FileName}: {path}");

        try
        {
            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<KitManifest>(stream, JsonOptions)
                ?? throw new KitManifestException($"{FileName} is empty: {path}");
        }
        catch (JsonException ex)
        {
            throw new KitManifestException($"Invalid {FileName} at {path}: {ex.Message}", ex);
        }
    }
}
