namespace ApParty.Core.Archipelago;

public sealed record ArchipelagoInstall(string Directory, Version Version)
{
    public string GeneratorPath => Path.Combine(Directory, "ArchipelagoGenerate.exe");
    public string ServerPath => Path.Combine(Directory, "ArchipelagoServer.exe");
    public string CustomWorldsDirectory => Path.Combine(Directory, "custom_worlds");
    public string PlayerTemplatesDirectory => Path.Combine(Directory, "Players", "Templates");
}
