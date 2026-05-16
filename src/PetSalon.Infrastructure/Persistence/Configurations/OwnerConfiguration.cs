using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetSalon.Core.Entities;

namespace PetSalon.Infrastructure.Persistence.Configurations;

public sealed class OwnerConfiguration : IEntityTypeConfiguration<Owner>
{
    public void Configure(EntityTypeBuilder<Owner> b)
    {
        b.ToTable("owners");
        b.HasKey(o => o.OwnerId);
        b.Property(o => o.OwnerId).HasMaxLength(40);
        b.Property(o => o.Name).IsRequired().HasMaxLength(100);
        b.Property(o => o.NationalId).IsRequired().HasMaxLength(40);
        b.Property(o => o.Phone).IsRequired().HasMaxLength(40);
        b.Property(o => o.Address).IsRequired().HasMaxLength(200);
        b.Property(o => o.EmergencyContactName).HasMaxLength(100);
        b.Property(o => o.EmergencyContactPhone).HasMaxLength(40);
        b.Property(o => o.EmergencyContactRelationship).HasMaxLength(40);
        b.Property(o => o.PreferredAnimalHospitalName).HasMaxLength(100);
        b.Property(o => o.PreferredAnimalHospitalPhone).HasMaxLength(40);
        b.Property(o => o.PreferredAnimalHospitalAddress).HasMaxLength(200);
        b.Property(o => o.StoredValueBalance).HasColumnType("decimal(12,2)");
        b.Property(o => o.Note).HasMaxLength(1000);

        b.HasMany(o => o.Pets)
            .WithOne(p => p.Owner)
            .HasForeignKey(p => p.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(o => o.Name);
    }
}
