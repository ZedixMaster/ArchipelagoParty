using ApParty.Core.Models;

namespace ApParty.Core.ApWorlds;

public sealed record ApWorldRepair(GameDefinition Game, ApWorldRepairOutcome Outcome, string? BackupPath, string? Problem);
