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

export type DownloadUrlResponse = {
  downloadUrl: string;
  expiresAtUtc?: string;
};

type DownloadUrlWireResponse = {
  url?: string;
  downloadUrl?: string;
  Url?: string;
  expiresAtUtc?: string;
  ExpiresAtUtc?: string;
};

function createIdempotencyKey(): string {
  return globalThis.crypto?.randomUUID?.() ?? `${Date.now().toString(36)}-${Math.random().toString(36).slice(2)}`;
}

function idempotencyHeaders(): Record<string, string> {
  return { "Idempotency-Key": createIdempotencyKey() };
}

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
        headers: idempotencyHeaders(),
        body: JSON.stringify(dto),
      });
    },

    completeUpload(dto: CompleteUploadRequest): Promise<void> {
      return request<void>("/api/media/upload/complete", {
        method: "POST",
        headers: idempotencyHeaders(),
        body: JSON.stringify(dto),
      });
    },

    async getAssetDownloadUrl(assetId: string): Promise<DownloadUrlResponse> {
      const response = await request<DownloadUrlWireResponse>(`/api/media/${encodeURIComponent(assetId)}/download-url`);
      const downloadUrl = response.downloadUrl ?? response.url ?? response.Url;
      if (!downloadUrl) {
        throw new Error("Download URL response did not include a URL.");
      }

      return {
        downloadUrl,
        expiresAtUtc: response.expiresAtUtc ?? response.ExpiresAtUtc,
      };
    },

    getAssetMetadata(assetId: string): Promise<AssetMetadata> {
      return request<AssetMetadata>(`/api/media/${encodeURIComponent(assetId)}/metadata`);
    },

    deleteAsset(assetId: string): Promise<void> {
      return request<void>(`/api/media/${encodeURIComponent(assetId)}`, {
        method: "DELETE",
        headers: idempotencyHeaders(),
      });
    }
  };
}
