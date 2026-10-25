using ApParty.Core.Models;

namespace ApParty.Core.Validation;

public interface IPlayerValidator
{
    IReadOnlyList<ValidationError> Validate(IReadOnlyList<PlayerSlot> players);
}
