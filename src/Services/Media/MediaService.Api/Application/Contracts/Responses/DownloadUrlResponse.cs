namespace MediaService.Api.Application.Contracts.Responses;

public sealed record DownloadUrlResponse(string Url, DateTimeOffset ExpiresAtUtc);
