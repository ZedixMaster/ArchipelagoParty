using ApParty.Core.Kit;
using ApParty.Core.Tests.TestSupport;

namespace ApParty.Core.Tests.Kit;

public sealed class KitManifestTests : IDisposable
{
    private readonly TempKit _kit = new();

    public void Dispose() => _kit.Dispose();

    [Fact]
    public void Loads_the_kit_and_archipelago_versions()
    {
        _kit.WithManifest("""{ "kitVersion": "0.2.1", "archipelagoVersion": "0.6.8" }""");

        var manifest = _kit.LoadManifest();

        Assert.Equal(new Version(0, 2, 1), manifest.KitVersion);
        Assert.Equal(new Version(0, 6, 8), manifest.ArchipelagoVersion);
    }

    [Fact]
    public void Missing_kit_json_throws_naming_the_path()
    {
        File.Delete(_kit.ManifestPath);

        var ex = Assert.Throws<KitManifestException>(() => _kit.LoadManifest());

        Assert.Contains(_kit.ManifestPath, ex.Message);
    }

    [Fact]
    public void Malformed_json_throws_naming_the_path()
    {
        _kit.WithManifest("{ not json");

        var ex = Assert.Throws<KitManifestException>(() => _kit.LoadManifest());

        Assert.Contains(_kit.ManifestPath, ex.Message);
    }

    [Fact]
    public void Missing_archipelago_version_throws()
    {
        _kit.WithManifest("""{ "kitVersion": "0.1.0" }""");

        Assert.Throws<KitManifestException>(() => _kit.LoadManifest());
    }

    [Fact]
    public void Unparseable_version_throws()
    {
        _kit.WithManifest("""{ "kitVersion": "0.1.0", "archipelagoVersion": "latest" }""");

        Assert.Throws<KitManifestException>(() => _kit.LoadManifest());
    }
}
