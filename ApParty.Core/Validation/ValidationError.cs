namespace ApParty.Core.Validation;

public sealed record ValidationError(int? PlayerIndex, string Message);
