import { normalizeDroppedFile, normalizeDroppedFiles } from "../useAttachments";

describe("normalizeDroppedFile", () => {
    const createObjectURL = jest.fn(() => "blob:test-file");

    beforeEach(() => {
        createObjectURL.mockClear();
        Object.defineProperty(URL, "createObjectURL", {
            configurable: true,
            value: createObjectURL,
        });
    });

    it("maps a web File into the DocumentPickerAsset shape", () => {
        const file = {
            name: "report.pdf",
            size: 4096,
            type: "application/pdf",
            lastModified: 123,
        } as File;

        const asset = normalizeDroppedFile(file);

        expect(asset).toMatchObject({
            name: "report.pdf",
            size: 4096,
            uri: "blob:test-file",
            mimeType: "application/pdf",
            lastModified: 123,
            file,
        });
        expect(createObjectURL).toHaveBeenCalledWith(file);
    });

    it("normalizes multiple files", () => {
        const files = [
            { name: "a.txt", size: 1, type: "text/plain", lastModified: 1 },
            { name: "b.txt", size: 2, type: "text/plain", lastModified: 2 },
        ] as File[];

        expect(normalizeDroppedFiles(files)).toHaveLength(2);
        expect(createObjectURL).toHaveBeenCalledTimes(2);
    });
});
