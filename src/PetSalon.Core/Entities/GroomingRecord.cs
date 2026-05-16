namespace PetSalon.Core.Entities;

public class GroomingRecord
{
    public string GroomingRecordId { get; set; } = string.Empty;
    public string AppointmentId { get; set; } = string.Empty;

    public DateOnly ServiceDate { get; set; }
    public TimeOnly ServiceTime { get; set; }

    public List<GroomingServiceItem> Services { get; set; } = new();
    public decimal TotalCost { get; set; }

    public decimal StoredValueDeduction { get; set; }
    public decimal CashPayment { get; set; }

    public PhysicalExamination PhysicalExamination { get; set; } = new();
    public List<string> Personality { get; set; } = new();
    public List<string> MedicalHistory { get; set; } = new();
    public string MedicalHistoryOther { get; set; } = string.Empty;

    public string OwnerNotes { get; set; } = string.Empty;
    public string ShopNotes { get; set; } = string.Empty;
    public string OtherNotes { get; set; } = string.Empty;

    public List<ContractVersion> ContractPaths { get; set; } = new();

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Appointment? Appointment { get; set; }
}

public class GroomingServiceItem
{
    public string Item { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class ContractVersion
{
    public int Version { get; set; }
    public string Path { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAt { get; set; }
}
