using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetSalon.Core.Entities;

namespace PetSalon.Infrastructure.Persistence.Configurations;

public sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> b)
    {
        b.ToTable("appointments");
        b.HasKey(a => a.AppointmentId);
        b.Property(a => a.AppointmentId).HasMaxLength(40);
        b.Property(a => a.OwnerId).IsRequired().HasMaxLength(40);
        b.Property(a => a.PetId).IsRequired().HasMaxLength(40);
        b.Property(a => a.Status).IsRequired().HasMaxLength(20);
        b.Property(a => a.CancelReason).HasMaxLength(500);
        b.Property(a => a.Note).HasMaxLength(500);

        b.HasOne(a => a.Owner)
            .WithMany()
            .HasForeignKey(a => a.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(a => a.Pet)
            .WithMany()
            .HasForeignKey(a => a.PetId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(a => a.GroomingRecord)
            .WithOne(g => g.Appointment)
            .HasForeignKey<GroomingRecord>(g => g.AppointmentId);

        b.HasIndex(a => a.Date);
        b.HasIndex(a => new { a.PetId, a.Date });
    }
}
