describe("reanimated web shim", () => {
    it.each(["FadeIn", "FadeInDown", "FadeInRight", "FadeOut", "LinearTransition", "ZoomIn", "ZoomOut"])(
        "exports a chainable %s animation builder",
        (builderName) => {
            const shim = require("../reanimated-web-shim");
            const builder = shim[builderName];

            expect(builder).toBeDefined();
            expect(builder.duration(180)).toBe(builder);
            expect(builder.delay(50)).toBe(builder);
        },
    );
});
