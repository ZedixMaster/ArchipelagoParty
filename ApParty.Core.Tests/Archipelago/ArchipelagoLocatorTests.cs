using ApParty.Core.Archipelago;
using ApParty.Core.Tests.TestSupport;

namespace ApParty.Core.Tests.Archipelago;

public sealed class ArchipelagoLocatorTests : IDisposable
{
    private readonly FakeArchipelagoInstall _fake = new();

    public void Dispose() => _fake.Dispose();

    [Fact]
    public void Finds_an_install_and_reads_its_version_from_manifest_json()
    {
        var dir = _fake.Create("ap");

        var result = Locate(dir);

        Assert.NotNull(result.Install);
        Assert.Equal(dir, result.Install.Directory);
        Assert.Equal(new Version(0, 6, 8), result.Install.Version);
    }

    [Fact]
    public void Install_exposes_the_paths_the_launcher_needs()
    {
        var install = new ArchipelagoInstall(@"C:\AP", new Version(0, 6, 8));

        Assert.Equal(@"C:\AP\ArchipelagoGenerate.exe", install.GeneratorPath);
        Assert.Equal(@"C:\AP\ArchipelagoServer.exe", install.ServerPath);
        Assert.Equal(@"C:\AP\custom_worlds", install.CustomWorldsDirectory);
        Assert.Equal(@"C:\AP\Players\Templates", install.PlayerTemplatesDirectory);
    }

    [Fact]
    public void Search_paths_are_tried_in_order_and_the_first_valid_install_wins()
    {
        var first = _fake.Create("first", """{ "version": [0, 6, 8] }""");
        var second = _fake.Create("second", """{ "version": [0, 7, 0] }""");

        var result = Locate(_fake.Path("missing"), first, second);

        Assert.Equal(first, result.Install?.Directory);
    }

    [Fact]
    public void Missing_directory_is_reported_and_the_search_continues()
    {
        var missing = _fake.Path("missing");
        var valid = _fake.Create("ap");

        var result = Locate(missing, valid);

        Assert.Equal(valid, result.Install?.Directory);
        Assert.Contains(result.Problems, p => p.Contains(missing));
    }

    [Fact]
    public void No_valid_install_returns_null_with_a_problem_for_every_path_tried()
    {
        var a = _fake.Path("a");
        var b = _fake.Path("b");

        var result = Locate(a, b);

        Assert.Null(result.Install);
        Assert.Equal(2, result.Problems.Count);
        Assert.Contains(a, result.Problems[0]);
        Assert.Contains(b, result.Problems[1]);
    }

    [Fact]
    public void Directory_without_manifest_is_rejected_naming_the_manifest()
    {
        var dir = _fake.Create("ap", manifestJson: null);

        var result = Locate(dir);

        Assert.Null(result.Install);
        Assert.Contains(Path.Combine(dir, "manifest.json"), Assert.Single(result.Problems));
    }

    [Fact]
    public void Malformed_manifest_is_rejected_naming_the_manifest()
    {
        var dir = _fake.Create("ap", manifestJson: "{ not json");

        var result = Locate(dir);

        Assert.Null(result.Install);
        Assert.Contains(Path.Combine(dir, "manifest.json"), Assert.Single(result.Problems));
    }

    [Theory]
    [InlineData("""{ "version": "0.6.8" }""")]
    [InlineData("""{ "version": [0] }""")]
    [InlineData("""{ "version": [0, 6, 8, 1, 2] }""")]
    [InlineData("""{ "version": [0, -6, 8] }""")]
    [InlineData("""{ "buildtime": "2026-10-04" }""")]
    public void Manifest_without_a_usable_version_is_rejected(string manifestJson)
    {
        var dir = _fake.Create("ap", manifestJson);

        var result = Locate(dir);

        Assert.Null(result.Install);
        Assert.Contains("version", Assert.Single(result.Problems));
    }

    [Theory]
    [InlineData(false, true, "ArchipelagoGenerate.exe")]
    [InlineData(true, false, "ArchipelagoServer.exe")]
    public void Directory_missing_a_required_executable_is_rejected_naming_it(bool withGenerator, bool withServer, string missingExe)
    {
        var dir = _fake.Create("ap", withGenerator: withGenerator, withServer: withServer);

        var result = Locate(dir);

        Assert.Null(result.Install);
        Assert.Contains(missingExe, Assert.Single(result.Problems));
    }

    [Fact]
    public void Problems_from_rejected_paths_are_kept_even_when_a_later_path_succeeds()
    {
        var broken = _fake.Create("broken", manifestJson: null);
        var valid = _fake.Create("valid");

        var result = Locate(broken, valid);

        Assert.Equal(valid, result.Install?.Directory);
        Assert.Contains(broken, Assert.Single(result.Problems));
    }

    [Fact]
    public void Default_install_directory_is_where_the_archipelago_installer_puts_it()
    {
        Assert.Equal(@"C:\ProgramData\Archipelago", ArchipelagoLocator.DefaultInstallDirectory);
    }

    private static ArchipelagoSearchResult Locate(params string[] searchPaths) =>
        new ArchipelagoLocator(searchPaths).Locate();
}
