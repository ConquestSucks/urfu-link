import { getAvatarVariantSize, getAvatarVariantUrl } from "../avatar-url";

describe("avatar variant urls", () => {
    it("maps display sizes to generated avatar variants", () => {
        expect(getAvatarVariantSize(40)).toBe(64);
        expect(getAvatarVariantSize(80)).toBe(128);
        expect(getAvatarVariantSize(160)).toBe(256);
    });

    it("rewrites canonical variant URLs and preserves query strings", () => {
        expect(getAvatarVariantUrl("https://cdn.test/avatar-256.webp?v=1", 80)).toBe(
            "https://cdn.test/avatar-128.webp?v=1",
        );
    });

    it("does not rewrite legacy URLs", () => {
        expect(getAvatarVariantUrl("https://cdn.test/avatar.png", 40)).toBe(
            "https://cdn.test/avatar.png",
        );
    });
});
