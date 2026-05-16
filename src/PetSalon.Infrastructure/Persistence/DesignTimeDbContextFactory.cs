using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PetSalon.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PetSalonDbContext>
{
    public PetSalonDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PetSalonDbContext>()
            .UseSqlite("Data Source=design.db")
            .Options;
        return new PetSalonDbContext(options);
    }
}
