namespace PetSalon.Core.Enums;

public static class AppointmentStatus
{
    public const string Booked = "已預約";
    public const string Completed = "已完成";
    public const string Cancelled = "已取消";

    public static readonly IReadOnlyList<string> All = new[] { Booked, Completed, Cancelled };
    public static bool IsValid(string s) => All.Contains(s);
}
