namespace ApParty.Core.Archipelago;

public sealed record ArchipelagoSearchResult(ArchipelagoInstall? Install, IReadOnlyList<string> Problems);
