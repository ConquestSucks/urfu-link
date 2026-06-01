type NavigatorUserAgentData = {
    mobile?: boolean;
    platform?: string;
};

export type BrowserDeviceInfo = {
    userAgent?: string;
    platform?: string;
    maxTouchPoints?: number;
    userAgentData?: NavigatorUserAgentData;
};

const DESKTOP_PLATFORM_PATTERN = /^(MacIntel|MacPPC|Mac68K|Win32|Win64|Windows|Linux x86_64|Linux i686|X11)$/i;
const IOS_PLATFORM_PATTERN = /^(iPhone|iPad|iPod)$/i;
const MOBILE_UA_PATTERN = /\b(Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini|Mobile)\b/i;
const ANDROID_PATTERN = /\bAndroid\b/i;

export const getCurrentBrowserDeviceInfo = (): BrowserDeviceInfo | null => {
    if (typeof navigator === "undefined") {
        return null;
    }

    const browserNavigator = navigator as Navigator & {
        userAgentData?: NavigatorUserAgentData;
    };

    return {
        userAgent: browserNavigator.userAgent,
        platform: browserNavigator.platform,
        maxTouchPoints: browserNavigator.maxTouchPoints,
        userAgentData: browserNavigator.userAgentData
            ? {
                  mobile: browserNavigator.userAgentData.mobile,
                  platform: browserNavigator.userAgentData.platform,
              }
            : undefined,
    };
};

export const isMobileBrowserDevice = (deviceInfo: BrowserDeviceInfo | null | undefined) => {
    if (!deviceInfo) {
        return false;
    }

    const userAgent = deviceInfo.userAgent ?? "";
    const platform = deviceInfo.platform ?? "";
    const userAgentDataPlatform = deviceInfo.userAgentData?.platform ?? "";
    const maxTouchPoints = deviceInfo.maxTouchPoints ?? 0;

    if (DESKTOP_PLATFORM_PATTERN.test(platform)) {
        return platform === "MacIntel" && maxTouchPoints > 1 && /\b(iPad|Macintosh)\b/i.test(userAgent);
    }

    if (
        IOS_PLATFORM_PATTERN.test(platform) ||
        IOS_PLATFORM_PATTERN.test(userAgentDataPlatform) ||
        ANDROID_PATTERN.test(platform) ||
        ANDROID_PATTERN.test(userAgentDataPlatform)
    ) {
        return true;
    }

    if (!MOBILE_UA_PATTERN.test(userAgent)) {
        return false;
    }

    return maxTouchPoints > 0 || ANDROID_PATTERN.test(userAgent);
};
