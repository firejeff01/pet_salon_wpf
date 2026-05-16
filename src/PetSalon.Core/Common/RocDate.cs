namespace PetSalon.Core.Common;

public static class RocDate
{
    public static string ToRocString(DateOnly date)
        => $"民國 {date.Year - 1911} 年 {date.Month} 月 {date.Day} 日";

    public static string ToRocString(DateTimeOffset dt)
        => ToRocString(DateOnly.FromDateTime(dt.LocalDateTime));

    public static string ToRocCompact(DateOnly date)
        => $"{date.Year - 1911}/{date.Month:D2}/{date.Day:D2}";
}
