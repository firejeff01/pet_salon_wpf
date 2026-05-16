namespace PetSalon.Core.Entities;

public class Appointment
{
    public string AppointmentId { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public string PetId { get; set; } = string.Empty;

    public DateOnly Date { get; set; }
    public TimeOnly Time { get; set; }

    public string Status { get; set; } = Enums.AppointmentStatus.Booked;
    public string CancelReason { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Owner? Owner { get; set; }
    public Pet? Pet { get; set; }
    public GroomingRecord? GroomingRecord { get; set; }
}
