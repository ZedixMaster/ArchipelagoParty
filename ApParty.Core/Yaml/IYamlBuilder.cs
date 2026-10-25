using ApParty.Core.Models;

namespace ApParty.Core.Yaml;

public interface IYamlBuilder
{
    string Build(PlayerSlot player);
}
