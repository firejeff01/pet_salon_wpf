using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetSalon.Core.Entities;

namespace PetSalon.Infrastructure.Persistence.Configurations;

public sealed class GroomingRecordConfiguration : IEntityTypeConfiguration<GroomingRecord>
{
    public void Configure(EntityTypeBuilder<GroomingRecord> b)
    {
        b.ToTable("grooming_records");
        b.HasKey(g => g.GroomingRecordId);
        b.Property(g => g.GroomingRecordId).HasMaxLength(40);
        b.Property(g => g.AppointmentId).IsRequired().HasMaxLength(40);

        var stringList = new ValueComparer<List<string>>(
            (a, c) => a!.SequenceEqual(c!),
            a => a.Aggregate(0, (h, v) => HashCode.Combine(h, v.GetHashCode())),
            a => a.ToList());

        b.Property(g => g.Services)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<GroomingServiceItem>>(v, (JsonSerializerOptions?)null) ?? new List<GroomingServiceItem>(),
                new ValueComparer<List<GroomingServiceItem>>(
                    (a, c) => a!.SequenceEqual(c!, EqualityComparer<GroomingServiceItem>.Default),
                    a => a.Aggregate(0, (h, v) => HashCode.Combine(h, v.Item.GetHashCode(), v.Price.GetHashCode())),
                    a => a.Select(x => new GroomingServiceItem { Item = x.Item, Price = x.Price }).ToList()))
            .HasColumnType("TEXT");

        b.Property(g => g.Personality)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>(),
                stringList)
            .HasColumnType("TEXT");

        b.Property(g => g.MedicalHistory)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>(),
                stringList)
            .HasColumnType("TEXT");

        b.Property(g => g.ContractPaths)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<ContractVersion>>(v, (JsonSerializerOptions?)null) ?? new List<ContractVersion>(),
                new ValueComparer<List<ContractVersion>>(
                    (a, c) => a!.SequenceEqual(c!),
                    a => a.Aggregate(0, (h, v) => HashCode.Combine(h, v.Version)),
                    a => a.Select(x => new ContractVersion { Version = x.Version, Path = x.Path, GeneratedAt = x.GeneratedAt }).ToList()))
            .HasColumnType("TEXT");

        b.Property(g => g.MedicalHistoryOther).HasMaxLength(500);
        b.Property(g => g.OwnerNotes).HasMaxLength(1000);
        b.Property(g => g.ShopNotes).HasMaxLength(1000);
        b.Property(g => g.OtherNotes).HasMaxLength(1000);
        b.Property(g => g.TotalCost).HasColumnType("decimal(12,2)");
        b.Property(g => g.StoredValueDeduction).HasColumnType("decimal(12,2)");
        b.Property(g => g.CashPayment).HasColumnType("decimal(12,2)");

        b.OwnsOne(g => g.PhysicalExamination, ex =>
        {
            ex.Property(e => e.Eyes).HasColumnName("phys_eyes").HasMaxLength(20);
            ex.Property(e => e.Ears).HasColumnName("phys_ears").HasMaxLength(20);
            ex.Property(e => e.Teeth).HasColumnName("phys_teeth").HasMaxLength(20);
            ex.Property(e => e.Limbs).HasColumnName("phys_limbs").HasMaxLength(20);
            ex.Property(e => e.Skin).HasColumnName("phys_skin").HasMaxLength(20);
            ex.Property(e => e.Fur).HasColumnName("phys_fur").HasMaxLength(20);
            ex.Property(e => e.EyesNote).HasColumnName("phys_eyes_note").HasMaxLength(30).IsRequired(false);
            ex.Property(e => e.EarsNote).HasColumnName("phys_ears_note").HasMaxLength(30).IsRequired(false);
            ex.Property(e => e.TeethNote).HasColumnName("phys_teeth_note").HasMaxLength(30).IsRequired(false);
            ex.Property(e => e.LimbsNote).HasColumnName("phys_limbs_note").HasMaxLength(30).IsRequired(false);
            ex.Property(e => e.SkinNote).HasColumnName("phys_skin_note").HasMaxLength(30).IsRequired(false);
            ex.Property(e => e.FurNote).HasColumnName("phys_fur_note").HasMaxLength(30).IsRequired(false);
        });

        b.HasIndex(g => g.AppointmentId).IsUnique();
    }
}
