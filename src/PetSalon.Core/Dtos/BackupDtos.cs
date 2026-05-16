namespace PetSalon.Core.Dtos;

public sealed record BackupFileInfo(string FileName, string AbsolutePath, DateTimeOffset CreatedAt, long SizeBytes);
