namespace ApParty.Core.Health;

public interface IEnvironmentChecker
{
    EnvironmentReport Check(IReadOnlyList<string> archipelagoSearchPaths);
}
