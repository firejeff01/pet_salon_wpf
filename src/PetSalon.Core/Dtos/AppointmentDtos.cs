namespace PetSalon.Core.Dtos;

public sealed class AppointmentCreateInput
{
    public string OwnerId { get; set; } = string.Empty;
    public string PetId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public TimeOnly Time { get; set; }
    public string Note { get; set; } = string.Empty;
}

public sealed class AppointmentUpdateInput
{
    public DateOnly? Date { get; set; }
    public TimeOnly? Time { get; set; }
    public string? Status { get; set; }
    public string? CancelReason { get; set; }
    public string? Note { get; set; }
}

public sealed class AppointmentListFilter
{
    public DateOnly? Date { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
    public string? Status { get; set; }
    public string? PetId { get; set; }
    public string? OwnerId { get; set; }
}

public sealed record CalendarSummaryEntry(
    DateOnly Date,
    int Count,
    IReadOnlyDictionary<string, int> StatusSummary);
