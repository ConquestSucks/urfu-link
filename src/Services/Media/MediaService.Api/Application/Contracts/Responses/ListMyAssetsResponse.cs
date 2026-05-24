namespace MediaService.Api.Application.Contracts.Responses;

public sealed record ListMyAssetsResponse(
    IReadOnlyList<AssetMetadataResponse> Items,
    Guid? NextCursor);
