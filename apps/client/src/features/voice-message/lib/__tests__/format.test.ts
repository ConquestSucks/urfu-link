import { getVoiceFileName } from "../format";

describe("voice message format helpers", () => {
    beforeEach(() => {
        jest.spyOn(Date, "now").mockReturnValue(123);
    });

    afterEach(() => {
        jest.restoreAllMocks();
    });

    it.each([
        ["audio/webm;codecs=opus", "voice-123.webm"],
        ["audio/ogg;codecs=opus", "voice-123.ogg"],
    ])("uses the base media type for recorder MIME parameters", (mimeType, expected) => {
        expect(getVoiceFileName(mimeType)).toBe(expected);
    });
});
