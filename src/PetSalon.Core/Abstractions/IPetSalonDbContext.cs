using Microsoft.EntityFrameworkCore;
using PetSalon.Core.Entities;

namespace PetSalon.Core.Abstractions;

public interface IPetSalonDbContext
{
    DbSet<Owner> Owners { get; }
    DbSet<Pet> Pets { get; }
    DbSet<Appointment> Appointments { get; }
    DbSet<GroomingRecord> GroomingRecords { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
