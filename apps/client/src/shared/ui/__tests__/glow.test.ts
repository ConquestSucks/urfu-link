import { glow } from "../glow";

describe("glow tokens", () => {
    it("keeps shared shadows restrained", () => {
        expect(glow.surface.boxShadow).toBe("0 8px 22px rgba(0, 0, 0, 0.24)");
        expect(glow.surfaceStrong.boxShadow).toBe("0 12px 32px rgba(0, 0, 0, 0.30)");
        expect(glow.brandSubtle.boxShadow).toBe("0 0 0 1px rgba(43, 127, 255, 0.12)");
        expect(glow.inputFocus.boxShadow).toBe(
            "0 0 0 1px rgba(96, 165, 250, 0.32), 0 0 0 3px rgba(43, 127, 255, 0.08)",
        );
    });

    it("avoids large colored blur halos", () => {
        Object.values(glow).forEach((style) => {
            expect(style.boxShadow).not.toMatch(/0 0 (1[5-9]|[2-9]\d)px/);
        });
    });
});
