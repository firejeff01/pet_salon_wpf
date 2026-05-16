using System.ComponentModel.DataAnnotations;

namespace PetSalon.Core.Entities;

public class Pet
{
    public string PetId { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Species { get; set; } = string.Empty;           // 犬 | 貓
    public string Breed { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;             // 公 | 母
    public string Age { get; set; } = string.Empty;                // 自由格式
    public bool IsNeutered { get; set; }

    public string? ChipNumber { get; set; }
    public string? UnregisteredIdMethod { get; set; }

    public List<string> Personality { get; set; } = new();
    public PhysicalExamination PhysicalExamination { get; set; } = new();
    public List<string> MedicalHistory { get; set; } = new();
    public string MedicalHistoryOther { get; set; } = string.Empty;

    public string Note { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Owner? Owner { get; set; }
}

public class PhysicalExamination
{
    public string Eyes { get; set; } = "正常";
    public string Ears { get; set; } = "正常";
    public string Teeth { get; set; } = "正常";
    public string Limbs { get; set; } = "正常";
    public string Skin { get; set; } = "正常";
    public string Fur { get; set; } = "正常";

    [MaxLength(30)]
    public string? EyesNote { get; set; }

    [MaxLength(30)]
    public string? EarsNote { get; set; }

    [MaxLength(30)]
    public string? TeethNote { get; set; }

    [MaxLength(30)]
    public string? LimbsNote { get; set; }

    [MaxLength(30)]
    public string? SkinNote { get; set; }

    [MaxLength(30)]
    public string? FurNote { get; set; }
}
