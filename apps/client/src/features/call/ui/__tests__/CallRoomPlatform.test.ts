import { isMobileBrowserDevice } from "../CallRoomPlatform";

describe("CallRoomPlatform", () => {
    it("does not treat a desktop platform as mobile when DevTools spoofs mobile signals", () => {
        expect(
            isMobileBrowserDevice({
                platform: "Win32",
                maxTouchPoints: 1,
                userAgent:
                    "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 Mobile/15E148",
                userAgentData: {
                    mobile: true,
                    platform: "iOS",
                },
            }),
        ).toBe(false);

        expect(
            isMobileBrowserDevice({
                platform: "MacIntel",
                maxTouchPoints: 1,
                userAgent:
                    "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 Chrome/125.0.0.0 Mobile Safari/537.36",
                userAgentData: {
                    mobile: true,
                    platform: "Android",
                },
            }),
        ).toBe(false);
    });

    it("detects real mobile browser platforms", () => {
        expect(
            isMobileBrowserDevice({
                platform: "iPhone",
                maxTouchPoints: 5,
                userAgent:
                    "Mozilla/5.0 (iPhone; CPU iPhone OS 17_0 like Mac OS X) AppleWebKit/605.1.15 Mobile/15E148",
            }),
        ).toBe(true);

        expect(
            isMobileBrowserDevice({
                platform: "Linux armv8l",
                maxTouchPoints: 5,
                userAgent:
                    "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 Chrome/125.0.0.0 Mobile Safari/537.36",
            }),
        ).toBe(true);
    });

    it("keeps iPadOS desktop-mode Safari switchable", () => {
        expect(
            isMobileBrowserDevice({
                platform: "MacIntel",
                maxTouchPoints: 5,
                userAgent:
                    "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15) AppleWebKit/605.1.15 Version/17.0 Safari/605.1.15",
            }),
        ).toBe(true);
    });

    it("returns false without browser device information", () => {
        expect(isMobileBrowserDevice(null)).toBe(false);
        expect(isMobileBrowserDevice(undefined)).toBe(false);
    });
});
