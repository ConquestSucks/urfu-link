describe("registerLiveKitGlobals web", () => {
    it("does not import the native LiveKit bridge on web", () => {
        jest.doMock("@livekit/react-native", () => {
            throw new Error("native LiveKit bridge must not be imported on web");
        });

        const { registerLiveKitGlobals } = require("../registerLiveKitGlobals.web");

        expect(() => registerLiveKitGlobals()).not.toThrow();
    });
});
