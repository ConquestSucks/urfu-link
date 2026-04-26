import { AuthHeaders, HandleUnauthorized, createRequest } from "./utils";

export type Visibility = "Private" | "Public";

export type InitUploadRequest = {
  fileName: string;
  size: number;
  mimeType: string;
  visibility: Visibility;
};

export type InitUploadResponse = {
  assetId: string;
  presignedPutUrl: string;
  expiresAt: string;
  bucket: string;
  objectKey: string;
};

export type CompleteUploadRequest = {
  assetId: string;
  checksum?: string;
};

export type AssetMetadata = {
  id: string;
  ownerId: string;
  visibility: Visibility;
  bucket: string;
  objectKey: string;
  size: number;
  mimeType: string;
  originalFileName: string;
  state: "Initiated" | "Uploaded" | "Failed" | "Deleted";
  createdAt: string;
};

export function createMediaApi(
  baseUrl: string,
  authHeaders: AuthHeaders,
  handleUnauthorized: HandleUnauthorized
) {
  const request = createRequest(baseUrl, authHeaders, handleUnauthorized);

  return {
    initUpload(dto: InitUploadRequest): Promise<InitUploadResponse> {
      return request<InitUploadResponse>("/api/media/upload/init", {
        method: "POST",
        body: JSON.stringify(dto),
      });
    },

    completeUpload(dto: CompleteUploadRequest): Promise<void> {
      return request<void>("/api/media/upload/complete", {
        method: "POST",
        body: JSON.stringify(dto),
      });
    },

    getAssetDownloadUrl(assetId: string): Promise<{ downloadUrl: string }> {
      return request<{ downloadUrl: string }>(`/api/media/${encodeURIComponent(assetId)}/download-url`);
    },

    getAssetMetadata(assetId: string): Promise<AssetMetadata> {
      return request<AssetMetadata>(`/api/media/${encodeURIComponent(assetId)}/metadata`);
    },

    deleteAsset(assetId: string): Promise<void> {
      return request<void>(`/api/media/${encodeURIComponent(assetId)}`, {
        method: "DELETE",
      });
    }
  };
}
