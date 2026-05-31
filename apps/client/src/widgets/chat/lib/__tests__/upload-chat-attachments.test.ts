import type { DocumentPickerAsset } from "expo-document-picker";
import { apiClient } from "@/shared/lib/api";
import { uploadChatAttachments } from "../upload-chat-attachments";

jest.mock("@/shared/lib/api", () => ({
    apiClient: {
        media: {
            initUpload: jest.fn(),
            completeUpload: jest.fn(),
        },
    },
}));

describe("uploadChatAttachments", () => {
    beforeEach(() => {
        jest.clearAllMocks();
        (globalThis.fetch as jest.Mock | undefined)?.mockRestore?.();
        jest.spyOn(globalThis, "fetch").mockImplementation(jest.fn());
        (apiClient.media.initUpload as jest.Mock).mockResolvedValue({
            assetId: "asset-1",
            presignedPutUrl: "https://storage.example/upload",
            expiresAt: "2026-05-24T10:00:00.000Z",
            bucket: "media-private",
            objectKey: "owner/asset/file",
        });
        (apiClient.media.completeUpload as jest.Mock).mockResolvedValue(undefined);
    });

    afterEach(() => {
        (globalThis.fetch as jest.Mock).mockRestore();
    });

    it("uploads voice drafts with explicit voice kind and duration", async () => {
        const blob = { size: 2048, type: "audio/m4a" } as Blob;
        (globalThis.fetch as jest.Mock)
            .mockResolvedValueOnce({
                ok: true,
                blob: jest.fn(async () => blob),
            })
            .mockResolvedValueOnce({ ok: true });

        const result = await uploadChatAttachments([], {
            uri: "file://voice.m4a",
            fileName: "voice.m4a",
            mimeType: "audio/m4a",
            durationSeconds: 17,
        });

        expect(result).toEqual(["asset-1"]);
        expect(apiClient.media.initUpload).toHaveBeenCalledWith(
            expect.objectContaining({
                fileName: "voice.m4a",
                size: 2048,
                mimeType: "audio/m4a",
                visibility: "Private",
                requestedKind: "Voice",
                durationSeconds: 17,
            }),
        );
        expect(globalThis.fetch).toHaveBeenLastCalledWith(
            "https://storage.example/upload",
            expect.objectContaining({ method: "PUT", body: blob }),
        );
    });

    it("keeps generic file uploads on the default media classifier path", async () => {
        const blob = { size: 4096, type: "application/pdf" } as Blob;
        (globalThis.fetch as jest.Mock)
            .mockResolvedValueOnce({
                ok: true,
                blob: jest.fn(async () => blob),
            })
            .mockResolvedValueOnce({ ok: true });
        const file: DocumentPickerAsset = {
            name: "report.pdf",
            uri: "file://report.pdf",
            size: 4096,
            mimeType: "application/pdf",
            lastModified: 1,
        };

        await uploadChatAttachments([file], null);

        const initRequest = (apiClient.media.initUpload as jest.Mock).mock.calls[0][0];
        expect(initRequest).toEqual(
            expect.objectContaining({
                fileName: "report.pdf",
                size: 4096,
                mimeType: "application/pdf",
                visibility: "Private",
            }),
        );
        expect(initRequest).not.toHaveProperty("requestedKind");
        expect(initRequest).not.toHaveProperty("durationSeconds");
    });
});
