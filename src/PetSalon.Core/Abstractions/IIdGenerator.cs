namespace PetSalon.Core.Abstractions;

public interface IIdGenerator
{
    string New(string prefix);
}
