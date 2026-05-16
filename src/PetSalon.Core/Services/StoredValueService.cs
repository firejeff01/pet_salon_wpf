using PetSalon.Core.Dtos;

namespace PetSalon.Core.Services;

public sealed class StoredValueService
{
    public StoredValueCalc Calculate(decimal balance, decimal cost)
    {
        if (balance < 0) balance = 0;
        if (cost < 0) cost = 0;
        var deduction = Math.Min(balance, cost);
        var cash = cost - deduction;
        var remaining = balance - deduction;
        return new StoredValueCalc(deduction, cash, remaining);
    }
}
