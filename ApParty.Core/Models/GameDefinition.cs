namespace ApParty.Core.Models;

public sealed record GameDefinition
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string ArchipelagoGameName { get; init; }
    public string? ApWorldFile { get; init; }
    public required string MinArchipelagoVersion { get; init; }
    public string? PatchExtension { get; init; }
}
