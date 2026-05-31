describe("nativewind animated interop", () => {
    it("registers className interop on the exported animated components", () => {
        jest.isolateModules(() => {
            const cssInterop = jest.fn((component) => component);
            const animatedView = function AnimatedView() {
                return null;
            };
            const animatedText = function AnimatedText() {
                return null;
            };
            const animatedPressable = function AnimatedPressable() {
                return null;
            };
            const createAnimatedComponent = jest
                .fn()
                .mockReturnValueOnce(animatedView)
                .mockReturnValueOnce(animatedText)
                .mockReturnValueOnce(animatedPressable);

            jest.doMock("nativewind", () => ({ cssInterop }));
            jest.doMock("react-native-reanimated", () => ({
                __esModule: true,
                default: {
                    createAnimatedComponent,
                },
            }));

            const interop = require("../nativewind-interop");

            expect(cssInterop).toHaveBeenCalledWith(interop.AnimatedView, { className: "style" });
            expect(cssInterop).toHaveBeenCalledWith(interop.AnimatedText, { className: "style" });
            expect(cssInterop).toHaveBeenCalledWith(interop.AnimatedPressable, { className: "style" });
        });
    });
});
