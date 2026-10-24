namespace ApParty.Core.Catalog;

public sealed class GameCatalogException(string message, Exception? innerException = null)
    : Exception(message, innerException);
