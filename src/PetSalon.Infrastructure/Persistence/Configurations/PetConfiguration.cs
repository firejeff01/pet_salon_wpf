using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PetSalon.Core.Entities;

namespace PetSalon.Infrastructure.Persistence.Configurations;

public sealed class PetConfiguration : IEntityTypeConfiguration<Pet>
{
    public void Configure(EntityTypeBuilder<Pet> b)
    {
        b.ToTable("pets");
        b.HasKey(p => p.PetId);
        b.Property(p => p.PetId).HasMaxLength(40);
        b.Property(p => p.OwnerId).IsRequired().HasMaxLength(40);
        b.Property(p => p.Name).IsRequired().HasMaxLength(100);
        b.Property(p => p.Species).IsRequired().HasMaxLength(10);
        b.Property(p => p.Breed).IsRequired().HasMaxLength(100);
        b.Property(p => p.Gender).IsRequired().HasMaxLength(10);
        b.Property(p => p.Age).HasMaxLength(40);
        b.Property(p => p.ChipNumber).HasMaxLength(50);
        b.Property(p => p.UnregisteredIdMethod).HasMaxLength(100);
        b.Property(p => p.MedicalHistoryOther).HasMaxLength(500);
        b.Property(p => p.Note).HasMaxLength(1000);

        var comparer = new ValueComparer<List<string>>(
            (a, c) => a!.SequenceEqual(c!),
            a => a.Aggregate(0, (h, v) => HashCode.Combine(h, v.GetHashCode())),
            a => a.ToList());

        b.Property(p => p.Personality)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>(),
                comparer)
            .HasColumnType("TEXT");

        b.Property(p => p.MedicalHistory)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>(),
                comparer)
            .HasColumnType("TEXT");

        b.OwnsOne(p => p.PhysicalExamination, ex =>
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

        b.HasIndex(p => p.OwnerId);
        b.HasIndex(p => p.Name);
    }
}
