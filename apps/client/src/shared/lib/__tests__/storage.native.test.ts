import { Platform } from "react-native";

const mockGetString = jest.fn();
const mockRemove = jest.fn();
const mockSet = jest.fn();

jest.mock("react-native-mmkv", () => ({
    createMMKV: jest.fn(() => ({
        getString: mockGetString,
        remove: mockRemove,
        set: mockSet,
    })),
}));

describe("appStorage native adapter", () => {
    const originalPlatformOS = Platform.OS;

    beforeEach(() => {
        jest.resetModules();
        jest.clearAllMocks();
        Object.defineProperty(Platform, "OS", { configurable: true, value: "android" });
    });

    afterAll(() => {
        Object.defineProperty(Platform, "OS", { configurable: true, value: originalPlatformOS });
    });

    it("removes native MMKV values with the current API", () => {
        const { appStorage } = require("../storage") as typeof import("../storage");

        appStorage.removeItem("pending-auth");

        expect(mockRemove).toHaveBeenCalledWith("pending-auth");
    });
});
