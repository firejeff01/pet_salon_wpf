using Microsoft.EntityFrameworkCore;
using PetSalon.Core.Abstractions;
using PetSalon.Core.Entities;
using PetSalon.Infrastructure.Persistence.Configurations;

namespace PetSalon.Infrastructure.Persistence;

public class PetSalonDbContext : DbContext, IPetSalonDbContext
{
    public PetSalonDbContext(DbContextOptions<PetSalonDbContext> options) : base(options) { }

    public DbSet<Owner> Owners => Set<Owner>();
    public DbSet<Pet> Pets => Set<Pet>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<GroomingRecord> GroomingRecords => Set<GroomingRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new OwnerConfiguration());
        modelBuilder.ApplyConfiguration(new PetConfiguration());
        modelBuilder.ApplyConfiguration(new AppointmentConfiguration());
        modelBuilder.ApplyConfiguration(new GroomingRecordConfiguration());
    }
}
