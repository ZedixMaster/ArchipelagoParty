namespace ApParty.Core.Kit;

public sealed class KitManifestException(string message, Exception? inner = null) : Exception(message, inner);
