namespace Einsparungs.Api.DTOs;

public sealed record BackupResponse(
    string FileName,
    long SizeBytes,
    DateTime CreatedAtUtc,
    string RelativePath);
