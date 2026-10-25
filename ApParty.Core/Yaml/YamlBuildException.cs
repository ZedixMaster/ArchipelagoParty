namespace ApParty.Core.Yaml;

public sealed class YamlBuildException(string message, Exception? innerException = null)
    : Exception(message, innerException);
