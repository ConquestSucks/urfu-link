import type { DocumentPickerAsset } from "expo-document-picker";
import { apiClient } from "@/shared/lib/api";
import type { VoiceRecordingDraft } from "@/features/voice-message";
import type { InitUploadRequest } from "@urfu-link/api-client";

type UploadBody = {
    body: BodyInit;
    fileName: string;
    size: number;
    mimeType: string;
};

const fetchBlob = async (uri: string) => {
    const response = await fetch(uri);
    if (!response.ok) {
        throw new Error(`Failed to read local upload body: HTTP ${response.status}`);
    }
    return response.blob();
};

const getDocumentUploadBody = async (file: DocumentPickerAsset): Promise<UploadBody> => {
    if (file.file) {
        return {
            body: file.file,
            fileName: file.name,
            size: file.size ?? file.file.size,
            mimeType: file.mimeType ?? file.file.type ?? "application/octet-stream",
        };
    }

    const blob = await fetchBlob(file.uri);
    return {
        body: blob,
        fileName: file.name,
        size: file.size ?? blob.size,
        mimeType: file.mimeType ?? (blob.type || "application/octet-stream"),
    };
};

const getVoiceUploadBody = async (draft: VoiceRecordingDraft): Promise<UploadBody> => {
    const blob = await fetchBlob(draft.uri);
    return {
        body: blob,
        fileName: draft.fileName,
        size: blob.size,
        mimeType: draft.mimeType || blob.type || "audio/m4a",
    };
};

const uploadBody = async (
    upload: UploadBody,
    options: {
        requestedKind?: "Voice";
        durationSeconds?: number;
    } = {},
) => {
    const initRequest: InitUploadRequest = {
        fileName: upload.fileName,
        size: upload.size,
        mimeType: upload.mimeType,
        visibility: "Private",
    };
    if (options.requestedKind) initRequest.requestedKind = options.requestedKind;
    if (options.durationSeconds !== undefined) {
        initRequest.durationSeconds = options.durationSeconds;
    }

    const initRes = await apiClient.media.initUpload(initRequest);

    await fetch(initRes.presignedPutUrl, {
        method: "PUT",
        body: upload.body,
    });

    await apiClient.media.completeUpload({ assetId: initRes.assetId });
    return initRes.assetId;
};

export const uploadChatAttachments = async (
    files: DocumentPickerAsset[],
    voiceDraft?: VoiceRecordingDraft | null,
) => {
    const assetIds: string[] = [];

    for (const file of files) {
        assetIds.push(await uploadBody(await getDocumentUploadBody(file)));
    }

    if (voiceDraft) {
        assetIds.push(
            await uploadBody(await getVoiceUploadBody(voiceDraft), {
                requestedKind: "Voice",
                durationSeconds: voiceDraft.durationSeconds,
            }),
        );
    }

    return assetIds;
};
