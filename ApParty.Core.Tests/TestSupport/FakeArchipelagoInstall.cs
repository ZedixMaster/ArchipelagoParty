namespace ApParty.Core.Tests.TestSupport;

internal sealed class FakeArchipelagoInstall : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("apparty-ap-").FullName;

    public string Path(string name) => System.IO.Path.Combine(_root, name);

    public string Create(string name, string? manifestJson = """{ "version": [0, 6, 8] }""", bool withGenerator = true, bool withServer = true)
    {
        var dir = Path(name);
        Directory.CreateDirectory(dir);

        if (manifestJson is not null)
            File.WriteAllText(System.IO.Path.Combine(dir, "manifest.json"), manifestJson);
        if (withGenerator)
            File.WriteAllText(System.IO.Path.Combine(dir, "ArchipelagoGenerate.exe"), "");
        if (withServer)
            File.WriteAllText(System.IO.Path.Combine(dir, "ArchipelagoServer.exe"), "");

        return dir;
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
